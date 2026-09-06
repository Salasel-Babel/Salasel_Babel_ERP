using Babel.Contracts.Subledger;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Hr.Application;
using Babel.Hr.Persistence;
using Babel.Core.CompanySetup;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Hr.Subledger;

/// <summary>
/// مطابقة دفتر الموظف المساعد بنقطة ضبطه — <b>مستنداً بمستند</b>.
/// <para>
/// والطرفان متساويا الحبيبيّة بحكم قرار الترحيل نفسه: قيدٌ لكل قسيمة يعني حركةً واحدة
/// في نقطة الضبط لكل قسيمة، وصفَّ محاولةٍ واحداً في جدول الوحدة لكل قسيمة. ولو رُحِّل
/// المسيّر قيداً واحداً لصار الطرفان بحبيبيّتين مختلفتين، <b>ولاستحالت هذه المطابقة
/// أصلاً</b>.
/// </para>
/// </summary>
public sealed class EmployeeReconciliationService : IApplicationService
{
    /// <summary>اسم نوع الدفتر المساعد كما تعرفه بيانات الدفتر — نصٌّ لا تعداد.</summary>
    private const string SubledgerKindName = "employee";

    /// <summary>
    /// فاصل مفتاح المطابقة — <b>محرف لا يظهر في أيٍّ من مكوّناته</b> (‏U+001F).
    /// والوصلُ على فاصل قد يحتويه أحد المكوّنات عطبُ تصادم بذاته، وقد لُدغ هذا
    /// المستودع به من قبل في <c>source_ref</c> المدموج.
    /// </summary>
    private const char KeySeparator = '\u001F';

    private readonly IEntitlementEnforcer _enforcer;
    private readonly HrDbContext _database;
    private readonly IControlPointReader _control;
    private readonly ICompanyMoneyResolver _company;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="control">قارئ نقطة الضبط — منفذٌ في العقد، لا وصولٌ إلى جداول الدفتر.</param>
    public EmployeeReconciliationService(
        IEntitlementEnforcer enforcer, HrRuntime runtime, IControlPointReader control)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(control);
        _enforcer = enforcer;
        _database = runtime.Database;
        _control = control;
        _company = runtime.Company;
    }

    /// <summary>يطابق الدفترين حتى تاريخ.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="asOf">التاريخ الذي تُقرأ الحركة حتى نهايته.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Read)]
    public async ValueTask<Result<EmployeeReconciliationReport>> ReconcileAsync(
        TenantId tenant,
        UserId actor,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Read, "Hr.Subledger.Reconcile", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<EmployeeReconciliationReport>.Failure(gate.Errors);
        }

        Result<CompanyMoney> money = await _company.ResolveAsync(tenant, cancellationToken).ConfigureAwait(false);
        if (money.IsFailure)
        {
            return Result<EmployeeReconciliationReport>.Failure(money.Errors);
        }

        Result<ControlPointSnapshot> snapshot = await _control
            .ReadAsync(tenant, SubledgerKindName, asOf, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (snapshot.IsFailure)
        {
            return Result<EmployeeReconciliationReport>.Failure(HrErrors.ControlPointUnavailable(snapshot.Errors));
        }

        List<DocumentPostingRow> attempts = await _database.Postings
            .Where(row => row.TenantId == tenant.Value && row.DocumentDate <= asOf)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, decimal> control = new(StringComparer.Ordinal);

        foreach (ControlPointMovement movement in snapshot.Value.Movements)
        {
            string key = Key(movement.DocumentType, movement.DocumentId, movement.PartyId);
            control[key] = control.TryGetValue(key, out decimal running) ? running + movement.Net : movement.Net;
        }

        List<EmployeeReconciliationDivergence> divergences = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        int matched = 0;

        foreach (DocumentPostingRow attempt in attempts
                     .OrderBy(static row => row.DocumentType, StringComparer.Ordinal)
                     .ThenBy(static row => row.DocumentId, StringComparer.Ordinal))
        {
            string key = Key(attempt.DocumentType, attempt.DocumentId, attempt.PartyId);
            seen.Add(key);

            if (string.Equals(attempt.State, PostingAttemptState.Attempting, StringComparison.Ordinal))
            {
                divergences.Add(Divergence(attempt, 0m, DivergenceReason.PostingUnresolved, money.Value));
                continue;
            }

            if (string.Equals(attempt.State, PostingAttemptState.Refused, StringComparison.Ordinal))
            {
                // رفضٌ مُسجَّل ليس انحرافاً: لا قيد ولا أثر، والمستند باقٍ على حاله.
                continue;
            }

            decimal actual = control.TryGetValue(key, out decimal value) ? value : 0m;

            if (actual == attempt.ControlEffect)
            {
                matched++;
                continue;
            }

            divergences.Add(Divergence(
                attempt,
                actual,
                actual == 0m ? DivergenceReason.MissingInControl : DivergenceReason.AmountMismatch, money.Value));
        }

        foreach (KeyValuePair<string, decimal> movement in control
                     .Where(pair => !seen.Contains(pair.Key))
                     .OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            string[] parts = movement.Key.Split(KeySeparator);

            divergences.Add(new EmployeeReconciliationDivergence(
                parts[0],
                parts[1],
                parts[2],
                Money.Zero(money.Value.Currency),
                Money.Of(movement.Value, money.Value.Currency),
                Money.Of(-movement.Value, money.Value.Currency),
                DivergenceReason.MissingInSubledger));
        }

        return Result<EmployeeReconciliationReport>.Success(new EmployeeReconciliationReport(
            asOf, matched, divergences.Count == 0, divergences));
    }

    private static EmployeeReconciliationDivergence Divergence(DocumentPostingRow attempt, decimal actual, string reason, CompanyMoney money)
        => new(
            attempt.DocumentType,
            attempt.DocumentId,
            attempt.PartyId,
            Money.Of(attempt.ControlEffect, money.Currency),
            Money.Of(actual, money.Currency),
            Money.Of(attempt.ControlEffect - actual, money.Currency),
            reason);

    /// <summary>مفتاح المطابقة: النوع والمعرّف والطرف، مفصولةً بمحرف لا يظهر في أيٍّ منها.</summary>
    private static string Key(string documentType, string documentId, string partyId)
        => documentType + KeySeparator + documentId + KeySeparator + partyId;
}
