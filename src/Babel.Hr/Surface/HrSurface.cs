using Babel.Hr.Application;
using Babel.Hr.Subledger;
using Babel.SharedKernel;

namespace Babel.Hr.Surface;

/// <summary>
/// <b>السطح المنشور لوحدة الموارد البشرية</b> — وهو ما يجوز للجذر التركيبي أن يسمّيه،
/// ولا شيء غيره.
/// <para>
/// <b>لماذا يوجد هذا الملفّ أصلاً:</b> القاعدة 13 (البند ب) تمنع <c>Babel.Api</c> من أن
/// يذكر أيّ نوع من فضاء اسم داخلي لوحدة — و<c>Application</c> و<c>Persistence</c>
/// و<c>Subledger</c> منها بالاسم، <b>ولو أُضيف النوع إلى قائمة السطح المنشور</b>.
/// والشكل مأخوذ حرفياً من <c>SalesSurface</c> و<c>InventorySurface</c>.
/// </para>
/// <para>
/// <b>وما لا يفعله هذا الملفّ — عمداً:</b> لا يُنفِذ استحقاقاً، ولا يقرّر شيئاً محاسبياً،
/// ولا يقرأ جدولاً، ولا يحسب مبلغاً. كل دالّة هنا تُترجم نوعاً منشوراً إلى مسوّدة الوحدة
/// وتنادي خدمة التطبيق التي تحمل <c>[RequiresEntitlement]</c> وتنادي المنفِّذ أوّل شيء.
/// </para>
/// <para>
/// <b>والمال يعبر هذا الحدّ <c>decimal</c> لا <c>Money</c></b> — كما في المبيعات
/// والمخزون: <c>Money</c> يحمل عملةً، وعملةُ المنشأة إعدادُ وحدةٍ لا معلومةُ نقل.
/// </para>
/// <para>
/// <b>ولاحظ ما ليس على هذا السطح إطلاقاً — ولكلٍّ سببه المكتوب:</b>
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>لا ترحيل لصرف السلفة.</b> الحدث <c>hr.employee_advance.paid</c> غير موجود في
///     مصفوفة الترحيل، والمحرك يرفض رمزاً لا يعرفه ولا يخترع قالباً. وبابٌ يَعِد بدورة
///     لا تكتمل أسوأ من غيابه.
///   </description></item>
///   <item><description>
///     <b>لا إجازات: لا جدول ولا باب.</b> مسحُ بيانات المصفوفة والدليل كلّها يعطي صفر
///     مطابقة لإجازة — لا حساب ولا دور ولا حدث. ونشرُ سجلٍّ لا يكتبه قيد ولا تطابقه
///     نقطة ضبط هو بعينه ما رُفض في باب السلفة، والمعيار لا يُطبَّق على أحدهما دون الآخر.
///   </description></item>
///   <item><description>
///     <b>لا توليد لملفّ حماية الأجور.</b> مواصفة الملفّ نفسها <b>غير متحقَّق منها</b>
///     في هذا المستودع، ومخزن المرفقات المنشور يقبل <b>مجموعة أنواع محتوى مغلقة</b>
///     ليس فيها نوعٌ نصّي — وتوسيعُها تغييرٌ في مجموعة مغلقة منشورة. فالباب مؤجَّل
///     ومكتوب في دَين التحقّق.
///   </description></item>
///   <item><description>
///     <b>ولا <c>PUT</c> ولا <c>PATCH</c> ولا <c>DELETE</c> على أي مورد</b>: الإنهاء
///     والترحيل موارد فرعية، وصفّ الأجر وصفّ النِّسَب يُضاف إليهما ولا يُعدَّلان.
///   </description></item>
/// </list>
/// </summary>
public sealed class HrSurface
{
    private readonly EmployeeService _employees;
    private readonly PayrollSettingsService _settings;
    private readonly PayrollRunService _runs;
    private readonly PayrollPaymentService _payments;
    private readonly SocialInsurancePaymentService _socialInsurance;
    private readonly EmployeeLedgerService _register;
    private readonly EndOfServiceService _endOfService;
    private readonly EmployeeReconciliationService _reconciliation;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ السطح فوق خدمات الوحدة.</summary>
    /// <param name="employees">البيانات الأساسية.</param>
    /// <param name="settings">إعدادات النِّسَب.</param>
    /// <param name="runs">المسيّر والقسائم.</param>
    /// <param name="payments">سندات صرف الرواتب.</param>
    /// <param name="socialInsurance">سداد التأمينات.</param>
    /// <param name="register">الجزاءات والسلف.</param>
    /// <param name="endOfService">مخصص نهاية الخدمة ومخالصته.</param>
    /// <param name="reconciliation">مطابقة دفتر الموظف.</param>
    /// <param name="options">إعدادات الوحدة — ومنها عملة المنشأة.</param>
    public HrSurface(
        EmployeeService employees,
        PayrollSettingsService settings,
        PayrollRunService runs,
        PayrollPaymentService payments,
        SocialInsurancePaymentService socialInsurance,
        EmployeeLedgerService register,
        EndOfServiceService endOfService,
        EmployeeReconciliationService reconciliation,
        HrOptions options)
    {
        ArgumentNullException.ThrowIfNull(employees);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(socialInsurance);
        ArgumentNullException.ThrowIfNull(register);
        ArgumentNullException.ThrowIfNull(endOfService);
        ArgumentNullException.ThrowIfNull(reconciliation);
        ArgumentNullException.ThrowIfNull(options);

        _employees = employees;
        _settings = settings;
        _runs = runs;
        _payments = payments;
        _socialInsurance = socialInsurance;
        _register = register;
        _endOfService = endOfService;
        _reconciliation = reconciliation;
        _currency = CurrencyCode.FromString(options.CompanyCurrency);
    }

    // ── الموظفون والبيانات الأساسية ──────────────────────────────────────────

    /// <summary>يسجّل موظفاً. <b>والخادم يولّد رمزه المعتم ولا يرسله العميل.</b></summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrEmployee>> RegisterEmployeeAsync(
        TenantId tenant,
        UserId actor,
        HrEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<EmployeeView> result = await _employees
            .RegisterAsync(
                tenant,
                actor,
                new EmployeeDraft(
                    request.Name,
                    request.ClassCode,
                    request.CostCenterId,
                    request.HiredOn,
                    new EmployeeIdentityDraft(
                        request.Identity.NationalId, request.Identity.Iban, request.Identity.BirthDate)),
                cancellationToken)
            .ConfigureAwait(false);

        return Map(result, Employee);
    }

    /// <summary>يقرأ موظفاً واحداً بهويته <b>مقنَّعة</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="employeeId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrEmployee>> ReadEmployeeAsync(
        TenantId tenant,
        UserId actor,
        Guid employeeId,
        CancellationToken cancellationToken = default)
        => Map(
            await _employees.GetAsync(tenant, actor, employeeId, cancellationToken).ConfigureAwait(false),
            Employee);

    /// <summary>يُنهي خدمة موظف — مورداً فرعياً لا حقلَ حالة يُعدَّل.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="employeeId">الموظف.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrEmployee>> TerminateEmployeeAsync(
        TenantId tenant,
        UserId actor,
        Guid employeeId,
        HrTerminationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Map(
            await _employees
                .TerminateAsync(tenant, actor, employeeId, request.EndedOn, request.ReasonKey, cancellationToken)
                .ConfigureAwait(false),
            Employee);
    }

    /// <summary>يعرّف مكوّن أجر بوسمَيه.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrPayComponent>> AddPayComponentAsync(
        TenantId tenant,
        UserId actor,
        HrPayComponentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PayComponentView> result = await _employees
            .AddPayComponentAsync(
                tenant,
                actor,
                new PayComponentDraft(
                    request.Code,
                    request.Name,
                    request.Kind,
                    request.EntersContributoryWage,
                    request.EntersEndOfServiceBase),
                cancellationToken)
            .ConfigureAwait(false);

        return Map(result, Component);
    }

    /// <summary>يقرأ تصنيفات مكوّنات الأجر.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<HrPayComponent>>> ListPayComponentsAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
        => MapMany(
            await _employees.ListPayComponentsAsync(tenant, actor, cancellationToken).ConfigureAwait(false),
            Component);

    /// <summary>يُسند قيمة مكوّن بتاريخ سريان — إنشاءٌ لا تعديل.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="employeeId">الموظف.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrPayElement>> AddPayElementAsync(
        TenantId tenant,
        UserId actor,
        Guid employeeId,
        HrPayElementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PayElementView> result = await _employees
            .AddPayElementAsync(
                tenant,
                actor,
                employeeId,
                new PayElementDraft(request.ComponentCode, request.EffectiveFrom, Money.Of(request.Amount, _currency)),
                cancellationToken)
            .ConfigureAwait(false);

        return Map(result, Element);
    }

    /// <summary>يقرأ أجر الموظف بسريانه.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="employeeId">الموظف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<HrPayElement>>> ListPayElementsAsync(
        TenantId tenant,
        UserId actor,
        Guid employeeId,
        CancellationToken cancellationToken = default)
        => MapMany(
            await _employees
                .ListPayElementsAsync(tenant, actor, employeeId, _currency, cancellationToken)
                .ConfigureAwait(false),
            Element);

    // ── النِّسَب ─────────────────────────────────────────────────────────────

    /// <summary>يودِع إصداراً من النِّسَب وحدودها بتاريخ سريانه ومعتمِده ومصدره.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrPayrollSettings>> DepositPayrollSettingsAsync(
        TenantId tenant,
        UserId actor,
        HrPayrollSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PayrollSettingsView> result = await _settings
            .DepositAsync(
                tenant,
                actor,
                new PayrollSettingsDraft(
                    request.ClassCode,
                    request.EffectiveFrom,
                    request.EmployerRate,
                    request.EmployeeRate,
                    Money.Of(request.MinimumContributoryWage, _currency),
                    Money.Of(request.MaximumContributoryWage, _currency),
                    request.ApprovedBy,
                    request.ApprovedOn,
                    request.SourceRef),
                cancellationToken)
            .ConfigureAwait(false);

        return Map(result, Settings);
    }

    /// <summary>يقرأ إصدارات النِّسَب بسريانها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<HrPayrollSettings>>> ListPayrollSettingsAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
        => MapMany(
            await _settings.ListAsync(tenant, actor, _currency, cancellationToken).ConfigureAwait(false),
            Settings);

    // ── المسيّر والقسائم ─────────────────────────────────────────────────────

    /// <summary>يُنشئ مسيّراً <b>مسوّدة</b>، ويُرفض بلا صفّ نِسَبٍ معتمد يغطّي الفترة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrPayrollRun>> DraftPayrollRunAsync(
        TenantId tenant,
        UserId actor,
        HrPayrollRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PayrollRunView> result = await _runs
            .DraftAsync(
                tenant,
                actor,
                new PayrollRunDraft(request.Number, request.PeriodCode, request.PeriodStart, request.PeriodEnd),
                cancellationToken)
            .ConfigureAwait(false);

        return Map(result, Run);
    }

    /// <summary>يقرأ المسيّر بحالته ومجاميعه.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="runId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrPayrollRun>> ReadPayrollRunAsync(
        TenantId tenant,
        UserId actor,
        Guid runId,
        CancellationToken cancellationToken = default)
        => Map(await _runs.GetAsync(tenant, actor, runId, cancellationToken).ConfigureAwait(false), Run);

    /// <summary>يقرأ قسائم المسيّر بمعرّفاتها ومعرّفات قيودها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="runId">المسيّر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<HrPayslip>>> ListPayslipsAsync(
        TenantId tenant,
        UserId actor,
        Guid runId,
        CancellationToken cancellationToken = default)
        => MapMany(
            await _runs.ListPayslipsAsync(tenant, actor, runId, cancellationToken).ConfigureAwait(false),
            Slip);

    /// <summary>يقرأ قسيمة واحدة بمكوّناتها ومعرّف قيدها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="payslipId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrPayslip>> ReadPayslipAsync(
        TenantId tenant,
        UserId actor,
        Guid payslipId,
        CancellationToken cancellationToken = default)
        => Map(await _runs.GetPayslipAsync(tenant, actor, payslipId, cancellationToken).ConfigureAwait(false), Slip);

    /// <summary>يرحّل الاستحقاق — <b>قيداً لكل قسيمة</b>، ويُرجع لكلٍّ معرّف قيدها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="runId">المسيّر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<HrPayslip>>> PostPayrollRunAsync(
        TenantId tenant,
        UserId actor,
        Guid runId,
        CancellationToken cancellationToken = default)
        => MapMany(await _runs.PostAsync(tenant, actor, runId, cancellationToken).ConfigureAwait(false), Slip);

    // ── مستندات الدفع ────────────────────────────────────────────────────────

    /// <summary>يُنشئ سند صرف رواتب <b>مسوّدة</b> على مسيّر مُرحَّل.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrPayrollPayment>> DraftPayrollPaymentAsync(
        TenantId tenant,
        UserId actor,
        HrPayrollPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PayrollPaymentView> result = await _payments
            .DraftAsync(
                tenant,
                actor,
                new PayrollPaymentDraft(
                    request.Number, request.RunId, request.PaidOn, request.SettlementMethod, request.TreasuryPartyId),
                cancellationToken)
            .ConfigureAwait(false);

        return Map(result, Payment);
    }

    /// <summary>يقرأ سند الصرف وسطوره ومعرّفات قيودها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="paymentId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrPayrollPayment>> ReadPayrollPaymentAsync(
        TenantId tenant,
        UserId actor,
        Guid paymentId,
        CancellationToken cancellationToken = default)
        => Map(await _payments.GetAsync(tenant, actor, paymentId, cancellationToken).ConfigureAwait(false), Payment);

    /// <summary>يرحّل صرف الرواتب — قيداً لكل سطر، ومعه طرف الخزينة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="paymentId">السند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrPayrollPayment>> PostPayrollPaymentAsync(
        TenantId tenant,
        UserId actor,
        Guid paymentId,
        CancellationToken cancellationToken = default)
        => Map(await _payments.PostAsync(tenant, actor, paymentId, cancellationToken).ConfigureAwait(false), Payment);

    /// <summary>يُنشئ سند سداد تأمينات <b>مسوّدة</b> للفترة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrSocialInsurancePayment>> DraftSocialInsurancePaymentAsync(
        TenantId tenant,
        UserId actor,
        HrSocialInsurancePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<SocialInsurancePaymentView> result = await _socialInsurance
            .DraftAsync(
                tenant,
                actor,
                new SocialInsurancePaymentDraft(
                    request.Number,
                    request.PeriodCode,
                    request.PaidOn,
                    Money.Of(request.Amount, _currency),
                    request.SettlementMethod,
                    request.TreasuryPartyId),
                cancellationToken)
            .ConfigureAwait(false);

        return Map(result, SocialInsurance);
    }

    /// <summary>يقرأ سند سداد التأمينات ومعه ما استُحقّ في فترته.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="paymentId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrSocialInsurancePayment>> ReadSocialInsurancePaymentAsync(
        TenantId tenant,
        UserId actor,
        Guid paymentId,
        CancellationToken cancellationToken = default)
        => Map(
            await _socialInsurance.GetAsync(tenant, actor, paymentId, cancellationToken).ConfigureAwait(false),
            SocialInsurance);

    /// <summary>يرحّل سداد التأمينات — <b>قيداً واحداً للفترة</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="paymentId">السند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrSocialInsurancePayment>> PostSocialInsurancePaymentAsync(
        TenantId tenant,
        UserId actor,
        Guid paymentId,
        CancellationToken cancellationToken = default)
        => Map(
            await _socialInsurance.PostAsync(tenant, actor, paymentId, cancellationToken).ConfigureAwait(false),
            SocialInsurance);

    // ── الجزاءات والسلف ──────────────────────────────────────────────────────

    /// <summary>يقيّد جزاءً معتمداً بفئة سببه. <b>ولا مورد ترحيل له.</b></summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrDeduction>> RecordDeductionAsync(
        TenantId tenant,
        UserId actor,
        HrDeductionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<EmployeeDeductionView> result = await _register
            .RecordDeductionAsync(
                tenant,
                actor,
                new EmployeeDeductionDraft(
                    request.EmployeeId,
                    request.PeriodCode,
                    request.CategoryKey,
                    Money.Of(request.Amount, _currency),
                    request.ApprovedBy,
                    request.ApprovedOn),
                cancellationToken)
            .ConfigureAwait(false);

        return Map(result, Deduction);
    }

    /// <summary>يقرأ جزاءً واحداً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="deductionId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrDeduction>> ReadDeductionAsync(
        TenantId tenant,
        UserId actor,
        Guid deductionId,
        CancellationToken cancellationToken = default)
        => Map(
            await _register.GetDeductionAsync(tenant, actor, deductionId, cancellationToken).ConfigureAwait(false),
            Deduction);

    /// <summary>يُنشئ سلفة <b>مسوّدة</b> بجدول أقساطها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrAdvance>> DraftAdvanceAsync(
        TenantId tenant,
        UserId actor,
        HrAdvanceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<EmployeeAdvanceView> result = await _register
            .DraftAdvanceAsync(
                tenant,
                actor,
                new EmployeeAdvanceDraft(
                    request.Number,
                    request.EmployeeId,
                    request.IssuedOn,
                    Money.Of(request.Amount, _currency),
                    request.SettlementMethod,
                    request.TreasuryPartyId,
                    [
                        .. request.Instalments.Select(line =>
                            new AdvanceInstalmentDraft(line.PeriodCode, Money.Of(line.Amount, _currency))),
                    ]),
                cancellationToken)
            .ConfigureAwait(false);

        return Map(result, Advance);
    }

    /// <summary>يقرأ سلفة بجدول سدادها والمتبقّي منها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="advanceId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrAdvance>> ReadAdvanceAsync(
        TenantId tenant,
        UserId actor,
        Guid advanceId,
        CancellationToken cancellationToken = default)
        => Map(
            await _register.GetAdvanceAsync(tenant, actor, advanceId, cancellationToken).ConfigureAwait(false),
            Advance);

    // ── نهاية الخدمة ─────────────────────────────────────────────────────────

    /// <summary>يُنشئ مستند استحقاق المخصص <b>مسوّدة</b> بحصص علاقات العمل.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrProvision>> DraftProvisionAsync(
        TenantId tenant,
        UserId actor,
        HrProvisionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<EndOfServiceProvisionView> result = await _endOfService
            .DraftProvisionAsync(
                tenant,
                actor,
                new EndOfServiceProvisionDraft(
                    request.Number,
                    request.PeriodCode,
                    request.AccruedOn,
                    request.MeasurementRef,
                    request.ApprovedBy,
                    [
                        .. request.Shares.Select(share =>
                            new ProvisionShareDraft(share.EmploymentId, Money.Of(share.PeriodShare, _currency))),
                    ]),
                cancellationToken)
            .ConfigureAwait(false);

        return Map(result, Provision);
    }

    /// <summary>يقرأ مستند الاستحقاق بحركاته.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="provisionId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrProvision>> ReadProvisionAsync(
        TenantId tenant,
        UserId actor,
        Guid provisionId,
        CancellationToken cancellationToken = default)
        => Map(
            await _endOfService.GetProvisionAsync(tenant, actor, provisionId, cancellationToken).ConfigureAwait(false),
            Provision);

    /// <summary>يرحّل الاستحقاق — قيداً لكل علاقة عمل.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="provisionId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrProvision>> PostProvisionAsync(
        TenantId tenant,
        UserId actor,
        Guid provisionId,
        CancellationToken cancellationToken = default)
        => Map(
            await _endOfService.PostProvisionAsync(tenant, actor, provisionId, cancellationToken).ConfigureAwait(false),
            Provision);

    /// <summary>يُنشئ مخالصة <b>مسوّدة</b> على علاقة عمل منتهية.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrSettlement>> DraftSettlementAsync(
        TenantId tenant,
        UserId actor,
        HrSettlementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<EndOfServiceSettlementView> result = await _endOfService
            .DraftSettlementAsync(
                tenant,
                actor,
                new EndOfServiceSettlementDraft(
                    request.Number,
                    request.EmploymentId,
                    request.SettledOn,
                    Money.Of(request.SettlementDue, _currency),
                    request.MeasurementRef,
                    request.SettlementMethod,
                    request.TreasuryPartyId),
                cancellationToken)
            .ConfigureAwait(false);

        return Map(result, Settlement);
    }

    /// <summary>يقرأ المخالصة قبل ترحيلها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="settlementId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrSettlement>> ReadSettlementAsync(
        TenantId tenant,
        UserId actor,
        Guid settlementId,
        CancellationToken cancellationToken = default)
        => Map(
            await _endOfService.GetSettlementAsync(tenant, actor, settlementId, cancellationToken).ConfigureAwait(false),
            Settlement);

    /// <summary>يرحّل المخالصة بسيناريوهاتها الثلاثة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="settlementId">المخالصة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrSettlement>> PostSettlementAsync(
        TenantId tenant,
        UserId actor,
        Guid settlementId,
        CancellationToken cancellationToken = default)
        => Map(
            await _endOfService.PostSettlementAsync(tenant, actor, settlementId, cancellationToken).ConfigureAwait(false),
            Settlement);

    // ── المطابقة ─────────────────────────────────────────────────────────────

    /// <summary>يطابق دفتر الموظف بنقطة ضبطه <b>مستنداً بمستند</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="asOf">تاريخ المطابقة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<HrReconciliation>> ReconcileEmployeeSubledgerAsync(
        TenantId tenant,
        UserId actor,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
        => Map(
            await _reconciliation.ReconcileAsync(tenant, actor, asOf, cancellationToken).ConfigureAwait(false),
            Reconciliation);

    // ── ترجمة الأنواع — نقلٌ بلا قرار ────────────────────────────────────────

    private static Result<TOut> Map<TIn, TOut>(Result<TIn> result, Func<TIn, TOut> project)
        => result.IsFailure ? Result<TOut>.Failure(result.Errors) : Result<TOut>.Success(project(result.Value));

    private static Result<IReadOnlyList<TOut>> MapMany<TIn, TOut>(
        Result<IReadOnlyList<TIn>> result, Func<TIn, TOut> project)
        => result.IsFailure
            ? Result<IReadOnlyList<TOut>>.Failure(result.Errors)
            : Result<IReadOnlyList<TOut>>.Success([.. result.Value.Select(project)]);

    private static HrEmployee Employee(EmployeeView view) => new(
        view.Id,
        view.Code,
        view.Name,
        view.ClassCode,
        view.CostCenterId,
        view.EmploymentId,
        view.StartedOn,
        view.EndedOn,
        view.State,
        new HrMaskedIdentity(view.Identity.NationalIdMask, view.Identity.IbanMask));

    private static HrPayComponent Component(PayComponentView view) => new(
        view.Id, view.Code, view.Name, view.Kind, view.EntersContributoryWage, view.EntersEndOfServiceBase);

    private static HrPayElement Element(PayElementView view)
        => new(view.Id, view.ComponentCode, view.EffectiveFrom, view.Amount.Amount);

    private static HrPayrollSettings Settings(PayrollSettingsView view) => new(
        view.Id,
        view.ClassCode,
        view.EffectiveFrom,
        view.EmployerRate,
        view.EmployeeRate,
        view.MinimumContributoryWage.Amount,
        view.MaximumContributoryWage.Amount,
        view.ApprovedBy,
        view.ApprovedOn,
        view.SourceRef);

    private static HrPayrollAmounts Amounts(PayrollAmounts amounts) => new(
        amounts.GrossEntitlements.Amount,
        amounts.EmployerSocialInsurance.Amount,
        amounts.EmployeeSocialInsurance.Amount,
        amounts.AdvanceInstalment.Amount,
        amounts.Deductions.Amount,
        amounts.NetPayable.Amount);

    private static HrPayrollRun Run(PayrollRunView view) => new(
        view.Id,
        view.Number,
        view.PeriodCode,
        view.PeriodStart,
        view.PeriodEnd,
        view.State,
        Amounts(view.Amounts),
        view.PayslipCount);

    private static HrPayslip Slip(PayslipView view) => new(
        view.Id,
        view.RunId,
        view.EmployeeId,
        view.EmploymentId,
        view.EmployeeCode,
        view.CostCenterId,
        view.ContributoryWage.Amount,
        Amounts(view.Amounts),
        [
            .. view.Components.Select(static component => new HrPayslipComponent(
                component.LineNo,
                component.ComponentCode,
                component.Kind,
                component.EntersContributoryWage,
                component.Amount.Amount)),
        ],
        view.State,
        view.EntryId,
        view.AlreadyPosted);

    private static HrPayrollPayment Payment(PayrollPaymentView view) => new(
        view.Id,
        view.Number,
        view.RunId,
        view.PaidOn,
        view.SettlementMethod,
        view.TreasuryPartyId,
        view.NetPayable.Amount,
        view.State,
        [
            .. view.Lines.Select(static line => new HrPayrollPaymentLine(
                line.LineNo, line.PayslipId, line.EmployeeCode, line.Amount.Amount, line.EntryId)),
        ],
        view.AlreadyPosted);

    private static HrSocialInsurancePayment SocialInsurance(SocialInsurancePaymentView view) => new(
        view.Id,
        view.Number,
        view.PeriodCode,
        view.PaidOn,
        view.Amount.Amount,
        view.AccruedForPeriod.Amount,
        view.SettlementMethod,
        view.TreasuryPartyId,
        view.State,
        view.EntryId,
        view.AlreadyPosted);

    private static HrDeduction Deduction(EmployeeDeductionView view) => new(
        view.Id,
        view.EmployeeId,
        view.EmployeeCode,
        view.PeriodCode,
        view.CategoryKey,
        view.Amount.Amount,
        view.ApprovedBy,
        view.ApprovedOn,
        view.ConsumedByPayslipId);

    private static HrAdvance Advance(EmployeeAdvanceView view) => new(
        view.Id,
        view.Number,
        view.EmployeeId,
        view.EmployeeCode,
        view.IssuedOn,
        view.Amount.Amount,
        view.SettlementMethod,
        view.TreasuryPartyId,
        view.OutstandingAmount.Amount,
        view.State,
        [
            .. view.Instalments.Select(static line => new HrInstalment(
                line.LineNo, line.PeriodCode, line.Amount.Amount, line.ConsumedByPayslipId)),
        ]);

    private static HrProvision Provision(EndOfServiceProvisionView view) => new(
        view.Id,
        view.Number,
        view.PeriodCode,
        view.AccruedOn,
        view.MeasurementRef,
        view.ApprovedBy,
        view.PeriodShare.Amount,
        view.State,
        [
            .. view.Movements.Select(static movement => new HrProvisionMovement(
                movement.Id,
                movement.EmploymentId,
                movement.EmployeeCode,
                movement.PeriodShare.Amount,
                movement.EntryId)),
        ],
        view.AlreadyPosted);

    private static HrSettlement Settlement(EndOfServiceSettlementView view) => new(
        view.Id,
        view.Number,
        view.EmploymentId,
        view.EmployeeCode,
        view.SettledOn,
        view.SettlementDue.Amount,
        view.ProvisionBalance.Amount,
        view.AmountPaid.Amount,
        view.Shortfall.Amount,
        view.Excess.Amount,
        view.ProvisionUtilised.Amount,
        view.ScenarioCode,
        view.MeasurementRef,
        view.SettlementMethod,
        view.TreasuryPartyId,
        view.State,
        view.EntryId,
        view.AlreadyPosted);

    private static HrReconciliation Reconciliation(EmployeeReconciliationReport report) => new(
        report.AsOf,
        report.MatchedDocuments,
        report.IsReconciled,
        [
            .. report.Divergences.Select(static divergence => new HrReconciliationDivergence(
                divergence.DocumentType,
                divergence.DocumentId,
                divergence.PartyId,
                divergence.SubledgerEffect.Amount,
                divergence.ControlEffect.Amount,
                divergence.Divergence.Amount,
                divergence.ReasonCode)),
        ]);
}
