using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Hr.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Hr.Application;

/// <summary>
/// سند صرف الرواتب على مسيّر مُرحَّل — <b>بسطرٍ لكل قسيمة، وقيدٍ لكل سطر</b>.
/// <para>
/// وسطر التسوية معلَنٌ في المصفوفة <c>subledger: "resolved"</c> <b>والمحرك يطويه إلى
/// <c>none</c></b> ثم يبحث عن الواقعة <c>subledger.none</c> — والاسم مضلّل. وحساب
/// التسوية الافتراضي حسابٌ ضابط، فبلا الواقعة يُرفض كل نداء.
/// </para>
/// </summary>
public sealed class PayrollPaymentService : IApplicationService
{
    /// <summary>نوع مستند سطر الصرف في هوية الإحكام — الحبيبيّة قسيمة لا سند.</summary>
    internal const string PaymentLineDocument = "HrPayrollPaymentLine";

    /// <summary>رمز حدث صرف الرواتب كما تسمّيه المصفوفة حرفياً.</summary>
    internal const string PaymentEvent = "hr.payroll.payment";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly HrDbContext _database;
    private readonly SubledgerPostingGateway _gateway;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="posting">عقد الترحيل.</param>
    public PayrollPaymentService(IEntitlementEnforcer enforcer, HrRuntime runtime, IPostingService posting)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(posting);
        _enforcer = enforcer;
        _database = runtime.Database;
        _gateway = new SubledgerPostingGateway(runtime.Database, posting, runtime.CostCenters);
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>يُنشئ سند صرف <b>مسوّدة</b> على مسيّر مُرحَّل، بسطرٍ لكل قسيمة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Write)]
    public async ValueTask<Result<PayrollPaymentView>> DraftAsync(
        TenantId tenant,
        UserId actor,
        PayrollPaymentDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Write, "Hr.PayrollPayment.Draft", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PayrollPaymentView>.Failure(gate.Errors);
        }

        if (string.IsNullOrWhiteSpace(draft.TreasuryPartyId))
        {
            return Result<PayrollPaymentView>.Failure(HrErrors.TreasuryPartyMissing(draft.Number));
        }

        if (!SettlementMethods.IsAccepted(draft.SettlementMethod))
        {
            return Result<PayrollPaymentView>.Failure(
                HrErrors.UnknownSettlementMethod(draft.SettlementMethod, SettlementMethods.Accepted));
        }

        if (await _database.PayrollPayments
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PayrollPaymentView>.Failure(HrErrors.DuplicateNumber(draft.Number));
        }

        PayrollRunRow? run = await _database.PayrollRuns
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == draft.RunId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            return Result<PayrollPaymentView>.Failure(HrErrors.DocumentNotFound("PayrollRun", draft.RunId));
        }

        if (!string.Equals(run.State, HrDocumentState.Posted, StringComparison.Ordinal))
        {
            return Result<PayrollPaymentView>.Failure(
                HrErrors.NotInState(run.Number, run.State, HrDocumentState.Posted));
        }

        List<PayslipRow> payslips = await _database.Payslips
            .Where(row => row.TenantId == tenant.Value && row.RunId == run.Id)
            .OrderBy(row => row.EmployeeCode)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (payslips.Count == 0)
        {
            return Result<PayrollPaymentView>.Failure(HrErrors.NoLines);
        }

        PayrollPaymentRow payment = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Number = draft.Number,
            RunId = run.Id,
            PaidOn = draft.PaidOn,
            SettlementMethod = draft.SettlementMethod,
            TreasuryPartyId = draft.TreasuryPartyId,
            State = HrDocumentState.Draft,
            NetPayable = payslips.Sum(static row => row.NetPayable),
        };

        List<PayrollPaymentLineRow> lines = [];
        int lineNo = 0;

        foreach (PayslipRow payslip in payslips)
        {
            lines.Add(new PayrollPaymentLineRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                PaymentId = payment.Id,
                PayslipId = payslip.Id,
                LineNo = ++lineNo,
                EmployeeCode = payslip.EmployeeCode,
                CostCenterId = payslip.CostCenterId,
                Amount = payslip.NetPayable,
            });
        }

        _database.PayrollPayments.Add(payment);
        _database.PayrollPaymentLines.AddRange(lines);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PayrollPaymentView>.Success(View(payment, lines, alreadyPosted: false));
    }

    /// <summary>يقرأ السند وسطوره ومعرّفات قيودها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="paymentId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Read)]
    public async ValueTask<Result<PayrollPaymentView>> GetAsync(
        TenantId tenant,
        UserId actor,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Read, "Hr.PayrollPayment.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PayrollPaymentView>.Failure(gate.Errors);
        }

        PayrollPaymentRow? payment = await _database.PayrollPayments
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == paymentId, cancellationToken)
            .ConfigureAwait(false);

        if (payment is null)
        {
            return Result<PayrollPaymentView>.Failure(HrErrors.DocumentNotFound("PayrollPayment", paymentId));
        }

        List<PayrollPaymentLineRow> lines = await LinesAsync(paymentId, cancellationToken).ConfigureAwait(false);
        return Result<PayrollPaymentView>.Success(View(payment, lines, alreadyPosted: false));
    }

    /// <summary>
    /// يرحّل صرف الرواتب: <b>قيدٌ لكل سطر</b>، سطرُه الأول على التزام الرواتب يحمل طرف
    /// الموظف، وسطرُه الثاني على حساب التسوية يحمل طرف الخزينة واقعةً نصّية.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="paymentId">السند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Write)]
    public async ValueTask<Result<PayrollPaymentView>> PostAsync(
        TenantId tenant,
        UserId actor,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Write, "Hr.PayrollPayment.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PayrollPaymentView>.Failure(gate.Errors);
        }

        PayrollPaymentRow? payment = await _database.PayrollPayments
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == paymentId, cancellationToken)
            .ConfigureAwait(false);

        if (payment is null)
        {
            return Result<PayrollPaymentView>.Failure(HrErrors.DocumentNotFound("PayrollPayment", paymentId));
        }

        List<PayrollPaymentLineRow> lines = await LinesAsync(paymentId, cancellationToken).ConfigureAwait(false);

        if (lines.Count == 0)
        {
            return Result<PayrollPaymentView>.Failure(HrErrors.NoLines);
        }

        PayrollRunRow run = await _database.PayrollRuns
            .FirstAsync(row => row.TenantId == tenant.Value && row.Id == payment.RunId, cancellationToken)
            .ConfigureAwait(false);

        bool everyLineWasAlreadyPosted = true;

        foreach (PayrollPaymentLineRow line in lines)
        {
            PostingIntent intent = new()
            {
                Tenant = tenant,
                DocumentType = PaymentLineDocument,
                DocumentId = line.Id,
                Trigger = PostingTrigger.OnSettlement,
                Event = new PostingEventCode(PaymentEvent),
                DocumentDate = payment.PaidOn,
                Narration = new LocalizedName(
                    "صرف رواتب " + run.PeriodCode + " · " + line.EmployeeCode,
                    "Payroll payment " + run.PeriodCode + " · " + line.EmployeeCode),
                Amounts = [new PostingAmount("net_payable", Money.Of(line.Amount, _currency))],
                Facts =
                [
                    new PostingFact("document.settlement_method", payment.SettlementMethod),
                    new PostingFact("subledger.employee", line.EmployeeCode),

                    // ⚠️ سطر التسوية معلَن `subledger: "resolved"`، والمحرك يطويه إلى
                    // النوع "none" ثم يبحث عن هذه الواقعة بالذات. ولا تُكتب
                    // `subledger.treasury` — ليس اسم النوع على هذا المسار.
                    new PostingFact("subledger.none", payment.TreasuryPartyId),
                ],
                Dimensions = [new PostingDimension("cost_center", line.CostCenterId)],
                PartyId = line.EmployeeCode,

                // سطرٌ واحد على دفتر الموظف، مدين: الالتزام يُطفأ.
                ControlEffect = line.Amount,
                Currency = _currency,
                Actor = actor,
                Generation = 1,
            };

            Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);

            if (posted.IsFailure)
            {
                return Result<PayrollPaymentView>.Failure(posted.Errors);
            }

            line.PostedEntryId = posted.Value.JournalEntryId;
            everyLineWasAlreadyPosted &= posted.Value.WasAlreadyPosted;
        }

        payment.State = HrDocumentState.Posted;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PayrollPaymentView>.Success(View(payment, lines, everyLineWasAlreadyPosted));
    }

    private async Task<List<PayrollPaymentLineRow>> LinesAsync(Guid paymentId, CancellationToken cancellationToken)
        => await _database.PayrollPaymentLines
            .Where(row => row.PaymentId == paymentId)
            .OrderBy(row => row.LineNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private PayrollPaymentView View(
        PayrollPaymentRow payment, IReadOnlyList<PayrollPaymentLineRow> lines, bool alreadyPosted) => new(
        payment.Id,
        payment.Number,
        payment.RunId,
        payment.PaidOn,
        payment.SettlementMethod,
        payment.TreasuryPartyId,
        Money.Of(payment.NetPayable, _currency),
        payment.State,
        [
            .. lines.Select(line => new PayrollPaymentLineView(
                line.LineNo, line.PayslipId, line.EmployeeCode, Money.Of(line.Amount, _currency), line.PostedEntryId)),
        ],
        alreadyPosted);
}
