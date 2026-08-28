using Babel.Contracts.Events;
using Babel.SharedKernel;

namespace Babel.Contracts.Compliance;

/// <summary>
/// نوع المستند الخاضع للفوترة الإلكترونية. <b>ليس نوع مستند الترحيل</b> — هوية الترحيل
/// شيء آخر تملكه وحدة المصدر (فخ-45 · فخ-46)؛ هذا وصف ما يُصدَر للمشتري وللجهة.
/// <para/>
/// The kind of document that goes to e-invoicing. This is not the posting source-document
/// type: posting identity belongs to the originating module.
/// </summary>
public enum TaxableDocumentKind
{
    /// <summary>فاتورة.</summary>
    Invoice,

    /// <summary>إشعار دائن.</summary>
    CreditNote,

    /// <summary>إشعار مدين.</summary>
    DebitNote
}

/// <summary>
/// طرف في مستند خاضع للضريبة. الاسم بالعربية والإنجليزية إلزاماً.
/// <para>A party on a taxable document; both names are mandatory.</para>
/// </summary>
/// <param name="Name">الاسم — <see cref="LocalizedName"/> يفرض الطرفين، وهما واقعتان مسجَّلتان يغطّيهما الختم.</param>
/// <param name="TaxRegistrationNumber">الرقم الضريبي إن وُجد. <b>غيابه ليس نقصاً</b> — هو ما يجعل المستند مبسّطاً.</param>
/// <param name="AddressAr">العنوان بالعربية.</param>
/// <param name="AddressEn">العنوان بالإنجليزية.</param>
public sealed record TaxableDocumentParty(
    LocalizedName Name,
    string? TaxRegistrationNumber = null,
    string? AddressAr = null,
    string? AddressEn = null);

/// <summary>
/// سطر في مستند خاضع للضريبة. <b>كل مبلغ <see cref="Money"/></b> — لا فاصلة عائمة
/// في أي موضع مالي (القاعدة 4). والكمية والنسبة <c>decimal</c> لأنهما ليستا مالاً.
/// <para>A line on a taxable document; every monetary field is <see cref="Money"/>.</para>
/// </summary>
/// <param name="LineNo">رقم السطر، يبدأ من ١ ولا يتكرّر داخل المستند.</param>
/// <param name="DescriptionAr">الوصف بالعربية.</param>
/// <param name="DescriptionEn">الوصف بالإنجليزية.</param>
/// <param name="Quantity">الكمية.</param>
/// <param name="UnitPrice">سعر الوحدة.</param>
/// <param name="NetAmount">الصافي قبل الضريبة.</param>
/// <param name="TaxRate">نسبة الضريبة ككسر عشري (0.15 لا 15).</param>
/// <param name="TaxAmount">مبلغ الضريبة.</param>
/// <param name="GrossAmount">الإجمالي شامل الضريبة.</param>
public sealed record TaxableDocumentLine(
    int LineNo,
    string DescriptionAr,
    string DescriptionEn,
    decimal Quantity,
    Money UnitPrice,
    Money NetAmount,
    decimal TaxRate,
    Money TaxAmount,
    Money GrossAmount);

/// <summary>
/// <b>الحقيقة التي تُطلق مسار الالتزام: مستند خاضع للضريبة رُحِّل بالفعل.</b>
/// <para>
/// ترتيب الأحداث في هذا المنتج ثابت ولا يُعكس: <b>يُرحَّل القيد أولاً</b>، ثم يُبلَّغ
/// الالتزام. ولذلك <see cref="JournalEntry"/> إلزامي وغير فارغ: حدّ الالتزام لا يبني
/// مستنداً لشيء لم يدخل الدفتر بعد. وهو <b>لا يكتب</b> في الدفتر ولا يقرأ منه رقماً
/// لغرض الإرسال — الإشارة إشارة فقط (القاعدة 1 · القاعدة 12).
/// </para>
/// <para>
/// The fact that starts the compliance path: a taxable document that has already been
/// posted. The ledger entry always exists first; compliance references it and never
/// writes to the ledger.
/// </para>
/// <para>
/// <b>ولا يحمل هذا الحدث مساراً.</b> اختيار المقاصة أو الإبلاغ قرار سياسة يقع في موضع
/// واحد داخل حدّ الالتزام؛ حمله هنا يُنشئ مصدرَي حقيقة للمسار، وهو بالضبط الخلط الذي
/// تمنعه بنية المسارين.
/// </para>
/// </summary>
public sealed record TaxableDocumentPosted : IBusinessEvent
{
    /// <inheritdoc />
    public required TenantId Tenant { get; init; }

    /// <summary>
    /// الوحدة التي أصدرت المستند. ليست ثابتة على المبيعات: المشتريات تُصدر إشعارات،
    /// ونقطة البيع تُصدر فواتير مبسّطة.
    /// </summary>
    public required BabelModule Origin { get; init; }

    /// <inheritdoc />
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// <b>وحدة الإصدار</b> — جهاز نقطة بيع، أو نقطة إصدار، أو فرع. الوحدة الذرّية في
    /// حدّ الالتزام: لها شهادتها وعدّادها وسلسلتها. لا يجوز أن تكون فارغة، ولا يجوز أن
    /// يتقاسمها جهازان.
    /// </summary>
    public required string IssuingUnit { get; init; }

    /// <summary>نوع مستند المصدر كما تسمّيه وحدته (مثلاً <c>sales.invoice</c>).</summary>
    public required string SourceDocumentType { get; init; }

    /// <summary>معرّف مستند المصدر داخل وحدته. مع النوع والمستأجر يُكوّنان مفتاح الحصانة.</summary>
    public required string SourceDocumentId { get; init; }

    /// <summary>نوع المستند الضريبي.</summary>
    public required TaxableDocumentKind Kind { get; init; }

    /// <summary>رقم المستند الظاهر للمشتري.</summary>
    public required string DocumentNumber { get; init; }

    /// <summary>لحظة الإصدار كما تظهر على المستند.</summary>
    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>البائع.</summary>
    public required TaxableDocumentParty Seller { get; init; }

    /// <summary>المشتري. <b>غيابه مسموح</b> ويعني مستنداً مبسّطاً.</summary>
    public TaxableDocumentParty? Buyer { get; init; }

    /// <summary>السطور. مستند بلا سطور مرفوض.</summary>
    public required IReadOnlyList<TaxableDocumentLine> Lines { get; init; }

    /// <summary>صافي المستند قبل الضريبة.</summary>
    public required Money NetTotal { get; init; }

    /// <summary>ضريبة المستند.</summary>
    public required Money TaxTotal { get; init; }

    /// <summary>إجمالي المستند شامل الضريبة. عملته هي عملة المستند كله.</summary>
    public required Money GrossTotal { get; init; }

    /// <summary>
    /// القيد المُرحَّل الذي نشأ عنه هذا المستند. <b>إشارة فقط.</b> فارغ = لم يُرحَّل،
    /// وهو رفض لا تحذير.
    /// </summary>
    public required Guid JournalEntry { get; init; }

    /// <summary>
    /// نوع مستند المصدر <b>الأصلي</b> الذي يصحّحه هذا الإشعار — إلزامي لإشعار دائن أو
    /// مدين، وممنوع لغيرهما. النوع والمعرّف معاً يحدّدان المستند الأصلي في حدّ الالتزام
    /// دون أن تعرف وحدة المصدر هويته هناك.
    /// </summary>
    public string? OriginalSourceDocumentType { get; init; }

    /// <summary>معرّف مستند المصدر الأصلي — إلزامي لإشعار دائن أو مدين، ممنوع لغيرهما.</summary>
    public string? OriginalSourceDocumentId { get; init; }

    /// <summary>سبب التصحيح بالعربية — إلزامي مع إشعار دائن أو مدين.</summary>
    public string? CorrectionReasonAr { get; init; }

    /// <summary>سبب التصحيح بالإنجليزية — إلزامي مع إشعار دائن أو مدين.</summary>
    public string? CorrectionReasonEn { get; init; }
}

/// <summary>
/// ما تعرفه وحدة المصدر بعد التسليم إلى الالتزام — <b>ولا شيء غيره</b>.
/// لا تعرف المزوّد، ولا الشهادة، ولا السلسلة، ولا رقم العدّاد كمفهوم تنظيمي.
/// <para/>
/// <b>‏<see cref="MayBeDelivered"/> هي الحقل الوحيد الذي يغيّر سلوك وحدة المصدر:</b>
/// مستند مسار المقاصة لا يُطبع ولا يُسلَّم للمشتري قبل أن يصير صحيحاً؛ ومستند مسار
/// الإبلاغ يصير صحيحاً فوراً لأنه صادر قانوناً ولا ينتظر الجهة.
/// </summary>
/// <param name="ComplianceDocumentId">هوية المستند داخل حدّ الالتزام.</param>
/// <param name="MayBeDelivered">هل يجوز تسليم المستند للمشتري الآن؟</param>
/// <param name="StatusAr">الحالة بالعربية، للعرض.</param>
/// <param name="StatusEn">الحالة بالإنجليزية، للعرض.</param>
/// <param name="GuidanceAr">ما يفعله المستخدم الآن، بالعربية.</param>
/// <param name="GuidanceEn">ما يفعله المستخدم الآن، بالإنجليزية.</param>
public sealed record ElectronicDocumentOutcome(
    Guid ComplianceDocumentId,
    bool MayBeDelivered,
    string StatusAr,
    string StatusEn,
    string GuidanceAr,
    string GuidanceEn);

/// <summary>
/// <b>المنفذ الذي تستدعيه وحدة المصدر بعد الترحيل — وهو الوصلة الوحيدة بين البيع والالتزام.</b>
/// <para>
/// عقد، لا تنفيذ: تنفيذه يعيش في <c>Babel.Compliance</c>، والجذر التركيبي وحده يربطهما.
/// وحدة المبيعات لا تشير إلى وحدة الالتزام ولا إلى عقد حدّها — الوحدات الأفقية تتخاطب
/// عبر <c>Babel.Contracts</c> وحدها (القاعدة 3).
/// </para>
/// <para>
/// The one seam between a posted sale and the compliance pipeline. A contract only: the
/// implementation lives in the compliance module and the composition root wires them.
/// </para>
/// </summary>
public interface IElectronicDocumentIntake
{
    /// <summary>
    /// يُسلّم حقيقة «رُحّل مستند خاضع للضريبة» إلى حدّ الالتزام، ويعود بما يجوز لوحدة
    /// المصدر أن تفعله الآن.
    /// <para/>
    /// <b>حصين بمفتاح <c>(المستأجر، نوع مستند المصدر، معرّف مستند المصدر)</c>:</b>
    /// نداءٌ ثانٍ بالمفتاح نفسه يعيد المستند القائم ولا يُنشئ ثانياً ولا يُرسل مرة أخرى.
    /// <para/>
    /// <b>والرفض ليس صمتاً أبداً:</b> كل فشل يعود بـ<see cref="Error"/> برمز ثابت
    /// ورسالتين — عربية وإنجليزية — على وحدة المصدر أن تعرضهما لا أن تبتلعهما.
    /// </summary>
    /// <param name="actor">المستخدم المسؤول — يُفحص استحقاقه قبل أي عمل.</param>
    /// <param name="document">الحقيقة المُرحَّلة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<ElectronicDocumentOutcome>> SubmitPostedDocumentAsync(
        UserId actor,
        TaxableDocumentPosted document,
        CancellationToken cancellationToken = default);
}
