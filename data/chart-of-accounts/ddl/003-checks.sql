-- =====================================================================
-- فحوص تشخيصية تُشغَّل بعد التحميل — diagnostic queries after loading
-- تُقرأ بالعين ولا يعتمد عليها التحقق الآلي: ذلك عمل tools/matrix-validator.
-- Read by eye; automated validation is the job of tools/matrix-validator.
-- =====================================================================

\echo '--- عدد الحسابات حسب التصنيف / accounts by type ---'
select t.name_en as account_type,
       count(*) filter (where a.is_postable)      as postable,
       count(*) filter (where not a.is_postable)  as rollup,
       count(*)                                   as total
  from coa.account a
  join coa.account_type t on t.code = a.account_type
 group by t.name_en, t.code
 order by t.code;

\echo '--- الحسابات المقترحة التي تنتظر مراجعة محاسب / proposed accounts awaiting accountant review ---'
select code, name_ar, name_en, source_ref
  from coa.account
 where status = 'proposed'
 order by code;

\echo '--- الحسابات المعاد تسميتها / renamed accounts ---'
select code, name_ar, name_en, caveat_ar
  from coa.account
 where status = 'renamed'
 order by code;

\echo '--- الحسابات التي تحمل تحفظاً غير مرفوع / accounts carrying an unlifted caveat ---'
select code, name_ar, caveat_ar
  from coa.account
 where caveat_ar like '%⚠️%'
 order by code;

\echo '--- الحسابات التي تفرض أبعاداً / accounts demanding dimensions ---'
select code, name_ar, required_dimensions
  from coa.account
 where cardinality(required_dimensions) > 0
 order by code;

\echo '--- الحسابات ذات الدفاتر المساعدة / accounts with a subledger ---'
select a.code, a.name_ar, s.name_en as subledger
  from coa.account a join coa.subledger_type s on s.code = a.subledger_type
 where a.subledger_type <> 'none'
 order by a.code;

\echo '--- الحسابات المقابلة / contra accounts ---'
select code, name_ar, name_en, account_type, natural_side
  from coa.account where is_contra order by code;

\echo '--- المساحة المتاحة للنمو في كل مجموعة / free code space per group ---'
select left(code, 2) as grp,
       count(*)      as used_detail_accounts,
       100 - count(*) as free_codes_in_group
  from coa.account
 where level = 4
 group by left(code, 2)
 order by grp;
