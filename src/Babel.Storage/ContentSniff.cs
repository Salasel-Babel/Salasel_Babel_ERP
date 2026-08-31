using System.Text;
using Babel.Contracts.Storage;

namespace Babel.Storage;

/// <summary>
/// <b>ما تقوله البايتات، لا ما يقوله العميل.</b>
/// <para>
/// ترويسة <c>Content-Type</c> واسم الملفّ كلاهما نصٌّ يكتبه الطرف الآخر بحرّية. وملفّ
/// تنفيذي يصل باسم <c>فاتورة.jpg</c> وترويسة <c>image/jpeg</c> يمرّ من كل فحص يقرأ
/// الإعلان، ثم يُقدَّم لاحقاً إلى متصفّح بالترويسة التي اخترعها هو. فالنوع هنا يُستنتَج
/// من الأرقام السحرية وحدها، و<b>ما لا يُتعرَّف عليه يُرفض</b>: لا عضو «غير معروف» في
/// <see cref="AttachmentMediaType"/> بقصد.
/// </para>
/// <para>
/// <b>وهذا شمٌّ لا تحليل.</b> لا يُفكّ ضغط، ولا يُفسَّر PDF، ولا تُبنى صورة في الذاكرة —
/// فمحلّل الصور هو نفسه سطح هجوم، والغرض هنا تصنيفٌ محافظ لا تحقّقٌ من سلامة الملفّ.
/// وما يخرج من هنا يُكتب في القاعدة ويُقدَّم لاحقاً بترويسة <c>Content-Type</c> تأتي
/// <b>من هذا الاستنتاج وحده</b>.
/// </para>
/// </summary>
public static class ContentSniff
{
    /// <summary>أطول ترويسة نحتاج قراءتها للتصنيف: اثنتا عشرة بايتاً (‏RIFF/WEBP و‏ftyp).</summary>
    public const int HeaderBytes = 12;

    private static ReadOnlySpan<byte> Jpeg => [0xFF, 0xD8, 0xFF];

    private static ReadOnlySpan<byte> Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> Pdf => "%PDF-"u8;

    private static ReadOnlySpan<byte> TiffLittleEndian => [0x49, 0x49, 0x2A, 0x00];

    private static ReadOnlySpan<byte> TiffBigEndian => [0x4D, 0x4D, 0x00, 0x2A];

    /// <summary>علامات نوع HEIF التي نقبلها. كاميرا الهاتف تكتب أحدها.</summary>
    private static readonly string[] HeifBrands =
        ["heic", "heix", "hevc", "hevx", "heim", "heis", "hevm", "hevs", "mif1", "msf1"];

    /// <summary>
    /// يصنّف البايتات، أو <c>null</c> إن لم تطابق نوعاً مقبولاً.
    /// </summary>
    /// <param name="content">البايتات كما وصلت.</param>
    /// <returns>النوع المُستنتَج، أو <c>null</c>.</returns>
    public static AttachmentMediaType? Of(ReadOnlySpan<byte> content)
    {
        if (content.StartsWith(Jpeg))
        {
            return AttachmentMediaType.Jpeg;
        }

        if (content.StartsWith(Png))
        {
            return AttachmentMediaType.Png;
        }

        if (content.StartsWith(Pdf))
        {
            return AttachmentMediaType.Pdf;
        }

        if (content.StartsWith(TiffLittleEndian) || content.StartsWith(TiffBigEndian))
        {
            return AttachmentMediaType.Tiff;
        }

        // ‏RIFF….WEBP — العلامة الثانية عند الإزاحة 8، والحجم بينهما لا يُقرأ.
        if (content.Length >= 12
            && content[..4].SequenceEqual("RIFF"u8)
            && content.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return AttachmentMediaType.Webp;
        }

        // ‏ISO-BMFF: ‏[الطول 4] ftyp [العلامة 4].
        if (content.Length >= 12 && content.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            string brand = Encoding.ASCII.GetString(content.Slice(8, 4));
            if (Array.Exists(HeifBrands, candidate => string.Equals(candidate, brand, StringComparison.Ordinal)))
            {
                return AttachmentMediaType.Heic;
            }
        }

        return null;
    }

    /// <summary>
    /// هل يتّفق ما أعلنه العميل مع ما شُمّ؟ إعلانٌ فارغ ليس مخالفة — <b>غيابُ ادّعاء
    /// ليس ادّعاءً كاذباً</b> — أمّا إعلانٌ يخالف البايتات فمخالفة تُسمّى.
    /// </summary>
    /// <param name="declared">الإعلان، وقد يحمل معاملات مثل <c>; charset=</c>.</param>
    /// <param name="sniffed">المشموم.</param>
    public static bool DeclarationAgrees(string? declared, AttachmentMediaType sniffed)
    {
        if (string.IsNullOrWhiteSpace(declared))
        {
            return true;
        }

        int semicolon = declared.IndexOf(';', StringComparison.Ordinal);
        string bare = (semicolon < 0 ? declared : declared[..semicolon]).Trim();

        return string.Equals(bare, AttachmentMediaTypes.NameOf(sniffed), StringComparison.OrdinalIgnoreCase);
    }
}
