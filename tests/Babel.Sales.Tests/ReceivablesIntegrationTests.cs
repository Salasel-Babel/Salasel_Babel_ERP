using System.Diagnostics;
using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Sales.Application;
using Babel.Sales.Subledger;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Sales.Tests;

/// <summary>
/// إثبات الذمم المدينة على PostgreSQL <b>حقيقية</b> ودفتر أستاذ <b>حقيقي</b>.
/// <para>كل مشهد هنا يقابل بنداً في مهمة الإثبات، ويطبع حكمه ودليله.</para>
/// </summary>
[Collection("receivables")]
public sealed class ReceivablesIntegrationTests : IAsyncLifetime
{
    private static readonly DateOnly March = new(2026, 3, 10);
    private static int _sequence;

    private Harness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await Harness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync()
    {
        _harness.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string Next(string prefix)
        => prefix + "-" + Interlocked.Increment(ref _sequence).ToString("D5", CultureInfo.InvariantCulture);

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · فاتورة مبيعات ترحّل، ونقطة الضبط تتحرّك بإجمالي الفاتورة بالضبط
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Sales_invoice_posts_and_the_control_point_moves_by_exactly_the_invoice_total()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.Tenant;

        decimal before = await LedgerProbe
            .ControlNetAsync(SalesTestEnvironment.Ledger.AppConnectionString, tenant, "customer", token);

        Guid customer = await _harness.CustomerAsync(Next("CUS"));

        Result<SalesDocumentView> created = await _harness.Invoices.CreateInvoiceAsync(
            tenant,
            Harness.Actor,
            new SalesDocumentDraft(Next("INV"), customer, March, "BR-01", [Harness.Line(10m, 1_000m)]),
            null,
            token);

        Assert.True(created.IsSuccess, Describe(created.Errors));
        Assert.Equal(11_500.0000m, created.Value.Totals.Gross.Amount);

        Result<SalesDocumentView> posted = await _harness.Invoices
            .PostInvoiceAsync(tenant, Harness.Actor, created.Value.Id, token);

        Assert.True(posted.IsSuccess, Describe(posted.Errors));
        Assert.NotNull(posted.Value.EntryId);

        decimal after = await LedgerProbe
            .ControlNetAsync(SalesTestEnvironment.Ledger.AppConnectionString, tenant, "customer", token);

        Proof.Require(
            after - before == 11_500.0000m,
            "فاتورة مبيعات ترحّل ونقطة ضبط العملاء تتحرّك بإجمالي الفاتورة بالضبط",
            "قبل=" + Proof.Money(before) + " بعد=" + Proof.Money(after) + " الفرق=" + Proof.Money(after - before));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · سند قبض يُخصَّص على فاتورتين ويترك المتبقّي الصحيح
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Receipt_allocates_across_two_invoices_and_leaves_the_correct_residual()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.Tenant;
        Guid customer = await _harness.CustomerAsync(Next("CUS"));

        Guid first = await PostedInvoiceAsync(customer, 1_000m, token);
        Guid second = await PostedInvoiceAsync(customer, 2_000m, token);

        decimal before = await LedgerProbe
            .ControlNetAsync(SalesTestEnvironment.Ledger.AppConnectionString, tenant, "customer", token);

        Result<SalesDocumentView> receipt = await _harness.Receipts.RecordReceiptAsync(
            tenant,
            Harness.Actor,
            new CustomerReceiptDraft(
                Next("RCP"), customer, March, "bank", "BANK-01",
                Harness.Sar(2_000m), Harness.Sar(0m),
                [
                    new AllocationDraft(first, Harness.Sar(1_150m)),
                    new AllocationDraft(second, Harness.Sar(850m)),
                ]),
            token);

        Assert.True(receipt.IsSuccess, Describe(receipt.Errors));

        Result<SalesDocumentView> posted = await _harness.Receipts
            .PostReceiptAsync(tenant, Harness.Actor, receipt.Value.Id, token);
        Assert.True(posted.IsSuccess, Describe(posted.Errors));

        decimal after = await LedgerProbe
            .ControlNetAsync(SalesTestEnvironment.Ledger.AppConnectionString, tenant, "customer", token);

        Result<AgingReport> aging = await _harness.Receivables.AgingAsync(tenant, Harness.Actor, March, token);
        PartyAging party = Assert.Single(aging.Value.Parties, p => p.PartyId == customer);

        // ‏1,150 + 2,300 = 3,450 مستحقة، وسُدّد 2,000 ⇒ المتبقّي 1,450 بالضبط.
        Proof.Require(
            party.Buckets.Total.Amount == 1_450.0000m && after - before == -2_000.0000m,
            "سند قبض واحد يُخصَّص على فاتورتين ويترك المتبقّي الصحيح",
            "المتبقّي=" + Proof.Money(party.Buckets.Total.Amount)
            + " وحركة نقطة الضبط=" + Proof.Money(after - before));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 · التخصيص الزائد مرفوض
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Over_allocation_is_refused()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.Tenant;
        Guid customer = await _harness.CustomerAsync(Next("CUS"));
        Guid invoice = await PostedInvoiceAsync(customer, 1_000m, token);

        Result<SalesDocumentView> beyondInvoice = await _harness.Receipts.RecordReceiptAsync(
            tenant,
            Harness.Actor,
            new CustomerReceiptDraft(
                Next("RCP"), customer, March, "bank", "BANK-01",
                Harness.Sar(5_000m), Harness.Sar(0m),
                [new AllocationDraft(invoice, Harness.Sar(2_000m))]),
            token);

        Result<SalesDocumentView> beyondReceipt = await _harness.Receipts.RecordReceiptAsync(
            tenant,
            Harness.Actor,
            new CustomerReceiptDraft(
                Next("RCP"), customer, March, "bank", "BANK-01",
                Harness.Sar(100m), Harness.Sar(0m),
                [new AllocationDraft(invoice, Harness.Sar(500m))]),
            token);

        Proof.Require(
            beyondInvoice.IsFailure && beyondInvoice.Errors[0].Code == "sales.over_allocation"
            && beyondReceipt.IsFailure && beyondReceipt.Errors[0].Code == "sales.over_allocation",
            "التخصيص الزائد مرفوض على الطرفين: أكثر مما على الفاتورة، وأكثر مما في السند",
            beyondInvoice.Errors[0].Code + " · " + beyondReceipt.Errors[0].Code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4 · إشعار دائن يعكس الأثر، والفاتورة الأصلية وقيدها لا يُمسّان
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Credit_note_reverses_the_effect_and_the_original_is_untouched()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.Tenant;
        Guid customer = await _harness.CustomerAsync(Next("CUS"));

        Result<SalesDocumentView> created = await _harness.Invoices.CreateInvoiceAsync(
            tenant,
            Harness.Actor,
            new SalesDocumentDraft(Next("INV"), customer, March, "BR-01", [Harness.Line(4m, 250m)]),
            null,
            token);
        Result<SalesDocumentView> posted = await _harness.Invoices
            .PostInvoiceAsync(tenant, Harness.Actor, created.Value.Id, token);
        Assert.True(posted.IsSuccess, Describe(posted.Errors));

        Guid originalEntry = posted.Value.EntryId!.Value;
        (string statusBefore, long linesBefore) = await LedgerProbe
            .EntryAsync(SalesTestEnvironment.Ledger.AppConnectionString, originalEntry, token);

        decimal before = await LedgerProbe
            .ControlNetAsync(SalesTestEnvironment.Ledger.AppConnectionString, tenant, "customer", token);

        Result<SalesDocumentView> note = await _harness.CreditNotes.CreateAsync(
            tenant,
            Harness.Actor,
            new CreditNoteDraft(Next("CRN"), created.Value.Id, March, [Harness.Line(4m, 250m)]),
            token);
        Assert.True(note.IsSuccess, Describe(note.Errors));

        Result<SalesDocumentView> postedNote = await _harness.CreditNotes
            .PostAsync(tenant, Harness.Actor, note.Value.Id, token);
        Assert.True(postedNote.IsSuccess, Describe(postedNote.Errors));

        decimal after = await LedgerProbe
            .ControlNetAsync(SalesTestEnvironment.Ledger.AppConnectionString, tenant, "customer", token);

        (string statusAfter, long linesAfter) = await LedgerProbe
            .EntryAsync(SalesTestEnvironment.Ledger.AppConnectionString, originalEntry, token);

        Result<SalesDocumentView> invoiceNow = await _harness.Invoices
            .GetInvoiceAsync(tenant, Harness.Actor, created.Value.Id, token);

        Proof.Require(
            after - before == -1_150.0000m
            && statusBefore == statusAfter && linesBefore == linesAfter
            && invoiceNow.Value.State == "POSTED"
            && invoiceNow.Value.Totals.Gross.Amount == 1_150.0000m,
            "إشعار دائن يعكس الأثر بقيد مستقلّ، والفاتورة الأصلية وقيدها لم يُمسّا",
            "حركة نقطة الضبط=" + Proof.Money(after - before)
            + " · قيد الأصل قبل=" + statusBefore + "/" + linesBefore.ToString(CultureInfo.InvariantCulture)
            + " بعد=" + statusAfter + "/" + linesAfter.ToString(CultureInfo.InvariantCulture)
            + " · حالة الفاتورة=" + invoiceNow.Value.State);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5 · مستند مكرَّر يُرحَّل مرة واحدة بالضبط تحت ثلاثة ترتيبات وصول مختلفة
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task A_duplicated_document_posts_exactly_once_under_three_arrival_orders()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.Tenant;
        Guid customer = await _harness.CustomerAsync(Next("CUS"));

        List<Guid> invoices = [];
        for (int index = 0; index < 3; index++)
        {
            Result<SalesDocumentView> created = await _harness.Invoices.CreateInvoiceAsync(
                tenant,
                Harness.Actor,
                new SalesDocumentDraft(Next("INV"), customer, March, "BR-01", [Harness.Line(1m, 500m)]),
                null,
                token);
            invoices.Add(created.Value.Id);
        }

        decimal before = await LedgerProbe
            .ControlNetAsync(SalesTestEnvironment.Ledger.AppConnectionString, tenant, "customer", token);

        // ثلاثة ترتيبات وصول، وتكرار مقصود في كلٍّ منها. الإحكام مفتاح لكل مستند
        // ومستقلّ عن الترتيب — لا حارس تصاعدي لكل عميل (فخ-13).
        int[][] orders =
        [
            [0, 1, 2, 0, 1, 2],
            [2, 0, 1, 2, 2, 0],
            [1, 2, 0, 1, 0, 2],
        ];

        foreach (int[] arrival in orders)
        {
            foreach (int index in arrival)
            {
                Result<SalesDocumentView> result = await _harness.Invoices
                    .PostInvoiceAsync(tenant, Harness.Actor, invoices[index], token);
                Assert.True(result.IsSuccess, Describe(result.Errors));
            }
        }

        decimal after = await LedgerProbe
            .ControlNetAsync(SalesTestEnvironment.Ledger.AppConnectionString, tenant, "customer", token);

        long entries = 0;
        foreach (Guid invoice in invoices)
        {
            entries += await LedgerProbe.EntryCountAsync(
                SalesTestEnvironment.Ledger.AppConnectionString,
                tenant,
                "SalesInvoice",
                invoice.ToString("D", CultureInfo.InvariantCulture),
                token);
        }

        Proof.Require(
            entries == 3 && after - before == 3 * 575.0000m,
            "ثمانية عشر نداء ترحيل بثلاثة ترتيبات وصول تُنتج ثلاثة قيود بالضبط",
            "عدد القيود=" + entries.ToString(CultureInfo.InvariantCulture)
            + " وحركة نقطة الضبط=" + Proof.Money(after - before));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 6 · الضريبة تُقرَّب على السطر، والمجموع مجموع سطور مقرَّبة
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Tax_is_rounded_per_line_and_the_total_is_the_sum_of_rounded_lines()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.Tenant;
        Guid customer = await _harness.CustomerAsync(Next("CUS"));

        // عشرة سطور صافي كلٍّ منها 0.10: ضريبة السطر 0.015 ⇒ 0.02 بعد التقريب،
        // فمجموع الضريبة 0.20. ولو قُرِّب المجموع بدل السطور لكان 0.15 —
        // فرق خمس هللات على فاتورة واحدة، وهو الفرق الذي يُناقَش مع الهيئة.
        List<SalesLineDraft> lines = [.. Enumerable.Range(0, 10).Select(_ => Harness.Line(1m, 0.10m))];

        Result<SalesDocumentView> created = await _harness.Invoices.CreateInvoiceAsync(
            tenant,
            Harness.Actor,
            new SalesDocumentDraft(Next("INV"), customer, March, "BR-01", lines),
            null,
            token);

        Assert.True(created.IsSuccess, Describe(created.Errors));

        decimal naive = decimal.Round(created.Value.Totals.Net.Amount * 0.15m, 2, MidpointRounding.AwayFromZero);

        decimal before = await LedgerProbe
            .ControlNetAsync(SalesTestEnvironment.Ledger.AppConnectionString, tenant, "customer", token);
        Result<SalesDocumentView> posted = await _harness.Invoices
            .PostInvoiceAsync(tenant, Harness.Actor, created.Value.Id, token);
        Assert.True(posted.IsSuccess, Describe(posted.Errors));
        decimal after = await LedgerProbe
            .ControlNetAsync(SalesTestEnvironment.Ledger.AppConnectionString, tenant, "customer", token);

        Proof.Require(
            created.Value.Totals.Tax.Amount == 0.2000m
            && naive == 0.1500m
            && created.Value.Totals.Gross.Amount == 1.2000m
            && after - before == 1.2000m,
            "الضريبة تُحسب وتُقرَّب على السطر، والمجموع مجموع سطور مقرَّبة ولا يُعاد تقريبه",
            "ضريبة مجموع السطور=" + Proof.Money(created.Value.Totals.Tax.Amount)
            + " مقابل تقريب المجموع=" + Proof.Money(naive)
            + " · حركة نقطة الضبط=" + Proof.Money(after - before));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 7 · أعمار الديون تطابق نقطة الضبط بالضبط · والمطابقة تُبلّغ صفراً
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Aging_buckets_tie_exactly_to_the_control_point_and_reconciliation_reports_zero()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.Tenant;
        Guid customer = await _harness.CustomerAsync(Next("CUS"), termsDays: 0);

        Guid invoice = await PostedInvoiceAsync(customer, 800m, token);

        Result<SalesDocumentView> advance = await _harness.Receipts.RecordAdvanceAsync(
            tenant,
            Harness.Actor,
            new CustomerAdvanceDraft(
                Next("ADV"), customer, March, "bank", "BANK-01", Harness.Sar(300m), Harness.Sar(0m), false),
            token);
        Assert.True(advance.IsSuccess, Describe(advance.Errors));
        Result<SalesDocumentView> postedAdvance = await _harness.Receipts
            .PostAdvanceAsync(tenant, Harness.Actor, advance.Value.Id, token);
        Assert.True(postedAdvance.IsSuccess, Describe(postedAdvance.Errors));

        Result<PostingReceipt> applied = await _harness.Receipts
            .ApplyAdvanceAsync(tenant, Harness.Actor, advance.Value.Id, invoice, Harness.Sar(300m), token);
        Assert.True(applied.IsSuccess, Describe(applied.Errors));

        DateOnly asOf = new(2026, 5, 31);
        Result<AgingReport> aging = await _harness.Receivables.AgingAsync(tenant, Harness.Actor, asOf, token);
        Assert.True(aging.IsSuccess, Describe(aging.Errors));

        decimal control = await LedgerProbe
            .ControlNetAsync(SalesTestEnvironment.Ledger.AppConnectionString, tenant, "customer", token);

        AgingBuckets totals = aging.Value.Totals;
        decimal sumOfBuckets = totals.NotDue.Amount + totals.Days1To30.Amount
            + totals.Days31To60.Amount + totals.Days61To90.Amount + totals.Over90.Amount;

        Result<ControlReconciliationReport> reconciliation = await _harness.Receivables
            .ReconcileAsync(tenant, Harness.Actor, asOf, token);
        Assert.True(reconciliation.IsSuccess, Describe(reconciliation.Errors));

        Proof.Require(
            totals.Total.Amount == control && sumOfBuckets == totals.Total.Amount,
            "شرائح أعمار الديون تطابق نقطة الضبط بالضبط",
            "مجموع الشرائح=" + Proof.Money(sumOfBuckets)
            + " ومجموع التقرير=" + Proof.Money(totals.Total.Amount)
            + " ونقطة الضبط=" + Proof.Money(control));

        Proof.Require(
            reconciliation.Value.IsReconciled && reconciliation.Value.Divergence.Amount == 0m,
            "المطابقة على مجموعة سليمة تُبلّغ انحرافاً صفرياً بلا مستند واحد مسؤول",
            "الدفتر المساعد=" + Proof.Money(reconciliation.Value.SubledgerTotal.Amount)
            + " ونقطة الضبط=" + Proof.Money(reconciliation.Value.ControlTotal.Amount)
            + " والانحرافات=" + reconciliation.Value.Divergences.Count.ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 8 · المطابقة تلتقط انحرافاً محقوناً وتُسمّي المستند المسؤول
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Reconciliation_identifies_an_injected_divergence_and_names_the_document()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.InjectedTenant;
        DateOnly asOf = new(2026, 5, 31);

        Result<ControlReconciliationReport> clean = await _harness.Receivables
            .ReconcileAsync(tenant, Harness.Actor, asOf, token);
        Assert.True(clean.IsSuccess, Describe(clean.Errors));
        Assert.True(clean.Value.IsReconciled);

        // الحقن: قيد يدوي على الحساب الضابط للعملاء بلا مستند في الدفتر المساعد —
        // وهو السبب الحقيقي الأشيع لانحراف الدفاتر المساعدة.
        string strayDocument = Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture);

        Result<PostingReceipt> stray = await _harness.Posting.PostAsync(
            new PostingRequest
            {
                Tenant = tenant,
                IdempotencyKey = new IdempotencyKey("manual:stray:" + strayDocument.Replace("-", string.Empty, StringComparison.Ordinal)),
                Source = new SourceDocument(BabelModule.Sales, "ManualJournal", strayDocument),
                Trigger = PostingTrigger.OnApproval,
                DocumentDate = March,
                Narration = new LocalizedName("قيد يدوي على الحساب الضابط", "Manual entry on the control account"),
                Currency = CurrencyCode.Sar,
                Lines =
                [
                    new PostingLine
                    {
                        Role = PostingRole.GrossAmount,
                        Side = PostingSide.Debit,
                        Amount = Harness.Sar(777m),
                        Subledger = new SubledgerReference(SubledgerKind.Customer, "GHOST"),
                    },
                    new PostingLine
                    {
                        Role = PostingRole.NetAmount,
                        Side = PostingSide.Credit,
                        Amount = Harness.Sar(777m),
                        Scope = new PostingScope("BR-01", null, null),
                    },
                ],
            },
            token);

        Assert.True(stray.IsSuccess, Describe(stray.Errors));

        Result<ControlReconciliationReport> dirty = await _harness.Receivables
            .ReconcileAsync(tenant, Harness.Actor, asOf, token);
        Assert.True(dirty.IsSuccess, Describe(dirty.Errors));

        ReconciliationDivergence responsible = Assert.Single(dirty.Value.Divergences);

        Proof.Require(
            !dirty.Value.IsReconciled
            && dirty.Value.Divergence.Amount == -777.0000m
            && responsible.ReasonCode == DivergenceReason.MissingInSubledger
            && responsible.DocumentId == strayDocument
            && responsible.PartyId == "GHOST",
            "المطابقة تلتقط الانحراف المحقون وتُسمّي المستند والطرف المسؤولين",
            "الانحراف=" + Proof.Money(dirty.Value.Divergence.Amount)
            + " · السبب=" + responsible.ReasonCode
            + " · المستند=" + responsible.DocumentType + "/" + responsible.DocumentId
            + " · الطرف=" + responsible.PartyId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 9 · ترحيل مرفوض يترك المستند متّسقاً وقابلاً لإعادة المحاولة
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task A_refused_posting_leaves_the_document_coherent_and_retryable()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.Tenant;
        Guid customer = await _harness.CustomerAsync(Next("CUS"));

        // فبراير مقفلة نهائياً في هذه البيئة: الرفض يأتي من قاعدة البيانات نفسها.
        DateOnly closed = new(2026, 2, 15);

        Result<SalesDocumentView> created = await _harness.Invoices.CreateInvoiceAsync(
            tenant,
            Harness.Actor,
            new SalesDocumentDraft(Next("INV"), customer, closed, "BR-01", [Harness.Line(2m, 100m)]),
            null,
            token);
        Assert.True(created.IsSuccess, Describe(created.Errors));

        Result<SalesDocumentView> first = await _harness.Invoices
            .PostInvoiceAsync(tenant, Harness.Actor, created.Value.Id, token);
        Result<SalesDocumentView> second = await _harness.Invoices
            .PostInvoiceAsync(tenant, Harness.Actor, created.Value.Id, token);

        Result<SalesDocumentView> stillDraft = await _harness.Invoices
            .GetInvoiceAsync(tenant, Harness.Actor, created.Value.Id, token);

        long entriesAfterRefusal = await LedgerProbe.EntryCountAsync(
            SalesTestEnvironment.Ledger.AppConnectionString,
            tenant,
            "SalesInvoice",
            created.Value.Id.ToString("D", CultureInfo.InvariantCulture),
            token);

        (string state, int attempts, string failure) = await AttemptAsync(created.Value.Id, token);

        // إصلاح السبب: تُفتح الفترة بدور المالك، وتُسقَط لقطة الشركة، ثم يُعاد النداء.
        await ReopenFebruaryAsync(tenant, token);
        _harness.LedgerRuntime.InvalidateReference(tenant.Value);

        Result<SalesDocumentView> retried = await _harness.Invoices
            .PostInvoiceAsync(tenant, Harness.Actor, created.Value.Id, token);

        long entriesAfterRetry = await LedgerProbe.EntryCountAsync(
            SalesTestEnvironment.Ledger.AppConnectionString,
            tenant,
            "SalesInvoice",
            created.Value.Id.ToString("D", CultureInfo.InvariantCulture),
            token);

        Proof.Require(
            first.IsFailure && second.IsFailure
            && stillDraft.Value.State == "DRAFT"
            && entriesAfterRefusal == 0
            && state == "REFUSED" && attempts == 2 && failure.Length > 0
            && retried.IsSuccess && entriesAfterRetry == 1,
            "الترحيل المرفوض يترك المستند مسوّدةً ومعه سبب مكتوب، وإعادة المحاولة بعد إصلاح السبب تنتج قيداً واحداً",
            "حالة المستند بعد الرفض=" + stillDraft.Value.State
            + " · قيود بعد الرفض=" + entriesAfterRefusal.ToString(CultureInfo.InvariantCulture)
            + " · سجلّ المحاولة=" + state + "/" + attempts.ToString(CultureInfo.InvariantCulture) + "/" + failure
            + " · قيود بعد إعادة المحاولة=" + entriesAfterRetry.ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 10 · العكس بقيد مضاد: الأصل باقٍ كما هو، والدفتر المساعد يبقى مطابقاً
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Reversing_a_posted_invoice_leaves_the_original_entry_intact_and_the_subledger_tied()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.Tenant;
        Guid customer = await _harness.CustomerAsync(Next("CUS"));

        Result<SalesDocumentView> created = await _harness.Invoices.CreateInvoiceAsync(
            tenant,
            Harness.Actor,
            new SalesDocumentDraft(Next("INV"), customer, March, "BR-01", [Harness.Line(1m, 640m)]),
            null,
            token);
        Result<SalesDocumentView> posted = await _harness.Invoices
            .PostInvoiceAsync(tenant, Harness.Actor, created.Value.Id, token);
        Assert.True(posted.IsSuccess, Describe(posted.Errors));

        Guid entryId = posted.Value.EntryId!.Value;
        (string statusBefore, long linesBefore) = await LedgerProbe
            .EntryAsync(SalesTestEnvironment.Ledger.AppConnectionString, entryId, token);

        Result<PostingReceipt> reversal = await _harness.Invoices.ReverseInvoiceAsync(
            tenant,
            Harness.Actor,
            created.Value.Id,
            new LocalizedName("خطأ في الفاتورة", "Invoice issued in error"),
            token);
        Assert.True(reversal.IsSuccess, Describe(reversal.Errors));

        (string statusAfter, long linesAfter) = await LedgerProbe
            .EntryAsync(SalesTestEnvironment.Ledger.AppConnectionString, entryId, token);

        DateOnly asOf = new(2026, 5, 31);
        Result<ControlReconciliationReport> reconciliation = await _harness.Receivables
            .ReconcileAsync(tenant, Harness.Actor, asOf, token);

        Proof.Require(
            statusBefore == statusAfter && linesBefore == linesAfter
            && reconciliation.Value.IsReconciled,
            "العكس يكتب قيداً مضاداً ولا يمسّ الأصل، والدفتر المساعد يبقى مطابقاً لنقطة ضبطه",
            "الأصل قبل=" + statusBefore + "/" + linesBefore.ToString(CultureInfo.InvariantCulture)
            + " بعد=" + statusAfter + "/" + linesAfter.ToString(CultureInfo.InvariantCulture)
            + " · انحراف المطابقة=" + Proof.Money(reconciliation.Value.Divergence.Amount));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 11 · كشف حساب العميل: رصيده الختامي هو رصيده في الدفتر المساعد
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task The_customer_statement_closing_balance_matches_the_subledger()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.Tenant;
        Guid customer = await _harness.CustomerAsync(Next("CUS"));

        Guid invoice = await PostedInvoiceAsync(customer, 1_200m, token);

        Result<SalesDocumentView> receipt = await _harness.Receipts.RecordReceiptAsync(
            tenant,
            Harness.Actor,
            new CustomerReceiptDraft(
                Next("RCP"), customer, March, "cash", "CASH-01",
                Harness.Sar(380m), Harness.Sar(20m),
                [new AllocationDraft(invoice, Harness.Sar(400m))]),
            token);
        Assert.True(receipt.IsSuccess, Describe(receipt.Errors));
        Result<SalesDocumentView> postedReceipt = await _harness.Receipts
            .PostReceiptAsync(tenant, Harness.Actor, receipt.Value.Id, token);
        Assert.True(postedReceipt.IsSuccess, Describe(postedReceipt.Errors));

        Result<PartyStatement> statement = await _harness.Receivables.StatementAsync(
            tenant, Harness.Actor, customer, new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 31), token);
        Assert.True(statement.IsSuccess, Describe(statement.Errors));

        Result<AgingReport> aging = await _harness.Receivables
            .AgingAsync(tenant, Harness.Actor, new DateOnly(2026, 5, 31), token);
        PartyAging party = Assert.Single(aging.Value.Parties, p => p.PartyId == customer);

        Proof.Require(
            statement.Value.Closing.Amount == party.Buckets.Total.Amount
            && statement.Value.Closing.Amount == 980.0000m
            && statement.Value.Lines.Count == 2,
            "كشف حساب العميل: رصيده الختامي هو رصيده في أعمار الديون بالضبط، وخصم التعجيل ينقص الذمة كاملاً",
            "الرصيد الختامي=" + Proof.Money(statement.Value.Closing.Amount)
            + " وأعمار الديون=" + Proof.Money(party.Buckets.Total.Amount)
            + " وعدد الحركات=" + statement.Value.Lines.Count.ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 12 · قيد تكلفة المبيعات المصاحب — مستند مستقلّ لا يبتلعه إحكام الفاتورة
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task The_accompanying_cost_of_sales_entry_posts_as_its_own_document()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.Tenant;
        Guid customer = await _harness.CustomerAsync(Next("CUS"));
        Guid invoice = await PostedInvoiceAsync(customer, 900m, token);

        Result<PostingReceipt> cost = await _harness.Invoices.PostCostOfSalesAsync(
            tenant,
            Harness.Actor,
            invoice,
            new CostOfSalesDraft("ITEM-1", "WH-01", "*", Harness.Sar(500m)),
            token);

        Assert.True(cost.IsSuccess, Describe(cost.Errors));

        long invoiceEntries = await LedgerProbe.EntryCountAsync(
            SalesTestEnvironment.Ledger.AppConnectionString, tenant, "SalesInvoice",
            invoice.ToString("D", CultureInfo.InvariantCulture), token);
        long costEntries = await LedgerProbe.EntryCountAsync(
            SalesTestEnvironment.Ledger.AppConnectionString, tenant, "SalesInvoiceCostOfSales",
            invoice.ToString("D", CultureInfo.InvariantCulture), token);

        Proof.Require(
            invoiceEntries == 1 && costEntries == 1 && !cost.Value.WasAlreadyPosted,
            "قيد التكلفة المصاحب يُرحَّل بنوع مستند مستقلّ فلا يبتلعه مفتاح إحكام الفاتورة",
            "قيود الفاتورة=" + invoiceEntries.ToString(CultureInfo.InvariantCulture)
            + " وقيود التكلفة=" + costEntries.ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 13 · الإنتاجية — دفعة فواتير على هذا الجهاز
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Throughput_of_posting_a_batch_of_invoices()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.Tenant;
        Guid customer = await _harness.CustomerAsync(Next("CUS"));

        const int Batch = 60;
        List<Guid> invoices = [];

        for (int index = 0; index < Batch; index++)
        {
            Result<SalesDocumentView> created = await _harness.Invoices.CreateInvoiceAsync(
                tenant,
                Harness.Actor,
                new SalesDocumentDraft(Next("INV"), customer, March, "BR-01", [Harness.Line(1m, 100m)]),
                null,
                token);
            invoices.Add(created.Value.Id);
        }

        Stopwatch clock = Stopwatch.StartNew();
        foreach (Guid invoice in invoices)
        {
            Result<SalesDocumentView> posted = await _harness.Invoices
                .PostInvoiceAsync(tenant, Harness.Actor, invoice, token);
            Assert.True(posted.IsSuccess, Describe(posted.Errors));
        }

        clock.Stop();
        double perSecond = Batch / clock.Elapsed.TotalSeconds;

        Proof.Require(
            perSecond > 0,
            "إنتاجية ترحيل دفعة فواتير عبر كامل مسار الوحدة",
            Batch.ToString(CultureInfo.InvariantCulture) + " فاتورة في "
            + clock.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture) + " ثانية = "
            + perSecond.ToString("0.0", CultureInfo.InvariantCulture) + " فاتورة/ث");

        Proof.Note(
            "التحفّظ: حاوية مشتركة بأربع أنوية افتراضية، وPostgreSQL على المضيف نفسه (RTT شبه صفري)، "
            + "وكاتب واحد متسلسل، ورقم يشمل كتابة الوحدة وقراءتها بـEF Core لا الترحيل وحده.");
    }

    private async Task<Guid> PostedInvoiceAsync(Guid customer, decimal unitPrice, CancellationToken token)
    {
        Result<SalesDocumentView> created = await _harness.Invoices.CreateInvoiceAsync(
            SalesTestEnvironment.Tenant,
            Harness.Actor,
            new SalesDocumentDraft(Next("INV"), customer, March, "BR-01", [Harness.Line(1m, unitPrice)]),
            null,
            token);

        Assert.True(created.IsSuccess, Describe(created.Errors));

        Result<SalesDocumentView> posted = await _harness.Invoices
            .PostInvoiceAsync(SalesTestEnvironment.Tenant, Harness.Actor, created.Value.Id, token);

        Assert.True(posted.IsSuccess, Describe(posted.Errors));
        return created.Value.Id;
    }

    private static async Task<(string State, int Attempts, string Failure)> AttemptAsync(Guid documentId, CancellationToken token)
    {
        await using NpgsqlConnection connection = new(SalesTestEnvironment.Sales.ConnectionString);
        await connection.OpenAsync(token);
        await using NpgsqlCommand command = new(
            """
            select "State", "AttemptCount", "FailureCode"
              from sales.document_posting
             where "DocumentType" = 'SalesInvoice' and "DocumentId" = $1
            """, connection);
        command.Parameters.AddWithValue(documentId.ToString("D", CultureInfo.InvariantCulture));
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        await reader.ReadAsync(token);
        return (reader.GetString(0), reader.GetInt32(1), reader.GetString(2));
    }

    private static async Task ReopenFebruaryAsync(TenantId tenant, CancellationToken token)
    {
        await using NpgsqlConnection owner = new(SalesTestEnvironment.Ledger.OwnerConnectionString);
        await owner.OpenAsync(token);
        await using NpgsqlCommand command = new(
            "update ledger.fiscal_period set state = 'open' where company_id = $1 and period_code = '2026-02'", owner);
        command.Parameters.AddWithValue(tenant.Value);
        await command.ExecuteNonQueryAsync(token);
    }

    private static string Describe(IReadOnlyList<Error> errors)
        => string.Join(" | ", errors.Select(static error => error.ToString()));
}
