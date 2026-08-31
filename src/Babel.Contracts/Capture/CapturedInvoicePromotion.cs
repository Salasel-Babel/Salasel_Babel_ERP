using Babel.SharedKernel;

namespace Babel.Contracts.Capture;

/// <summary>
/// <b>مفاتيح حقول أمر الترقية</b> — معرّفات لا نصوص عرض (‏ADR-0021).
/// <para>
/// وهي في العقد لا في وحدة الالتقاط لأن <b>طرفَي الوصلة يستعملانها</b>: الملتقِط يكتب
/// بها خريطة المصادر، والمستقبِل يقرأ بها مصدر الحقل قبل أن يقرّر. ومفتاحٌ منسوخ في
/// الطرفين ينحرف نصفه عند أول إعادة تسمية، فيصير «لا مصدر لهذا الحقل» جواباً على حقل
/// له مصدر — وهو أخطر من غياب الخريطة أصلاً.
/// </para>
/// </summary>
public static class PromotionFields
{
    /// <summary>اسم البائع.</summary>
    public const string SellerName = "seller_name";

    /// <summary>الرقم الضريبي للبائع.</summary>
    public const string SellerVatNumber = "seller_vat_number";

    /// <summary>رقم الفاتورة لدى المورد.</summary>
    public const string InvoiceNumber = "invoice_number";

    /// <summary>تاريخ الإصدار.</summary>
    public const string IssuedOn = "issued_on";

    /// <summary>العملة.</summary>
    public const string Currency = "currency";

    /// <summary>الصافي قبل الضريبة.</summary>
    public const string Net = "net";

    /// <summary>نسبة الضريبة.</summary>
    public const string TaxRate = "tax_rate";

    /// <summary>مبلغ الضريبة.</summary>
    public const string TaxTotal = "tax_total";

    /// <summary>الإجمالي شامل الضريبة.</summary>
    public const string GrossTotal = "gross_total";

    /// <summary>الحدث المقترح.</summary>
    public const string SuggestedEvent = "suggested_event";

    /// <summary>
    /// تصنيف المصروف — <b>مؤهّل دور</b>، لا يقرؤه ماسح ولا يقترحه نموذج.
    /// انظر <see cref="PromotionOrder.ExpenseCategory"/>.
    /// </summary>
    public const string ExpenseCategory = "expense_category";
}

/// <summary>سطر في أمر الترقية. أرقام لا أدوار ولا جوانب ولا حسابات.</summary>
/// <param name="LineNo">الترتيب.</param>
/// <param name="Description">البيان كما ورد.</param>
/// <param name="Quantity">الكمية.</param>
/// <param name="UnitPrice">سعر الوحدة.</param>
/// <param name="LineNet">صافي السطر.</param>
public sealed record PromotionLine(int LineNo, string Description, decimal Quantity, decimal UnitPrice, decimal LineNet);

/// <summary>
/// <b>أمر ترقية</b>: ما تُسلّمه وحدة الالتقاط للوحدة المالكة للمستند.
/// <para>
/// <b>لاحظ ما ليس هنا:</b> لا سطر ترحيل، ولا دور مع جانب، ولا مفتاح حصانة، ولا معرّف
/// قيد. أمر الترقية يصف <b>ما قرأناه</b>، والوحدة المالكة هي التي تنشئ مستندها بخدماتها
/// المعتادة وتذهب منه إلى محرك الترحيل — وهو <b>المسار الوحيد</b> إلى الدفتر.
/// </para>
/// <para>
/// ويحمل الأمر <see cref="Provenance"/>: مصدر كل حقل. الوحدة المستقبِلة تعرف أي رقم
/// مُصدَّق وأيّه مقروء، فتقرّر على أساس معلوم لا على أساس «وصلني رقم».
/// </para>
/// <para>
/// <b>ولا مركز تكلفة هنا:</b> المركز ليس على الفاتورة، ولا يُخترع. والمستقبِل لا يسمّي
/// مركزاً فيحلّه <c>ICostCenterResolver</c> إلى المركز الافتراضي للمنشأة — وهو موجود
/// دائماً بحكم ‏ADR-0026، وهو نفسه المركز الذي تحصل عليه كل فاتورة مصروف لم يُذكر عليها
/// مركز. حقلٌ هنا كان سيصنع <b>جواباً ثانياً</b> عن سؤال «أيّ مركز؟» خارج الموضع الوحيد
/// الذي يجيب عنه.
/// </para>
/// </summary>
public sealed record PromotionOrder
{
    /// <summary>المستأجر.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>معرّف المسوّدة التي نشأ عنها الأمر — أثرٌ يُرجَع إليه في التدقيق.</summary>
    public required Guid DraftId { get; init; }

    /// <summary>الفاعل الذي رقّى. إنسان دائماً — لا فاعل نظام.</summary>
    public required UserId PromotedBy { get; init; }

    /// <summary>اسم المورد كما ورد.</summary>
    public required string SupplierName { get; init; }

    /// <summary>الرقم الضريبي للمورد.</summary>
    public required string SupplierVatNumber { get; init; }

    /// <summary>رقم الفاتورة لدى المورد.</summary>
    public required string InvoiceNumber { get; init; }

    /// <summary>تاريخ الإصدار الميلادي.</summary>
    public required DateOnly IssuedOn { get; init; }

    /// <summary>العملة.</summary>
    public required CurrencyCode Currency { get; init; }

    /// <summary>الصافي قبل الضريبة.</summary>
    public required decimal Net { get; init; }

    /// <summary>نسبة الضريبة كسراً عشرياً.</summary>
    public required decimal TaxRate { get; init; }

    /// <summary>مبلغ الضريبة.</summary>
    public required decimal TaxTotal { get; init; }

    /// <summary>الإجمالي شامل الضريبة.</summary>
    public required decimal GrossTotal { get; init; }

    /// <summary>رمز الحدث في مصفوفة الترحيل — معرّف من مفردات مغلقة، لا رمز حساب.</summary>
    public required string EventCode { get; init; }

    /// <summary>رمز الدور المقترح، أو فارغ.</summary>
    public string RoleCode { get; init; } = string.Empty;

    /// <summary>
    /// <b>تصنيف المصروف — مؤهّل الدور، أو فارغ.</b>
    /// <para>
    /// وهو <b>ليس على الفاتورة</b>: لا يقرؤه ماسح ولا يحمله رمز مُصدَّق. ولا يقترحه
    /// نموذج أيضاً — مفردات النموذج المغلقة رموزُ <b>أحداث وأدوار</b> فحسب، وليس فيها
    /// مؤهّلات، فمؤهّلٌ «مقترَح» سلسلةٌ حرّة لا تُقابَل بشيء وتصل إلى خريطة الأدوار بلا فحص.
    /// </para>
    /// <para>
    /// فمصدره الوحيد <b>إنسان يكتبه عند التأكيد</b> — ومصدره في <see cref="Provenance"/>
    /// يجب أن يكون <see cref="FieldProvenance.Typed"/>؛ والمستقبِل يرفض ما عداه. وحين
    /// يُترك فارغاً يُرحَّل بمؤهّل المصفوفة العام <c>*</c>: حسابُ مصروفٍ <b>غير مصنَّف
    /// وظاهر أنه كذلك</b>، لا حسابٌ بعينه يبدو مؤكَّداً وهو مخمَّن.
    /// </para>
    /// </summary>
    public string ExpenseCategory { get; init; } = string.Empty;

    /// <summary>السطور.</summary>
    public required IReadOnlyList<PromotionLine> Lines { get; init; }

    /// <summary>مصدر كل حقل، بمفاتيح <see cref="PromotionFields"/>.</summary>
    public required IReadOnlyDictionary<string, FieldProvenance> Provenance { get; init; }

    /// <summary>مصدر حقل بمفتاحه، أو غيابه إن لم تذكره الخريطة.</summary>
    /// <param name="field">مفتاح الحقل من <see cref="PromotionFields"/>.</param>
    public FieldProvenance? ProvenanceOf(string field)
        => Provenance.TryGetValue(field ?? string.Empty, out FieldProvenance found) ? found : null;
}

/// <summary>
/// إشارة إلى المستند الحقيقي بعد أن أنشأته وحدته. المسوّدة لا تحمل معرّف مستند قبل هذا.
/// </summary>
/// <param name="Module">الوحدة المالكة.</param>
/// <param name="DocumentType">نوع المستند داخلها.</param>
/// <param name="DocumentId">معرّفه داخلها.</param>
public sealed record PromotedDocumentReference(BabelModule Module, string DocumentType, string DocumentId);

/// <summary>
/// <b>منفذ الترقية.</b> الوحدة المالكة للمستند تنفّذه، والجذر التركيبي يوصله.
/// <para>
/// وجوده واجهةً هو ما يجعل «الترقية تمرّ بخدمات الوحدة المعتادة» <b>قابلاً للإنفاذ</b>:
/// وحدة الالتقاط لا تملك سبيلاً آخر — لا سياق قاعدة بيانات، ولا مرجعاً لوحدة أفقية،
/// ولا معرفةً بمحرك الترحيل أصلاً.
/// </para>
/// <para>
/// <b>ولماذا يسكن العقود:</b> الطرفان وحدتان أفقيتان، ولا تعرف إحداهما الأخرى (القاعدة 3).
/// فلو بقي المنفذ في <c>Babel.Ai</c> لوجب على <c>Babel.Purchasing</c> أن تعتمد عليها كي
/// تنفّذه — وهو ما يمنعه البناء. والمنفذ في العقود يجعل التنفيذ ممكناً <b>بلا أن تكتسب
/// أيّ وحدة معرفةً بجارتها</b>.
/// </para>
/// </summary>
public interface ICapturedInvoiceReceiver
{
    /// <summary>يستقبل أمر ترقية وينشئ المستند بخدمات وحدته.</summary>
    /// <param name="order">الأمر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<PromotedDocumentReference>> ReceiveAsync(PromotionOrder order, CancellationToken cancellationToken = default);
}
