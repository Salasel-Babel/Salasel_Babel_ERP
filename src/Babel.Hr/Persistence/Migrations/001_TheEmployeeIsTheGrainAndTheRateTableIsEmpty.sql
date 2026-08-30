-- ═══════════════════════════════════════════════════════════════════════════
-- هجرة الموارد البشرية 001 — الموظف هو الحبيبيّة، وجدول النِّسَب يُسلَّم فارغاً
-- ═══════════════════════════════════════════════════════════════════════════
--
-- **مخطّط `hr` جديدٌ كلّه**، فأوّل إنزال إنشاءٌ لا ترقية: `EnsureCreated` يبني الشكل
-- كاملاً في قاعدة فارغة **ولا يفعل شيئاً في قاعدة قائمة** — وهذا النصّ هو ما يجعل
-- قاعدةً أُنشئت بنسخة أقدم من النموذج تبلغ الشكل الحالي.
--
-- **وما يفرضه هذا النصّ بعد الإنشاء، ولا يترك واحداً منها لانضباط الشيفرة:**
--   ١ · هوية الإحكام **سداسية** على `hr.document_posting`، و`DocumentId` فيها
--       **معرّف القسيمة لا معرّف المسيّر**. وقيدٌ واحد للمسيّر يكتب طرفاً واحداً
--       ومركز تكلفة واحداً لكل الموظفين، **وهو متوازن تماماً** — لا ميزان مراجعة
--       يُظهره ولا سلسلة بصمات.
--   ٢ · متطابقة القسيمة مفروضة في القاعدة:
--       net_payable = gross_entitlements - employee_social_insurance
--                     - advance_installment - deductions
--   ٣ · متطابقة المخالصة: provision_utilised = amount_paid - shortfall + excess
--   ٤ · صفّ نِسَبٍ بلا مرجع مصدر مرفوض: النسبة تدخل من هذا الجدول وحده، ومن
--       يُدخلها يُسمّي مصدرها ومعتمِدها (‏CONTRIBUTING §3.6 · البند م-14).
--   ٥ · حركة المخصص مفتاحها (المستأجر · **علاقة العمل** · الفترة): المخالصة
--       تحتاج الرصيد لهذا الموظف، لا رقماً للمنشأة.
--
-- **ولا صفّ بيانات واحد يُدرَج هنا.** ولا نسبة، ولا سقف أجر خاضع، ولا مدّة إشعار،
-- ولا معادلة مكافأة. جدول `hr.payroll_settings` يُسلَّم **فارغاً**، ومسيّرٌ لفترة لا
-- يغطّيها صفٌّ سارٍ معتمد يُرفض بـ`hr.payroll_settings_missing`.
--
-- والهجرة **معاملاتية** ومكتوبة لتُعاد بلا أثر.

do $migration$
begin
    -- قاعدة لم يُنشأ فيها المخطّط بعد ليست حالة خطأ.
    if to_regclass('hr.document_posting') is null then
        return;
    end if;

    -- ── ١ · هوية الإحكام السداسية، ورمز حدثٍ غير فارغ ───────────────────────
    create unique index if not exists uq_hr_posting_identity
        on hr.document_posting ("TenantId", "DocumentType", "DocumentId", "TriggerCode", "Generation", "EventCode");

    if not exists (
        select 1 from pg_constraint where conname = 'ck_hr_document_posting_event_code')
    then
        alter table hr.document_posting
            add constraint ck_hr_document_posting_event_code
            check (length(btrim("EventCode")) > 0);
    end if;

    -- ── ٢ · متطابقة القسيمة ─────────────────────────────────────────────────
    if to_regclass('hr.payslip') is not null
       and not exists (select 1 from pg_constraint where conname = 'ck_hr_payslip_net_identity')
    then
        alter table hr.payslip
            add constraint ck_hr_payslip_net_identity
            check ("NetPayable" = "GrossEntitlements" - "EmployeeSocialInsurance"
                                  - "AdvanceInstalment" - "Deductions");
    end if;

    -- ── ٣ · متطابقة المخالصة ────────────────────────────────────────────────
    if to_regclass('hr.eos_settlement') is not null
       and not exists (select 1 from pg_constraint where conname = 'ck_hr_eos_settlement_identity')
    then
        alter table hr.eos_settlement
            add constraint ck_hr_eos_settlement_identity
            check ("ProvisionUtilised" = "AmountPaid" - "Shortfall" + "Excess");
    end if;

    -- ── ٤ · لا صفّ نِسَبٍ بلا مرجع مصدر ─────────────────────────────────────
    if to_regclass('hr.payroll_settings') is not null
       and not exists (select 1 from pg_constraint where conname = 'ck_hr_payroll_settings_source_ref')
    then
        alter table hr.payroll_settings
            add constraint ck_hr_payroll_settings_source_ref
            check (length(btrim("SourceRef")) > 0);
    end if;

    -- ── ٥ · لا مرجع أساس قياس فارغ على مستند الاستحقاق ──────────────────────
    if to_regclass('hr.eos_provision') is not null
       and not exists (select 1 from pg_constraint where conname = 'ck_hr_eos_provision_measurement_ref')
    then
        alter table hr.eos_provision
            add constraint ck_hr_eos_provision_measurement_ref
            check (length(btrim("MeasurementRef")) > 0);
    end if;

    -- ── ٦ · حركة المخصص بحبيبيّة علاقة العمل ────────────────────────────────
    if to_regclass('hr.eos_provision_movement') is not null then
        create unique index if not exists uq_hr_eos_movement_period
            on hr.eos_provision_movement ("TenantId", "EmploymentId", "PeriodCode");
    end if;

    -- ── ٧ · صفّ الأجر يُضاف ولا يُعدَّل ─────────────────────────────────────
    if to_regclass('hr.pay_element') is not null then
        create unique index if not exists uq_hr_pay_element_effective
            on hr.pay_element ("TenantId", "EmploymentId", "ComponentCode", "EffectiveFrom");
    end if;
end
$migration$;
