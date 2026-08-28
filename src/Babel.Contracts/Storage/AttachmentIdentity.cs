using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Contracts.Storage;

/// <summary>
/// معرّف مرفق — <b>غامض عمداً ولا يحمل معنى</b>.
/// <para>
/// لا يُشتقّ من اسم ملف ولا من رقم مستند ولا من تسلسل، ولا يُقرأ منه شيء عن صاحبه.
/// و<b>ليس مساراً</b>: المسار على القرص مفتاحٌ ثانٍ مستقلّ (<see cref="StoredAttachment.ObjectKey"/>)
/// لا يُشتقّ من هذا المعرّف ولا يعود إليه. فتسريب معرّف لا يعطي مساراً، وتسريب مسار
/// لا يعطي معرّفاً، و<b>لا واحد منهما يعطي بايتة واحدة</b> بلا مستأجرٍ مطابق.
/// </para>
/// </summary>
/// <param name="Value">القيمة.</param>
public readonly record struct AttachmentId(Guid Value)
{
    /// <summary>قيمة غير مخصّصة. وجودها في مسار قراءة أو كتابة خطأ برمجي.</summary>
    public static AttachmentId None => new(Guid.Empty);

    /// <summary>هل المعرّف مخصّص فعلاً؟</summary>
    public bool IsAssigned => Value != Guid.Empty;

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}

/// <summary>
/// أنواع المحتوى المقبولة — <b>مجموعة مغلقة</b>.
/// <para>
/// النوع هنا هو ما <b>استُنتج من البايتات</b>، لا ما أعلنه العميل. ولا يوجد عضو
/// «غير معروف»: ما لا يُتعرَّف عليه <b>يُرفض</b> ولا يُخزَّن بنوع محايد — فالمحايد
/// يُقدَّم لاحقاً إلى متصفّح بترويسة يخترعها القارئ.
/// </para>
/// </summary>
public enum AttachmentMediaType
{
    /// <summary>‏<c>image/jpeg</c>.</summary>
    Jpeg = 1,

    /// <summary>‏<c>image/png</c>.</summary>
    Png = 2,

    /// <summary>‏<c>application/pdf</c>.</summary>
    Pdf = 3,

    /// <summary>‏<c>image/tiff</c>.</summary>
    Tiff = 4,

    /// <summary>‏<c>image/webp</c>.</summary>
    Webp = 5,

    /// <summary>‏<c>image/heic</c> — كاميرا آيفون الافتراضية، وهي شائعة في الالتقاط بالهاتف.</summary>
    Heic = 6,
}

/// <summary>
/// جسر بين المجموعة المغلقة ونصّ نوع المحتوى. <b>دالّة واحدة في المشروع</b> تكتب هذا
/// النصّ، فلا يخترعه مستدعٍ ولا يُنسخ حرفياً في موضعين فينحرف أحدهما.
/// </summary>
public static class AttachmentMediaTypes
{
    /// <summary>نصّ نوع المحتوى لعضو المجموعة المغلقة.</summary>
    /// <param name="mediaType">النوع.</param>
    /// <returns>النصّ، مثل <c>image/jpeg</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">لعضو غير معرَّف.</exception>
    public static string NameOf(AttachmentMediaType mediaType) => mediaType switch
    {
        AttachmentMediaType.Jpeg => "image/jpeg",
        AttachmentMediaType.Png => "image/png",
        AttachmentMediaType.Pdf => "application/pdf",
        AttachmentMediaType.Tiff => "image/tiff",
        AttachmentMediaType.Webp => "image/webp",
        AttachmentMediaType.Heic => "image/heic",
        _ => throw new ArgumentOutOfRangeException(nameof(mediaType)),
    };

    /// <summary>الامتداد الذي يكتبه المخزن، لا الذي يرسله العميل.</summary>
    /// <param name="mediaType">النوع.</param>
    /// <returns>الامتداد بلا نقطة.</returns>
    /// <exception cref="ArgumentOutOfRangeException">لعضو غير معرَّف.</exception>
    public static string ExtensionOf(AttachmentMediaType mediaType) => mediaType switch
    {
        AttachmentMediaType.Jpeg => "jpg",
        AttachmentMediaType.Png => "png",
        AttachmentMediaType.Pdf => "pdf",
        AttachmentMediaType.Tiff => "tif",
        AttachmentMediaType.Webp => "webp",
        AttachmentMediaType.Heic => "heic",
        _ => throw new ArgumentOutOfRangeException(nameof(mediaType)),
    };
}

/// <summary>
/// علامة سحب — <b>لا حذف</b>.
/// <para>
/// المرفق سند إثبات لقيد، والاحتفاظ به واجب نظامي. فما يقابل «احذفه» هنا صفٌّ جديد
/// يقول إنه سُحب، ومن سحبه، ومتى، ولماذا؛ والبايتات باقية كما هي.
/// </para>
/// </summary>
/// <param name="WithdrawnAt">لحظة السحب.</param>
/// <param name="WithdrawnBy">من سحبه — إنسان، لا نظام.</param>
/// <param name="ReasonKey">مفتاح سبب من مجموعة مغلقة عند المستدعي، لا نصّ حرّ يُعرض.</param>
public sealed record AttachmentWithdrawal(DateTimeOffset WithdrawnAt, UserId WithdrawnBy, string ReasonKey);
