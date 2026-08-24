-- =====================================================================
-- سلاسل بابل — مخطط دليل الحسابات
-- Salasel Babel — chart of accounts schema
--
-- كل مبلغ NUMERIC(19,4) بلا استثناء — لا float ولا double في أي حقل مالي.
-- Every monetary column is NUMERIC(19,4) — no float or double anywhere.
-- CONTRIBUTING §3.2 · docs/analysis/02-architecture.md §3.1
--
-- هذا المخطط بيانات تأسيسية مشتركة. عزل المستأجرين يُطبَّق فوقه بالنموذج
-- المختار في 02-architecture.md §9 ولا يُفترض هنا.
-- This schema holds shared seed data. Tenant isolation is layered on top of it
-- by whichever model §9 of 02-architecture.md settles on, and is not assumed here.
-- =====================================================================

create schema if not exists coa;

-- ---------------------------------------------------------------------
-- قوائم القيم المحصورة — enumerations
-- ---------------------------------------------------------------------

create table coa.account_type (
    code            text primary key,
    name_ar         text not null check (length(btrim(name_ar)) > 0),
    name_en         text not null check (length(btrim(name_en)) > 0),
    natural_side    text not null check (natural_side in ('debit','credit'))
);

insert into coa.account_type (code, name_ar, name_en, natural_side) values
    ('asset',     'الأصول',        'Assets',      'debit'),
    ('liability', 'الخصوم',        'Liabilities', 'credit'),
    ('equity',    'حقوق الملكية',  'Equity',      'credit'),
    ('revenue',   'الإيرادات',     'Revenue',     'credit'),
    ('expense',   'المصروفات',     'Expenses',    'debit')
on conflict (code) do nothing;

create table coa.statement_section (
    code            text primary key,
    name_ar         text not null check (length(btrim(name_ar)) > 0),
    name_en         text not null check (length(btrim(name_en)) > 0),
    sort_order      int  not null
);

insert into coa.statement_section (code, name_ar, name_en, sort_order) values
    ('current_asset',          'الأصول المتداولة',            'Current assets',          10),
    ('non_current_asset',      'الأصول غير المتداولة',        'Non-current assets',      20),
    ('current_liability',      'الخصوم المتداولة',            'Current liabilities',     30),
    ('non_current_liability',  'الخصوم غير المتداولة',        'Non-current liabilities', 40),
    ('equity',                 'حقوق الملكية',                'Equity',                  50),
    ('revenue',                'الإيرادات',                   'Revenue',                 60),
    ('cost_of_revenue',        'تكلفة الإيرادات',             'Cost of revenue',         70),
    ('operating_expense',      'المصروفات التشغيلية والإدارية','Operating expenses',      80),
    ('other_income',           'الإيرادات الأخرى',            'Other income',            90),
    ('other_expense',          'المصروفات الأخرى',            'Other expenses',         100)
on conflict (code) do nothing;

create table coa.dimension (
    code              text primary key,
    name_ar           text    not null check (length(btrim(name_ar)) > 0),
    name_en           text    not null check (length(btrim(name_en)) > 0),
    is_hierarchical   boolean not null default true,
    default_required  boolean not null default false,
    status            text    not null check (status in ('drafted','proposed','renamed')),
    note_ar           text    not null default '',
    note_en           text    not null default ''
);

create table coa.subledger_type (
    code                      text primary key,
    name_ar                   text not null check (length(btrim(name_ar)) > 0),
    name_en                   text not null check (length(btrim(name_en)) > 0),
    control_accounts          text not null default '',
    reconciliation_report_ar  text not null default '',
    reconciliation_report_en  text not null default '',
    status                    text not null check (status in ('drafted','proposed','renamed'))
);

-- ---------------------------------------------------------------------
-- شجرة الحسابات — the account tree
-- ---------------------------------------------------------------------

create table coa.account (
    code                text        primary key
                        check (code ~ '^[1-9][0-9]{0,3}$'),
    name_ar             text        not null check (length(btrim(name_ar)) > 0),
    name_en             text        not null check (length(btrim(name_en)) > 0),
    parent_code         text        references coa.account (code),
    level               int         not null check (level between 1 and 4),
    account_type        text        not null references coa.account_type (code),
    natural_side        text        not null check (natural_side in ('debit','credit')),
    is_postable         boolean     not null,
    is_contra           boolean     not null default false,
    statement_section   text        references coa.statement_section (code),
    subledger_type      text        not null default 'none' references coa.subledger_type (code),
    required_dimensions text[]      not null default '{}',
    currency_mode       text        not null default 'any'
                        check (currency_mode in ('any','company_only','fixed')),
    currency_code       text        not null default '',
    is_protected        boolean     not null default false,
    is_active           boolean     not null default true,
    status              text        not null check (status in ('drafted','proposed','renamed')),
    source_ref          text        not null default '',
    caveat_ar           text        not null default '',
    caveat_en           text        not null default '',

    -- الترقيم يحدد الشجرة: المستوى من طول الرمز والأب من بادئته.
    -- The numbering defines the tree: the level is the code length and the parent is its prefix.
    constraint account_level_matches_code
        check (level = case when length(code) = 4 then 4 else length(code) end),
    constraint account_parent_matches_code
        check ((level = 1 and parent_code is null)
            or (level > 1 and parent_code = left(code, case when level = 4 then 3 else level - 1 end))),

    -- الترحيل على الحساب التفصيلي فقط. chart-of-accounts.md §1
    -- Posting is only ever on a detail account.
    constraint account_only_level_4_is_postable
        check (is_postable = (level = 4)),

    -- الحساب التجميعي لا يظهر سطراً في القوائم ولا يفرض أبعاداً.
    constraint account_rollup_has_no_statement_section
        check ((level = 4) = (statement_section is not null)),
    constraint account_rollup_demands_no_dimensions
        check (level = 4 or cardinality(required_dimensions) = 0),

    -- الحساب المقابل طبيعته معكوسة، وغير المقابل طبيعته مطابقة لتصنيفه.
    -- A contra account's natural side is inverted; a normal one's matches its type.
    constraint account_contra_side_is_inverted
        check (is_contra = (natural_side <> case
            when account_type in ('asset','expense') then 'debit' else 'credit' end)),

    constraint account_fixed_currency_names_it
        check (currency_mode <> 'fixed' or length(btrim(currency_code)) > 0)
);

create index account_parent_idx on coa.account (parent_code);
create index account_type_idx   on coa.account (account_type);
create index account_postable_idx on coa.account (is_postable) where is_postable;

comment on table coa.account is
    'دليل الحسابات — شجرة من أربعة مستويات يحددها الرمز نفسه. الترحيل على المستوى الرابع فقط.';
comment on column coa.account.required_dimensions is
    'الأبعاد الإلزامية على سطر القيد. لا ترحيل على هذا الحساب دون تحديدها جميعاً.';
comment on column coa.account.is_protected is
    'محمي من الحذف: حساب يعتمد عليه المحرك أو خريطة الأدوار. الحساب الذي عليه حركة لا يُحذف أبداً — يُعطَّل فقط.';

-- كل بُعد إلزامي على حساب يجب أن يكون بُعداً معرَّفاً.
-- Every mandatory dimension on an account must be a dimension that exists.
create or replace function coa.assert_dimensions_exist() returns trigger
language plpgsql as $$
declare d text;
begin
    foreach d in array new.required_dimensions loop
        if not exists (select 1 from coa.dimension where code = d) then
            raise exception 'بُعد غير معروف % على الحساب % / unknown dimension % on account %',
                d, new.code, d, new.code;
        end if;
    end loop;
    return new;
end $$;

create trigger account_dimensions_exist
    before insert or update on coa.account
    for each row execute function coa.assert_dimensions_exist();

-- الحساب الذي عليه حركة لا يُحذف — والحساب المحمي لا يُحذف إطلاقاً.
-- 03-accounting-core.md §3: an account carrying movement is never deleted, only deactivated.
create or replace function coa.refuse_protected_delete() returns trigger
language plpgsql as $$
begin
    if old.is_protected then
        raise exception 'الحساب % محمي من الحذف — يُعطَّل ولا يُحذف / account % is protected from deletion; deactivate it instead',
            old.code, old.code;
    end if;
    return old;
end $$;

create trigger account_refuse_protected_delete
    before delete on coa.account
    for each row execute function coa.refuse_protected_delete();

-- ---------------------------------------------------------------------
-- عرض مسطّح للشجرة يفيد التقارير — a flattened tree view for reporting
-- ---------------------------------------------------------------------

create or replace view coa.account_tree as
with recursive walk as (
    select code, name_ar, name_en, parent_code, level, account_type,
           code as path, name_ar as path_ar
      from coa.account
     where parent_code is null
    union all
    select a.code, a.name_ar, a.name_en, a.parent_code, a.level, a.account_type,
           w.path || ' > ' || a.code, w.path_ar || ' ← ' || a.name_ar
      from coa.account a join walk w on a.parent_code = w.code
)
select * from walk;
