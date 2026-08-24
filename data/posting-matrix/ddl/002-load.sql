-- =====================================================================
-- تحميل مصفوفة الترحيل من ملفات CSV و JSON
-- Load the posting matrix from the CSV and JSON files
--
-- التشغيل من مجلد data/posting-matrix:
--   psql -v ON_ERROR_STOP=1 -d <db> -f ddl/001-schema.sql -f ddl/002-load.sql
--
-- ملفات الأحداث JSON تُقرأ عبر متغير psql بتوسعة الأمر العكسي، فلا حاجة إلى
-- صلاحية superuser ولا إلى pg_read_file.
-- The JSON event files are read through a psql variable via backtick expansion,
-- so neither superuser rights nor pg_read_file are needed.
-- =====================================================================

\set ON_ERROR_STOP on

begin;

set constraints all deferred;

truncate table matrix.event_line, matrix.business_event, matrix.guard_rule cascade;
truncate table matrix.tenant_role_map cascade;
truncate table matrix.account_role cascade;

\copy matrix.account_role (code, name_ar, name_en, expected_account_type, expected_side, qualifier_meaning_ar, qualifier_meaning_en, status, note_ar, note_en) from 'account-roles.csv' with (format csv, header true, force_not_null (qualifier_meaning_ar, qualifier_meaning_en, note_ar, note_en))

update matrix.account_role
   set expected_account_type = nullif(expected_account_type, ''),
       expected_side = nullif(expected_side, '');

\copy matrix.tenant_role_map (tenant_id, role_code, qualifier, account_code, status, note_ar, note_en) from 'role-map.default.csv' with (format csv, header true, force_not_null (note_ar, note_en))

-- ---------------------------------------------------------------------
-- الأحداث — one staging row per JSON file, then normalised with SQL
-- ---------------------------------------------------------------------

create temporary table _event_file (doc jsonb) on commit drop;

\set f `cat events/assets.json`
insert into _event_file values (:'f'::jsonb);
\set f `cat events/hr.json`
insert into _event_file values (:'f'::jsonb);
\set f `cat events/inventory.json`
insert into _event_file values (:'f'::jsonb);
\set f `cat events/ledger.json`
insert into _event_file values (:'f'::jsonb);
\set f `cat events/pos.json`
insert into _event_file values (:'f'::jsonb);
\set f `cat events/projects.json`
insert into _event_file values (:'f'::jsonb);
\set f `cat events/purchasing.json`
insert into _event_file values (:'f'::jsonb);
\set f `cat events/realestate.json`
insert into _event_file values (:'f'::jsonb);
\set f `cat events/sales.json`
insert into _event_file values (:'f'::jsonb);
\set f `cat events/tax.json`
insert into _event_file values (:'f'::jsonb);
\set f `cat events/treasury.json`
insert into _event_file values (:'f'::jsonb);

create temporary table _event (e jsonb) on commit drop;
insert into _event
select jsonb_array_elements(doc -> 'events') from _event_file;

insert into matrix.business_event (
    event_code, name_ar, name_en, module, status, source_ref,
    trigger_ar, trigger_en, precondition_ar, precondition_en,
    reversal_ar, reversal_en, posts_entry,
    amounts, identities, conditions, scenarios, caveats)
select e ->> 'event_code',
       e ->> 'name_ar',
       e ->> 'name_en',
       e ->> 'module',
       e ->> 'status',
       coalesce(e ->> 'source_ref', ''),
       e #>> '{trigger,name_ar}',
       e #>> '{trigger,name_en}',
       coalesce(e #>> '{precondition,name_ar}', ''),
       coalesce(e #>> '{precondition,name_en}', ''),
       coalesce(e #>> '{reversal,name_ar}', ''),
       coalesce(e #>> '{reversal,name_en}', ''),
       coalesce((e ->> 'posts_entry')::boolean, true),
       coalesce(e -> 'amounts',    '{}'::jsonb),
       coalesce(e -> 'identities', '{}'::jsonb),
       coalesce(e -> 'conditions', '{}'::jsonb),
       coalesce(e -> 'scenarios',  '[]'::jsonb),
       coalesce(e -> 'caveats',    '[]'::jsonb)
  from _event;

insert into matrix.event_line (
    event_code, line_no, line_kind, role_code, qualifier_source, side,
    amount_expression, dimensions, subledger, applies_when, sweep, note_ar, note_en)
select e ->> 'event_code',
       (l ->> 'line_no')::int,
       coalesce(l ->> 'line_kind', 'role'),
       l ->> 'role',
       l ->> 'qualifier_source',
       l ->> 'side',
       l ->> 'amount',
       coalesce((select array_agg(x) from jsonb_array_elements_text(l -> 'dimensions') x), '{}'),
       l ->> 'subledger',
       case jsonb_typeof(l -> 'when')
            when 'string' then array[l ->> 'when']
            when 'array'  then coalesce((select array_agg(x) from jsonb_array_elements_text(l -> 'when') x), '{}')
            else '{}'::text[] end,
       l -> 'sweep',
       coalesce(l ->> 'note_ar', ''),
       coalesce(l ->> 'note_en', '')
  from _event, lateral jsonb_array_elements(e -> 'lines') l;

-- ---------------------------------------------------------------------
-- قواعد الحجب — guard rules
-- ---------------------------------------------------------------------

create temporary table _guard_file (doc jsonb) on commit drop;
\set g `cat guard-rules.json`
insert into _guard_file values (:'g'::jsonb);

insert into matrix.guard_rule (
    rule_id, name_ar, name_en, severity, status, source_ref,
    applies_to, condition_expr, message_ar, message_en,
    rationale_ar, rationale_en, enforcement)
select r ->> 'rule_id', r ->> 'name_ar', r ->> 'name_en',
       r ->> 'severity', r ->> 'status', coalesce(r ->> 'source_ref', ''),
       r -> 'applies_to', r ->> 'condition', r ->> 'message_ar', r ->> 'message_en',
       coalesce(r ->> 'rationale_ar', ''), coalesce(r ->> 'rationale_en', ''),
       coalesce(r -> 'enforcement', '{}'::jsonb)
  from _guard_file, lateral jsonb_array_elements(doc -> 'rules') r;

commit;

-- ---------------------------------------------------------------------
-- الفحوص التي لا يجوز أن تفشل بعد التحميل
-- Post-load assertions that must never fail
-- ---------------------------------------------------------------------

do $$
declare n bigint;
begin
    select count(*) into n from matrix.account_role r
     where not exists (select 1 from matrix.tenant_role_map m
                        where m.role_code = r.code and m.qualifier = '*');
    if n > 0 then raise exception 'roles with no default mapping: %', n; end if;

    select count(*) into n from matrix.event_line l
     where l.line_kind = 'role'
       and not exists (select 1 from matrix.account_role r where r.code = l.role_code);
    if n > 0 then raise exception 'matrix lines naming an unknown role: %', n; end if;

    select count(*) into n
      from matrix.event_line l
      join matrix.tenant_role_map m on m.role_code = l.role_code and m.qualifier = '*'
      join coa.account a on a.code = m.account_code
     where not a.is_postable;
    if n > 0 then raise exception 'matrix lines resolving to a rollup account: %', n; end if;

    raise notice 'posting matrix loaded: % roles, % mappings, % events, % lines, % guard rules',
        (select count(*) from matrix.account_role),
        (select count(*) from matrix.tenant_role_map),
        (select count(*) from matrix.business_event),
        (select count(*) from matrix.event_line),
        (select count(*) from matrix.guard_rule);
end $$;
