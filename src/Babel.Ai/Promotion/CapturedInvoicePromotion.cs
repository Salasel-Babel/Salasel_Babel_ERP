using Babel.Ai.Capture;
using Babel.SharedKernel;

namespace Babel.Ai.Promotion;

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

    /// <summary>السطور.</summary>
    public required IReadOnlyList<PromotionLine> Lines { get; init; }

    /// <summary>مصدر كل حقل، بمفتاح الحقل نفسه المستعمل في المسوّدة.</summary>
    public required IReadOnlyDictionary<string, FieldProvenance> Provenance { get; init; }
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
/// </summary>
public interface ICapturedInvoiceReceiver
{
    /// <summary>يستقبل أمر ترقية وينشئ المستند بخدمات وحدته.</summary>
    /// <param name="order">الأمر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<PromotedDocumentReference>> ReceiveAsync(PromotionOrder order, CancellationToken cancellationToken = default);
}

/// <summary>
/// تأكيد بشري قبل الترقية: <b>من أكّد، وأي الحقول أكّد</b>.
/// <para>
/// وليس علماً منطقياً واحداً: الحقل المُصدَّق يُلمَح، والحقل المقروء أو المُستنتَج
/// <b>يُؤكَّد كلٌّ منه على حدة</b> — وهذا هو ما يمنع شاشةً تُدرِّب الإنسان على زرّ واحد.
/// </para>
/// </summary>
/// <param name="ConfirmedFields">مفاتيح الحقول المؤكَّدة.</param>
public sealed record PromotionConfirmation(IReadOnlySet<string> ConfirmedFields);
