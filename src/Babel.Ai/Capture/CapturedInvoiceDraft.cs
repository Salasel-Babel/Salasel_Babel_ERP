using Babel.Ai.Reconciliation;
using Babel.Ai.Suggestions;
using Babel.Contracts.Capture;
using Babel.Contracts.Parameters;
using Babel.Contracts.Storage;
using Babel.SharedKernel;

namespace Babel.Ai.Capture;

/// <summary>من أين وصل المستند.</summary>
public enum CaptureChannel
{
    /// <summary>صورة أو مسح ضوئي.</summary>
    Image = 1,

    /// <summary>ملف PDF.</summary>
    Pdf = 2,

    /// <summary>صورة وصلت من محادثة (واتساب مثلاً).</summary>
    Chat = 3,

    /// <summary>رفع يدوي من الشاشة.</summary>
    Upload = 4,
}

/// <summary>
/// حالة المسوّدة. <b>ولا توجد حالة «مُرحَّلة»</b>: الترحيل ليس حالة من حالات هذه المسوّدة،
/// بل حدث يقع في وحدة أخرى بعد أن تنتهي هذه المسوّدة إلى <see cref="Promoted"/>.
/// </summary>
public enum DraftState
{
    /// <summary>التُقطت ولم تُطابَق حسابياً بعد.</summary>
    Captured = 1,

    /// <summary>طوبقت حسابياً وبقيت ملاحظات مفتوحة — لا تُرقّى.</summary>
    Disputed = 2,

    /// <summary>طوبقت حسابياً بلا ملاحظات — قابلة للترقية بعد تأكيد بشري.</summary>
    Reconciled = 3,

    /// <summary>رفضها إنسان.</summary>
    Rejected = 4,

    /// <summary>رقّاها إنسان، فأنشأت وحدةُ المستند مستنداً حقيقياً.</summary>
    Promoted = 5,
}

/// <summary>سطر ملتقَط. كل قيمة فيه تحمل مصدرها.</summary>
public sealed record CapturedInvoiceLine
{
    /// <summary>ترتيب السطر كما قُرئ.</summary>
    public required int LineNo { get; init; }

    /// <summary>بيان السطر.</summary>
    public required CapturedField<string> Description { get; init; }

    /// <summary>الكمية.</summary>
    public required CapturedField<decimal> Quantity { get; init; }

    /// <summary>سعر الوحدة.</summary>
    public required CapturedField<decimal> UnitPrice { get; init; }

    /// <summary>صافي السطر كما قُرئ — لا كما حُسب. المقارنة بينهما هي نصف المطابقة.</summary>
    public required CapturedField<decimal> LineNet { get; init; }
}

/// <summary>
/// <b>مسوّدة ما قبل الإدخال. ليست مستنداً محاسبياً.</b>
/// <para>
/// المستند الملتقَط <b>ليس</b> فاتورة مشتريات ولا قيداً ولا حركة دفتر مساعد. وهو مسوّدة
/// يُرقّيها إنسان، ويجب أن يكون <b>عاجزاً بنيوياً</b> عن بلوغ دفتر الأستاذ أو أي دفتر
/// مساعد قبل ذلك.
/// </para>
/// <para>
/// <b>والعجز هنا بنيوي لا عُرفي:</b> وحدة <c>Babel.Ai</c> كلها لا تعرف <c>Babel.Contracts.Posting</c>
/// — لا <c>IPostingService</c> ولا <c>PostingRequest</c> ولا <c>PostingLine</c> — فلا يوجد في
/// هذه الوحدة ما يمكن أن يُبنى منه طلب ترحيل أصلاً. القاعدة مفروضة باختبار معماري في
/// <c>tests/Babel.Ai.Tests</c> على غرار القاعدة 12، لا بمراجعة ولا بتعليق.
/// </para>
/// <para>
/// والترقية تمرّ <b>بخدمات الوحدة المالكة للمستند</b> عبر <see cref="ICapturedInvoiceReceiver"/>،
/// لا بكتابة صفوف مباشرة: مسوّدة تُرقّي نفسها بالكتابة تُعيد إنتاج صنف العطل الذي أنفق
/// هذا المستودع شهراً في إزالته — <b>مسار ثانٍ يجيب عن سؤال أُصلح المسار الأول ليجيب عنه</b>.
/// </para>
/// </summary>
public sealed record CapturedInvoiceDraft
{
    /// <summary>مفتاح حقل اسم البائع في مجموعة التأكيدات.</summary>
    public const string SellerNameField = PromotionFields.SellerName;

    /// <summary>مفتاح حقل الرقم الضريبي للبائع.</summary>
    public const string SellerVatNumberField = PromotionFields.SellerVatNumber;

    /// <summary>مفتاح حقل رقم الفاتورة.</summary>
    public const string InvoiceNumberField = PromotionFields.InvoiceNumber;

    /// <summary>مفتاح حقل تاريخ الإصدار.</summary>
    public const string IssuedOnField = PromotionFields.IssuedOn;

    /// <summary>مفتاح حقل العملة.</summary>
    public const string CurrencyField = PromotionFields.Currency;

    /// <summary>مفتاح حقل الصافي قبل الضريبة.</summary>
    public const string NetField = PromotionFields.Net;

    /// <summary>مفتاح حقل نسبة الضريبة.</summary>
    public const string TaxRateField = PromotionFields.TaxRate;

    /// <summary>مفتاح حقل مبلغ الضريبة.</summary>
    public const string TaxTotalField = PromotionFields.TaxTotal;

    /// <summary>مفتاح حقل الإجمالي شامل الضريبة.</summary>
    public const string GrossTotalField = PromotionFields.GrossTotal;

    /// <summary>مفتاح حقل الحدث المقترح.</summary>
    public const string SuggestedEventField = PromotionFields.SuggestedEvent;

    /// <summary>معرّف المسوّدة. معرّف مسوّدة لا معرّف مستند.</summary>
    public required Guid DraftId { get; init; }

    /// <summary>المستأجر.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>القناة التي وصل منها المستند.</summary>
    public required CaptureChannel Channel { get; init; }

    /// <summary>لحظة الالتقاط.</summary>
    public required DateTimeOffset CapturedAt { get; init; }

    /// <summary>معرّف المزوّد الذي استخرج السطور — يُسجَّل كي يُعرف من قرأ ماذا.</summary>
    public required string ExtractionProviderId { get; init; }

    /// <summary>
    /// <b>المستند المصدر — إشارةٌ إلى المرفق المُودَع، لا بايتاته.</b>
    /// <para>
    /// المسوّدة تحمل ما قُرئ من الصورة، والصورة نفسها سندُ إثبات يعيش في المخزن
    /// ويُقرأ عبر <see cref="IAttachmentStore"/>. وحملُ البايتات هنا كان سيعني
    /// نسخةً ثانية منها في كل مسوّدة، ونسخةً ثالثة في كل سجلّ طلب.
    /// </para>
    /// </summary>
    public required AttachmentId SourceDocument { get; init; }

    /// <summary>
    /// بصمة بايتات المستند المصدر كما سجّلها المخزن.
    /// <para>
    /// <b>ولماذا تُنسخ هنا:</b> كي يبقى الربط بين ما قرأه النموذج وما في المخزن
    /// <b>قابلاً للتحقّق من المسوّدة وحدها</b>. مسوّدةٌ تحمل معرّفاً فقط تصير بلا
    /// معنى لو بُدِّلت البايتات تحت المعرّف؛ ومعها البصمة يُكتشف ذلك بمقارنة.
    /// </para>
    /// </summary>
    public required string SourceDocumentHash { get; init; }

    /// <summary>اسم البائع.</summary>
    public required CapturedField<string> SellerName { get; init; }

    /// <summary>الرقم الضريبي للبائع.</summary>
    public required CapturedField<string> SellerVatNumber { get; init; }

    /// <summary>رقم الفاتورة لدى المورد.</summary>
    public required CapturedField<string> InvoiceNumber { get; init; }

    /// <summary>تاريخ الإصدار.</summary>
    public required CapturedField<DateOnly> IssuedOn { get; init; }

    /// <summary>العملة.</summary>
    public required CapturedField<CurrencyCode> Currency { get; init; }

    /// <summary>الصافي قبل الضريبة.</summary>
    public required CapturedField<decimal> Net { get; init; }

    /// <summary>نسبة الضريبة كسراً عشرياً.</summary>
    public required CapturedField<decimal> TaxRate { get; init; }

    /// <summary>مبلغ الضريبة.</summary>
    public required CapturedField<decimal> TaxTotal { get; init; }

    /// <summary>الإجمالي شامل الضريبة.</summary>
    public required CapturedField<decimal> GrossTotal { get; init; }

    /// <summary>السطور.</summary>
    public required IReadOnlyList<CapturedInvoiceLine> Lines { get; init; }

    /// <summary>حالة المسوّدة.</summary>
    public required DraftState State { get; init; }

    /// <summary>ملاحظات المطابقة الحسابية. غير الفارغة تمنع الترقية.</summary>
    public IReadOnlyList<ReconciliationFinding> Findings { get; init; } = [];

    /// <summary>الحدث المقترح — دور أو رمز حدث من مفردات مغلقة، ولا رمز حساب أبداً.</summary>
    public PostingSuggestion? Suggestion { get; init; }

    /// <summary>
    /// <b>لقطةُ المعامِلات التي مُلئ منها حقلٌ في هذه المسوّدة</b> — أو غيابُها إن لم
    /// يُملأ منها شيء.
    /// <para>
    /// وهي <b>غائبةٌ عمداً</b> حين تكون النسبة مطبوعةً على المستند: لم يُستعمل معامِل،
    /// فادّعاءُ استعماله في السجلّ كذبٌ صغير يُقرأ حقيقةً بعد سنتين. وحين تُملأ النسبة
    /// من الخدمة تحمل اللقطة <b>معرّف الإصدار والقيم المستعمَلة معاً</b>، فتعبر مع أمر
    /// الترقية إلى المستند الحقيقي ولا تبقى في مسوّدة.
    /// </para>
    /// </summary>
    public ParameterSnapshot? Parameters { get; init; }

    /// <summary>
    /// الحقول التي <b>لا تكفي فيها اللمحة</b>: كل حقل واجبه مراجعة أو قرار.
    /// وهذه بالضبط هي المجموعة التي يشترط <c>InvoiceCaptureService</c> تأكيدها قبل الترقية.
    /// </summary>
    public IReadOnlyList<string> FieldsNeedingHumanJudgement()
    {
        List<string> keys = [];
        Add(keys, SellerNameField, SellerName.Duty);
        Add(keys, SellerVatNumberField, SellerVatNumber.Duty);
        Add(keys, InvoiceNumberField, InvoiceNumber.Duty);
        Add(keys, IssuedOnField, IssuedOn.Duty);
        Add(keys, CurrencyField, Currency.Duty);
        Add(keys, NetField, Net.Duty);
        Add(keys, TaxRateField, TaxRate.Duty);
        Add(keys, TaxTotalField, TaxTotal.Duty);
        Add(keys, GrossTotalField, GrossTotal.Duty);

        if (Suggestion is not null)
        {
            keys.Add(SuggestedEventField);
        }

        return keys;
    }

    private static void Add(List<string> keys, string key, ProvenanceDuty duty)
    {
        if (duty is ProvenanceDuty.Review or ProvenanceDuty.Decide)
        {
            keys.Add(key);
        }
    }
}
