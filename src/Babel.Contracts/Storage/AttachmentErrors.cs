using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Contracts.Storage;

/// <summary>
/// أخطاء حدّ المرفقات — <b>في العقد لا في المحوّل</b>، لأن المستدعي يعتمد على الرمز
/// ولا يعرف أي محوّل رُكِّب. رموزٌ ثابتة ورسالتان لكلٍّ منها.
/// </summary>
public static class AttachmentErrors
{
    /// <summary>لا مرفق بهذا المعرّف <b>داخل هذا المستأجر</b>.</summary>
    /// <param name="id">المعرّف.</param>
    public static Error NotFound(AttachmentId id) => new(
        "storage.attachment_not_found",
        "لا مرفق بهذا المعرّف: " + id,
        "no attachment with this identifier: " + id);

    /// <summary>حمولة فارغة. لا يُودَع صفر بايت.</summary>
    public static Error Empty => new(
        "storage.content_empty",
        "لا بايتات في الإيداع — المرفق الفارغ لا يُخزَّن.",
        "the submission carries no bytes; an empty attachment is not stored.");

    /// <summary>تجاوز السقف.</summary>
    /// <param name="length">الطول المُقدَّم.</param>
    /// <param name="limit">السقف.</param>
    public static Error TooLarge(long length, long limit) => new(
        "storage.content_too_large",
        string.Format(CultureInfo.InvariantCulture, "حجم المرفق {0} بايت ويتجاوز السقف {1}.", length, limit),
        string.Format(CultureInfo.InvariantCulture, "the attachment is {0} bytes and exceeds the {1} byte cap.", length, limit));

    /// <summary>
    /// لم تُعرف البايتات. <b>ولا يُخزَّن ما لا يُعرف</b>: نوعٌ محايد اليوم هو ترويسة
    /// يخترعها قارئٌ غداً.
    /// </summary>
    public static Error UnrecognisedContent => new(
        "storage.content_not_recognised",
        "لم تُتعرَّف بايتات المرفق على أي نوع مقبول — يُرفض ولا يُخزَّن بنوع محايد.",
        "the attachment bytes match no accepted type; it is refused rather than stored as a neutral type.");

    /// <summary>الإعلان يخالف المشموم.</summary>
    /// <param name="declared">ما أعلنه العميل.</param>
    /// <param name="sniffed">ما قالته البايتات.</param>
    public static Error DeclaredTypeMismatch(string declared, AttachmentMediaType sniffed) => new(
        "storage.declared_type_mismatch",
        "نوع المحتوى المُعلَن «" + declared + "» يخالف ما تقوله البايتات «" + AttachmentMediaTypes.NameOf(sniffed) + "».",
        "the declared content type '" + declared + "' contradicts the sniffed type '" + AttachmentMediaTypes.NameOf(sniffed) + "'.");

    /// <summary>اسم ملفّ لا يمكن تطهيره إلى اسم — لا مسار ولا نصّ فارغ.</summary>
    /// <param name="declared">ما أُرسل.</param>
    public static Error FileNameRefused(string declared) => new(
        "storage.file_name_refused",
        "اسم الملفّ المُرسَل مرفوض — الاسم بيانات لا مسار: «" + declared + "».",
        "the supplied file name is refused; a file name is data, never a path: '" + declared + "'.");

    /// <summary>
    /// البايتات في المخزن لا تطابق البصمة المُسجَّلة. <b>هذا هو الاكتشاف الذي وُجدت
    /// البصمة لأجله</b> — ولا تُسلَّم البايتات لقارئ بعده.
    /// </summary>
    /// <param name="id">المعرّف.</param>
    /// <param name="recorded">المُسجَّلة.</param>
    /// <param name="observed">المحسوبة الآن.</param>
    public static Error HashMismatch(AttachmentId id, string recorded, string observed) => new(
        "storage.content_hash_mismatch",
        "بايتات المرفق " + id + " لا تطابق بصمتها المُسجَّلة (" + recorded + " ≠ " + observed + ") — عبثٌ أو تلف، ولا تُسلَّم.",
        "the bytes of attachment " + id + " do not match the recorded digest (" + recorded + " ≠ " + observed + "): tampering or corruption; they are not served.");

    /// <summary>البايتات غائبة عن المخزن والصفّ قائم.</summary>
    /// <param name="id">المعرّف.</param>
    public static Error ContentMissing(AttachmentId id) => new(
        "storage.content_missing",
        "صفّ المرفق " + id + " قائم والبايتات غائبة عن المخزن.",
        "the row for attachment " + id + " exists but its bytes are missing from the store.");

    /// <summary>محاولة تصحيح مرفق مسحوب.</summary>
    /// <param name="id">المعرّف.</param>
    public static Error AlreadyWithdrawn(AttachmentId id) => new(
        "storage.attachment_withdrawn",
        "المرفق " + id + " مسحوب — لا يُصحَّح ولا يُسحب مرّتين.",
        "attachment " + id + " is withdrawn: it is neither corrected nor withdrawn twice.");

    /// <summary>محاولة تصحيح إصدار سبق أن صُحِّح.</summary>
    /// <param name="id">المعرّف.</param>
    /// <param name="successor">الخلف القائم.</param>
    public static Error AlreadySuperseded(AttachmentId id, AttachmentId successor) => new(
        "storage.attachment_already_superseded",
        "المرفق " + id + " صُحِّح من قبل بالإصدار " + successor + " — السلسلة خطّية ولا تتفرّع.",
        "attachment " + id + " was already superseded by " + successor + "; the chain is linear and does not fork.");

    /// <summary>تذكرة لا يصحّ توقيعها.</summary>
    public static Error TicketNotSigned => new(
        "storage.ticket_signature_invalid",
        "توقيع تذكرة الوصول غير صحيح.",
        "the access ticket signature is invalid.");

    /// <summary>تذكرة منتهية.</summary>
    public static Error TicketExpired => new(
        "storage.ticket_expired",
        "انتهت صلاحية تذكرة الوصول.",
        "the access ticket has expired.");

    /// <summary>
    /// عمر تذكرة يتجاوز السقف المُعلَن.
    /// <para>
    /// <b>و<c>TimeSpan</c> لا <c>double</c> بالثواني</b>: القاعدة 4 تمنع الفاصلة العائمة
    /// في العقود منعاً باتاً، ومدّةٌ تعبر حدّاً بوصفها <c>double</c> هي مدّةٌ تُقارَن
    /// يوماً بمدّةٍ أخرى فتختلفان في الخانة السابعة عشرة.
    /// </para>
    /// </summary>
    /// <param name="requested">العمر المطلوب.</param>
    /// <param name="cap">السقف.</param>
    public static Error TicketLifetimeRefused(TimeSpan requested, TimeSpan cap) => new(
        "storage.ticket_lifetime_refused",
        string.Format(CultureInfo.InvariantCulture, "عمر التذكرة المطلوب {0} ويتجاوز السقف {1}.", requested, cap),
        string.Format(CultureInfo.InvariantCulture, "the requested ticket lifetime of {0} exceeds the {1} cap.", requested, cap));
}
