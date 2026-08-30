using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Hr.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Hr.Application;

/// <summary>
/// مخصص مكافأة نهاية الخدمة: استحقاقه الدوري، ومخالصته.
/// <para>
/// <b>والوحدة لا تقيس المخصص ولا تعرف معادلته.</b> طريقة قياس المخصص ومدخلاتها —
/// وأيّ أجرٍ يدخل الوعاء، وكيف تُعامَل الاستقالة مقابل الإنهاء، وهل يُخصم زمنياً —
/// كلّها <b>تحتاج اعتماد المحاسب القانوني</b>، ونصّ المصفوفة على المبلغ صريح: «بطريقة
/// القياس المعتمدة — لا تُخترع في هذا التسليم». فالمبلغ يصل من معتمِد المستند ومعه
/// مرجعٌ يسمّي أساسه، والوحدة <b>تُثبت الحركة ولا تُقدّرها</b>.
/// </para>
/// <para>
/// <b>وما تحسبه الوحدة من عندها هو ما تملكه وحدها</b>: رصيد المخصص لعلاقة عمل — مجموع
/// حركاتها المُرحَّلة ناقص ما استُنفد في مخالصات سابقة — ثم العجز والزيادة والسيناريو
/// المنطبق، وكلّها اشتقاقٌ حسابي من رقمين لا اجتهادٌ محاسبي.
/// </para>
/// <para>
/// <b>ولا مُشغّل دوري ولا جدول عمل ولا مجدوِل في هذه الوحدة.</b> مستند الاستحقاق
/// يُنشئه نداءٌ صريح؛ ونمطُ الجدولة محجوزٌ للانتزاع من <c>Babel.Compliance</c> ولا
/// يُخترع مرّتين (ADR-0048 §2.3 · البند ح-7). وثمنُ ذلك مُعلَن: نسيان الاستحقاق شهراً
/// <b>لا يُصدر خطأً ولا سطر سجل</b>.
/// </para>
/// </summary>
public sealed class EndOfServiceService : IApplicationService
{
    /// <summary>نوع مستند حركة المخصص في هوية الإحكام — الحبيبيّة علاقة عمل.</summary>
    internal const string MovementDocument = "HrEndOfServiceMovement";

    /// <summary>نوع مستند المخالصة في هوية الإحكام.</summary>
    internal const string SettlementDocument = "HrEndOfServiceSettlement";

    /// <summary>رمز حدث الاستحقاق الدوري كما تسمّيه المصفوفة حرفياً.</summary>
    internal const string AccrualEvent = "hr.end_of_service.accrual";

    /// <summary>رمز حدث المخالصة كما تسمّيه المصفوفة حرفياً.</summary>
    internal const string SettlementEvent = "hr.end_of_service.settlement";

    /// <summary>سيناريو «المخصص مطابق» بالاسم الذي تعدّده المصفوفة.</summary>
    internal const string ScenarioExact = "exact";

    /// <summary>سيناريو «المخصص ناقص».</summary>
    internal const string ScenarioShort = "short";

    /// <summary>سيناريو «المخصص زائد».</summary>
    internal const string ScenarioExcess = "excess";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly HrDbContext _database;
    private readonly SubledgerPostingGateway _gateway;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="posting">عقد الترحيل.</param>
    public EndOfServiceService(IEntitlementEnforcer enforcer, HrRuntime runtime, IPostingService posting)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(posting);
        _enforcer = enforcer;
        _database = runtime.Database;
        _gateway = new SubledgerPostingGateway(runtime.Database, posting, runtime.CostCenters);
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>
    /// يُنشئ مستند استحقاق <b>مسوّدة</b> بحصص علاقات العمل — <b>ومستندٌ يُنشئه نداءٌ
    /// صريح لا مهمّة مجدولة</b>.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Write)]
    public async ValueTask<Result<EndOfServiceProvisionView>> DraftProvisionAsync(
        TenantId tenant,
        UserId actor,
        EndOfServiceProvisionDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Write, "Hr.EndOfService.DraftProvision", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<EndOfServiceProvisionView>.Failure(gate.Errors);
        }

        if (draft.Shares.Count == 0)
        {
            return Result<EndOfServiceProvisionView>.Failure(HrErrors.NoLines);
        }

        if (draft.Shares.Any(static share => share.PeriodShare.Amount < 0m))
        {
            return Result<EndOfServiceProvisionView>.Failure(HrErrors.NegativeAmount);
        }

        if (await _database.Provisions
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<EndOfServiceProvisionView>.Failure(HrErrors.DuplicateNumber(draft.Number));
        }

        EndOfServiceProvisionRow provision = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Number = draft.Number,
            PeriodCode = draft.PeriodCode,
            AccruedOn = draft.AccruedOn,
            MeasurementRef = draft.MeasurementRef,
            ApprovedBy = draft.ApprovedBy,
            State = HrDocumentState.Draft,
            PeriodShare = draft.Shares.Sum(static share => share.PeriodShare.Amount),
        };

        List<EndOfServiceMovementRow> movements = [];

        foreach (ProvisionShareDraft share in draft.Shares)
        {
            EmploymentRow? employment = await _database.Employments
                .FirstOrDefaultAsync(
                    row => row.TenantId == tenant.Value && row.Id == share.EmploymentId, cancellationToken)
                .ConfigureAwait(false);

            if (employment is null)
            {
                return Result<EndOfServiceProvisionView>.Failure(HrErrors.EmploymentNotFound(share.EmploymentId));
            }

            EmployeeRow employee = await _database.Employees
                .FirstAsync(row => row.TenantId == tenant.Value && row.Id == employment.EmployeeId, cancellationToken)
                .ConfigureAwait(false);

            movements.Add(new EndOfServiceMovementRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                ProvisionId = provision.Id,
                EmploymentId = employment.Id,
                EmployeeCode = employee.Code,
                CostCenterId = employee.CostCenterId,
                PeriodCode = draft.PeriodCode,
                PeriodShare = share.PeriodShare.Amount,
            });
        }

        _database.Provisions.Add(provision);
        _database.ProvisionMovements.AddRange(movements);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<EndOfServiceProvisionView>.Success(View(provision, movements, alreadyPosted: false));
    }

    /// <summary>يقرأ مستند الاستحقاق بحركاته.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="provisionId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Read)]
    public async ValueTask<Result<EndOfServiceProvisionView>> GetProvisionAsync(
        TenantId tenant,
        UserId actor,
        Guid provisionId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Read, "Hr.EndOfService.GetProvision", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<EndOfServiceProvisionView>.Failure(gate.Errors);
        }

        EndOfServiceProvisionRow? provision = await _database.Provisions
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == provisionId, cancellationToken)
            .ConfigureAwait(false);

        if (provision is null)
        {
            return Result<EndOfServiceProvisionView>.Failure(
                HrErrors.DocumentNotFound("EndOfServiceProvision", provisionId));
        }

        List<EndOfServiceMovementRow> movements = await MovementsAsync(provisionId, cancellationToken)
            .ConfigureAwait(false);

        return Result<EndOfServiceProvisionView>.Success(View(provision, movements, alreadyPosted: false));
    }

    /// <summary>
    /// يرحّل الاستحقاق: <b>قيدٌ لكل علاقة عمل</b>. وتغيير التقدير قيدٌ مستقلّ لا تعديلٌ
    /// للسابق، بنصّ المصفوفة.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="provisionId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Write)]
    public async ValueTask<Result<EndOfServiceProvisionView>> PostProvisionAsync(
        TenantId tenant,
        UserId actor,
        Guid provisionId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Write, "Hr.EndOfService.PostProvision", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<EndOfServiceProvisionView>.Failure(gate.Errors);
        }

        EndOfServiceProvisionRow? provision = await _database.Provisions
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == provisionId, cancellationToken)
            .ConfigureAwait(false);

        if (provision is null)
        {
            return Result<EndOfServiceProvisionView>.Failure(
                HrErrors.DocumentNotFound("EndOfServiceProvision", provisionId));
        }

        List<EndOfServiceMovementRow> movements = await MovementsAsync(provisionId, cancellationToken)
            .ConfigureAwait(false);

        if (movements.Count == 0)
        {
            return Result<EndOfServiceProvisionView>.Failure(HrErrors.NoLines);
        }

        bool everyMovementWasAlreadyPosted = true;

        foreach (EndOfServiceMovementRow movement in movements)
        {
            PostingIntent intent = new()
            {
                Tenant = tenant,
                DocumentType = MovementDocument,
                DocumentId = movement.Id,

                // ‏**المُطلِق `OnApproval` لا `Periodic` — ويُقال ذلك صراحةً.**
                // نصّ المصفوفة يصف الواقعة بأنها دورية، لكن `PostingTrigger.Periodic`
                // **بلا كاتب واحد في الإنتاج** اليوم، وبناءُ كاتبٍ له يمنح القيمة
                // معنىً قبل أن يُغلق البند ح-7 عن حبيبيّة الاستحقاق ونمط الجدولة.
                // فالمُطلِق هنا يصف **ما وقع فعلاً**: اعتمادُ إنسانٍ لمستند.
                Trigger = PostingTrigger.OnApproval,
                Event = new PostingEventCode(AccrualEvent),
                DocumentDate = provision.AccruedOn,
                Narration = new LocalizedName(
                    "استحقاق مخصص نهاية الخدمة " + provision.PeriodCode + " · " + movement.EmployeeCode,
                    "End-of-service provision accrual " + provision.PeriodCode + " · " + movement.EmployeeCode),
                Amounts = [new PostingAmount("period_share", Money.Of(movement.PeriodShare, _currency))],
                Facts = [new PostingFact("subledger.employee", movement.EmployeeCode)],
                Dimensions = [new PostingDimension("cost_center", movement.CostCenterId)],
                PartyId = movement.EmployeeCode,

                // سطرٌ واحد على دفتر الموظف، دائن: المخصص يتكوّن.
                ControlEffect = -movement.PeriodShare,
                Currency = _currency,
                Actor = actor,
                Generation = 1,
            };

            Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);

            if (posted.IsFailure)
            {
                return Result<EndOfServiceProvisionView>.Failure(posted.Errors);
            }

            movement.PostedEntryId = posted.Value.JournalEntryId;
            everyMovementWasAlreadyPosted &= posted.Value.WasAlreadyPosted;
        }

        provision.State = HrDocumentState.Posted;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<EndOfServiceProvisionView>.Success(View(provision, movements, everyMovementWasAlreadyPosted));
    }

    /// <summary>
    /// يُنشئ مخالصة <b>مسوّدة</b> على علاقة عمل منتهية، <b>والسيناريو المنطبق مُسمّى في
    /// الجواب</b> لا مستنتَجاً من فرق مبلغين عند القارئ.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Write)]
    public async ValueTask<Result<EndOfServiceSettlementView>> DraftSettlementAsync(
        TenantId tenant,
        UserId actor,
        EndOfServiceSettlementDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Write, "Hr.EndOfService.DraftSettlement", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<EndOfServiceSettlementView>.Failure(gate.Errors);
        }

        if (draft.SettlementDue.Amount < 0m)
        {
            return Result<EndOfServiceSettlementView>.Failure(HrErrors.NegativeAmount);
        }

        if (draft.SettlementDue.Currency != _currency)
        {
            return Result<EndOfServiceSettlementView>.Failure(
                HrErrors.CurrencyMismatch(_currency, draft.SettlementDue.Currency, "settlementDue"));
        }

        if (string.IsNullOrWhiteSpace(draft.TreasuryPartyId))
        {
            return Result<EndOfServiceSettlementView>.Failure(HrErrors.TreasuryPartyMissing(draft.Number));
        }

        if (!SettlementMethods.IsAccepted(draft.SettlementMethod))
        {
            return Result<EndOfServiceSettlementView>.Failure(
                HrErrors.UnknownSettlementMethod(draft.SettlementMethod, SettlementMethods.Accepted));
        }

        if (await _database.Settlements
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<EndOfServiceSettlementView>.Failure(HrErrors.DuplicateNumber(draft.Number));
        }

        EmploymentRow? employment = await _database.Employments
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == draft.EmploymentId, cancellationToken)
            .ConfigureAwait(false);

        if (employment is null)
        {
            return Result<EndOfServiceSettlementView>.Failure(HrErrors.EmploymentNotFound(draft.EmploymentId));
        }

        if (!string.Equals(employment.State, EmploymentState.Terminated, StringComparison.Ordinal))
        {
            return Result<EndOfServiceSettlementView>.Failure(HrErrors.EmploymentNotTerminated(employment.Id));
        }

        EmployeeRow employee = await _database.Employees
            .FirstAsync(row => row.TenantId == tenant.Value && row.Id == employment.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        decimal balance = await ProvisionBalanceAsync(tenant, employment.Id, cancellationToken).ConfigureAwait(false);

        decimal due = draft.SettlementDue.Amount;
        decimal shortfall = due > balance ? due - balance : 0m;
        decimal excess = balance > due ? balance - due : 0m;
        decimal utilised = due - shortfall + excess;

        EndOfServiceSettlementRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Number = draft.Number,
            EmploymentId = employment.Id,
            EmployeeCode = employee.Code,
            CostCenterId = employee.CostCenterId,
            SettledOn = draft.SettledOn,
            SettlementDue = due,
            ProvisionBalance = balance,
            AmountPaid = due,
            Shortfall = shortfall,
            Excess = excess,
            ProvisionUtilised = utilised,
            ScenarioCode = shortfall > 0m ? ScenarioShort : excess > 0m ? ScenarioExcess : ScenarioExact,
            MeasurementRef = draft.MeasurementRef,
            SettlementMethod = draft.SettlementMethod,
            TreasuryPartyId = draft.TreasuryPartyId,
            State = HrDocumentState.Draft,
        };

        _database.Settlements.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<EndOfServiceSettlementView>.Success(View(row, alreadyPosted: false));
    }

    /// <summary>يقرأ المخالصة — وهي أكثر مستند في الوحدة عرضةً للنزاع.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="settlementId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Read)]
    public async ValueTask<Result<EndOfServiceSettlementView>> GetSettlementAsync(
        TenantId tenant,
        UserId actor,
        Guid settlementId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Read, "Hr.EndOfService.GetSettlement", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<EndOfServiceSettlementView>.Failure(gate.Errors);
        }

        EndOfServiceSettlementRow? row = await _database.Settlements
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == settlementId, cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? Result<EndOfServiceSettlementView>.Failure(
                HrErrors.DocumentNotFound("EndOfServiceSettlement", settlementId))
            : Result<EndOfServiceSettlementView>.Success(View(row, alreadyPosted: false));
    }

    /// <summary>
    /// يرحّل المخالصة بسيناريوهاتها الثلاثة.
    /// <para>
    /// <b>ويُمرَّر في قاموس المبالغ — إضافةً إلى الأربعة المعلَنة — <c>provision_balance</c>
    /// و<c>settlement_due</c></b>: تعبيرا الشرط <c>provision_short</c> و
    /// <c>provision_excess</c> يستعملانهما وهما <b>ليسا في كتلة <c>amounts</c></b> على
    /// الحدث، وغيابُهما يُنتج <c>UndecidableCondition</c> — رفضاً لا نجاحاً ناقصاً.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="settlementId">المخالصة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Write)]
    public async ValueTask<Result<EndOfServiceSettlementView>> PostSettlementAsync(
        TenantId tenant,
        UserId actor,
        Guid settlementId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Write, "Hr.EndOfService.PostSettlement", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<EndOfServiceSettlementView>.Failure(gate.Errors);
        }

        EndOfServiceSettlementRow? row = await _database.Settlements
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == settlementId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<EndOfServiceSettlementView>.Failure(
                HrErrors.DocumentNotFound("EndOfServiceSettlement", settlementId));
        }

        PostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = SettlementDocument,
            DocumentId = row.Id,
            Trigger = PostingTrigger.OnSettlement,
            Event = new PostingEventCode(SettlementEvent),
            DocumentDate = row.SettledOn,
            Narration = new LocalizedName(
                "مخالصة نهاية خدمة · " + row.EmployeeCode,
                "End-of-service settlement · " + row.EmployeeCode),
            Amounts =
            [
                new PostingAmount("amount_paid", Money.Of(row.AmountPaid, _currency)),
                new PostingAmount("provision_utilised", Money.Of(row.ProvisionUtilised, _currency)),
                new PostingAmount("shortfall", Money.Of(row.Shortfall, _currency)),
                new PostingAmount("excess", Money.Of(row.Excess, _currency)),

                // ‏**المفردتان اللتان لا تظهران في كتلة amounts على الحدث** ويستعملهما
                // تعبيرا الشرط. وبدونهما: UndecidableCondition.
                new PostingAmount("provision_balance", Money.Of(row.ProvisionBalance, _currency)),
                new PostingAmount("settlement_due", Money.Of(row.SettlementDue, _currency)),
            ],
            Facts =
            [
                new PostingFact("document.settlement_method", row.SettlementMethod),
                new PostingFact("subledger.employee", row.EmployeeCode),
                new PostingFact("subledger.none", row.TreasuryPartyId),
            ],
            Dimensions = [new PostingDimension("cost_center", row.CostCenterId)],
            PartyId = row.EmployeeCode,

            // سطرٌ واحد على دفتر الموظف، مدين: المخصص يُستنفد.
            ControlEffect = row.ProvisionUtilised,
            Currency = _currency,
            Actor = actor,
            Generation = row.PostingGeneration,
        };

        Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);

        if (posted.IsFailure)
        {
            return Result<EndOfServiceSettlementView>.Failure(posted.Errors);
        }

        row.State = HrDocumentState.Posted;
        row.PostedEntryId = posted.Value.JournalEntryId;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<EndOfServiceSettlementView>.Success(View(row, posted.Value.WasAlreadyPosted));
    }

    /// <summary>
    /// رصيد المخصص لعلاقة عمل: مجموع حركاتها <b>المُرحَّلة</b> ناقص ما استُنفد في
    /// مخالصات <b>مُرحَّلة</b>. ومسوّدةٌ لم تُرحَّل لا تُحرّك رصيداً.
    /// </summary>
    private async Task<decimal> ProvisionBalanceAsync(
        TenantId tenant, Guid employmentId, CancellationToken cancellationToken)
    {
        decimal accrued = await _database.ProvisionMovements
            .Where(row => row.TenantId == tenant.Value && row.EmploymentId == employmentId && row.PostedEntryId != null)
            .SumAsync(row => row.PeriodShare, cancellationToken)
            .ConfigureAwait(false);

        decimal utilised = await _database.Settlements
            .Where(row => row.TenantId == tenant.Value
                          && row.EmploymentId == employmentId
                          && row.State == HrDocumentState.Posted)
            .SumAsync(row => row.ProvisionUtilised, cancellationToken)
            .ConfigureAwait(false);

        return accrued - utilised;
    }

    private async Task<List<EndOfServiceMovementRow>> MovementsAsync(Guid provisionId, CancellationToken cancellationToken)
        => await _database.ProvisionMovements
            .Where(row => row.ProvisionId == provisionId)
            .OrderBy(row => row.EmployeeCode)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private EndOfServiceProvisionView View(
        EndOfServiceProvisionRow provision, IReadOnlyList<EndOfServiceMovementRow> movements, bool alreadyPosted) => new(
        provision.Id,
        provision.Number,
        provision.PeriodCode,
        provision.AccruedOn,
        provision.MeasurementRef,
        provision.ApprovedBy,
        Money.Of(provision.PeriodShare, _currency),
        provision.State,
        [
            .. movements.Select(movement => new ProvisionMovementView(
                movement.Id,
                movement.EmploymentId,
                movement.EmployeeCode,
                Money.Of(movement.PeriodShare, _currency),
                movement.PostedEntryId)),
        ],
        alreadyPosted);

    private EndOfServiceSettlementView View(EndOfServiceSettlementRow row, bool alreadyPosted) => new(
        row.Id,
        row.Number,
        row.EmploymentId,
        row.EmployeeCode,
        row.SettledOn,
        Money.Of(row.SettlementDue, _currency),
        Money.Of(row.ProvisionBalance, _currency),
        Money.Of(row.AmountPaid, _currency),
        Money.Of(row.Shortfall, _currency),
        Money.Of(row.Excess, _currency),
        Money.Of(row.ProvisionUtilised, _currency),
        row.ScenarioCode,
        row.MeasurementRef,
        row.SettlementMethod,
        row.TreasuryPartyId,
        row.State,
        row.PostedEntryId,
        alreadyPosted);
}
