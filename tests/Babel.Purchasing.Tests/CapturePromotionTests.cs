using System.Globalization;
using Babel.Ai;
using Babel.Ai.Capture;
using Babel.Ai.Extraction;
using Babel.Ai.Promotion;
using Babel.Ai.Suggestions;
using Babel.Compliance.Zatca.Qr;
using Babel.Contracts.Capture;
using Babel.Purchasing.Application;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Purchasing.Tests;

/// <summary>ساعة ثابتة: المسوّدة تحمل لحظة التقاط حتمية، فتتساوى تشغيلتان.</summary>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>
/// <b>السلسلة موصولة من طرف إلى طرف: رمز مُصدَّق ← مورد مُطابَق ← فاتورة مُرحَّلة.</b>
/// <para>
/// وحدة <c>Babel.Ai</c> تقرأ رمز فاتورة المورد فتُخرج مسوّدة ما قبل الإدخال بمصدر لكل
/// حقل، ويؤكّدها إنسان، ثم تُسلّم أمر ترقية عبر منفذ في <c>Babel.Contracts</c>. وهذا
/// الملف يُثبت أن الطرف الأخير — <b>الذي لم يكن له تنفيذ في المستودع كله</b> — صار
/// موصولاً، وأن المستند يبلغ دفتر أستاذ <b>حقيقياً</b> بالمسار المعتاد لا بمسار ثانٍ.
/// </para>
/// <para>
/// ولا محاكاة لأي طرف: الرمز مولَّد بمُرمِّز الهيئة القائم، والفكّ بفاكّه، والدفتر
/// منشورٌ بهجراته وبياناته المرجعية، والقيد يُقرأ من الجداول بعد الترحيل.
/// </para>
/// </summary>
[Collection("payables")]
public sealed class CapturePromotionTests : IAsyncLifetime
{
    private const string SellerName = "شركة الأفق للخدمات اللوجستية";
    private const string SellerVat = "300000000000003";
    private const string OtherVat = "301111111111113";
    private static readonly DateTimeOffset IssuedAt = new(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly IssuedOn = new(2026, 3, 10);

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
        => prefix + "-CAP-" + Interlocked.Increment(ref _sequence).ToString("D5", CultureInfo.InvariantCulture);

    private static string Ledger => PurchasingTestEnvironment.Ledger.AppConnectionString;

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · السلسلة كاملةً: رمز الهيئة ← مسوّدة ← مورد ← فاتورة ← قيد في الدفتر
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task An_attested_qr_becomes_a_posted_supplier_bill_through_the_normal_service()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;

        string supplierCode = Next("SUP");
        Guid supplierId = await SupplierWithVatAsync(supplierCode, SellerVat, token);

        // ── الرمز مولَّد بمُرمِّز الهيئة القائم، لا بنصّ مكتوب بيد ──────────────
        string qr = ZatcaQr.Phase1(SellerName, SellerVat, IssuedAt, grossTotal: 1_150.00m, taxTotal: 150.00m);

        Capture capture = Build();
        string number = Next("INV");

        Result<CapturedInvoiceDraft> captured = await capture.Service.CaptureAsync(
            tenant, Harness.Actor, capture.Request(qr), token);
        Assert.True(captured.IsSuccess, Describe(captured.Errors));

        CapturedInvoiceDraft draft = captured.Value;

        // الحقول الخمسة المُصدَّقة جاءت من الرمز لا من النموذج.
        Proof.Note("مصدر البائع=" + draft.SellerName.Provenance
            + " · الرقم الضريبي=" + draft.SellerVatNumber.Provenance
            + " · التاريخ=" + draft.IssuedOn.Provenance
            + " · الضريبة=" + draft.TaxTotal.Provenance
            + " · الإجمالي=" + draft.GrossTotal.Provenance);

        decimal before = await LedgerProbe.ControlNetAsync(Ledger, tenant, "supplier", token);

        Result<PromotedDocumentReference> promoted = await capture.Service.PromoteAsync(
            tenant, Harness.Actor, draft.DraftId, ConfirmAll(draft), token);
        Assert.True(promoted.IsSuccess, Describe(promoted.Errors));

        Guid billId = Guid.Parse(promoted.Value.DocumentId);

        // ── الترحيل خطوة الوحدة المعتادة نفسها، لا خطوة تخصّ الملتقَط ──────────
        Result<PurchasingDocumentView> posted = await _harness.Bills
            .PostBillAsync(tenant, Harness.Actor, billId, token);
        Assert.True(posted.IsSuccess, Describe(posted.Errors));

        decimal after = await LedgerProbe.ControlNetAsync(Ledger, tenant, "supplier", token);
        PostedLine[] lines = await LinesOfAsync(billId, token);
        CapturedInvoiceDraft settled = (await capture.Store.FindAsync(tenant, draft.DraftId, token))!;

        Proof.Note("سطور القيد: " + string.Join(" · ", lines.Select(static line =>
            line.Account + " " + line.Side + " " + Proof.Money(line.Amount) + " مركز=" + line.CostCenter)));

        Proof.Require(
            draft.SellerVatNumber.Provenance == FieldProvenance.Attested
            && draft.GrossTotal.Provenance == FieldProvenance.Attested
            && promoted.Value.Module == BabelModule.Purchasing
            && promoted.Value.DocumentType == "SupplierBill"
            && posted.Value.Totals.Gross.Amount == 1_150.0000m
            && posted.Value.EntryId is not null
            && before - after == 1_150.0000m
            && settled.State == DraftState.Promoted
            && lines.Length == 3,
            "رمز فاتورة مُصدَّق يصير فاتورة مورد مُرحَّلة بخدمة الوحدة المعتادة، وذمة المورد تتحرّك بالإجمالي المُصدَّق",
            "المورد=" + supplierCode + " (" + supplierId.ToString("D", CultureInfo.InvariantCulture)[..8] + ")"
            + " · المستند=" + promoted.Value.Module + "/" + promoted.Value.DocumentType
            + " · إجمالي الفاتورة=" + Proof.Money(posted.Value.Totals.Gross.Amount)
            + " · الإجمالي في الرمز=1150.0000"
            + " · حركة نقطة ضبط الموردين (دائن)=" + Proof.Money(before - after)
            + " · حالة المسوّدة=" + settled.State
            + " · سطور القيد=" + lines.Length.ToString(CultureInfo.InvariantCulture)
            + " · القيد=" + posted.Value.EntryId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · الغموض يُرفض ولا يُخمَّن — ولا يُكتب صفّ واحد
    // ═══════════════════════════════════════════════════════════════════════
    //
    // ‏**هذا هو الغرض كلّه.** رقمٌ مُصدَّق يحمله موردان فعّالان: اختيار «الأول» يُنتج
    // إسناداً يحمل مظهر التحقّق بلا التحقّق — أسوأ من غياب الإسناد أصلاً.
    [Fact]
    public async Task An_ambiguous_vat_number_is_refused_by_name_and_writes_nothing()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;

        string first = Next("AMB");
        string second = Next("AMB");
        await SupplierWithVatAsync(first, OtherVat, token);
        await SupplierWithVatAsync(second, OtherVat, token);

        Attempt attempt = await PromoteAsync(OtherVat, SellerName, token);

        Proof.Require(
            attempt.Result.IsFailure
            && attempt.Result.Errors[0].Code == "purchasing.supplier.vat_number_ambiguous"
            && attempt.Result.Errors[0].MessageAr.Contains(first, StringComparison.Ordinal)
            && attempt.Result.Errors[0].MessageAr.Contains(second, StringComparison.Ordinal)
            && attempt.BillsWritten == 0
            && attempt.DraftState != DraftState.Promoted,
            "رقمٌ مُصدَّق على موردين فعّالين يُرفض بالغموض ويُسمّي المرشّحين، ولا يُكتب مستند",
            "الرمز=" + Code(attempt.Result)
            + " · المرشّحان في الرسالة=" + (attempt.Result.Errors[0].MessageAr.Contains(first, StringComparison.Ordinal)
                && attempt.Result.Errors[0].MessageAr.Contains(second, StringComparison.Ordinal) ? "نعم" : "لا")
            + " · فواتير كُتبت=" + attempt.BillsWritten.ToString(CultureInfo.InvariantCulture)
            + " · حالة المسوّدة=" + attempt.DraftState);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 · رقم لا مورد له، ورقم على موقوفين وحدهم — رسالتان مختلفتان عمداً
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task An_unknown_number_and_a_number_on_deactivated_suppliers_refuse_differently()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;

        // (أ) لا مورد بهذا الرقم أصلاً.
        Attempt unknown = await PromoteAsync("399999999999993", SellerName, token);

        // (ب) الرقم موجود على مورد **موقوف** وحده.
        string code = Next("OFF");
        Guid inactive = await SupplierWithVatAsync(code, "302222222222223", token);
        await DeactivateAsync(tenant, inactive, token);

        Attempt onlyInactive = await PromoteAsync("302222222222223", SellerName, token);

        Proof.Require(
            unknown.Result.IsFailure
            && unknown.Result.Errors[0].Code == "purchasing.supplier.vat_number_not_found"
            && onlyInactive.Result.IsFailure
            && onlyInactive.Result.Errors[0].Code == "purchasing.supplier.vat_number_only_inactive"
            && onlyInactive.Result.Errors[0].MessageAr.Contains(code, StringComparison.Ordinal)
            && unknown.BillsWritten == 0
            && onlyInactive.BillsWritten == 0,
            "«لا مورد» و«موقوفون وحدهم» رسالتان مختلفتان: الثانية تمنع إنشاء مورد ثالث بالرقم نفسه",
            "غير موجود=" + Code(unknown.Result)
            + " · موقوفون=" + Code(onlyInactive.Result)
            + " ويسمّي «" + code + "»"
            + " · فواتير كُتبت=" + (unknown.BillsWritten + onlyInactive.BillsWritten).ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4 · المطابقة الآلية امتياز المُصدَّق وحده
    // ═══════════════════════════════════════════════════════════════════════
    //
    // مسوّدة بلا رمز: كل حقولها **مقروءة ضوئياً**. ورقمٌ بثقة 0.94 يُطابق مورداً بعينه
    // يُنتج الإسناد الخاطئ نفسه الذي يحرسه البند 2 — لكن بلا أن يُسمّى مرشّحان.
    [Fact]
    public async Task A_vat_number_that_was_only_read_never_matches_a_supplier()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;

        await SupplierWithVatAsync(Next("RED"), "303333333333333", token);

        Capture capture = Build();

        // بلا حمولة رمز: الرقم مقروء لا مُصدَّق.
        Result<CapturedInvoiceDraft> captured = await capture.Service.CaptureAsync(
            tenant, Harness.Actor, capture.Request(null, "303333333333333"), token);
        Assert.True(captured.IsSuccess, Describe(captured.Errors));

        Result<PromotedDocumentReference> promoted = await capture.Service.PromoteAsync(
            tenant, Harness.Actor, captured.Value.DraftId, ConfirmAll(captured.Value), token);

        Proof.Require(
            captured.Value.SellerVatNumber.Provenance == FieldProvenance.Read
            && promoted.IsFailure
            && promoted.Errors[0].Code == "purchasing.promotion.vat_number_not_attested",
            "رقمٌ مقروء ضوئياً لا يُطابق مورداً مهما بلغت ثقته — المطابقة الآلية امتياز المُصدَّق",
            "مصدر الرقم=" + captured.Value.SellerVatNumber.Provenance
            + " · الثقة=" + captured.Value.SellerVatNumber.Confidence?.ToString("0.00", CultureInfo.InvariantCulture)
            + " · النتيجة=" + Code(promoted));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5 · تصنيف المصروف: بيد إنسان، أو المؤهّل العام — ولا يقترحه نموذج
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task The_expense_category_comes_from_a_human_or_falls_back_to_the_wildcard()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;

        await SupplierWithVatAsync(Next("CAT"), "304444444444443", token);

        // (أ) لم يكتب الإنسان تصنيفاً ⇒ المؤهّل العام، وحسابُ مصروفٍ غير مصنَّف.
        Attempt bare = await PromoteAsync("304444444444443", SellerName, token);
        Assert.True(bare.Result.IsSuccess, Describe(bare.Result.Errors));
        string bareAccount = await ExpenseAccountAsync(tenant, Guid.Parse(bare.Result.Value.DocumentId), token);

        // (ب) كتبه إنسان ⇒ يُستعمل كما كُتب، وحسابٌ بعينه.
        Attempt typed = await PromoteAsync("304444444444443", SellerName, token, category: "rent");
        Assert.True(typed.Result.IsSuccess, Describe(typed.Result.Errors));
        string typedAccount = await ExpenseAccountAsync(tenant, Guid.Parse(typed.Result.Value.DocumentId), token);

        // (ج) تصنيفٌ مصدره غير الإنسان ⇒ رفض. والأمر يُبنى هنا مباشرةً لأن وحدة الالتقاط
        //     **لا تملك مساراً** يضع تصنيفاً بمصدر آخر — وهذا بالضبط ما يجب أن يبقى صحيحاً.
        Result<PromotedDocumentReference> inferred = await _harness.Promotion.ReceiveAsync(
            OrderWith("304444444444443", Next("INF"), category: "rent", source: FieldProvenance.Inferred), token);

        Proof.Require(
            bareAccount == "5901"
            && typedAccount == "5510"
            && inferred.IsFailure
            && inferred.Errors[0].Code == "purchasing.promotion.expense_category_not_typed",
            "التصنيف الغائب يُرحَّل بالمؤهّل العام على حساب مصروف غير مصنَّف، والمكتوب بيد إنسان يُرحَّل على حسابه، والمقترَح يُرفض",
            "بلا تصنيف ⇒ حساب " + bareAccount
            + " · «rent» بيد إنسان ⇒ حساب " + typedAccount
            + " · «rent» بمصدر Inferred ⇒ " + Code(inferred));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 6 · مركز التكلفة: تكوين المستأجر لا تخمين النموذج
    // ═══════════════════════════════════════════════════════════════════════
    //
    // المركز ليس على الفاتورة، ولا يُخترع، ولا يُطلب من الإنسان أن يكتبه على شاشةٍ
    // لا تحمل الفاتورة جوابه. والأمر لا يحمل حقلاً له أصلاً، فيحلّه ICostCenterResolver
    // إلى المركز الافتراضي للمنشأة (‏ADR-0026) — وهو موجود دائماً بحكم التأسيس.
    [Fact]
    public async Task The_cost_centre_is_the_tenant_default_and_the_order_carries_no_field_for_it()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;

        await SupplierWithVatAsync(Next("CC"), "305555555555553", token);
        Attempt attempt = await PromoteAsync("305555555555553", SellerName, token);
        Assert.True(attempt.Result.IsSuccess, Describe(attempt.Result.Errors));

        Guid billId = Guid.Parse(attempt.Result.Value.DocumentId);
        Result<PurchasingDocumentView> posted = await _harness.Bills
            .PostBillAsync(tenant, Harness.Actor, billId, token);
        Assert.True(posted.IsSuccess, Describe(posted.Errors));

        PostedLine[] lines = await LinesOfAsync(billId, token);
        string[] centres = [.. lines.Where(static line => line.CostCenter.Length > 0)
            .Select(static line => line.CostCenter).Distinct(StringComparer.Ordinal)];

        bool orderHasNoCostCentreField = typeof(PromotionOrder)
            .GetProperties()
            .All(static property => !property.Name.Contains("Cost", StringComparison.OrdinalIgnoreCase));

        Proof.Require(
            orderHasNoCostCentreField
            && centres.Length == 1
            && centres[0] == "cc.001",
            "أمر الترقية لا يحمل حقل مركز تكلفة أصلاً، والمركز في القيد هو افتراضي المنشأة",
            "حقل مركز في الأمر=" + (orderHasNoCostCentreField ? "لا يوجد" : "موجود!")
            + " · مراكز القيد=" + string.Join(" · ", centres));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 7 · بوابة القبول موصولة في المشتريات، وغياب الملفّ رفضٌ لا فتح
    // ═══════════════════════════════════════════════════════════════════════
    //
    // مستأجران، شيفرة واحدة، مصفوفة واحدة — وصفّا ملفّ مختلفان. والمستند الملتقَط
    // (الحدث الأساسي) يمرّ عند الاثنين، والمخزني (حدث قدرة) يُرفض عند من أطفأها.
    [Fact]
    public async Task The_admission_gate_separates_the_base_event_from_the_capability_gated_one()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;

        await SupplierWithVatAsync(Next("ADM"), "306666666666663", token);

        // (أ) القدرة مُطفأة: الاستلام مرفوض — ومعه الفاتورة المخزنية كلها.
        await _harness.SaveProfileAsync(tenant, threeWayMatch: false, landedCost: true, token);

        Result<PurchasingDocumentView> receiptRefused = await _harness.Receipts
            .PostAsync(tenant, Harness.Actor, Guid.CreateVersion7(), token);

        // والمستند الملتقَط يمرّ: حدثه هو الحدث **الأساسي**، ولا قدرة تفتحه.
        Attempt expenseWhileOff = await PromoteAsync("306666666666663", SellerName, token);

        // (ب) القدرة مُشغَّلة: المسار المخزني يُفتح من جديد.
        await _harness.SaveProfileAsync(tenant, threeWayMatch: true, landedCost: true, token);

        Result<PurchasingDocumentView> receiptAllowed = await _harness.Receipts
            .PostAsync(tenant, Harness.Actor, Guid.CreateVersion7(), token);

        // (ج) لا ملفّ أصلاً: رفضٌ لا فتح.
        using Harness bare = await Harness.CreateWithoutProfilesAsync(token);
        Result<PurchasingDocumentView> noProfile = await bare.Receipts
            .PostAsync(tenant, Harness.Actor, Guid.CreateVersion7(), token);

        // وأعيدت التجهيزة إلى حالها كي لا يرث اختبارٌ تالٍ ملفّاً ناقصاً.
        await _harness.SaveProfileAsync(tenant, threeWayMatch: true, landedCost: true, token);

        Proof.Require(
            receiptRefused.IsFailure
            && receiptRefused.Errors[0].Code == "document_admission.capability_not_enabled"
            && receiptRefused.Errors[0].MessageAr.Contains("receipt", StringComparison.Ordinal)
            && expenseWhileOff.Result.IsSuccess
            && receiptAllowed.IsFailure
            && receiptAllowed.Errors[0].Code == "purchasing.document_not_found"
            && noProfile.IsFailure
            && noProfile.Errors[0].Code == "purchasing.capability_profile_missing",
            "القدرة المُطفأة تُغلق مسارها وحده، والحدث الأساسي يمرّ، وغياب الملفّ رفضٌ لا فتح",
            "القدرة مُطفأة ⇒ " + Code(receiptRefused)
            + " · الملتقَط (الحدث الأساسي) ⇒ " + (expenseWhileOff.Result.IsSuccess ? "مرّ" : Code(expenseWhileOff.Result))
            + " · القدرة مُشغَّلة ⇒ " + Code(receiptAllowed) + " (تجاوز القبول وسقط عند وجود المستند)"
            + " · بلا ملفّ ⇒ " + Code(noProfile));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 8 · لا يُكتب رقم محسوب فوق رقم مُصدَّق
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task A_computed_total_is_never_written_over_an_attested_one()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        await SupplierWithVatAsync(Next("MIS"), "307777777777773", token);

        // أمرٌ سطوره لا تُنتج إجماليه: الوحدة تحسب 100 ضريبةً على 1000 والأمر يقول 150.
        PromotionOrder mismatched = OrderWith("307777777777773", Next("MIS"), taxTotal: 150.00m, taxRate: 0.10m);

        Result<PromotedDocumentReference> refused = await _harness.Promotion.ReceiveAsync(mismatched, token);

        Proof.Require(
            refused.IsFailure && refused.Errors[0].Code == "purchasing.promotion.totals_disagree_with_attested",
            "اختلاف بين ما تحسبه الوحدة وما يحمله الأمر يُرفض ولا يُكتب المحسوب مكان المُصدَّق",
            "النتيجة=" + Code(refused) + " · الرسالة=" + (refused.IsFailure ? refused.Errors[0].MessageAr : "(نجح!)"));
    }

    // ── أدوات ───────────────────────────────────────────────────────────────

    private sealed record Attempt(Result<PromotedDocumentReference> Result, long BillsWritten, DraftState DraftState);

    private sealed record PostedLine(string Account, string Side, decimal Amount, string CostCenter);

    private sealed record Capture(InvoiceCaptureService Service, ICapturedDraftStore Store, TenantId Tenant)
    {
        public ExtractionRequest Request(string? qr, string? vatNumber = null) => new()
        {
            Tenant = Tenant,
            DocumentId = "CAP-DOC",
            Channel = CaptureChannel.Chat,
            MediaType = "image/jpeg",
            Content = new byte[] { 0xFF, 0xD8, 0xFF },
            QrPayload = qr,
        };
    }

    /// <summary>
    /// خدمة التقاط كاملة موصولة بمستقبِل <b>المشتريات الحقيقي</b> — لا مُحاكاة تسجّل
    /// ما وصلها. وهذا هو الفرق بين «الأمر يُبنى صحيحاً» و«السلسلة موصولة».
    /// </summary>
    private Capture Build(string vatNumber = SellerVat)
    {
        InMemoryCapturedDraftStore store = new();

        DeterministicExtractionProvider provider = new DeterministicExtractionProvider()
            .Answering("CAP-DOC", new ComposedExtraction
            {
                SellerName = SellerName,
                SellerVatNumber = vatNumber,
                InvoiceNumber = Next("INV"),
                IssuedOn = IssuedOn,
                Net = 1_000.00m,
                TaxTotal = 150.00m,
                GrossTotal = 1_150.00m,
                TaxRate = 0.15m,
                Lines = [new ComposedLine("خدمات نقل — مارس", 1m, 1_000.00m, 1_000.00m)],
                SuggestedEventCode = "purchasing.invoice.expense.posted",
                SuggestedRoleCode = "ap_supplier_control",
                Rationale = "مورد خدمات بلا أمر شراء ولا استلام مخزني",
            });

        InvoiceCaptureService service = new(
            new AlwaysEntitled(),
            provider,
            new ZatcaQrAttestationReader(),
            MatrixPostingVocabulary.Default,
            store,
            _harness.Promotion,
            new AiOptions(),
            new FixedClock(IssuedAt));

        return new Capture(service, store, PurchasingTestEnvironment.Tenant);
    }

    private static PromotionConfirmation ConfirmAll(CapturedInvoiceDraft draft, string category = "")
        => new(new HashSet<string>(draft.FieldsNeedingHumanJudgement(), StringComparer.Ordinal))
        {
            ExpenseCategory = category,
        };

    /// <summary>يلتقط ويُرقّي بالمسار الكامل، ويعدّ ما كُتب من فواتير قبل الترقية وبعدها.</summary>
    private async Task<Attempt> PromoteAsync(
        string vatNumber, string sellerName, CancellationToken token, string category = "")
    {
        TenantId tenant = PurchasingTestEnvironment.Tenant;
        Capture capture = Build(vatNumber);

        string qr = ZatcaQr.Phase1(sellerName, vatNumber, IssuedAt, 1_150.00m, 150.00m);

        Result<CapturedInvoiceDraft> captured = await capture.Service.CaptureAsync(
            tenant, Harness.Actor, capture.Request(qr), token);
        Assert.True(captured.IsSuccess, Describe(captured.Errors));

        long before = await BillCountAsync(tenant, token);

        Result<PromotedDocumentReference> promoted = await capture.Service.PromoteAsync(
            tenant, Harness.Actor, captured.Value.DraftId, ConfirmAll(captured.Value, category), token);

        long after = await BillCountAsync(tenant, token);
        CapturedInvoiceDraft settled = (await capture.Store.FindAsync(tenant, captured.Value.DraftId, token))!;

        return new Attempt(promoted, after - before, settled.State);
    }

    /// <summary>أمر ترقية يُبنى مباشرةً — لبلوغ حالات لا تملك وحدة الالتقاط مساراً إليها.</summary>
    private static PromotionOrder OrderWith(
        string vatNumber,
        string number,
        string category = "",
        FieldProvenance source = FieldProvenance.Typed,
        decimal taxTotal = 150.00m,
        decimal taxRate = 0.15m)
    {
        Dictionary<string, FieldProvenance> provenance = new(StringComparer.Ordinal)
        {
            [PromotionFields.SellerVatNumber] = FieldProvenance.Attested,
            [PromotionFields.GrossTotal] = FieldProvenance.Attested,
        };

        if (category.Length > 0)
        {
            provenance[PromotionFields.ExpenseCategory] = source;
        }

        return new PromotionOrder
        {
            Tenant = PurchasingTestEnvironment.Tenant,
            DraftId = Guid.CreateVersion7(),
            PromotedBy = Harness.Actor,
            SupplierName = SellerName,
            SupplierVatNumber = vatNumber,
            InvoiceNumber = number,
            IssuedOn = IssuedOn,
            Currency = CurrencyCode.Sar,
            Net = 1_000.00m,
            TaxRate = taxRate,
            TaxTotal = taxTotal,
            GrossTotal = 1_000.00m + taxTotal,
            EventCode = "purchasing.invoice.expense.posted",
            ExpenseCategory = category,
            Lines = [new PromotionLine(1, "خدمات نقل — مارس", 1m, 1_000.00m, 1_000.00m)],
            Provenance = provenance,
        };
    }

    private async Task<Guid> SupplierWithVatAsync(string code, string vatNumber, CancellationToken token)
    {
        Result<SupplierView> created = await _harness.Suppliers.CreateAsync(
            PurchasingTestEnvironment.Tenant,
            Harness.Actor,
            new SupplierDraft(
                code,
                new LocalizedName("مورد " + code, "Supplier " + code),
                Harness.Sar(0m),
                30,
                vatNumber),
            token);

        Assert.True(created.IsSuccess, Describe(created.Errors));
        return created.Value.Id;
    }

    /// <summary>
    /// يوقف مورداً. <b>بـSQL مباشر</b> لأن الوحدة لا تُعلن عملية إيقاف بعد — والمطلوب
    /// إثباته هنا سلوك <b>المطابقة</b> أمام صفٍّ موقوف، لا وجود شاشة إيقاف.
    /// </summary>
    private static async Task DeactivateAsync(TenantId tenant, Guid supplierId, CancellationToken token)
    {
        await using NpgsqlConnection connection = new(PurchasingTestEnvironment.Purchasing.ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        await using NpgsqlCommand command = new(
            """update purchasing.supplier set "IsActive" = false where "TenantId" = $1 and "Id" = $2""", connection);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(supplierId);
        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    private static async Task<long> BillCountAsync(TenantId tenant, CancellationToken token)
    {
        await using NpgsqlConnection connection = new(PurchasingTestEnvironment.Purchasing.ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        await using NpgsqlCommand command = new(
            """select count(*) from purchasing.supplier_bill where "TenantId" = $1""", connection);
        command.Parameters.AddWithValue(tenant.Value);
        return (long)(await command.ExecuteScalarAsync(token).ConfigureAwait(false))!;
    }

    private static async Task<PostedLine[]> LinesOfAsync(Guid billId, CancellationToken token)
    {
        List<PostedLine> lines = [];

        await using NpgsqlConnection connection = new(Ledger);
        await connection.OpenAsync(token).ConfigureAwait(false);
        await using NpgsqlCommand command = new(
            """
            select l.account_code,
                   case when l.debit_company > 0 then 'مدين' else 'دائن' end,
                   greatest(l.debit_company, l.credit_company),
                   coalesce(l.cost_center_id, '')
              from ledger.journal_line l
              join ledger.journal_entry e on e.entry_id = l.entry_id
             where e.source_doc_type = 'SupplierBill' and e.source_doc_id = $1
             order by l.account_code
            """, connection);
        command.Parameters.AddWithValue(billId.ToString("D", CultureInfo.InvariantCulture));

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            lines.Add(new PostedLine(reader.GetString(0), reader.GetString(1), reader.GetDecimal(2), reader.GetString(3)));
        }

        return [.. lines];
    }

    /// <summary>حساب سطر المصروف في قيد الفاتورة — الحساب الذي اختاره المؤهّل.</summary>
    private async Task<string> ExpenseAccountAsync(TenantId tenant, Guid billId, CancellationToken token)
    {
        Result<PurchasingDocumentView> posted = await _harness.Bills
            .PostBillAsync(tenant, Harness.Actor, billId, token);
        Assert.True(posted.IsSuccess, Describe(posted.Errors));

        PostedLine[] lines = await LinesOfAsync(billId, token);

        return lines.First(static line => line.Side == "مدين" && line.Amount >= 1_000m).Account;
    }

    private static string Code<T>(Result<T> result)
        => result.IsSuccess ? "(نجح)" : string.Join(" | ", result.Errors.Select(static error => error.Code));

    private static string Describe(IReadOnlyList<Error> errors)
        => string.Join(" | ", errors.Select(static error => error.ToString()));
}
