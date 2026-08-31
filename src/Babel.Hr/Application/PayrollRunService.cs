using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Hr.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Hr.Application;

/// <summary>
/// مسيّر الرواتب: بناؤه من الأجور المُسنَدة والمكوّنات والإعدادات السارية، ثم ترحيله
/// <b>قيداً لكل قسيمة</b>.
/// <para>
/// <b>ولا مجموع يرسله العميل</b>: مجموعٌ يصل من الخارج مصدرُ حقيقةٍ ثانٍ ينحرف عن
/// الأول، والمتطابقة المعلَنة في المصفوفة تُفحَص على صفّ القسيمة وتُفرَض في القاعدة.
/// </para>
/// </summary>
public sealed class PayrollRunService : IApplicationService
{
    /// <summary>نوع مستند القسيمة في هوية الإحكام — <b>وهو حبيبيّة الترحيل</b>.</summary>
    internal const string PayslipDocument = "HrPayslip";

    /// <summary>رمز حدث استحقاق الرواتب كما تسمّيه المصفوفة حرفياً.</summary>
    internal const string AccrualEvent = "hr.payroll.accrual";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly HrDbContext _database;
    private readonly PayrollSettingsService _settings;
    private readonly SubledgerPostingGateway _gateway;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="posting">عقد الترحيل.</param>
    /// <param name="settings">إعدادات النِّسَب — تُقرأ ولا تُخترع.</param>
    public PayrollRunService(
        IEntitlementEnforcer enforcer,
        HrRuntime runtime,
        IPostingService posting,
        PayrollSettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(settings);
        _enforcer = enforcer;
        _database = runtime.Database;
        _settings = settings;
        _gateway = new SubledgerPostingGateway(runtime.Database, posting, runtime.CostCenters);
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>
    /// يبني مسيّراً <b>مسوّدة</b> بقسائمه. <b>ويُرفض إن لم يوجد صفّ نِسَبٍ معتمد يغطّي
    /// الفترة</b> — رفضٌ صريح برمز مستقرّ يسمّي البند المعلَّق، ولا قيمة افتراضية واحدة.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Write)]
    public async ValueTask<Result<PayrollRunView>> DraftAsync(
        TenantId tenant,
        UserId actor,
        PayrollRunDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Write, "Hr.PayrollRun.Draft", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PayrollRunView>.Failure(gate.Errors);
        }

        if (await _database.PayrollRuns
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PayrollRunView>.Failure(HrErrors.DuplicateNumber(draft.Number));
        }

        // ‏**المنع في الخدمة لا في فهرس** — حتى يُجاب سؤال «هل يُسمح بأكثر من مسيّر
        // مُرحَّل للفترة؟». وفهرسٌ اليوم يفترض جوابه في مفتاح على جدولٍ لا يُحذف منه شيء.
        PayrollRunRow? existing = await _database.PayrollRuns
            .FirstOrDefaultAsync(
                row => row.TenantId == tenant.Value && row.PeriodCode == draft.PeriodCode, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return Result<PayrollRunView>.Failure(
                HrErrors.PeriodAlreadyHasARun(draft.PeriodCode, existing.Number));
        }

        List<EmploymentRow> employments = await _database.Employments
            .Where(row => row.TenantId == tenant.Value
                          && row.StartedOn <= draft.PeriodEnd
                          && (row.EndedOn == null || row.EndedOn >= draft.PeriodStart))
            .OrderBy(row => row.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (employments.Count == 0)
        {
            return Result<PayrollRunView>.Failure(HrErrors.NoPayslips);
        }

        Dictionary<Guid, EmployeeRow> employees = await _database.Employees
            .Where(row => row.TenantId == tenant.Value)
            .ToDictionaryAsync(row => row.Id, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, PayComponentRow> components = await _database.PayComponents
            .Where(row => row.TenantId == tenant.Value)
            .ToDictionaryAsync(row => row.Code, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        PayrollRunRow run = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Number = draft.Number,
            PeriodCode = draft.PeriodCode,
            PeriodStart = draft.PeriodStart,
            PeriodEnd = draft.PeriodEnd,
            State = HrDocumentState.Draft,
        };

        List<PayslipRow> payslips = [];
        List<PayslipComponentRow> lines = [];

        foreach (EmploymentRow employment in employments)
        {
            if (!employees.TryGetValue(employment.EmployeeId, out EmployeeRow? employee))
            {
                continue;
            }

            // ── الرفض الحاكم: لا صفّ نِسَبٍ سارٍ ⇒ لا مسيّر ────────────────────
            PayrollSettingsRow? rates = await _settings
                .EffectiveAsync(tenant, employee.ClassCode, draft.PeriodEnd, cancellationToken)
                .ConfigureAwait(false);

            if (rates is null)
            {
                return Result<PayrollRunView>.Failure(
                    HrErrors.PayrollSettingsMissing(employee.ClassCode, draft.PeriodEnd));
            }

            List<PayElementRow> elements = await _database.PayElements
                .Where(row => row.TenantId == tenant.Value
                              && row.EmploymentId == employment.Id
                              && row.EffectiveFrom <= draft.PeriodEnd)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // الصفّ السارِي لكل مكوّن: آخر تاريخ سريان لا يتجاوز نهاية الفترة.
            List<PayElementRow> effective =
            [
                .. elements
                    .GroupBy(static row => row.ComponentCode, StringComparer.Ordinal)
                    .Select(static group => group.OrderByDescending(static row => row.EffectiveFrom).First())
                    .OrderBy(static row => row.ComponentCode, StringComparer.Ordinal),
            ];

            decimal gross = 0m;
            decimal componentDeductions = 0m;
            decimal contributoryBase = 0m;
            PayslipRow payslip = new()
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                RunId = run.Id,
                EmployeeId = employee.Id,
                EmploymentId = employment.Id,
                EmployeeCode = employee.Code,
                CostCenterId = employee.CostCenterId,
                State = HrDocumentState.Draft,
            };

            int lineNo = 0;

            foreach (PayElementRow element in effective)
            {
                if (!components.TryGetValue(element.ComponentCode, out PayComponentRow? component))
                {
                    return Result<PayrollRunView>.Failure(HrErrors.PayComponentNotFound(element.ComponentCode));
                }

                if (string.Equals(component.Kind, "earning", StringComparison.Ordinal))
                {
                    gross += element.Amount;

                    if (component.EntersContributoryWage)
                    {
                        contributoryBase += element.Amount;
                    }
                }
                else
                {
                    componentDeductions += element.Amount;
                }

                lines.Add(new PayslipComponentRow
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = tenant.Value,
                    PayslipId = payslip.Id,
                    LineNo = ++lineNo,
                    ComponentCode = component.Code,
                    Kind = component.Kind,
                    EntersContributoryWage = component.EntersContributoryWage,
                    Amount = element.Amount,
                });
            }

            // الجزاءات المعتمدة لهذه الفترة، ما لم تُستهلك في قسيمة سابقة.
            List<EmployeeDeductionRow> penalties = await _database.Deductions
                .Where(row => row.TenantId == tenant.Value
                              && row.EmployeeId == employee.Id
                              && row.PeriodCode == draft.PeriodCode
                              && row.ConsumedByPayslipId == null)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // أقساط السلف المستحقّة في هذه الفترة، ما لم تُستقطع من قبل.
            List<Guid> advanceIds = await _database.Advances
                .Where(row => row.TenantId == tenant.Value && row.EmployeeId == employee.Id)
                .Select(row => row.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            List<AdvanceInstalmentRow> instalments = await _database.AdvanceInstalments
                .Where(row => row.TenantId == tenant.Value
                              && advanceIds.Contains(row.AdvanceId)
                              && row.PeriodCode == draft.PeriodCode
                              && row.ConsumedByPayslipId == null)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            decimal contributoryWage = PayrollMath.Clamp(
                contributoryBase, rates.MinimumContributoryWage, rates.MaximumContributoryWage);

            payslip.ContributoryWage = contributoryWage;
            payslip.GrossEntitlements = PayrollMath.ToCanonicalScale(gross);
            payslip.EmployerSocialInsurance = PayrollMath.Share(contributoryWage, rates.EmployerRate);
            payslip.EmployeeSocialInsurance = PayrollMath.Share(contributoryWage, rates.EmployeeRate);
            payslip.AdvanceInstalment = PayrollMath.ToCanonicalScale(instalments.Sum(static row => row.Amount));
            payslip.Deductions = PayrollMath.ToCanonicalScale(
                componentDeductions + penalties.Sum(static row => row.Amount));
            payslip.NetPayable = payslip.GrossEntitlements
                                 - payslip.EmployeeSocialInsurance
                                 - payslip.AdvanceInstalment
                                 - payslip.Deductions;

            foreach (EmployeeDeductionRow penalty in penalties)
            {
                penalty.ConsumedByPayslipId = payslip.Id;
            }

            foreach (AdvanceInstalmentRow instalment in instalments)
            {
                instalment.ConsumedByPayslipId = payslip.Id;
            }

            payslips.Add(payslip);
        }

        run.GrossEntitlements = payslips.Sum(static row => row.GrossEntitlements);
        run.EmployerSocialInsurance = payslips.Sum(static row => row.EmployerSocialInsurance);
        run.EmployeeSocialInsurance = payslips.Sum(static row => row.EmployeeSocialInsurance);
        run.AdvanceInstalment = payslips.Sum(static row => row.AdvanceInstalment);
        run.Deductions = payslips.Sum(static row => row.Deductions);
        run.NetPayable = payslips.Sum(static row => row.NetPayable);

        _database.PayrollRuns.Add(run);
        _database.Payslips.AddRange(payslips);
        _database.PayslipComponents.AddRange(lines);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PayrollRunView>.Success(View(run, payslips.Count));
    }

    /// <summary>يقرأ المسيّر بحالته ومجاميعه.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="runId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Read)]
    public async ValueTask<Result<PayrollRunView>> GetAsync(
        TenantId tenant,
        UserId actor,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Read, "Hr.PayrollRun.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PayrollRunView>.Failure(gate.Errors);
        }

        PayrollRunRow? run = await _database.PayrollRuns
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == runId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            return Result<PayrollRunView>.Failure(HrErrors.DocumentNotFound("PayrollRun", runId));
        }

        int count = await _database.Payslips
            .CountAsync(row => row.TenantId == tenant.Value && row.RunId == runId, cancellationToken)
            .ConfigureAwait(false);

        return Result<PayrollRunView>.Success(View(run, count));
    }

    /// <summary>
    /// يقرأ قسائم المسيّر <b>بمعرّفاتها ومعرّفات قيودها</b> — وبلا هذه المعرّفات يصير
    /// باب الدفع باباً لا يوصل إليه بابٌ آخر.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="runId">المسيّر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<PayslipView>>> ListPayslipsAsync(
        TenantId tenant,
        UserId actor,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Read, "Hr.Payslip.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<PayslipView>>.Failure(gate.Errors);
        }

        if (!await _database.PayrollRuns
                .AnyAsync(row => row.TenantId == tenant.Value && row.Id == runId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<IReadOnlyList<PayslipView>>.Failure(HrErrors.DocumentNotFound("PayrollRun", runId));
        }

        List<PayslipRow> rows = await _database.Payslips
            .Where(row => row.TenantId == tenant.Value && row.RunId == runId)
            .OrderBy(row => row.EmployeeCode)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<PayslipView>>.Success([.. rows.Select(row => Slip(row, []))]);
    }

    /// <summary>يقرأ قسيمة واحدة بمكوّناتها ومعرّف قيدها — <b>وهي مستند الترحيل</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="payslipId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Read)]
    public async ValueTask<Result<PayslipView>> GetPayslipAsync(
        TenantId tenant,
        UserId actor,
        Guid payslipId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Read, "Hr.Payslip.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PayslipView>.Failure(gate.Errors);
        }

        PayslipRow? payslip = await _database.Payslips
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == payslipId, cancellationToken)
            .ConfigureAwait(false);

        if (payslip is null)
        {
            return Result<PayslipView>.Failure(HrErrors.DocumentNotFound("Payslip", payslipId));
        }

        List<PayslipComponentRow> components = await _database.PayslipComponents
            .Where(row => row.PayslipId == payslipId)
            .OrderBy(row => row.LineNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<PayslipView>.Success(Slip(payslip, components));
    }

    /// <summary>
    /// يرحّل استحقاق المسيّر: <b>نداءٌ واحد يُصدر قيداً لكل قسيمة</b>، لكلٍّ هويّته
    /// السداسية بـ<c>DocumentId</c> = معرّف القسيمة.
    /// <para>
    /// <b>ولا قيد واحد للمسيّر بحال</b>: مسار القالب يحلّ الطرف من واقعةٍ واحدة لكل
    /// طلب ويقرأ مركز التكلفة من قاموسٍ واحد، فقيدٌ واحد لثلاثمئة موظف كان سيكتب ذمّة
    /// الجميع على طرفٍ واحد ومركزٍ واحد — <b>وهو متوازن تماماً، وسلسلته سليمة، وميزان
    /// مراجعته صحيح</b>، ولا شيء يُظهره.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="runId">المسيّر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Write)]
    public async ValueTask<Result<IReadOnlyList<PayslipView>>> PostAsync(
        TenantId tenant,
        UserId actor,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Write, "Hr.PayrollRun.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<PayslipView>>.Failure(gate.Errors);
        }

        PayrollRunRow? run = await _database.PayrollRuns
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == runId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            return Result<IReadOnlyList<PayslipView>>.Failure(HrErrors.DocumentNotFound("PayrollRun", runId));
        }

        List<PayslipRow> payslips = await _database.Payslips
            .Where(row => row.TenantId == tenant.Value && row.RunId == runId)
            .OrderBy(row => row.EmployeeCode)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (payslips.Count == 0)
        {
            return Result<IReadOnlyList<PayslipView>>.Failure(HrErrors.NoPayslips);
        }

        List<PayslipView> results = [];

        foreach (PayslipRow payslip in payslips)
        {
            PostingIntent intent = new()
            {
                Tenant = tenant,
                DocumentType = PayslipDocument,

                // ‏**هنا يقع القرار كلّه**: معرّف القسيمة لا معرّف المسيّر.
                DocumentId = payslip.Id,
                Trigger = PostingTrigger.OnApproval,
                Event = new PostingEventCode(AccrualEvent),
                DocumentDate = run.PeriodEnd,
                Narration = Narration(run.PeriodCode, payslip.EmployeeCode),
                Amounts =
                [
                    new PostingAmount("gross_entitlements", Money.Of(payslip.GrossEntitlements, _currency)),
                    new PostingAmount("employer_social_insurance", Money.Of(payslip.EmployerSocialInsurance, _currency)),
                    new PostingAmount("employee_social_insurance", Money.Of(payslip.EmployeeSocialInsurance, _currency)),
                    new PostingAmount("advance_installment", Money.Of(payslip.AdvanceInstalment, _currency)),
                    new PostingAmount("deductions", Money.Of(payslip.Deductions, _currency)),
                    new PostingAmount("net_payable", Money.Of(payslip.NetPayable, _currency)),
                ],
                Facts = [new PostingFact("subledger.employee", payslip.EmployeeCode)],
                Dimensions = [new PostingDimension("cost_center", payslip.CostCenterId)],
                PartyId = payslip.EmployeeCode,

                // أثر سطور دفتر الموظف وحدها بمنطق «مدين ناقص دائن»: ثلاثة سطور دائنة.
                ControlEffect = -(payslip.NetPayable + payslip.AdvanceInstalment + payslip.Deductions),
                Currency = _currency,
                Actor = actor,
                Generation = payslip.PostingGeneration,
            };

            Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);

            if (posted.IsFailure)
            {
                return Result<IReadOnlyList<PayslipView>>.Failure(posted.Errors);
            }

            payslip.State = HrDocumentState.Posted;
            payslip.PostedEntryId = posted.Value.JournalEntryId;
            results.Add(Slip(payslip, []) with { AlreadyPosted = posted.Value.WasAlreadyPosted });
        }

        run.State = HrDocumentState.Posted;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<IReadOnlyList<PayslipView>>.Success(results);
    }

    /// <summary>
    /// بيان القيد — <b>يُركَّب من الفترة والرمز المعتم وحدهما</b>، ولا نصّ حرّ فيه ولا
    /// اسم ولا رقم شخصي: حقلا البيان داخل البايتات المُجزَّأة، وما دخلها لا يُمحى.
    /// </summary>
    private static LocalizedName Narration(string periodCode, string employeeCode)
        => new(
            "استحقاق رواتب " + periodCode + " · " + employeeCode,
            "Payroll accrual " + periodCode + " · " + employeeCode);

    private PayrollAmounts Amounts(
        decimal gross, decimal employer, decimal employee, decimal advance, decimal deductions, decimal net)
        => new(
            Money.Of(gross, _currency),
            Money.Of(employer, _currency),
            Money.Of(employee, _currency),
            Money.Of(advance, _currency),
            Money.Of(deductions, _currency),
            Money.Of(net, _currency));

    private PayrollRunView View(PayrollRunRow run, int payslipCount) => new(
        run.Id,
        run.Number,
        run.PeriodCode,
        run.PeriodStart,
        run.PeriodEnd,
        run.State,
        Amounts(
            run.GrossEntitlements,
            run.EmployerSocialInsurance,
            run.EmployeeSocialInsurance,
            run.AdvanceInstalment,
            run.Deductions,
            run.NetPayable),
        payslipCount);

    private PayslipView Slip(PayslipRow row, IReadOnlyList<PayslipComponentRow> components) => new(
        row.Id,
        row.RunId,
        row.EmployeeId,
        row.EmploymentId,
        row.EmployeeCode,
        row.CostCenterId,
        Money.Of(row.ContributoryWage, _currency),
        Amounts(
            row.GrossEntitlements,
            row.EmployerSocialInsurance,
            row.EmployeeSocialInsurance,
            row.AdvanceInstalment,
            row.Deductions,
            row.NetPayable),
        [
            .. components.Select(component => new PayslipComponentView(
                component.LineNo,
                component.ComponentCode,
                component.Kind,
                component.EntersContributoryWage,
                Money.Of(component.Amount, _currency))),
        ],
        row.State,
        row.PostedEntryId,
        AlreadyPosted: false);
}
