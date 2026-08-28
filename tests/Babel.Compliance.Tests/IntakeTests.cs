using Babel.Compliance.Abstractions;
using Babel.Compliance.Application;
using Babel.Compliance.Intake;
using Babel.Compliance.Model;
using Babel.Compliance.Pipeline;
using Babel.Compliance.Zatca.Documents;
using Babel.Contracts.Compliance;
using Babel.Core.Audit;
using Babel.Core.Entitlement;
using Babel.Core.Metering;
using Babel.SharedKernel;
using Xunit;
using KernelTenantId = Babel.SharedKernel.TenantId;

namespace Babel.Compliance.Tests;

/// <summary>
/// <b>الوصلة الغائبة، مُثبَتة من طرفها إلى طرفها.</b>
/// <para>
/// كل اختبار هنا يبدأ من حقيقة «رُحّلت فاتورة» بالشكل الذي تستطيع وحدة المبيعات
/// إنتاجه — <see cref="TaxableDocumentPosted"/> في <c>Babel.Contracts</c> — ويمرّ
/// بنقطة الدخول العامة نفسها التي يركّبها الجذر التركيبي، ثم بالمُنسِّق نفسه،
/// ثم بالمزوّد الوهمي. <b>لا يُستدعى مسار ثانٍ في أي منها.</b>
/// </para>
/// <para>
/// ولا شيء هنا مُتحقَّق منه أمام الهيئة: المزوّد وهمي، والجهة وهمية، ولا بايتة واحدة
/// من هذا المسار عُرضت على بيئة اختبار حقيقية للهيئة.
/// </para>
/// </summary>
public sealed class IntakeTests
{
    private static readonly KernelTenantId Tenant = new(Guid.Parse("0198c0de-0000-7000-8000-00000000ac01"));
    private static readonly UserId Actor = new(Guid.Parse("0198c0de-0000-7000-8000-0000000000a1"));
    private const string Unit = "POS-01";

    /// <summary>
    /// تركيب كامل: المنفِّذ الحقيقي للاستحقاق، والمخزن، والمُنسِّق، والمزوّد الوهمي،
    /// ونقطة الدخول العامة — كلها موصولة كما يصلها الجذر التركيبي.
    /// </summary>
    private sealed class Composition : IDisposable
    {
        public Composition(EntitlementState state = EntitlementState.Entitled)
        {
            Harness = new Harness();
            Audit = new InMemoryAuditLog();
            Usage = new InMemoryUsageStore();
            Entitlements = new InMemoryEntitlementService(Audit, Harness.Clock);
            Entitlements.ApplyAsync(new EntitlementChangeRequest(
                Tenant,
                new Dictionary<BabelModule, EntitlementState> { [BabelModule.Compliance] = state },
                UserId.SystemActor,
                "تهيئة اختبار")).AsTask().GetAwaiter().GetResult();

            Service = new EInvoiceSubmissionService(
                new EntitlementEnforcer(Entitlements, Usage, Harness.Clock),
                Harness.Service,
                Harness.Store,
                new ZatcaFlowPolicy());
        }

        public Harness Harness { get; }
        public InMemoryAuditLog Audit { get; }
        public InMemoryUsageStore Usage { get; }
        public InMemoryEntitlementService Entitlements { get; }
        public EInvoiceSubmissionService Service { get; }

        public Task<IssuingUnitRegistration> OnboardAsync() => Harness.OnboardAsync(
            new IssuingUnitId(Unit), new Abstractions.TenantId(Tenant.Value.ToString("D")));

        public void Dispose() => Harness.Dispose();
    }

    private static Money Sar(decimal amount) => Money.Of(amount, CurrencyCode.Sar);

    /// <summary>
    /// فاتورة كما تنتجها وحدة المصدر بعد الترحيل. <b>القيد يُرحَّل أولاً دائماً</b>:
    /// يُؤخذ من الدفتر الوهمي، فلا يوجد في أي اختبار مستند بلا قيد سابق.
    /// </summary>
    private static TaxableDocumentPosted Posted(
        Composition c,
        string number,
        string sourceId,
        bool withBuyerVat,
        decimal net = 1000.0000m)
    {
        decimal tax = decimal.Round(net * 0.15m, 4, MidpointRounding.ToEven);
        decimal gross = net + tax;

        JournalEntryRef entry = c.Harness.Ledger.Post(
            new Abstractions.TenantId(Tenant.Value.ToString("D")), new IssuingUnitId(Unit),
            number, net, tax, gross, c.Harness.Clock.GetUtcNow());

        return new TaxableDocumentPosted
        {
            Tenant = Tenant,
            Origin = BabelModule.Sales,
            OccurredAt = c.Harness.Clock.GetUtcNow(),
            IssuingUnit = Unit,
            SourceDocumentType = "sales.invoice",
            SourceDocumentId = sourceId,
            Kind = TaxableDocumentKind.Invoice,
            DocumentNumber = number,
            IssuedAt = c.Harness.Clock.GetUtcNow(),
            Seller = new TaxableDocumentParty(
                new LocalizedName("سلاسل بابل للمقاولات", "Salasel Babel Contracting"),
                "300000000000003", "الرياض", "Riyadh"),
            Buyer = withBuyerVat
                ? new TaxableDocumentParty(
                    new LocalizedName("شركة العميل", "Client Co"), "310000000000003", "جدة", "Jeddah")
                : null,
            Lines =
            [
                new TaxableDocumentLine(1, "خدمات استشارية", "Consulting services",
                    Quantity: 1.0000m, UnitPrice: Sar(net), NetAmount: Sar(net),
                    TaxRate: 0.15m, TaxAmount: Sar(tax), GrossAmount: Sar(gross))
            ],
            NetTotal = Sar(net),
            TaxTotal = Sar(tax),
            GrossTotal = Sar(gross),
            JournalEntry = entry.Value
        };
    }

    // ───────────────────────────────────────────────────── من الطرف إلى الطرف

    [Fact]
    public async Task A_posted_invoice_with_a_buyer_vat_number_is_cleared_end_to_end()
    {
        using var c = new Composition();
        await c.OnboardAsync();

        Result<ElectronicDocumentOutcome> result =
            await c.Service.SubmitPostedDocumentAsync(Actor, Posted(c, "INV-1001", "sales-1001", withBuyerVat: true), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, string.Join(" · ", result.Errors.Select(e => e.ToString())));
        ElectronicDocumentOutcome outcome = result.Value;

        var id = new ComplianceDocumentId(outcome.ComplianceDocumentId);
        ComplianceRecord record = c.Harness.Record(id);

        // المسار اختارته السياسة، لا حقل في الحدث.
        Assert.Equal(ComplianceFlow.Clearance, record.Flow);
        Assert.Equal(ComplianceStatus.Accepted, record.Status);
        Assert.True(outcome.MayBeDelivered);
        Assert.NotEmpty(outcome.StatusAr);
        Assert.NotEmpty(outcome.StatusEn);

        // المستند مرّ بكل خطوة، ولم يقفز فوق تسجيل المحاولة.
        Assert.Equal(
            [ComplianceStatus.Built, ComplianceStatus.Queued, ComplianceStatus.Submitting, ComplianceStatus.Accepted],
            c.Harness.StatusPath(id));

        // خانة السلسلة حُجزت والعدّاد بدأ من واحد، والبايتات جُمِّدت.
        Assert.Equal(1, record.Counter);
        Assert.NotEmpty(record.FrozenPayload);
        Assert.NotEmpty(record.SubmissionFingerprint);
        Assert.Equal(new JournalEntryRef(record.JournalEntry.Value), record.JournalEntry);

        // وأهم شيء: القيد المُرحَّل لم يُمَسّ.
        c.Harness.Ledger.AssertUntouched();
    }

    [Fact]
    public async Task A_posted_invoice_without_a_buyer_vat_number_goes_to_reporting_and_is_deliverable_at_once()
    {
        using var c = new Composition();
        await c.OnboardAsync();

        Result<ElectronicDocumentOutcome> result =
            await c.Service.SubmitPostedDocumentAsync(Actor, Posted(c, "INV-2001", "sales-2001", withBuyerVat: false), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, string.Join(" · ", result.Errors.Select(e => e.ToString())));
        var id = new ComplianceDocumentId(result.Value.ComplianceDocumentId);

        // البيع اكتمل: المستند يُسلَّم الآن ولا ينتظر الجهة.
        Assert.True(result.Value.MayBeDelivered);
        Assert.Equal(ComplianceFlow.Reporting, c.Harness.Record(id).Flow);
        Assert.Equal(ComplianceStatus.Queued, c.Harness.Record(id).Status);

        // والإبلاغ يجري بعده في العامل الخلفي.
        int drained = await c.Harness.Service.DrainReportingQueueAsync(10, TestContext.Current.CancellationToken);
        Assert.Equal(1, drained);
        Assert.True(c.Harness.Record(id).IsAccepted);

        c.Harness.Ledger.AssertUntouched();
    }

    [Fact]
    public async Task The_same_posted_invoice_delivered_twice_produces_one_compliance_document()
    {
        using var c = new Composition();
        await c.OnboardAsync();

        TaxableDocumentPosted posted = Posted(c, "INV-3001", "sales-3001", withBuyerVat: true);

        Result<ElectronicDocumentOutcome> first = await c.Service.SubmitPostedDocumentAsync(Actor, posted, TestContext.Current.CancellationToken);
        Result<ElectronicDocumentOutcome> second = await c.Service.SubmitPostedDocumentAsync(Actor, posted, TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.ComplianceDocumentId, second.Value.ComplianceDocumentId);

        // لا عدّاد ثانٍ، ولا محاولة إرسال ثانية: مستند نظامي واحد لبيعة واحدة.
        var id = new ComplianceDocumentId(first.Value.ComplianceDocumentId);
        Assert.Equal(1, c.Harness.Record(id).Counter);
        Assert.Single(c.Harness.Store.PeekAttempts(id));
    }

    [Fact]
    public async Task The_derived_identity_is_a_function_of_the_source_document_not_of_the_clock()
    {
        ComplianceDocumentId a = PostedDocumentTranslator.DocumentIdOf(Tenant, "sales.invoice", "sales-9");
        ComplianceDocumentId b = PostedDocumentTranslator.DocumentIdOf(Tenant, "sales.invoice", "sales-9");
        ComplianceDocumentId other = PostedDocumentTranslator.DocumentIdOf(Tenant, "sales.invoice", "sales-10");

        Assert.Equal(a, b);
        Assert.NotEqual(a, other);
        await Task.CompletedTask;
    }

    // ───────────────────────────────────────────────────────────── الاستحقاق

    [Fact]
    public async Task A_lapsed_subscription_refuses_the_submission_and_says_so_in_both_languages()
    {
        using var c = new Composition(EntitlementState.ReadOnly);
        await c.OnboardAsync();

        Result<ElectronicDocumentOutcome> result =
            await c.Service.SubmitPostedDocumentAsync(Actor, Posted(c, "INV-4001", "sales-4001", withBuyerVat: true), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.All(result.Errors, e =>
        {
            Assert.NotEmpty(e.MessageAr);
            Assert.NotEmpty(e.MessageEn);
            Assert.NotEqual(e.MessageAr, e.MessageEn);
        });

        // والرفض ليس صمتاً: لا مستند بُني، ولا خانة سلسلة أُحرقت.
        ComplianceDocumentId id = PostedDocumentTranslator.DocumentIdOf(Tenant, "sales.invoice", "sales-4001");
        Assert.Null(c.Harness.Store.Peek(id));
    }

    [Fact]
    public async Task A_lapsed_subscription_still_reads_an_existing_submission()
    {
        using var c = new Composition();
        await c.OnboardAsync();
        await c.Service.SubmitPostedDocumentAsync(Actor, Posted(c, "INV-5001", "sales-5001", withBuyerVat: true), TestContext.Current.CancellationToken);

        await c.Entitlements.ApplyAsync(new EntitlementChangeRequest(
            Tenant,
            new Dictionary<BabelModule, EntitlementState> { [BabelModule.Compliance] = EntitlementState.ReadOnly },
            UserId.SystemActor,
            "انقضاء الاشتراك"), TestContext.Current.CancellationToken);

        Result<ComplianceView> read =
            await c.Service.ReadSubmissionAsync(Tenant, Actor, "sales.invoice", "sales-5001", TestContext.Current.CancellationToken);

        Assert.True(read.IsSuccess, string.Join(" · ", read.Errors.Select(e => e.ToString())));
        Assert.Equal(ComplianceStatus.Accepted, read.Value.Record.Status);
    }

    // ─────────────────────────────────────────────────── حرّاس بوّابة الاستقبال

    [Fact]
    public async Task A_document_with_no_journal_entry_is_refused_before_anything_is_written()
    {
        using var c = new Composition();
        await c.OnboardAsync();

        TaxableDocumentPosted unposted =
            Posted(c, "INV-6001", "sales-6001", withBuyerVat: true) with { JournalEntry = Guid.Empty };

        Result<ElectronicDocumentOutcome> result = await c.Service.SubmitPostedDocumentAsync(Actor, unposted, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, e => e.Code == "compliance.intake.not_posted");
        Assert.Null(c.Harness.Store.Peek(
            PostedDocumentTranslator.DocumentIdOf(Tenant, "sales.invoice", "sales-6001")));
    }

    /// <summary>
    /// <b>صفوف الجدول رموزُ أخطاء نصّية، لا مندوبات.</b> ‏<c>Func</c> في
    /// <c>TheoryData</c> غير قابل للتسلسل، فيكتشفه المستكشِف بنداً واحداً ويُنفَّذ تسعة —
    /// ومسحُ العزل يقارن المُكتشَف بالمُنفَّذ ويرفض الفرق، بحقّ: مسحٌ لم يُنفّذ ما اكتشفه
    /// لا يعني شيئاً. <b>مقيس: 1084 مُنفَّذاً مقابل 1076 مُكتشَفاً — والفرق ثمانية بالضبط.</b>
    /// </summary>
    public static TheoryData<string> MalformedCodes() =>
    [
        "compliance.intake.totals_inconsistent",
        "compliance.intake.currency_mismatch",
        "compliance.intake.lines_do_not_sum",
        "compliance.intake.no_lines",
        "compliance.intake.issuing_unit_missing",
        "compliance.intake.source_identity_missing",
        "compliance.intake.document_number_missing",
        "compliance.intake.correction_incomplete",
        "compliance.intake.correction_on_plain_invoice",
    ];

    /// <summary>يكسر الحقيقة بالطريقة التي يوجبها الرمز المنتظَر — والرمز هو مفتاح الحالة.</summary>
    private static TaxableDocumentPosted Break(string code, TaxableDocumentPosted p) => code switch
    {
        "compliance.intake.totals_inconsistent" =>
            p with { GrossTotal = Money.Of(p.GrossTotal.Amount + 1.0000m, CurrencyCode.Sar) },
        "compliance.intake.currency_mismatch" =>
            p with { TaxTotal = Money.Of(p.TaxTotal.Amount, new CurrencyCode("USD")) },
        "compliance.intake.lines_do_not_sum" =>
            p with { Lines = [p.Lines[0] with { NetAmount = Money.Of(1.0000m, CurrencyCode.Sar) }] },
        "compliance.intake.no_lines" => p with { Lines = [] },
        "compliance.intake.issuing_unit_missing" => p with { IssuingUnit = "  " },
        "compliance.intake.source_identity_missing" => p with { SourceDocumentId = "" },
        "compliance.intake.document_number_missing" => p with { DocumentNumber = "" },
        "compliance.intake.correction_incomplete" => p with { Kind = TaxableDocumentKind.CreditNote },
        "compliance.intake.correction_on_plain_invoice" =>
            p with { CorrectionReasonAr = "خطأ في السعر", CorrectionReasonEn = "price error" },
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "رمز غير مُغطّى في جدول الحالات")
    };

    [Theory]
    [MemberData(nameof(MalformedCodes))]
    public async Task A_malformed_fact_is_refused_by_code_and_nothing_is_written(string expectedCode)
    {
        using var c = new Composition();
        await c.OnboardAsync();

        TaxableDocumentPosted posted = Break(expectedCode, Posted(c, "INV-7001", "sales-7001", withBuyerVat: true));

        Result<ElectronicDocumentOutcome> result = await c.Service.SubmitPostedDocumentAsync(Actor, posted, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure, $"كان يُنتظر رفضٌ بالرمز {expectedCode}");
        Assert.Contains(result.Errors, e => e.Code == expectedCode);
        Assert.All(result.Errors, e => Assert.NotEqual(e.MessageAr, e.MessageEn));
        Assert.Null(c.Harness.Store.Peek(
            PostedDocumentTranslator.DocumentIdOf(Tenant, "sales.invoice", "sales-7001")));
    }

    [Fact]
    public async Task An_issuing_unit_that_never_onboarded_is_refused_not_thrown_at_the_caller()
    {
        using var c = new Composition();
        // لا تسجيل: الوحدة لم تمرّ بدورة التسجيل أصلاً.

        Result<ElectronicDocumentOutcome> result =
            await c.Service.SubmitPostedDocumentAsync(Actor, Posted(c, "INV-8001", "sales-8001", withBuyerVat: true), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, e => e.Code == "compliance.intake.issuing_unit_not_ready");
    }

    [Fact]
    public async Task A_credit_note_that_names_its_original_is_accepted_and_points_at_it()
    {
        using var c = new Composition();
        await c.OnboardAsync();

        await c.Service.SubmitPostedDocumentAsync(Actor, Posted(c, "INV-9001", "sales-9001", withBuyerVat: true), TestContext.Current.CancellationToken);

        TaxableDocumentPosted note = Posted(c, "CN-9001", "sales-cn-9001", withBuyerVat: true, net: 200.0000m) with
        {
            Kind = TaxableDocumentKind.CreditNote,
            OriginalSourceDocumentType = "sales.invoice",
            OriginalSourceDocumentId = "sales-9001",
            CorrectionReasonAr = "خصم متفق عليه بعد الإصدار",
            CorrectionReasonEn = "agreed discount after issue"
        };

        Result<ElectronicDocumentOutcome> result = await c.Service.SubmitPostedDocumentAsync(Actor, note, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, string.Join(" · ", result.Errors.Select(e => e.ToString())));

        var noteId = new ComplianceDocumentId(result.Value.ComplianceDocumentId);
        Assert.Equal(ComplianceDocumentKind.CreditNote, c.Harness.Record(noteId).Kind);

        // الإشعار في السلسلة نفسها بعد الفاتورة — لا سلسلة ثانية للتصحيحات.
        Assert.Equal(2, c.Harness.Record(noteId).Counter);
        c.Harness.Ledger.AssertUntouched();
    }
}
