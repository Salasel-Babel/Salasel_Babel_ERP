using System.Globalization;
using Babel.Ai;
using Babel.Ai.Capture;
using Babel.Ai.Extraction;
using Babel.Ai.Promotion;
using Babel.Ai.Suggestions;
using Babel.Compliance.Zatca.Qr;
using Babel.Contracts.Capture;
using Babel.Contracts.Parameters;
using Babel.Contracts.Storage;
using Babel.Core.Parameters;
using Babel.Purchasing.Application;
using Babel.SharedKernel;
using Babel.Storage;
using Xunit;

namespace Babel.Purchasing.Tests;

/// <summary>
/// <b>تغييرُ نسبةٍ لا يُعيد كتابة الماضي — والمستندُ المُرحَّل يتذكّر ما استُعمل.</b>
/// <para>
/// هذا هو <b>محور</b> خدمة المعامِلات، ولا يُثبَت بوصف. فاتورةُ مورد تُرحَّل بإصدارٍ
/// أوّل، ثمّ يُودَع إصدارٌ ثانٍ بنسبةٍ مختلفة يسري <b>قبل تاريخ الفاتورة نفسها</b>،
/// ثمّ تُقرأ الفاتورة الأولى فتُوجَد كما كانت وتُسمّي إصدارَها.
/// </para>
/// <para>
/// <b>ولا محاكاة لطرفٍ واحد:</b> الرمز مولَّد بمُرمِّز الهيئة القائم، والإصدارات في
/// قاعدة <c>core</c> حقيقية بمخطّطها المنشور، والفاتورة في قاعدة <c>purchasing</c>
/// حقيقية، والقيد في دفتر أستاذ حقيقي. و<b>القاعدتان منفصلتان فعلاً</b> — كما في
/// <c>deploy/compose.yml</c> — فلا مفتاح أجنبيّ بين الفاتورة وإصدارها، وذلك بعينه
/// سببُ وجود اللقطة.
/// </para>
/// <para>
/// <b>والشاهد السالب:</b> حين تُجعل القراءة تحلّ الإصدارَ السارِي اليوم بدل أن تقرأ
/// اللقطة المحفوظة، يسقط هذا الإثبات — ومُخرَجه الحرفي مُدوَّن في
/// <c>docs/evidence/measurements.md</c>.
/// </para>
/// </summary>
[Collection("payables")]
public sealed class TheBillRemembersItsParametersTests : IAsyncLifetime
{
    private const string SellerName = "شركة الأفق للخدمات اللوجستية";
    private const string SellerVat = "300000000000003";

    /// <summary>لحظة إصدار الفاتورة — وهي التاريخ الذي يُحلّ عليه المعامِل.</summary>
    private static readonly DateTimeOffset IssuedAt = new(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly IssuedOn = new(2026, 3, 10);

    /// <summary>سريان الإصدار الأوّل — قبل الفاتورة.</summary>
    private static readonly DateOnly FirstEffectiveFrom = new(2026, 1, 1);

    /// <summary>
    /// سريان الإصدار الثاني — <b>قبل الفاتورة أيضاً، وذلك هو بيت القصيد</b>: لو كان
    /// بعدها لَما أثبت شيئاً، إذ لا قارئ يحلّه لتاريخٍ يسبقه. وهو يسبقها، فلو حُلَّ
    /// اليوم لأعطى رقماً آخر.
    /// </summary>
    private static readonly DateOnly SecondEffectiveFrom = new(2026, 2, 1);

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
        => prefix + "-PRM-" + Interlocked.Increment(ref _sequence).ToString("D5", CultureInfo.InvariantCulture);

    // ═══════════════════════════════════════════════════════════════════════
    // ١ · فاتورةٌ رُحِّلت بإصدارٍ أوّل تبقى عليه بعد إيداع إصدارٍ ثانٍ
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task فاتورةٌ_رُحِّلت_بإصدارٍ_أوّل_تبقى_عليه_بعد_إيداع_إصدارٍ_ثانٍ_بنسبةٍ_مختلفة()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.ParameterHistoryTenant;

        // ── ١ · الإصدار الأوّل: نسبةٌ أودعها إنسان بمصدرها وتاريخ سريانها ──────
        Result<ParameterVersionView> first = await _harness.ParameterSettings.DepositAsync(
            tenant, Harness.Actor, Draft(FirstEffectiveFrom, 0.15m), token);
        Assert.True(first.IsSuccess, Describe(first.Errors));

        // ── ٢ · فاتورةٌ لا تحمل نسبتها مطبوعة، فتُملأ من الخدمة ────────────────
        string supplierCode = Next("SUP");
        Guid supplierId = await SupplierWithVatAsync(tenant, supplierCode, SellerVat, token);

        string qr = ZatcaQr.Phase1(SellerName, SellerVat, IssuedAt, grossTotal: 1_150.00m, taxTotal: 150.00m);
        Capture capture = Build(tenant);

        Result<CapturedInvoiceDraft> captured =
            await capture.Service.CaptureAsync(tenant, Harness.Actor, capture.Request(qr), token);
        Assert.True(captured.IsSuccess, Describe(captured.Errors));

        CapturedInvoiceDraft draft = captured.Value;

        // النسبة **لم تُقرأ من المستند**: مصدرها «من الإعدادات»، ومعها لقطتُها.
        Assert.Equal(FieldProvenance.Defaulted, draft.TaxRate.Provenance);
        Assert.NotNull(draft.Parameters);
        Assert.Equal(first.Value.Id, draft.Parameters!.VersionId);

        Result<PromotedDocumentReference> promoted = await capture.Service.PromoteAsync(
            tenant, Harness.Actor, draft.DraftId, ConfirmAll(draft), token);
        Assert.True(promoted.IsSuccess, Describe(promoted.Errors));

        Guid billId = Guid.Parse(promoted.Value.DocumentId);

        Result<PurchasingDocumentView> posted =
            await _harness.Bills.PostBillAsync(tenant, Harness.Actor, billId, token);
        Assert.True(posted.IsSuccess, Describe(posted.Errors));

        decimal taxWhenPosted = posted.Value.Totals.Tax.Amount;

        // ── ٣ · ثمّ يُغيّر صاحبُ المصلحة النسبة: إصدارٌ ثانٍ يسري **قبل** الفاتورة ──
        Result<ParameterVersionView> second = await _harness.ParameterSettings.DepositAsync(
            tenant, Harness.Actor, Draft(SecondEffectiveFrom, 0.20m), token);
        Assert.True(second.IsSuccess, Describe(second.Errors));

        // والعالَم تحرّك فعلاً: الحلّ لتاريخ الفاتورة نفسه يعطي الإصدار الثاني اليوم.
        Result<ParameterSnapshot> today = await _harness.Parameters.ResolveAsync(
            tenant, ParameterCatalogue.ValueAddedTax, IssuedOn, token);
        Assert.True(today.IsSuccess, Describe(today.Errors));
        Assert.Equal(second.Value.Id, today.Value.VersionId);
        Assert.Equal(0.20m, today.Value.Find(ParameterCatalogue.ValueAddedTaxStandardRate));

        // ── ٤ · والفاتورة الأولى كما كانت، وتسمّي إصدارها ──────────────────────
        Result<ParameterSnapshot?> remembered =
            await _harness.Bills.ReadBillParametersAsync(tenant, Harness.Actor, billId, token);
        Assert.True(remembered.IsSuccess, Describe(remembered.Errors));
        Assert.NotNull(remembered.Value);

        Result<PurchasingDocumentView> reread =
            await _harness.Bills.GetBillAsync(tenant, Harness.Actor, billId, token);
        Assert.True(reread.IsSuccess, Describe(reread.Errors));

        ParameterSnapshot kept = remembered.Value!;

        Proof.Require(
            kept.VersionId == first.Value.Id
            && kept.VersionId != second.Value.Id
            && kept.Find(ParameterCatalogue.ValueAddedTaxStandardRate) == 0.15m
            && kept.EffectiveFrom == FirstEffectiveFrom
            && reread.Value.Totals.Tax.Amount == taxWhenPosted
            && reread.Value.Totals.Gross.Amount == 1_150.0000m
            && today.Value.VersionId == second.Value.Id,
            "تغييرُ النسبة لا يُعيد كتابة الماضي: الفاتورة المُرحَّلة تبقى على إصدارها وتسمّيه، "
            + "بينما الحلُّ الجاري لتاريخها نفسه صار يعطي الإصدار الثاني",
            "الإصدار المحفوظ على الفاتورة=" + kept.VersionId.ToString("D", CultureInfo.InvariantCulture)[..8]
            + " · نسبتُه=" + Number(kept.Find(ParameterCatalogue.ValueAddedTaxStandardRate) ?? -1m)
            + " · سريانُه=" + kept.EffectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            + " · الإصدار السارِي اليوم لتاريخ الفاتورة="
            + today.Value.VersionId.ToString("D", CultureInfo.InvariantCulture)[..8]
            + " · نسبتُه=" + Number(today.Value.Find(ParameterCatalogue.ValueAddedTaxStandardRate) ?? -1m)
            + " · ضريبة الفاتورة عند الترحيل=" + Proof.Money(taxWhenPosted)
            + " · ضريبتها بعد الإيداع الثاني=" + Proof.Money(reread.Value.Totals.Tax.Amount)
            + " · إجماليها=" + Proof.Money(reread.Value.Totals.Gross.Amount));

        // ولا نداءَ إلى قاعدةٍ أخرى في القراءة: اللقطة على الفاتورة نفسها، وهي تحمل
        // القيمة لا معرّفاً وحده.
        Proof.Note("اللقطة المحفوظة بشكلها القانوني: " + kept.Canonical());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٢ · قائمةُ مراجعة المحاسب تسمّي المستند الذي استعمل الإصدار غير الموقَّع
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task قائمةُ_المراجعة_تسمّي_الفاتورة_المُرحَّلة_التي_استعملت_الإصدار_غير_الموقَّع()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.ParameterReviewTenant;

        Result<ParameterVersionView> version = await _harness.ParameterSettings.DepositAsync(
            tenant, Harness.Actor, Draft(FirstEffectiveFrom, 0.15m), token);
        Assert.True(version.IsSuccess, Describe(version.Errors));

        string supplierCode = Next("SUP");
        await SupplierWithVatAsync(tenant, supplierCode, SellerVat, token);

        string qr = ZatcaQr.Phase1(SellerName, SellerVat, IssuedAt, grossTotal: 1_150.00m, taxTotal: 150.00m);
        Capture capture = Build(tenant);

        Result<CapturedInvoiceDraft> captured =
            await capture.Service.CaptureAsync(tenant, Harness.Actor, capture.Request(qr), token);
        Assert.True(captured.IsSuccess, Describe(captured.Errors));

        Result<PromotedDocumentReference> promoted = await capture.Service.PromoteAsync(
            tenant, Harness.Actor, captured.Value.DraftId, ConfirmAll(captured.Value), token);
        Assert.True(promoted.IsSuccess, Describe(promoted.Errors));

        Guid billId = Guid.Parse(promoted.Value.DocumentId);

        // ‏**قبل الترحيل: لا استعمال.** الفاتورة تحمل لقطتها منذ إنشائها، لكن سجلّ
        // المراجعة يقول «استعمله مستندٌ **مُرحَّل**» — ومسوّدةٌ ليست مستنداً مُرحَّلاً.
        Result<IReadOnlyList<ParameterReviewView>> beforePosting =
            await _harness.ParameterSettings.ReviewAsync(tenant, Harness.Actor, token);
        Assert.True(beforePosting.IsSuccess, Describe(beforePosting.Errors));

        int usagesBefore = beforePosting.Value
            .Where(entry => entry.Version.Id == version.Value.Id)
            .Sum(entry => entry.Usages.Count);

        Result<PurchasingDocumentView> posted =
            await _harness.Bills.PostBillAsync(tenant, Harness.Actor, billId, token);
        Assert.True(posted.IsSuccess, Describe(posted.Errors));

        // وترحيلٌ ثانٍ للمستند نفسه: آمنُ التكرار، ولا صفَّ استعمالٍ ثانياً.
        Result<PurchasingDocumentView> again =
            await _harness.Bills.PostBillAsync(tenant, Harness.Actor, billId, token);
        Assert.True(again.IsSuccess, Describe(again.Errors));

        Result<IReadOnlyList<ParameterReviewView>> review =
            await _harness.ParameterSettings.ReviewAsync(tenant, Harness.Actor, token);
        Assert.True(review.IsSuccess, Describe(review.Errors));

        ParameterReviewView entryForVersion =
            Assert.Single(review.Value, entry => entry.Version.Id == version.Value.Id);

        Proof.Require(
            usagesBefore == 0
            && entryForVersion.Usages.Count == 1
            && entryForVersion.Usages[0].DocumentId == billId
            && entryForVersion.Usages[0].Module == BabelModule.Purchasing
            && entryForVersion.Version.Approval != ParameterApproval.AuditorSigned
            && review.Value.Any(static entry => entry.Version.Scope == ParameterScope.Platform),
            "قائمةُ مراجعة المحاسب — بابُ قراءةٍ واحد — تُخرج كلَّ إصدارٍ غير موقَّع ومعه "
            + "كلُّ مستندٍ مُرحَّلٍ استعمله، والترحيلُ المكرّر لا يضاعف صفّاً",
            "استعمالاتٌ قبل الترحيل=" + usagesBefore.ToString(CultureInfo.InvariantCulture)
            + " · بعد ترحيلين=" + entryForVersion.Usages.Count.ToString(CultureInfo.InvariantCulture)
            + " · المستند=" + entryForVersion.Usages[0].Module + "/" + entryForVersion.Usages[0].DocumentType
            + " " + billId.ToString("D", CultureInfo.InvariantCulture)[..8]
            + " · صفوفُ القائمة=" + review.Value.Count.ToString(CultureInfo.InvariantCulture)
            + " · حالةُ الإصدار=" + ParameterApprovalInfo.TokenOf(entryForVersion.Version.Approval));
    }

    // ═══════════════════════════════════════════════════════════════════════

    private static ParameterVersionDraft Draft(DateOnly effectiveFrom, decimal rate) => new(
        ParameterCatalogue.ValueAddedTax,
        effectiveFrom,
        ParameterApproval.TenantApproved,
        "مديرة المالية",
        effectiveFrom.AddDays(-5),
        "قرارُ الشركة رقم 7 — نسخته في المرفقات. وهو إيداعُ اختبارٍ لا إفادةٌ عن نظام.",
        new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            [ParameterCatalogue.ValueAddedTaxStandardRate] = rate,
        });

    private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Describe(IReadOnlyList<Error> errors)
        => string.Join(" · ", errors.Select(static error => error.Code + ": " + error.MessageAr));

    private static PromotionConfirmation ConfirmAll(CapturedInvoiceDraft draft)
        => new(new HashSet<string>(draft.FieldsNeedingHumanJudgement(), StringComparer.Ordinal));

    private async Task<Guid> SupplierWithVatAsync(
        TenantId tenant, string code, string vatNumber, CancellationToken token)
    {
        Result<SupplierView> created = await _harness.Suppliers.CreateAsync(
            tenant,
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
    /// خدمةُ التقاطٍ موصولةٌ بمستقبِل المشتريات الحقيقي، <b>ومزوّدُها لا يطبع نسبة</b>
    /// — وذلك هو الشرط الذي يجعل المعامِل يُستعمل أصلاً.
    /// </summary>
    private Capture Build(TenantId tenant)
    {
        InMemoryCapturedDraftStore store = new();
        InMemoryAttachmentStore attachments = new();

        ValueTask<Result<StoredAttachment>> put = attachments.PutAsync(new AttachmentSubmission
        {
            Tenant = tenant,
            Actor = Harness.Actor,
            Content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 },
            DeclaredFileName = "فاتورة بلا نسبة مطبوعة.jpg",
            DeclaredMediaType = "image/jpeg",
        });

        AttachmentId document = put.IsCompleted
            ? put.Result.Value.Id
            : throw new InvalidOperationException("المحوّل في الذاكرة لم يكتمل تزامنياً");

        DeterministicExtractionProvider provider = new DeterministicExtractionProvider()
            .Answering(document.ToString(), new ComposedExtraction
            {
                SellerName = SellerName,
                SellerVatNumber = SellerVat,
                InvoiceNumber = Next("INV"),
                IssuedOn = IssuedOn,
                Net = 1_000.00m,
                TaxTotal = 150.00m,
                GrossTotal = 1_150.00m,

                // ‏**ولا `TaxRate` هنا عمداً**: المستند لم يطبع نسبته، فتُملأ من الخدمة.
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
            attachments,
            _harness.Promotion,

            // ‏**المثيل نفسه الذي تكتب فيه الفاتورة استعمالَها** — لا مثيلٌ ثانٍ.
            _harness.Parameters,
            new AiOptions(),
            new FixedClock(IssuedAt));

        return new Capture(service, store, document, tenant);
    }

    /// <summary>تجهيزةُ التقاطٍ واحدة: الخدمة ومخزنها والمستند والمنشأة.</summary>
    private sealed record Capture(
        InvoiceCaptureService Service,
        InMemoryCapturedDraftStore Store,
        AttachmentId Document,
        TenantId Tenant)
    {
        /// <summary>طلب التقاط — إشارةٌ إلى مستند مُودَع، ولا بايتة فيه.</summary>
        public CaptureRequest Request(string? qr) => new()
        {
            Tenant = Tenant,
            Document = Document,
            Channel = CaptureChannel.Chat,
            QrPayload = qr,
        };
    }
}
