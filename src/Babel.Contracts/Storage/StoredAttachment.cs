using Babel.SharedKernel;

namespace Babel.Contracts.Storage;

/// <summary>
/// ما يُقدَّم للإيداع. <b>حقلان منه لا يُصدَّقان إطلاقاً</b> — الاسم والنوع المُعلَنان —
/// وهما مُسمّيان بذلك في اسميهما كي لا يقرأهما أحد على أنهما حقيقة.
/// </summary>
public sealed record AttachmentSubmission
{
    /// <summary>المستأجر. جزء من المفتاح في كل مسار، لا مرشّح.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>الفاعل — إنسان يودِع، ويُسجَّل على الصفّ.</summary>
    public required UserId Actor { get; init; }

    /// <summary>البايتات. تُقرأ مرّة، وتُشمّ، وتُجزَّأ، وتُكتب — ولا تُرمَّز نصّاً في أي خطوة.</summary>
    public required ReadOnlyMemory<byte> Content { get; init; }

    /// <summary>
    /// اسم الملفّ كما أرسله العميل — <b>بيانات لا مسار</b>. يُطهَّر قبل الحفظ، ولا
    /// يُشارك في بناء المسار على القرص بحال.
    /// </summary>
    public string? DeclaredFileName { get; init; }

    /// <summary>
    /// نوع المحتوى كما أعلنه العميل — <b>يُقارَن بالمشموم ولا يحلّ محلّه</b>. إعلانٌ
    /// يخالف البايتات رفضٌ باسمه، لا تصحيحٌ صامت.
    /// </summary>
    public string? DeclaredMediaType { get; init; }

    /// <summary>
    /// نوع المستند المصدر الذي يستند إليه هذا المرفق — <c>sales.invoice</c> مثلاً —
    /// أو <c>null</c> لمرفقٍ لا مستند له.
    /// <para>
    /// <b>وهو رمزٌ لا نصٌّ معروض</b>: يُرشَّح به الجرد، ولا يُترجَم ولا يُعرض. ويُقرن
    /// دائماً بـ<see cref="SourceDocumentId"/> — أحدهما بلا الآخر يُرفض، لأن «مرفقات
    /// فواتير المبيعات كلّها» ليست سؤالاً يُجاب داخل مستأجر بلا معرّف.
    /// </para>
    /// </summary>
    public string? SourceDocumentType { get; init; }

    /// <summary>معرّف المستند المصدر، أو <c>null</c>. يُقرن بنوعه ولا يُرسل وحده.</summary>
    public Guid? SourceDocumentId { get; init; }

    /// <summary>
    /// المرفق الذي يصحّحه هذا الإيداع، أو <see cref="AttachmentId.None"/>.
    /// <b>التصحيح إصدار جديد يشير إلى سلفه</b>، ولا يكتب فوقه.
    /// </summary>
    public AttachmentId Supersedes { get; init; }
}

/// <summary>
/// وصف مرفق مُودَع — <b>البايتات في المخزن، وهذا ما في القاعدة</b>.
/// <para>
/// و<see cref="ContentHash"/> هو ما يجعل المسار وحده غير كافٍ للادّعاء: ملفٌّ بُدِّل
/// تحت المسار نفسه <b>يُكتشف</b>، كما يُكتشف صفّ دفتر عُبث به. مسارٌ بلا بصمة لا يثبت
/// شيئاً — يثبت فقط أن شيئاً ما كان هناك.
/// </para>
/// </summary>
public sealed record StoredAttachment
{
    /// <summary>المعرّف الغامض.</summary>
    public required AttachmentId Id { get; init; }

    /// <summary>المستأجر المالك.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>النوع <b>المشموم من البايتات</b>، لا المُعلَن.</summary>
    public required AttachmentMediaType MediaType { get; init; }

    /// <summary>عدد البايتات كما كُتبت.</summary>
    public required long ByteLength { get; init; }

    /// <summary>‏SHA-256 للبايتات، ستّ‌عشرياً صغيراً، أربعة وستون محرفاً.</summary>
    public required string ContentHash { get; init; }

    /// <summary>
    /// مفتاح الكائن داخل المخزن — <b>مسار نسبي غامض يولّده المخزن</b>، لا اسم العميل
    /// ولا معرّف المرفق. يعيش في القاعدة وحدها.
    /// </summary>
    public required string ObjectKey { get; init; }

    /// <summary>اسم العرض بعد التطهير. للعرض وحده، ولا يدخل أي مسار.</summary>
    public required string FileName { get; init; }

    /// <summary>لحظة الإيداع.</summary>
    public required DateTimeOffset StoredAt { get; init; }

    /// <summary>من أودع.</summary>
    public required UserId StoredBy { get; init; }

    /// <summary>رقم الإصدار — يبدأ بواحد، ويزيد مع كل تصحيح.</summary>
    public required int Version { get; init; }

    /// <summary>سلفُ هذا الإصدار، أو <see cref="AttachmentId.None"/> للإصدار الأول.</summary>
    public AttachmentId Supersedes { get; init; }

    /// <summary>خلفُ هذا الإصدار إن صُحِّح، أو <see cref="AttachmentId.None"/>.</summary>
    public AttachmentId SupersededBy { get; init; }

    /// <summary>نوع المستند المصدر كما أُودع، أو <c>null</c>.</summary>
    public string? SourceDocumentType { get; init; }

    /// <summary>معرّف المستند المصدر كما أُودع، أو <c>null</c>.</summary>
    public Guid? SourceDocumentId { get; init; }

    /// <summary>علامة السحب إن سُحب، أو <c>null</c>. والبايتات باقية في الحالتين.</summary>
    public AttachmentWithdrawal? Withdrawal { get; init; }

    /// <summary>هل هذا هو الإصدار القائم — لا مسحوب ولا مُصحَّح بعده؟</summary>
    public bool IsCurrent => !SupersededBy.IsAssigned && Withdrawal is null;
}

/// <summary>بايتات مرفق مع وصفه. لا تُعاد بايتات بلا الوصف الذي يقول ما هي.</summary>
public sealed record AttachmentContent
{
    /// <summary>الوصف كما في القاعدة.</summary>
    public required StoredAttachment Descriptor { get; init; }

    /// <summary>البايتات كما قُرئت من المخزن.</summary>
    public required ReadOnlyMemory<byte> Content { get; init; }
}

/// <summary>
/// نتيجة إعادة قراءة البايتات ومقارنة بصمتها بالمُسجَّلة.
/// <para>
/// <b>وثمنها مذكور في النتيجة نفسها</b> — <see cref="BytesRead"/> و<see cref="Elapsed"/> —
/// لأن التحقّق يقرأ الملفّ كاملاً: عمليةٌ خطّية في حجم المرفق، لا استعلام فهرس.
/// فمن يجدولها على مليون مرفق يعرف ماذا يجدول.
/// </para>
/// </summary>
public sealed record AttachmentIntegrity
{
    /// <summary>المرفق المفحوص.</summary>
    public required AttachmentId Id { get; init; }

    /// <summary>هل طابقت البصمة المُعادة حسابها البصمةَ المُسجَّلة؟</summary>
    public required bool Matches { get; init; }

    /// <summary>البصمة المُسجَّلة في القاعدة.</summary>
    public required string RecordedHash { get; init; }

    /// <summary>البصمة المحسوبة الآن من بايتات المخزن.</summary>
    public required string ObservedHash { get; init; }

    /// <summary>عدد البايتات التي قُرئت فعلاً.</summary>
    public required long BytesRead { get; init; }

    /// <summary>زمن القراءة والتجزئة.</summary>
    public required TimeSpan Elapsed { get; init; }
}

/// <summary>
/// سؤال الجرد — <b>والمستأجر جزء منه لا مرشّح يُضاف</b>.
/// <para>
/// ولا سؤال «كل مرفقات هذا النوع» بلا معرّف مستند: نوعٌ وحده يُنتج جرداً على مستوى
/// المستأجر كلّه، وهو استعلامٌ لا يخدم شاشةً واحدة ويكلّف مسحاً. فالحقلان يُرسلان
/// معاً أو لا يُرسل أيّهما.
/// </para>
/// </summary>
public sealed record AttachmentQuery
{
    /// <summary>السقف الأعلى لعدد الصفوف في الصفحة الواحدة.</summary>
    public const int MaximumPageSize = 100;

    /// <summary>حجم الصفحة الافتراضي حين لا يطلب المستدعي حجماً.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>المستأجر — جزء من المفتاح في كل استعلام.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>نوع المستند المصدر، أو <c>null</c> لجرد المستأجر كلّه.</summary>
    public string? SourceDocumentType { get; init; }

    /// <summary>معرّف المستند المصدر، أو <c>null</c>.</summary>
    public Guid? SourceDocumentId { get; init; }

    /// <summary>عدد الصفوف المتخطّاة.</summary>
    public int Skip { get; init; }

    /// <summary>عدد الصفوف المطلوبة.</summary>
    public int Take { get; init; } = DefaultPageSize;
}

/// <summary>
/// صفحة من الجرد ومعها <b>المجموع الكلّي</b> — لا «هل بعدها المزيد؟» وحدها: عميلٌ يبني
/// ترقيم صفحات يحتاج العدد ليعرف كم صفحة، ولا يستطيع أن يشتقّه من صفحةٍ واحدة.
/// </summary>
public sealed record AttachmentPage
{
    /// <summary>الصفوف، الأحدث أولاً.</summary>
    public required IReadOnlyList<StoredAttachment> Items { get; init; }

    /// <summary>مجموع ما يطابق الترشيح داخل هذا المستأجر.</summary>
    public required int Total { get; init; }

    /// <summary>عدد الصفوف المتخطّاة كما نُفِّذت.</summary>
    public required int Skip { get; init; }

    /// <summary>حجم الصفحة كما نُفِّذ.</summary>
    public required int Take { get; init; }
}
