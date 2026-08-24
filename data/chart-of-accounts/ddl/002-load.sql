-- =====================================================================
-- تحميل البيانات التأسيسية لدليل الحسابات من ملفات CSV
-- Load the chart of accounts seed from the CSV files
--
-- التشغيل من مجلد data/chart-of-accounts:
--   psql -v ON_ERROR_STOP=1 -d <db> -f ddl/001-schema.sql -f ddl/002-load.sql
--
-- \copy يقرأ الملف من جهاز العميل لا من الخادم، فلا حاجة لصلاحية superuser.
-- \copy reads from the client machine, not the server, so no superuser rights are needed.
-- =====================================================================

\set ON_ERROR_STOP on

begin;

truncate table coa.account cascade;
truncate table coa.dimension cascade;
truncate table coa.subledger_type cascade;

\copy coa.dimension (code, name_ar, name_en, is_hierarchical, default_required, status, note_ar, note_en) from 'dimensions.csv' with (format csv, header true, force_not_null (note_ar, note_en))

\copy coa.subledger_type (code, name_ar, name_en, control_accounts, reconciliation_report_ar, reconciliation_report_en, status) from 'subledger-types.csv' with (format csv, header true, force_not_null (control_accounts, reconciliation_report_ar, reconciliation_report_en))

-- الحسابات تُحمَّل إلى جدول مرحلي أولاً لأن العمود required_dimensions مفصول بالرمز |
-- والأب يجب أن يُدرج قبل ابنه؛ الترتيب بالمستوى يضمن ذلك.
-- Accounts land in a staging table first: required_dimensions is pipe-delimited and a
-- parent must be inserted before its child, which ordering by level guarantees.
create temporary table _account_stage (
    code text, name_ar text, name_en text, parent_code text, level int,
    account_type text, natural_side text, is_postable boolean, is_contra boolean,
    statement_section text, subledger_type text, required_dimensions text,
    currency_mode text, currency_code text, is_protected boolean, status text,
    source_ref text, caveat_ar text, caveat_en text
) on commit drop;

\copy _account_stage from 'accounts.csv' with (format csv, header true)

insert into coa.account (
    code, name_ar, name_en, parent_code, level, account_type, natural_side,
    is_postable, is_contra, statement_section, subledger_type, required_dimensions,
    currency_mode, currency_code, is_protected, status, source_ref, caveat_ar, caveat_en)
select code, name_ar, name_en,
       nullif(parent_code, ''), level, account_type, natural_side,
       is_postable, is_contra,
       nullif(statement_section, ''),
       coalesce(nullif(subledger_type, ''), 'none'),
       case when coalesce(required_dimensions, '') = '' then '{}'::text[]
            else string_to_array(required_dimensions, '|') end,
       coalesce(nullif(currency_mode, ''), 'any'),
       coalesce(currency_code, ''),
       is_protected, status, coalesce(source_ref, ''),
       coalesce(caveat_ar, ''), coalesce(caveat_en, '')
  from _account_stage
 order by level, code;

commit;

-- الفحوص التي لا يجوز أن تفشل بعد التحميل
-- Post-load assertions that must never fail
do $$
declare n bigint;
begin
    select count(*) into n from coa.account where level > 1 and parent_code is null;
    if n > 0 then raise exception 'accounts with no parent below level 1: %', n; end if;

    select count(*) into n
      from coa.account a
     where a.subledger_type <> 'none'
       and not exists (select 1 from coa.subledger_type s where s.code = a.subledger_type);
    if n > 0 then raise exception 'accounts referencing an unknown subledger type: %', n; end if;

    select count(*) into n from coa.account where is_postable and statement_section is null;
    if n > 0 then raise exception 'postable accounts with no statement section: %', n; end if;

    raise notice 'chart of accounts loaded: % accounts (% postable)',
        (select count(*) from coa.account),
        (select count(*) from coa.account where is_postable);
end $$;
