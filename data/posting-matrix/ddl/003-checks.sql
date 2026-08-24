-- =====================================================================
-- فحوص تشخيصية على مصفوفة الترحيل بعد التحميل
-- Diagnostic queries on the posting matrix after loading
-- التحقق الآلي الكامل في tools/matrix-validator — هذه للقراءة بالعين.
-- =====================================================================

\echo '--- الأحداث حسب الوحدة / events by module ---'
select module,
       count(*)                                as events,
       count(*) filter (where posts_entry)     as posting,
       count(*) filter (where not posts_entry) as no_entry_by_design,
       count(*) filter (where status = 'proposed') as proposed
  from matrix.business_event group by module order by module;

\echo '--- الأحداث التي تحمل تحفظاً غير مرفوع / events carrying an unlifted caveat ---'
select event_code, name_ar,
       jsonb_array_length(caveats) as caveats
  from matrix.business_event
 where caveats @> '[]'::jsonb and jsonb_array_length(caveats) > 0
   and caveats::text like '%⚠️%'
 order by event_code;

\echo '--- الأدوار التي لا يستخدمها أي سطر مصفوفة / roles no matrix line uses ---'
select r.code, r.name_ar
  from matrix.account_role r
 where not exists (select 1 from matrix.event_line l where l.role_code = r.code)
 order by r.code;

\echo '--- الحسابات التي لا يصل إليها أي دور / accounts no role can reach ---'
select a.code, a.name_ar, a.status
  from coa.account a
 where a.is_postable
   and not exists (select 1 from matrix.tenant_role_map m where m.account_code = a.code)
 order by a.code;

\echo '--- ما يُحلّ إليه كل سطر في المستأجر الافتراضي / what each line resolves to for the default tenant ---'
select event_code, line_no, side, role_code, account_code, account_name_ar, amount_expression
  from matrix.resolved_line
 where line_kind = 'role'
 order by event_code, line_no
 limit 25;

\echo '--- قواعد الحجب الفعّالة / active guard rules ---'
select rule_id, severity, name_ar, applies_to ->> 'role' as role, condition_expr
  from matrix.guard_rule where is_active order by rule_id;

\echo '--- إثبات: لا سطر في أي حدث «مُدار لصالح الغير» يمسّ دور إيراد الإيجار ---'
\echo '--- proof: no line in any managed-for-others event touches the rental revenue role ---'
select count(*) as must_be_zero
  from matrix.event_line l
 where l.role_code = 'rental_revenue'
   and l.event_code like '%managed%';
