using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Hr.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Hr.Application;

/// <summary>
/// سداد اشتراك التأمينات للفترة.
/// <para>
/// <b>وهو المستند الوحيد في هذه الوحدة الذي يُرحَّل قيداً واحداً للفترة</b> — ويجوز
/// ذلك فيه وحده لأن سطره الأول على حساب الالتزام <b>بلا دفتر مساعد</b> (مقيس في دليل
/// الحسابات)، فلا طرفَ يُفقد بالتجميع. <b>لكن سطره الثاني يحمل طرف الخزينة</b> كسائر
/// مستندات الدفع.
/// </para>
/// <para>
/// <b>والمبلغ يصل من المستدعي ولا تُمليه الوحدة</b>: فاتورة الجهة قد تخالف ما استحقّته
/// المسيّرات لأسباب مشروعة. والوحدة تُرجع <c>accruedForPeriod</c> إلى جانبه <b>للمقارنة
/// لا للإملاء</b>، فيُرى الفارق قبل الاعتماد بدل أن يُكتشف عند التسوية.
/// </para>
/// </summary>
public sealed class SocialInsurancePaymentService : IApplicationService
{
    /// <summary>نوع المستند في هوية الإحكام.</summary>
    internal const string PaymentDocument = "HrSocialInsurancePayment";

    /// <summary>رمز الحدث كما تسمّيه المصفوفة حرفياً.</summary>
    internal const string PaymentEvent = "hr.social_insurance.payment";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly HrDbContext _database;
    private readonly SubledgerPostingGateway _gateway;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="posting">عقد الترحيل.</param>
    public SocialInsurancePaymentService(IEntitlementEnforcer enforcer, HrRuntime runtime, IPostingService posting)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(posting);
        _enforcer = enforcer;
        _database = runtime.Database;
        _gateway = new SubledgerPostingGateway(runtime.Database, posting, runtime.CostCenters);
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>يُنشئ سند سداد <b>مسوّدة</b> للفترة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Write)]
    public async ValueTask<Result<SocialInsurancePaymentView>> DraftAsync(
        TenantId tenant,
        UserId actor,
        SocialInsurancePaymentDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Write, "Hr.SocialInsurancePayment.Draft", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SocialInsurancePaymentView>.Failure(gate.Errors);
        }

        if (draft.Amount.Currency != _currency)
        {
            return Result<SocialInsurancePaymentView>.Failure(
                HrErrors.CurrencyMismatch(_currency, draft.Amount.Currency, "amount"));
        }

        if (draft.Amount.Amount < 0m)
        {
            return Result<SocialInsurancePaymentView>.Failure(HrErrors.NegativeAmount);
        }

        if (string.IsNullOrWhiteSpace(draft.TreasuryPartyId))
        {
            return Result<SocialInsurancePaymentView>.Failure(HrErrors.TreasuryPartyMissing(draft.Number));
        }

        if (!SettlementMethods.IsAccepted(draft.SettlementMethod))
        {
            return Result<SocialInsurancePaymentView>.Failure(
                HrErrors.UnknownSettlementMethod(draft.SettlementMethod, SettlementMethods.Accepted));
        }

        if (await _database.SocialInsurancePayments
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<SocialInsurancePaymentView>.Failure(HrErrors.DuplicateNumber(draft.Number));
        }

        SocialInsurancePaymentRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Number = draft.Number,
            PeriodCode = draft.PeriodCode,
            PaidOn = draft.PaidOn,
            Amount = draft.Amount.Amount,
            SettlementMethod = draft.SettlementMethod,
            TreasuryPartyId = draft.TreasuryPartyId,
            State = HrDocumentState.Draft,
        };

        _database.SocialInsurancePayments.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        decimal accrued = await AccruedAsync(tenant, draft.PeriodCode, cancellationToken).ConfigureAwait(false);
        return Result<SocialInsurancePaymentView>.Success(View(row, accrued, alreadyPosted: false));
    }

    /// <summary>يقرأ السند ومعه ما استُحقّ في فترته من مسيّرات مُرحَّلة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="paymentId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Read)]
    public async ValueTask<Result<SocialInsurancePaymentView>> GetAsync(
        TenantId tenant,
        UserId actor,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Read, "Hr.SocialInsurancePayment.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SocialInsurancePaymentView>.Failure(gate.Errors);
        }

        SocialInsurancePaymentRow? row = await _database.SocialInsurancePayments
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == paymentId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<SocialInsurancePaymentView>.Failure(
                HrErrors.DocumentNotFound("SocialInsurancePayment", paymentId));
        }

        decimal accrued = await AccruedAsync(tenant, row.PeriodCode, cancellationToken).ConfigureAwait(false);
        return Result<SocialInsurancePaymentView>.Success(View(row, accrued, alreadyPosted: false));
    }

    /// <summary>يرحّل السداد — <b>قيدٌ واحد للفترة</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="paymentId">السند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Write)]
    public async ValueTask<Result<SocialInsurancePaymentView>> PostAsync(
        TenantId tenant,
        UserId actor,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Write, "Hr.SocialInsurancePayment.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SocialInsurancePaymentView>.Failure(gate.Errors);
        }

        SocialInsurancePaymentRow? row = await _database.SocialInsurancePayments
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == paymentId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<SocialInsurancePaymentView>.Failure(
                HrErrors.DocumentNotFound("SocialInsurancePayment", paymentId));
        }

        PostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = PaymentDocument,
            DocumentId = row.Id,
            Trigger = PostingTrigger.OnSettlement,
            Event = new PostingEventCode(PaymentEvent),
            DocumentDate = row.PaidOn,
            Narration = new LocalizedName(
                "سداد تأمينات " + row.PeriodCode,
                "Social insurance payment " + row.PeriodCode),
            Amounts = [new PostingAmount("amount", Money.Of(row.Amount, _currency))],
            Facts =
            [
                new PostingFact("document.settlement_method", row.SettlementMethod),

                // ولو كان السطر الأول بلا دفتر مساعد، فسطر التسوية يحمل طرف الخزينة.
                new PostingFact("subledger.none", row.TreasuryPartyId),
            ],
            Dimensions = [],

            // لا طرف موظف على هذا المستند: سطره الأول بلا دفتر مساعد.
            PartyId = string.Empty,
            ControlEffect = 0m,
            Currency = _currency,
            Actor = actor,
            Generation = row.PostingGeneration,
        };

        Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);

        if (posted.IsFailure)
        {
            return Result<SocialInsurancePaymentView>.Failure(posted.Errors);
        }

        row.State = HrDocumentState.Posted;
        row.PostedEntryId = posted.Value.JournalEntryId;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        decimal accrued = await AccruedAsync(tenant, row.PeriodCode, cancellationToken).ConfigureAwait(false);
        return Result<SocialInsurancePaymentView>.Success(View(row, accrued, posted.Value.WasAlreadyPosted));
    }

    /// <summary>ما استُحقّ من اشتراك في فترة من مسيّرات <b>مُرحَّلة</b> وحدها.</summary>
    private async Task<decimal> AccruedAsync(TenantId tenant, string periodCode, CancellationToken cancellationToken)
    {
        List<Guid> runs = await _database.PayrollRuns
            .Where(row => row.TenantId == tenant.Value
                          && row.PeriodCode == periodCode
                          && row.State == HrDocumentState.Posted)
            .Select(row => row.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (runs.Count == 0)
        {
            return 0m;
        }

        return await _database.Payslips
            .Where(row => row.TenantId == tenant.Value && runs.Contains(row.RunId))
            .SumAsync(row => row.EmployerSocialInsurance + row.EmployeeSocialInsurance, cancellationToken)
            .ConfigureAwait(false);
    }

    private SocialInsurancePaymentView View(SocialInsurancePaymentRow row, decimal accrued, bool alreadyPosted) => new(
        row.Id,
        row.Number,
        row.PeriodCode,
        row.PaidOn,
        Money.Of(row.Amount, _currency),
        Money.Of(accrued, _currency),
        row.SettlementMethod,
        row.TreasuryPartyId,
        row.State,
        row.PostedEntryId,
        alreadyPosted);
}
