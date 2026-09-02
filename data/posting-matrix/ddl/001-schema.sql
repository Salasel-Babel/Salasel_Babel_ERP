-- =====================================================================
-- سلاسل بابل — مخطط مصفوفة الترحيل
-- Salasel Babel — posting matrix schema
--
-- المبدأ الذي يقوم عليه هذا المخطط: الوحدة تصف حدثاً تجارياً، والمصفوفة وحدها
-- تقرر أي حساب يُدين وأي حساب يُدان. لا وحدة تسمّي حساباً، ولا رمز حساب في الكود.
-- The principle this schema exists to enforce: a module describes a business event and the
-- matrix alone decides which accounts are debited and credited. No module names an account
-- and no account code ever appears in code.
-- docs/analysis/03-accounting-core.md §4 · docs/analysis/02-architecture.md §13
-- =====================================================================

create schema if not exists matrix;

-- ---------------------------------------------------------------------
-- الأدوار وخريطتها لكل مستأجر — roles and their per-tenant mapping
-- ---------------------------------------------------------------------

create table matrix.account_role (
    code                    text primary key,
    name_ar                 text not null check (length(btrim(name_ar)) > 0),
    name_en                 text not null check (length(btrim(name_en)) > 0),
    expected_account_type   text references coa.account_type (code),
    expected_side           text check (expected_side is null or expected_side in ('debit','credit')),
    qualifier_meaning_ar    text not null default '',
    qualifier_meaning_en    text not null default '',
    status                  text not null check (status in ('drafted','proposed','renamed')),
    note_ar                 text not null default '',
    note_en                 text not null default ''
);

comment on table matrix.account_role is
    'الأدوار المحاسبية. سطر المصفوفة يشير إلى دور لا إلى رمز حساب — وهذا ما يجعل المنتج متعدد المستأجرين ممكناً.';

-- المؤهل يسمح بدور واحد يُحلّ إلى حسابات مختلفة حسب سياق المستند
-- (طريقة الدفع، مجموعة الصنف، تصنيف المصروف). النجمة هي التعيين الافتراضي.
-- The qualifier lets one role resolve to different accounts depending on document context
-- (settlement method, item group, expense category). '*' is the default mapping.
create table matrix.tenant_role_map (
    tenant_id       text not null,
    role_code       text not null references matrix.account_role (code),
    qualifier       text not null default '*',
    account_code    text not null references coa.account (code),
    status          text not null check (status in ('drafted','proposed','renamed')),
    note_ar         text not null default '',
    note_en         text not null default '',
    primary key (tenant_id, role_code, qualifier)
);

create index tenant_role_map_account_idx on matrix.tenant_role_map (account_code);

comment on table matrix.tenant_role_map is
    'خريطة الأدوار لكل مستأجر. تغيير الحساب الذي يشير إليه دور = تعديل صف هنا، لا تعديل كود ولا نشر إصدار.';

-- الدور يجب أن يُحلّ دائماً: بلا تعيين افتراضي يقف محرك الترحيل.
-- A role must always resolve: without a default mapping the posting engine stalls.
create or replace function matrix.assert_default_mapping_exists() returns trigger
language plpgsql as $$
begin
    if not exists (select 1 from matrix.tenant_role_map
                    where tenant_id = new.tenant_id and role_code = new.role_code and qualifier = '*')
    then
        raise exception 'الدور % لدى المستأجر % بلا تعيين افتراضي / role % has no default (*) mapping for tenant %',
            new.role_code, new.tenant_id, new.role_code, new.tenant_id;
    end if;
    return new;
end $$;

create constraint trigger tenant_role_map_has_default
    after insert or update on matrix.tenant_role_map
    deferrable initially deferred
    for each row execute function matrix.assert_default_mapping_exists();

-- الترحيل على الحساب التفصيلي فقط — يُفرض هنا أيضاً لا في الكود وحده.
create or replace function matrix.assert_mapped_account_is_postable() returns trigger
language plpgsql as $$
declare postable boolean;
begin
    select is_postable into postable from coa.account where code = new.account_code;
    if not postable then
        raise exception 'الدور % معيَّن إلى الحساب التجميعي % / role % is mapped to rollup account %',
            new.role_code, new.account_code, new.role_code, new.account_code;
    end if;
    return new;
end $$;

create trigger tenant_role_map_postable
    before insert or update on matrix.tenant_role_map
    for each row execute function matrix.assert_mapped_account_is_postable();

-- ---------------------------------------------------------------------
-- الأحداث وسطورها — events and their lines
-- ---------------------------------------------------------------------

create table matrix.business_event (
    event_code      text primary key
                    check (event_code ~ '^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$'),
    name_ar         text    not null check (length(btrim(name_ar)) > 0),
    -- العربية سجلٌّ إلزامي والإنجليزية شرحٌ اختياري (ADR-0021 بند 2 · ADR-جديد
    -- gloss-is-not-column-debt). و NULL يعني «لم يُكتب» ويُقبل؛ والفراغ يعني «كُتب
    -- فارغاً» ويُرفض — مفتاحٌ بلا قيمة نصفُ ترجمةٍ لا اختيار.
    name_en         text             check (name_en is null or length(btrim(name_en)) > 0),
    module          text    not null,
    status          text    not null check (status in ('drafted','proposed')),
    source_ref      text    not null default '',
    trigger_ar      text    not null check (length(btrim(trigger_ar)) > 0),
    trigger_en      text             check (trigger_en is null or length(btrim(trigger_en)) > 0),
    precondition_ar text    not null default '',
    precondition_en text    not null default '',
    reversal_ar     text    not null default '',
    reversal_en     text    not null default '',
    posts_entry     boolean not null default true,
    is_active       boolean not null default true,
    amounts         jsonb   not null default '{}'::jsonb,
    identities      jsonb   not null default '{}'::jsonb,
    conditions      jsonb   not null default '{}'::jsonb,
    scenarios       jsonb   not null default '[]'::jsonb,
    caveats         jsonb   not null default '[]'::jsonb
);

comment on table matrix.business_event is
    'حدث تجاري واحد. إضافة حدث جديد = صف هنا وسطوره في matrix.event_line — بلا تعديل كود.';
comment on column matrix.business_event.posts_entry is
    'حدث معلَن أنه لا يولّد قيداً هو بيان سياسة محاسبية صريح (07-real-estate.md §14.1 و§14.3-ب) لا إغفال.';

create table matrix.event_line (
    event_code          text    not null references matrix.business_event (event_code) on delete cascade,
    line_no             int     not null check (line_no >= 1),
    line_kind           text    not null
                        check (line_kind in ('role','sweep','import','manual','mirror')),
    role_code           text    references matrix.account_role (code),
    qualifier_source    text,
    side                text    not null check (side in ('debit','credit','mirror')),
    amount_expression   text    not null check (length(btrim(amount_expression)) > 0),
    dimensions          text[]  not null default '{}',
    subledger           text,
    applies_when        text[]  not null default '{}',
    sweep               jsonb,
    note_ar             text    not null default '',
    note_en             text    not null default '',
    primary key (event_code, line_no),

    -- سطر من نوع role يجب أن يسمّي دوراً؛ وأي نوع آخر يجب أن يسمّي محدِّداً.
    constraint event_line_role_or_selector
        check ((line_kind = 'role' and role_code is not null and sweep is null)
            or (line_kind <> 'role' and role_code is null and sweep is not null)),
    constraint event_line_mirror_side
        check ((line_kind = 'mirror') = (side = 'mirror'))
);

create index event_line_role_idx on matrix.event_line (role_code);

-- حدث معلَن أنه لا يولّد قيداً لا يحمل سطوراً، وحدث يولّد قيداً يحمل سطرين على الأقل.
-- An event declared to post nothing carries no lines; one that posts carries at least two.
create or replace function matrix.assert_event_line_count() returns trigger
language plpgsql as $$
declare posts boolean; n int;
begin
    select posts_entry into posts from matrix.business_event
     where event_code = coalesce(new.event_code, old.event_code);
    if posts is null then return null; end if;

    select count(*) into n from matrix.event_line
     where event_code = coalesce(new.event_code, old.event_code);

    -- سطر «mirror» واحد يعكس قيداً متوازناً سطراً بسطر: توازنه موروث لا مُدّعى.
    -- A single mirror line reproduces a balanced source entry line by line:
    -- its balance is inherited, not asserted.
    if n = 1 and exists (select 1 from matrix.event_line
                          where event_code = coalesce(new.event_code, old.event_code)
                            and line_kind = 'mirror')
    then
        return null;
    end if;

    if posts and n < 2 then
        raise exception 'الحدث % يولّد قيداً ويحمل % سطراً — القيد يحتاج سطرين على الأقل / event % posts an entry with only % line(s)',
            coalesce(new.event_code, old.event_code), n, coalesce(new.event_code, old.event_code), n;
    end if;
    if not posts and n > 0 then
        raise exception 'الحدث % معلَن أنه لا يولّد قيداً ومع ذلك يحمل سطوراً / event % is declared to post no entry yet carries lines',
            coalesce(new.event_code, old.event_code), coalesce(new.event_code, old.event_code);
    end if;
    return null;
end $$;

create constraint trigger event_line_count
    after insert or update or delete on matrix.event_line
    deferrable initially deferred
    for each row execute function matrix.assert_event_line_count();

-- ---------------------------------------------------------------------
-- قواعد الحجب — guard rules
-- ---------------------------------------------------------------------

create table matrix.guard_rule (
    rule_id         text primary key,
    name_ar         text not null check (length(btrim(name_ar)) > 0),
    name_en         text not null check (length(btrim(name_en)) > 0),
    severity        text not null check (severity in ('block','warn')),
    status          text not null check (status in ('drafted','proposed')),
    source_ref      text not null default '',
    applies_to      jsonb not null,
    condition_expr  text not null check (length(btrim(condition_expr)) > 0),
    message_ar      text not null check (length(btrim(message_ar)) > 0),
    message_en      text not null check (length(btrim(message_en)) > 0),
    rationale_ar    text not null default '',
    rationale_en    text not null default '',
    enforcement     jsonb not null default '{}'::jsonb,
    is_active       boolean not null default true
);

comment on table matrix.guard_rule is
    'قواعد يقيّمها محرك الترحيل قبل كتابة أي سطر. severity=block تعني رفض القيد كاملاً — لا تحذيراً يمكن تجاوزه.';

-- ---------------------------------------------------------------------
-- عرض يُظهر ما يُحلّ إليه كل سطر فعلياً لمستأجر بعينه
-- A view showing what each line actually resolves to for a given tenant
-- ---------------------------------------------------------------------

create or replace function matrix.resolve(p_tenant text, p_role text, p_qualifier text default '*')
returns text language sql stable as $$
    select coalesce(
        (select account_code from matrix.tenant_role_map
          where tenant_id = p_tenant and role_code = p_role and qualifier = p_qualifier),
        (select account_code from matrix.tenant_role_map
          where tenant_id = p_tenant and role_code = p_role and qualifier = '*'))
$$;

create or replace view matrix.resolved_line as
select l.event_code, e.name_ar as event_name_ar, e.module, l.line_no, l.line_kind,
       l.role_code, l.qualifier_source, l.side, l.amount_expression,
       m.tenant_id,
       m.account_code,
       a.name_ar as account_name_ar, a.name_en as account_name_en,
       a.is_postable, a.required_dimensions, l.dimensions as declared_dimensions,
       a.subledger_type, l.subledger as declared_subledger,
       l.applies_when
  from matrix.event_line l
  join matrix.business_event e on e.event_code = l.event_code
  left join matrix.tenant_role_map m on m.role_code = l.role_code and m.qualifier = '*'
  left join coa.account a on a.code = m.account_code;
