-- ═══════════════════════════════════════════════════════════════════════════
-- الطبقتان الثانية والثالثة من إنفاذ الدفتر — مشغّلات القيود
--
-- الجداول والفهارس وقيود التحقق يبنيها ترحيل EF Core من نموذج LedgerDbContext.
-- ما في هذا الملف هو ما **لا يستطيع** نموذج EF التعبير عنه، وهو بالضبط ما يجعل
-- الضمانات صحيحة مهما كان مسار الكتابة: مشغّل قيد مؤجَّل إلى COMMIT، ودوال
-- plpgsql، وقواعد حجب على مستوى الصف.
--
-- الطبقات الثلاث، بهذا الترتيب المقصود (الأبعد عن الكود أولاً، لأن الكود هو
-- ما يتغيّر):
--   1) الصلاحيات      — تمنع            (LedgerGrants.sql)
--   2) مشغّل مؤجَّل     — يرفض عند COMMIT  (هنا)
--   3) تأكيدات الكود  — تكتشف مبكراً برسالة مفهومة (PostingService.AssertBalanced)
-- ═══════════════════════════════════════════════════════════════════════════

-- ═══════════════════════════════════════════════════════════════════════════
-- الطبقة الثانية: مشغّل قيد DEFERRABLE INITIALLY DEFERRED يعمل عند COMMIT
--
-- يفحص عند لحظة التثبيت: مجموع المدين = مجموع الدائن **بعملة الشركة**، وعدد
-- السطور >= 2. مؤجَّل لأن الفحص عند كل سطر يجعل إدراج قيد متعدّد السطور
-- مستحيلاً أصلاً. ويعمل **مهما كان مسار الكتابة**: EF Core، SQL خام، psql،
-- سكربت مدير قاعدة بيانات (ADR-0003 دليل 2).
-- ═══════════════════════════════════════════════════════════════════════════
create or replace function ledger.assert_entry_balanced() returns trigger
language plpgsql security definer set search_path = ledger, pg_catalog as $fn$
declare
    v_debit  numeric(19,4);
    v_credit numeric(19,4);
    v_lines  int;
begin
    select coalesce(sum(debit_company), 0), coalesce(sum(credit_company), 0), count(*)
      into v_debit, v_credit, v_lines
      from ledger.journal_line
     where entry_id = new.entry_id;

    if v_lines < 2 then
        raise exception
            'UNBALANCED_ENTRY entry=% : قيد اليومية يحتاج سطرين على الأقل (وُجد %) / a journal entry needs at least two lines (found %)',
            new.entry_id, v_lines, v_lines
            using errcode = 'check_violation';
    end if;

    if v_debit <> v_credit then
        raise exception
            'UNBALANCED_ENTRY entry=% debit=% credit=% difference=%',
            new.entry_id, v_debit, v_credit, (v_debit - v_credit)
            using errcode = 'check_violation';
    end if;

    return null;
end
$fn$;

drop trigger if exists trg_journal_line_balanced on ledger.journal_line;
create constraint trigger trg_journal_line_balanced
    after insert on ledger.journal_line
    deferrable initially deferred
    for each row execute function ledger.assert_entry_balanced();

drop trigger if exists trg_journal_entry_balanced on ledger.journal_entry;
create constraint trigger trg_journal_entry_balanced
    after insert on ledger.journal_entry
    deferrable initially deferred
    for each row execute function ledger.assert_entry_balanced();

-- ═══════════════════════════════════════════════════════════════════════════
-- الطبقة الثالثة في قواعد الحجب: قيود لا يستطيع خطأ برمجي تجاوزها
--
-- GR-COA-001 — لا ترحيل على حساب تجميعي.
-- GR-COA-002 — لا ترحيل بلا كل بُعد إلزامي على الحساب.
-- GR-RE-001  — لا ترحيل إلى دور إيراد الإيجار على عقار «مُدار لصالح الغير».
-- ═══════════════════════════════════════════════════════════════════════════
create or replace function ledger.assert_line_allowed() returns trigger
language plpgsql security definer set search_path = ledger, pg_catalog as $fn$
declare
    v_postable   boolean;
    v_required   text[];
    v_dimension  text;
    v_ownership  text;
begin
    select is_postable, required_dimensions
      into v_postable, v_required
      from ledger.account
     where company_id = new.company_id and account_code = new.account_code;

    -- GR-COA-001
    if not coalesce(v_postable, false) then
        raise exception
            'GR-COA-001 account=% : يُمنع الترحيل على حساب تجميعي — الترحيل على الحساب التفصيلي فقط / posting to a rollup account is refused',
            new.account_code
            using errcode = 'check_violation';
    end if;

    -- GR-COA-002
    foreach v_dimension in array coalesce(v_required, '{}'::text[]) loop
        if (v_dimension = 'branch'      and new.branch_id      is null)
        or (v_dimension = 'cost_center' and new.cost_center_id is null)
        or (v_dimension = 'project'     and new.project_id     is null)
        or (v_dimension = 'property'    and new.property_id    is null)
        or (v_dimension = 'unit'        and new.unit_id        is null)
        or (v_dimension = 'warehouse'   and new.warehouse_id   is null)
        or (v_dimension = 'boq_item'    and new.boq_item_id    is null) then
            raise exception
                'GR-COA-002 account=% dimension=% : يُمنع الترحيل على هذا الحساب دون بُعده الإلزامي / posting without a mandatory dimension is refused',
                new.account_code, v_dimension
                using errcode = 'check_violation';
        end if;
    end loop;

    -- GR-RE-001 — الطبقة التي لا يتجاوزها خطأ برمجي.
    -- في نموذج إدارة أملاك الغير الأجرة المحصَّلة **التزام تجاه المالك** لا إيراد
    -- للشركة، وإيراد الشركة هو العمولة وحدها. الخطأ هنا يضخّم الإيراد واحداً
    -- وعشرين ضعفاً في المثال الوارد في 07-real-estate.md §1.3.
    if new.role_code = 'rental_revenue' and new.property_id is not null then
        select ownership_model into v_ownership
          from ledger.property_dimension
         where company_id = new.company_id and property_id = new.property_id;

        if v_ownership = 'managed_for_others' then
            raise exception
                'GR-RE-001 property=% : يُمنع الترحيل إلى حساب إيرادات الإيجار لأن نموذج ملكية العقار «مُدار لصالح الغير» / rental revenue posting refused under the managed_for_others model',
                new.property_id
                using errcode = 'check_violation';
        end if;
    end if;

    return null;
end
$fn$;

drop trigger if exists trg_journal_line_allowed on ledger.journal_line;
create constraint trigger trg_journal_line_allowed
    after insert on ledger.journal_line
    deferrable initially deferred
    for each row execute function ledger.assert_line_allowed();
