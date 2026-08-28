namespace Babel.Api.Wire;

/// <summary>
/// وصف مرفق على السلك.
/// <para>
/// <b>ولاحظ ما ليس فيه: مفتاح الكائن في المخزن.</b> المفتاح مسارٌ فيزيائي يفهمه المحوّل
/// ويعيش في القاعدة وحدها (‏ADR-0046 §5)، ونشرُه يجعل عميلاً يبني عليه ثم ينكسر يوم يصير
/// المحوّل مخزناً كائنياً. والمسار الذي يحتاجه العميل هو <see cref="ContentPath"/>:
/// عنوان الباب الذي تُنزَّل منه البايتات بتذكرة.
/// </para>
/// <para>
/// <b>ولا حقل «هل هو الحالي؟» مشتقّ هنا:</b> الحالة تُقرأ من <c>supersededBy</c> ومن
/// <c>withdrawal</c> معاً، وحقلٌ ثالث يُلخّصهما كان سيصير مصدر حقيقة ثانياً ينحرف.
/// </para>
/// </summary>
/// <param name="Id">المعرّف الغامض — لا يُشتقّ من اسم ولا من مسار.</param>
/// <param name="MediaType">النوع <b>المشموم من البايتات</b>، لا المُعلَن.</param>
/// <param name="ByteLength">عدد البايتات كما كُتبت.</param>
/// <param name="ContentHash">‏SHA-256 ستّ‌عشرياً صغيراً، أربعة وستون محرفاً.</param>
/// <param name="FileName">اسم العرض بعد التطهير — للعرض وحده، ولا يدخل أي مسار.</param>
/// <param name="ContentPath">مسار تنزيل البايتات على هذا السطح — ويحتاج تذكرة موقّعة.</param>
/// <param name="StoredAt">لحظة الإيداع.</param>
/// <param name="StoredBy">من أودع.</param>
/// <param name="Version">رقم الإصدار — يبدأ بواحد ويزيد مع كل تصحيح.</param>
/// <param name="Supersedes">سلفُ هذا الإصدار، أو <c>null</c> للإصدار الأول.</param>
/// <param name="SupersededBy">خلفُ هذا الإصدار إن صُحِّح، أو <c>null</c>.</param>
/// <param name="SourceDocumentType">رمز نوع المستند المصدر، أو <c>null</c>.</param>
/// <param name="SourceDocumentId">معرّف المستند المصدر، أو <c>null</c>.</param>
/// <param name="Withdrawal">علامة السحب إن سُحب — والبايتات باقية في الحالتين.</param>
internal sealed record AttachmentDto(
    string Id,
    string MediaType,
    long ByteLength,
    string ContentHash,
    string FileName,
    string ContentPath,
    string StoredAt,
    string StoredBy,
    int Version,
    string? Supersedes,
    string? SupersededBy,
    string? SourceDocumentType,
    string? SourceDocumentId,
    AttachmentWithdrawalDto? Withdrawal);

/// <summary>علامة سحب على السلك — <b>لا حذف</b>، والبايتات والبصمة باقيتان.</summary>
/// <param name="WithdrawnAt">لحظة السحب.</param>
/// <param name="WithdrawnBy">من سحبه — إنسان، لا نظام.</param>
/// <param name="ReasonKey">مفتاح السبب: رمزٌ يقرؤه برنامج، لا نصّ يُعرض على شاشة.</param>
internal sealed record AttachmentWithdrawalDto(string WithdrawnAt, string WithdrawnBy, string ReasonKey);

/// <summary>صفحة من جرد المرفقات ومعها المجموع الكلّي.</summary>
/// <param name="Items">الصفوف، الأحدث أولاً.</param>
/// <param name="Total">مجموع ما يطابق الترشيح داخل هذه الشركة.</param>
/// <param name="Skip">عدد الصفوف المتخطّاة كما نُفِّذت.</param>
/// <param name="Take">حجم الصفحة كما نُفِّذ.</param>
internal sealed record AttachmentPageDto(IReadOnlyList<AttachmentDto> Items, int Total, int Skip, int Take);

/// <summary>
/// طلب سحب مرفق. <b>والسبب مفتاحٌ من مجموعة يملكها المستدعي، لا نصّ حرّ</b>: نصٌّ حرّ
/// يُكتب بلغة كاتبه ثم يُقرأ في تقرير بلغة أخرى، ولا يُرشَّح عليه ولا يُترجَم.
/// </summary>
internal sealed record WithdrawAttachmentRequestDto
{
    /// <summary>مفتاح السبب: أحرف لاتينية صغيرة وأرقام ونقطة وشرطة سفلية، حتى 64 محرفاً.</summary>
    public required string ReasonKey { get; init; }
}

/// <summary>
/// طلب سكّ تذكرة تنزيل.
/// <para>
/// <b>والعمر بالثواني عدداً صحيحاً</b> — لا كسراً عشرياً ولا فاصلة عائمة: مدّةٌ تعبر
/// السلك <c>double</c> تُقارَن يوماً بمدّة أخرى فتختلفان في الخانة السابعة عشرة.
/// وطلبٌ يتجاوز السقف <b>يُرفض ولا يُقصّ</b>: القصّ الصامت يجعل المستدعي يظنّ أنه
/// أصدر ساعةً وقد أصدر خمس دقائق.
/// </para>
/// </summary>
internal sealed record IssueAttachmentTicketRequestDto
{
    /// <summary>عمر التذكرة بالثواني. السقف المُعلن خمس دقائق، وما تجاوزه يُرفض.</summary>
    public required int LifetimeSeconds { get; init; }
}

/// <summary>
/// تذكرة تنزيل موقّعة كما تخرج على السلك.
/// <para>
/// <b>وهي ما يُعطى للمتصفّح، لا المسار ولا المعرّف.</b> مستأجرها <b>داخل</b> البايتات
/// الموقّعة لا بجانبها، ويُقارَن بمستأجر الجلسة عند الاستهلاك. <b>ولا تُبطَل قبل
/// انتهائها</b> — لا قائمة إبطال ولا حالة في القاعدة — وذلك ثمن كونها بلا حالة،
/// ولذلك السقف بالدقائق لا بالساعات.
/// </para>
/// </summary>
/// <param name="Token">الرمز الموقّع، نصّاً آمناً في المسار وفي سلسلة الاستعلام.</param>
/// <param name="AttachmentId">المرفق الذي تفتحه.</param>
/// <param name="ExpiresAt">لحظة الانتهاء.</param>
/// <param name="ContentPath">المسار الكامل الذي يُنزَّل به المرفق بهذه التذكرة.</param>
internal sealed record AttachmentTicketDto(
    string Token,
    string AttachmentId,
    string ExpiresAt,
    string ContentPath);
