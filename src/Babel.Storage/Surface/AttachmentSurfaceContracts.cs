namespace Babel.Storage.Surface;

/// <summary>
/// ما يُقدَّم لإيداع مرفق. <b>وحقلان منه لا يُصدَّقان</b> — الاسم والنوع المُعلَنان —
/// وهما مسمّيان بذلك كي لا يقرأهما أحد على أنهما حقيقة.
/// <para>
/// <b>ولا حقل مستأجر فيه ولا حقل فاعل</b>: كلاهما يأتي من الاعتماد ومن المسار، لا من
/// الجسم. وحقلٌ في حمولةٍ اسمه <c>tenantId</c> هو أول ثغرة عبور بين المستأجرين.
/// </para>
/// </summary>
public sealed record AttachmentDeposit
{
    /// <summary>البايتات كما وصلت. تُشمّ وتُجزَّأ وتُكتب، ولا تُرمَّز نصّاً في أي خطوة.</summary>
    public required ReadOnlyMemory<byte> Content { get; init; }

    /// <summary>اسم الملفّ كما أرسله العميل — <b>بيانات لا مسار</b>، ويُطهَّر قبل الحفظ.</summary>
    public string? DeclaredFileName { get; init; }

    /// <summary>النوع كما أعلنه العميل — <b>يُقارَن بالمشموم ولا يحلّ محلّه</b>.</summary>
    public string? DeclaredMediaType { get; init; }

    /// <summary>رمز نوع المستند المصدر، أو <c>null</c>.</summary>
    public string? SourceDocumentType { get; init; }

    /// <summary>معرّف المستند المصدر، أو <c>null</c>. يُقرن بنوعه ولا يُرسل وحده.</summary>
    public Guid? SourceDocumentId { get; init; }
}

/// <summary>
/// وصف مرفق كما يخرج من السطح المنشور.
/// <para>
/// <b>ولاحظ ما ليس فيه: مفتاح الكائن في المخزن.</b> المفتاح يعيش في القاعدة وحدها
/// (‏ADR-0046 §5): هو مسارٌ فيزيائي يفهمه المحوّل، ونشرُه يجعل عميلاً يبني عليه ثم
/// ينكسر يوم يصير المحوّل مخزناً كائنياً. والمسار الذي يحتاجه العميل هو <b>عنوان باب
/// التنزيل</b> — يبنيه سطح HTTP من مساراته المُعلنة، لا موضعُ البايتات على قرص.
/// </para>
/// </summary>
/// <param name="Id">المعرّف الغامض.</param>
/// <param name="MediaType">النوع <b>المشموم من البايتات</b>، لا المُعلَن.</param>
/// <param name="ByteLength">عدد البايتات كما كُتبت.</param>
/// <param name="ContentHash">‏SHA-256 ستّ‌عشرياً صغيراً، أربعة وستون محرفاً.</param>
/// <param name="FileName">اسم العرض بعد التطهير.</param>
/// <param name="StoredAt">لحظة الإيداع.</param>
/// <param name="StoredBy">من أودع.</param>
/// <param name="Version">رقم الإصدار — يبدأ بواحد ويزيد مع كل تصحيح.</param>
/// <param name="Supersedes">سلفُ هذا الإصدار، أو <c>null</c>.</param>
/// <param name="SupersededBy">خلفُ هذا الإصدار إن صُحِّح، أو <c>null</c>.</param>
/// <param name="SourceDocumentType">رمز نوع المستند المصدر، أو <c>null</c>.</param>
/// <param name="SourceDocumentId">معرّف المستند المصدر، أو <c>null</c>.</param>
/// <param name="Withdrawal">علامة السحب إن سُحب — والبايتات باقية في الحالتين.</param>
public sealed record AttachmentRecord(
    Guid Id,
    string MediaType,
    long ByteLength,
    string ContentHash,
    string FileName,
    DateTimeOffset StoredAt,
    Guid StoredBy,
    int Version,
    Guid? Supersedes,
    Guid? SupersededBy,
    string? SourceDocumentType,
    Guid? SourceDocumentId,
    AttachmentWithdrawalRecord? Withdrawal);

/// <summary>علامة سحب كما تخرج من السطح — <b>لا حذف</b>، والبايتات باقية.</summary>
/// <param name="WithdrawnAt">لحظة السحب.</param>
/// <param name="WithdrawnBy">من سحبه — إنسان، لا نظام.</param>
/// <param name="ReasonKey">مفتاح السبب من مجموعة مغلقة عند المستدعي، لا نصّ حرّ يُعرض.</param>
public sealed record AttachmentWithdrawalRecord(DateTimeOffset WithdrawnAt, Guid WithdrawnBy, string ReasonKey);

/// <summary>صفحة من جرد المرفقات، ومعها المجموع الكلّي لما يطابق الترشيح.</summary>
/// <param name="Items">الصفوف، الأحدث أولاً.</param>
/// <param name="Total">مجموع ما يطابق الترشيح داخل هذا المستأجر.</param>
/// <param name="Skip">عدد الصفوف المتخطّاة كما نُفِّذت.</param>
/// <param name="Take">حجم الصفحة كما نُفِّذ.</param>
public sealed record AttachmentInventory(IReadOnlyList<AttachmentRecord> Items, int Total, int Skip, int Take);

/// <summary>
/// تذكرة تنزيل موقّعة كما تخرج من السطح. <b>هي ما يُعطى للمتصفّح</b>، لا المسار ولا
/// المعرّف؛ ومستأجرها <b>داخل</b> البايتات الموقّعة لا بجانبها.
/// </summary>
/// <param name="Token">الرمز الموقّع، نصّاً آمناً في المسار.</param>
/// <param name="AttachmentId">المرفق الذي تفتحه.</param>
/// <param name="ExpiresAt">لحظة الانتهاء.</param>
public sealed record AttachmentAccessTicket(string Token, Guid AttachmentId, DateTimeOffset ExpiresAt);

/// <summary>بايتات مرفق مع وصفه. <b>ولا تُعاد بايتات بلا الوصف الذي يقول ما هي.</b></summary>
/// <param name="Descriptor">الوصف كما في القاعدة.</param>
/// <param name="Content">البايتات كما قُرئت من المخزن، بعد فحص البصمة.</param>
public sealed record AttachmentBytes(AttachmentRecord Descriptor, ReadOnlyMemory<byte> Content);
