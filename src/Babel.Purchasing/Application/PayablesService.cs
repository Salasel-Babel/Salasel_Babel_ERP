using System.Globalization;
using Babel.Contracts.Subledger;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Purchasing.Persistence;
using Babel.Purchasing.Subledger;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Purchasing.Application;

/// <summary>
/// الدفتر المساعد للذمم الدائنة: أعمار الذمم، وكشف حساب المورد، و<b>مطابقة الدفتر
/// المساعد بنقطة ضبطه</b>.
/// <para>
/// نقطة ضبط الموردين تشمل ذمم الموردين <b>والبضاعة المستلمة غير المفوترة</b> معاً:
/// كلاهما التزام قائم على المنشأة، والفصل بينهما في العرض لا في المطابقة. حساب
/// البضاعة المستلمة غير المفوترة هو الحساب الذي يظلّ منتفخاً بصمت لسنوات حين لا
/// يُطابَق بمستنداته.
/// </para>
/// </summary>
public sealed class PayablesService : IApplicationService
{
    /// <summary>نوع الدفتر المساعد كما تعرّفه بيانات الدفتر.</summary>
    internal const string SubledgerKindCode = "supplier";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly PurchasingDbContext _database;
    private readonly IControlPointReader _controlPoint;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="controlPoint">قارئ نقطة الضبط.</param>
    public PayablesService(IEntitlementEnforcer enforcer, PurchasingRuntime runtime, IControlPointReader controlPoint)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(controlPoint);
        _enforcer = enforcer;
        _database = runtime.Database;
        _controlPoint = controlPoint;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>أعمار الذمم الدائنة حتى تاريخ.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="asOf">التاريخ.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Read)]
    public async ValueTask<Result<AgingReport>> AgingAsync(
        TenantId tenant,
        UserId actor,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Read, "Purchasing.Payables.Aging", cancellationToken)
            .ConfigureAwait(false);

        return gate.IsFailure
            ? Result<AgingReport>.Failure(gate.Errors)
            : Result<AgingReport>.Success(await BuildAgingAsync(tenant, asOf, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>كشف حساب مورد بين تاريخين.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="supplierId">المورد.</param>
    /// <param name="from">من.</param>
    /// <param name="to">إلى.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Read)]
    public async ValueTask<Result<PartyStatement>> StatementAsync(
        TenantId tenant,
        UserId actor,
        Guid supplierId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Read, "Purchasing.Payables.Statement", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PartyStatement>.Failure(gate.Errors);
        }

        if (!await _database.Suppliers
                .AnyAsync(row => row.TenantId == tenant.Value && row.Id == supplierId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PartyStatement>.Failure(PurchasingErrors.SupplierNotFound(supplierId));
        }

        List<OpenItem> items = await OpenItemsAsync(tenant, to, cancellationToken).ConfigureAwait(false);
        List<OpenItem> mine = [.. items
            .Where(item => item.SupplierId == supplierId)
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
                Money.Of(item.Effect < 0m ? -item.Effect : 0m, _currency),
                Money.Of(item.Effect > 0m ? item.Effect : 0m, _currency),
                Money.Of(running, _currency)));
        }

        return Result<PartyStatement>.Success(new PartyStatement(
            supplierId, from, to, Money.Of(opening, _currency), lines, Money.Of(running, _currency)));
    }

    /// <summary>
    /// يطابق الدفتر المساعد بنقطة ضبطه ويُسمّي المستندات المسؤولة عن أي فارق.
    /// <para>الفارق يُقارَن بالصفر بالضبط — لا حدّ تسامح.</para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="asOf">تاريخ المطابقة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Read)]
    public async ValueTask<Result<ControlReconciliationReport>> ReconcileAsync(
        TenantId tenant,
        UserId actor,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Read, "Purchasing.Payables.Reconcile", cancellationToken)
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
            return Result<ControlReconciliationReport>.Failure(PurchasingErrors.ControlPointUnavailable(snapshot.Errors));
        }

        AgingReport aging = await BuildAgingAsync(tenant, asOf, cancellationToken).ConfigureAwait(false);
        decimal subledgerTotal = aging.Totals.Total.Amount;

        // نقطة الضبط تُقرأ «مدين ناقص دائن»؛ والذمم الدائنة تُقرأ موجبةً بالعكس.
        decimal controlTotal = -snapshot.Value.Net;

        List<DocumentPostingRow> postings = await _database.Postings
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.DocumentDate <= asOf)
            .OrderBy(row => row.DocumentType)
            .ThenBy(row => row.DocumentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, decimal> ledgerSide = new(StringComparer.Ordinal);
        Dictionary<string, ControlPointMovement> movements = new(StringComparer.Ordinal);
        foreach (ControlPointMovement movement in snapshot.Value.Movements)
        {
            ledgerSide[Key(movement.DocumentType, movement.DocumentId)] = -movement.Net;
            movements[Key(movement.DocumentType, movement.DocumentId)] = movement;
        }

        List<ReconciliationDivergence> divergences = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        // ‏**التجميع بالمستند لا بالصفّ.** نقطة الضبط تُقرأ مجمّعةً بالمستند، وللمستند
        // الواحد بعد توسيع الهوية أكثر من صفّ محاولة عند الإطلاق نفسه (فاتورة مورد
        // بشقّ بضاعة وشقّ مصروف). فمقارنة صفّ واحد بحركة المستند كاملةً تُنتج
        // «انحرافاً» لا وجود له. المقارنة الصحيحة: مجموع آثار صفوف المستند مقابل
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
                    divergences.Add(Divergence(pending, 0m, DivergenceReason.PostingUnresolved));
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

            bool known = ledgerSide.TryGetValue(document.Key, out decimal ledgerNet);

            if (!known && effect != 0m)
            {
                divergences.Add(Divergence(witness, effect, 0m, DivergenceReason.MissingInControl));
                continue;
            }

            if (effect != ledgerNet)
            {
                divergences.Add(Divergence(witness, effect, ledgerNet, DivergenceReason.AmountMismatch));
            }
        }

        foreach (ControlPointMovement movement in snapshot.Value.Movements)
        {
            string key = Key(movement.DocumentType, movement.DocumentId);
            if (seen.Contains(key) || movement.Net == 0m)
            {
                continue;
            }

            divergences.Add(new ReconciliationDivergence(
                movement.DocumentType,
                movement.DocumentId,
                movement.PartyId,
                Money.Of(0m, _currency),
                Money.Of(-movement.Net, _currency),
                Money.Of(movement.Net, _currency),
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

    private ReconciliationDivergence Divergence(DocumentPostingRow posting, decimal ledgerNet, string reason)
        => Divergence(posting, posting.ControlEffect, ledgerNet, reason);

    /// <summary>انحراف بأثر مجمّع للمستند، والصفّ الشاهد يُسمّي النوع والمعرّف والطرف.</summary>
    private ReconciliationDivergence Divergence(
        DocumentPostingRow witness, decimal effect, decimal ledgerNet, string reason) => new(
        witness.DocumentType,
        witness.DocumentId,
        witness.PartyId,
        Money.Of(effect, _currency),
        Money.Of(ledgerNet, _currency),
        Money.Of(effect - ledgerNet, _currency),
        reason);

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

        List<SupplierRow> suppliers = await _database.Suppliers
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value)
            .OrderBy(row => row.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<PartyAging> parties = [];
        decimal[] totals = new decimal[5];

        foreach (SupplierRow supplier in suppliers)
        {
            decimal[] buckets = new decimal[5];

            foreach (OpenItem item in items.Where(item => item.SupplierId == supplier.Id))
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
                    supplier.Id,
                    supplier.Code,
                    new LocalizedName(supplier.NameAr, supplier.NameEn),
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

    private async Task<List<OpenItem>> OpenItemsAsync(TenantId tenant, DateOnly asOf, CancellationToken cancellationToken)
    {
        List<OpenItem> items = [];

        List<SupplierBillRow> bills = await _database.Bills
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.State == PurchasingDocumentState.Posted
                          && row.IssuedOn <= asOf)
            .OrderBy(row => row.IssuedOn).ThenBy(row => row.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // ── إعفاء «بضاعة مستلمة لم تُفوتر» بالمُرحَّل وحده ────────────────────
        // حقل GoodsReceiptRow.BilledValue يزيد عند إنشاء مسوّدة الفاتورة، لأنه
        // حجز للمطابقة الثلاثية يمنع تفويتر الاستلام نفسه مرّتين. أمّا الحساب
        // الضابط فلا يُعفى إلا عند الترحيل. فلو أعفى الدفتر المساعد البند المفتوح
        // بالمسوّدة، لنقص عن حسابه الضابط بقيمة كل فاتورة مخزنية لم تُرحَّل بعد —
        // انحرافاً صامتاً لا يُسمّي مستنداً واحداً مسؤولاً عنه. الإعفاء هنا من
        // الفواتير المُرحَّلة وحدها.
        Dictionary<Guid, decimal> relieved = [];
        foreach (SupplierBillRow bill in bills)
        {
            if (bill.ReceiptId is { } receiptId)
            {
                relieved[receiptId] = relieved.GetValueOrDefault(receiptId) + bill.ReceiptValue;
            }
        }

        List<GoodsReceiptRow> receipts = await _database.Receipts
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.State == PurchasingDocumentState.Posted
                          && row.ReceivedOn <= asOf)
            .OrderBy(row => row.ReceivedOn).ThenBy(row => row.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (GoodsReceiptRow receipt in receipts)
        {
            items.Add(new OpenItem(
                receipt.SupplierId,
                GoodsReceiptService.ReceiptLineDocument,
                receipt.Number,
                receipt.ReceivedOn,
                null,
                receipt.ReceiptCost,
                receipt.ReceiptCost - relieved.GetValueOrDefault(receipt.Id),
                new LocalizedName("بضاعة مستلمة لم تُفوتر " + receipt.Number, "Goods received not invoiced " + receipt.Number)));
        }

        foreach (SupplierBillRow bill in bills)
        {
            items.Add(new OpenItem(
                bill.SupplierId,
                SupplierBillService.BillDocument,
                bill.Number,
                bill.IssuedOn,
                bill.DueOn,
                bill.GrossTotal - bill.ReceiptValue,
                bill.GrossTotal - bill.AllocatedAmount,
                new LocalizedName("فاتورة مورد " + bill.Number, "Supplier bill " + bill.Number)));
        }

        List<SupplierPaymentRow> payments = await _database.Payments
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.State == PurchasingDocumentState.Posted
                          && row.PaidOn <= asOf)
            .OrderBy(row => row.PaidOn).ThenBy(row => row.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (SupplierPaymentRow payment in payments)
        {
            items.Add(new OpenItem(
                payment.SupplierId,
                SupplierPaymentService.PaymentDocument,
                payment.Number,
                payment.PaidOn,
                null,
                -payment.PaidAmount,
                -(payment.PaidAmount - payment.AllocatedAmount),
                new LocalizedName("سند صرف " + payment.Number, "Supplier payment " + payment.Number)));
        }

        List<DebitNoteRow> notes = await _database.DebitNotes
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.State == PurchasingDocumentState.Posted
                          && row.IssuedOn <= asOf)
            .OrderBy(row => row.IssuedOn).ThenBy(row => row.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (DebitNoteRow note in notes)
        {
            items.Add(new OpenItem(
                note.SupplierId,
                SupplierBillService.DebitNoteDocument,
                note.Number,
                note.IssuedOn,
                null,
                -note.GrossTotal,
                -(note.GrossTotal - note.AllocatedAmount),
                new LocalizedName("إشعار مدين " + note.Number, "Debit note " + note.Number)));
        }

        List<LandedCostRow> costs = await _database.LandedCosts
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.State == PurchasingDocumentState.Posted
                          && row.Source == "supplier_invoice"
                          && row.IncurredOn <= asOf)
            .OrderBy(row => row.IncurredOn).ThenBy(row => row.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (LandedCostRow cost in costs)
        {
            items.Add(new OpenItem(
                cost.SupplierId,
                SupplierPaymentService.LandedCostDocument,
                cost.Number,
                cost.IncurredOn,
                null,
                cost.CostAmount,
                cost.CostAmount - cost.AllocatedAmount,
                new LocalizedName("تكلفة استيراد " + cost.Number, "Landed cost " + cost.Number)));
        }

        return items;
    }

    /// <summary>بند مفتوح واحد في الدفتر المساعد. الموجب التزام على المنشأة.</summary>
    private sealed record OpenItem(
        Guid SupplierId,
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
