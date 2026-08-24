using System.Globalization;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Sales.Persistence;
using Babel.Sales.Subledger;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Sales.Application;

/// <summary>
/// الدفتر المساعد للذمم المدينة: أعمار الديون، وكشف حساب العميل، و<b>مطابقة الدفتر
/// المساعد بنقطة ضبطه</b>.
/// <para>
/// المطابقة هنا <b>وظيفة لا تقرير</b>: تقارن مجموع الدفتر المساعد المحسوب من مستنداته
/// برصيد نقطة الضبط في دفتر الأستاذ، وتُسمّي المستندات المسؤولة عن أي فارق. دفترٌ
/// مساعد ينحرف بصمت عن نقطة ضبطه هو أشيع عيب في الأنظمة المحاسبية، ولا يُكتشف إلا
/// بعد شهور — ولذلك يُبنى الكشف عنه اليوم لا حين يُطلَب.
/// </para>
/// </summary>
public sealed class ReceivablesService : IApplicationService
{
    /// <summary>نوع الدفتر المساعد كما تعرّفه بيانات الدفتر.</summary>
    internal const string SubledgerKindCode = "customer";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly SalesDbContext _database;
    private readonly IControlPointReader _controlPoint;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="controlPoint">قارئ نقطة الضبط — يصله الجذر التركيبي بالدفتر.</param>
    public ReceivablesService(IEntitlementEnforcer enforcer, SalesRuntime runtime, IControlPointReader controlPoint)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(controlPoint);
        _enforcer = enforcer;
        _database = runtime.Database;
        _controlPoint = controlPoint;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>أعمار ديون العملاء حتى تاريخ.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="asOf">التاريخ.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Read)]
    public async ValueTask<Result<AgingReport>> AgingAsync(
        TenantId tenant,
        UserId actor,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Read, "Sales.Receivables.Aging", cancellationToken)
            .ConfigureAwait(false);

        return gate.IsFailure
            ? Result<AgingReport>.Failure(gate.Errors)
            : Result<AgingReport>.Success(await BuildAgingAsync(tenant, asOf, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>كشف حساب عميل بين تاريخين.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="customerId">العميل.</param>
    /// <param name="from">من تاريخ.</param>
    /// <param name="to">إلى تاريخ.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Read)]
    public async ValueTask<Result<PartyStatement>> StatementAsync(
        TenantId tenant,
        UserId actor,
        Guid customerId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Read, "Sales.Receivables.Statement", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PartyStatement>.Failure(gate.Errors);
        }

        if (!await _database.Customers
                .AnyAsync(row => row.TenantId == tenant.Value && row.Id == customerId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PartyStatement>.Failure(SalesErrors.CustomerNotFound(customerId));
        }

        List<OpenItem> items = await OpenItemsAsync(tenant, to, cancellationToken).ConfigureAwait(false);
        List<OpenItem> mine = [.. items
            .Where(item => item.CustomerId == customerId)
            .OrderBy(static item => item.Date)
            .ThenBy(static item => item.Number, StringComparer.Ordinal)];

        decimal opening = mine.Where(item => item.Date < from).Sum(static item => item.Effect);
        decimal running = opening;
        List<StatementLine> lines = [];

        foreach (OpenItem item in mine.Where(item => item.Date >= from))
        {
            running += item.Effect;
            lines.Add(new StatementLine(
                item.Date,
                item.DocumentType,
                item.Number,
                item.Description,
                Money.Of(item.Effect > 0m ? item.Effect : 0m, _currency),
                Money.Of(item.Effect < 0m ? -item.Effect : 0m, _currency),
                Money.Of(running, _currency)));
        }

        return Result<PartyStatement>.Success(new PartyStatement(
            customerId, from, to, Money.Of(opening, _currency), lines, Money.Of(running, _currency)));
    }

    /// <summary>
    /// يطابق الدفتر المساعد بنقطة ضبطه ويُسمّي المستندات المسؤولة عن أي فارق.
    /// <para>الفارق يُقارَن بالصفر بالضبط — لا «قريب من الصفر» ولا حدّ تسامح.</para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="asOf">تاريخ المطابقة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Read)]
    public async ValueTask<Result<ControlReconciliationReport>> ReconcileAsync(
        TenantId tenant,
        UserId actor,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Read, "Sales.Receivables.Reconcile", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ControlReconciliationReport>.Failure(gate.Errors);
        }

        Result<ControlPointSnapshot> snapshot = await _controlPoint
            .ReadAsync(tenant, SubledgerKindCode, asOf, cancellationToken)
            .ConfigureAwait(false);

        if (snapshot.IsFailure)
        {
            return Result<ControlReconciliationReport>.Failure(SalesErrors.ControlPointUnavailable(snapshot.Errors));
        }

        AgingReport aging = await BuildAgingAsync(tenant, asOf, cancellationToken).ConfigureAwait(false);
        decimal subledgerTotal = aging.Totals.Total.Amount;
        decimal controlTotal = snapshot.Value.Net;

        List<DocumentPostingRow> postings = await _database.Postings
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.DocumentDate <= asOf)
            .OrderBy(row => row.DocumentType)
            .ThenBy(row => row.DocumentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, ControlPointMovement> ledgerSide = new(StringComparer.Ordinal);
        foreach (ControlPointMovement movement in snapshot.Value.Movements)
        {
            ledgerSide[Key(movement.DocumentType, movement.DocumentId)] = movement;
        }

        List<ReconciliationDivergence> divergences = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        // ‏**التجميع بالمستند لا بالصفّ.** نقطة الضبط تُقرأ مجمّعةً بالمستند، وللمستند
        // الواحد بعد توسيع الهوية أكثر من صفّ محاولة عند الإطلاق نفسه (الاعتراف
        // بالإيراد وقيد التكلفة). فمقارنة صفّ التكلفة — وأثره على نقطة ضبط العملاء
        // صفر — بحركة المستند كاملةً تُنتج «انحرافاً» لا وجود له، وتُسقط المطابقة
        // على فاتورة سليمة تماماً. المقارنة الصحيحة: مجموع آثار صفوف المستند مقابل
        // صافي حركته في الدفتر.
        foreach (IGrouping<string, DocumentPostingRow> document in postings
                     .GroupBy(row => Key(row.DocumentType, row.DocumentId), StringComparer.Ordinal))
        {
            seen.Add(document.Key);

            List<DocumentPostingRow> unresolved =
                [.. document.Where(static row => row.State == PostingAttemptState.Attempting)];

            if (unresolved.Count > 0)
            {
                // محاولة معلّقة تُسمّى بذاتها لا مجمّعة: الغرض تسمية ما لم يُحسم.
                foreach (DocumentPostingRow pending in unresolved)
                {
                    divergences.Add(new ReconciliationDivergence(
                        pending.DocumentType,
                        pending.DocumentId,
                        pending.PartyId,
                        Money.Of(pending.ControlEffect, _currency),
                        Money.Of(0m, _currency),
                        Money.Of(pending.ControlEffect, _currency),
                        DivergenceReason.PostingUnresolved));
                }

                continue;
            }

            List<DocumentPostingRow> posted =
                [.. document.Where(static row => row.State == PostingAttemptState.Posted)];

            if (posted.Count == 0)
            {
                continue;
            }

            decimal effect = posted.Sum(static row => row.ControlEffect);

            // الطرف المسؤول هو صاحب الأثر غير الصفري إن وُجد — لا أوّل صفّ اتّفق.
            DocumentPostingRow witness = posted.Find(static row => row.ControlEffect != 0m) ?? posted[0];

            bool known = ledgerSide.TryGetValue(document.Key, out ControlPointMovement? movement);
            decimal ledgerNet = known ? movement!.Net : 0m;

            if (!known && effect != 0m)
            {
                divergences.Add(new ReconciliationDivergence(
                    witness.DocumentType,
                    witness.DocumentId,
                    witness.PartyId,
                    Money.Of(effect, _currency),
                    Money.Of(0m, _currency),
                    Money.Of(effect, _currency),
                    DivergenceReason.MissingInControl));
                continue;
            }

            if (effect != ledgerNet)
            {
                divergences.Add(new ReconciliationDivergence(
                    witness.DocumentType,
                    witness.DocumentId,
                    witness.PartyId,
                    Money.Of(effect, _currency),
                    Money.Of(ledgerNet, _currency),
                    Money.Of(effect - ledgerNet, _currency),
                    DivergenceReason.AmountMismatch));
            }
        }

        foreach (ControlPointMovement movement in snapshot.Value.Movements)
        {
            if (seen.Contains(Key(movement.DocumentType, movement.DocumentId)) || movement.Net == 0m)
            {
                continue;
            }

            divergences.Add(new ReconciliationDivergence(
                movement.DocumentType,
                movement.DocumentId,
                movement.PartyId,
                Money.Of(0m, _currency),
                Money.Of(movement.Net, _currency),
                Money.Of(-movement.Net, _currency),
                DivergenceReason.MissingInSubledger));
        }

        decimal divergence = subledgerTotal - controlTotal;

        return Result<ControlReconciliationReport>.Success(new ControlReconciliationReport(
            asOf,
            Money.Of(subledgerTotal, _currency),
            Money.Of(controlTotal, _currency),
            Money.Of(divergence, _currency),
            divergence == 0m && divergences.Count == 0,
            [.. divergences.OrderBy(static d => d.DocumentType, StringComparer.Ordinal)
                           .ThenBy(static d => d.DocumentId, StringComparer.Ordinal)]));
    }

    /// <summary>
    /// مفتاح المستند. المكوّنان مسبوقان بطوليهما: مفتاحٌ مبني بالوصل على فاصل قد
    /// يحتويه أحد المكوّنات هو عطب تصادم بذاته، ولُدغ المستودع به في <c>source_ref</c>
    /// المدموج حيث أنتج <c>("A/B","C")</c> و<c>("A","B/C")</c> البايتات نفسها.
    /// </summary>
    private static string Key(string documentType, string documentId)
        => documentType.Length.ToString(CultureInfo.InvariantCulture) + ":" + documentType
           + documentId.Length.ToString(CultureInfo.InvariantCulture) + ":" + documentId;

    private async Task<AgingReport> BuildAgingAsync(TenantId tenant, DateOnly asOf, CancellationToken cancellationToken)
    {
        List<OpenItem> items = await OpenItemsAsync(tenant, asOf, cancellationToken).ConfigureAwait(false);

        List<CustomerRow> customers = await _database.Customers
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value)
            .OrderBy(row => row.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<PartyAging> parties = [];
        decimal[] totals = new decimal[5];

        foreach (CustomerRow customer in customers)
        {
            decimal[] buckets = new decimal[5];

            foreach (OpenItem item in items.Where(item => item.CustomerId == customer.Id))
            {
                if (item.Outstanding == 0m)
                {
                    continue;
                }

                int age = item.DueOn is { } due ? asOf.DayNumber - due.DayNumber : 0;
                int slot = age <= 0 ? 0 : age <= 30 ? 1 : age <= 60 ? 2 : age <= 90 ? 3 : 4;
                buckets[slot] += item.Outstanding;
            }

            for (int index = 0; index < buckets.Length; index++)
            {
                totals[index] += buckets[index];
            }

            if (buckets.Any(static value => value != 0m))
            {
                parties.Add(new PartyAging(
                    customer.Id,
                    customer.Code,
                    new LocalizedName(customer.NameAr, customer.NameEn),
                    Buckets(buckets)));
            }
        }

        return new AgingReport(asOf, parties, Buckets(totals));
    }

    private AgingBuckets Buckets(decimal[] values) => new(
        Money.Of(values[0], _currency),
        Money.Of(values[1], _currency),
        Money.Of(values[2], _currency),
        Money.Of(values[3], _currency),
        Money.Of(values[4], _currency),
        Money.Of(values.Sum(), _currency));

    /// <summary>
    /// البنود المفتوحة حتى تاريخ: الفواتير بما تبقّى عليها، والمستندات الدائنة بما لم
    /// يُخصَّص منها. المجموع هنا هو ما يجب أن يساوي نقطة الضبط بالضبط.
    /// </summary>
    private async Task<List<OpenItem>> OpenItemsAsync(TenantId tenant, DateOnly asOf, CancellationToken cancellationToken)
    {
        List<OpenItem> items = [];

        List<SalesInvoiceRow> invoices = await _database.Invoices
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.State == SalesDocumentState.Posted
                          && row.IssuedOn <= asOf)
            .OrderBy(row => row.IssuedOn).ThenBy(row => row.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (SalesInvoiceRow invoice in invoices)
        {
            items.Add(new OpenItem(
                invoice.CustomerId,
                SalesInvoiceService.InvoiceDocument,
                invoice.Number,
                invoice.IssuedOn,
                invoice.DueOn,
                invoice.GrossTotal,
                invoice.GrossTotal - invoice.AllocatedAmount - invoice.AdvanceApplied,
                new LocalizedName("فاتورة مبيعات " + invoice.Number, "Sales invoice " + invoice.Number)));
        }

        List<CreditNoteRow> notes = await _database.CreditNotes
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.State == SalesDocumentState.Posted
                          && row.IssuedOn <= asOf)
            .OrderBy(row => row.IssuedOn).ThenBy(row => row.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (CreditNoteRow note in notes)
        {
            items.Add(new OpenItem(
                note.CustomerId,
                CreditNoteService.CreditNoteDocument,
                note.Number,
                note.IssuedOn,
                null,
                -note.GrossTotal,
                -(note.GrossTotal - note.AllocatedAmount),
                new LocalizedName("إشعار دائن " + note.Number, "Credit note " + note.Number)));
        }

        List<CustomerReceiptRow> receipts = await _database.Receipts
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.State == SalesDocumentState.Posted
                          && row.ReceivedOn <= asOf)
            .OrderBy(row => row.ReceivedOn).ThenBy(row => row.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (CustomerReceiptRow receipt in receipts)
        {
            decimal settled = receipt.ReceivedAmount + receipt.DiscountAmount;
            items.Add(new OpenItem(
                receipt.CustomerId,
                CustomerReceiptService.ReceiptDocument,
                receipt.Number,
                receipt.ReceivedOn,
                null,
                -settled,
                -(settled - receipt.AllocatedAmount),
                new LocalizedName("سند قبض " + receipt.Number, "Customer receipt " + receipt.Number)));
        }

        List<CustomerAdvanceRow> advances = await _database.Advances
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.State == SalesDocumentState.Posted
                          && row.ReceivedOn <= asOf)
            .OrderBy(row => row.ReceivedOn).ThenBy(row => row.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (CustomerAdvanceRow advance in advances)
        {
            items.Add(new OpenItem(
                advance.CustomerId,
                CustomerReceiptService.AdvanceDocument,
                advance.Number,
                advance.ReceivedOn,
                null,
                -advance.NetAmount,
                -(advance.NetAmount - advance.AppliedAmount),
                new LocalizedName("دفعة مقدمة " + advance.Number, "Customer advance " + advance.Number)));
        }

        return items;
    }

    /// <summary>بند مفتوح واحد في الدفتر المساعد.</summary>
    private sealed record OpenItem(
        Guid CustomerId,
        string DocumentType,
        string Number,
        DateOnly Date,
        DateOnly? DueOn,
        decimal Effect,
        decimal Outstanding,
        LocalizedName Description);

    /// <summary>يُستعمل في رسائل التشخيص فقط.</summary>
    internal static string Format(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}
