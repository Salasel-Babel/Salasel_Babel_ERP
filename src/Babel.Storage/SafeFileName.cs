using System.Globalization;
using System.Text;
using Babel.Contracts.Storage;
using Babel.SharedKernel;

namespace Babel.Storage;

/// <summary>
/// <b>اسم الملفّ بيانات، لا مسار.</b>
/// <para>
/// الشكل الكلاسيكي للعطل هو أن يُوصَل اسمٌ من مستخدم بجذر المخزن، فيخرج
/// <c>../../etc/passwd</c> من المجلد كلّه. والعلاج هنا ليس ترشيح <c>..</c> — الترشيح
/// سباقُ تسلّح يخسره المدافع مع أول ترميز — بل أن <b>لا يشارك الاسم في بناء أي مسار
/// إطلاقاً</b>: مفتاح الكائن على القرص يولّده المخزن من عشوائيةٍ معمّاة، والاسم يُحفظ
/// في عمود للعرض وحده.
/// </para>
/// <para>
/// والتطهير هنا هو <b>الطبقة الثانية</b>: اسمٌ يُعرض على شاشة أو يُوضع في ترويسة
/// <c>Content-Disposition</c> لا يجوز أن يحمل فاصل مسار ولا محرف تحكّم ولا محرفاً
/// اتجاهياً غير مرئي (فخ-23: ‏<c>U+202E</c> يقلب <c>gpj.exe</c> فيُقرأ <c>exe.jpg</c>).
/// </para>
/// </summary>
public static class SafeFileName
{
    /// <summary>أقصى طول للاسم المحفوظ بعد التطهير.</summary>
    public const int MaximumLength = 120;

    /// <summary>الاسم الذي يُكتب حين لا يُرسل العميل اسماً صالحاً أصلاً.</summary>
    public const string Fallback = "attachment";

    /// <summary>
    /// يطهّر اسماً معلَناً إلى اسم عرضٍ آمن، أو يرفضه.
    /// <para>
    /// اسمٌ غائب <b>ليس رفضاً</b>: يُكتب <see cref="Fallback"/> ومعه امتداد النوع
    /// المشموم. أمّا اسمٌ موجودٌ لا يبقى منه محرف مقبول واحد فرفضٌ باسمه — لأن ذلك
    /// بالضبط شكل الاسم المصنوع للهجوم.
    /// </para>
    /// </summary>
    /// <param name="declared">الاسم كما أرسله العميل، وقد يكون <c>null</c>.</param>
    /// <param name="mediaType">النوع المشموم — منه يأتي الامتداد المكتوب.</param>
    /// <returns>الاسم المطهَّر أو خطأً.</returns>
    public static Result<string> Sanitise(string? declared, AttachmentMediaType mediaType)
    {
        string extension = "." + AttachmentMediaTypes.ExtensionOf(mediaType);

        if (string.IsNullOrWhiteSpace(declared))
        {
            return Result<string>.Success(Fallback + extension);
        }

        // ‏NFC مرّة واحدة عند الحدّ (فخ-24). ثم تُسقط كل المحارف غير المقبولة.
        string normalised = declared.Normalize(NormalizationForm.FormC);
        StringBuilder kept = new(normalised.Length);

        foreach (char character in normalised)
        {
            if (IsAcceptable(character))
            {
                kept.Append(character);
            }
        }

        string cleaned = kept.ToString().Trim(' ', '.');

        if (cleaned.Length == 0)
        {
            return Result<string>.Failure(AttachmentErrors.FileNameRefused(declared));
        }

        // الامتداد يأتي من البايتات لا من الاسم: اسمٌ ينتهي بـ.jpg وبايتاته PDF يُحفظ
        // بامتداد pdf، فلا يتناقض ما يُعرض مع ما يُقدَّم.
        string stem = StemOf(cleaned);
        if (stem.Length == 0)
        {
            return Result<string>.Failure(AttachmentErrors.FileNameRefused(declared));
        }

        if (stem.Length > MaximumLength - extension.Length)
        {
            stem = stem[..(MaximumLength - extension.Length)];
        }

        return Result<string>.Success(stem + extension);
    }

    private static string StemOf(string cleaned)
    {
        int dot = cleaned.LastIndexOf('.');
        return dot <= 0 ? cleaned : cleaned[..dot];
    }

    /// <summary>
    /// المحارف المقبولة. <b>قائمة سماح لا قائمة منع</b>: المنع يفوته ما لم يُتوقَّع،
    /// والسماح يفوته ما هو مشروع — وثمن الثاني اسمُ عرضٍ أفقر، وثمن الأول ثغرة.
    /// </summary>
    private static bool IsAcceptable(char character)
    {
        if (character is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|' or '\0')
        {
            return false;
        }

        // محارف التحكّم، ومنها فواصل الأسطر — لا تدخل ترويسة ولا اسماً.
        if (char.IsControl(character))
        {
            return false;
        }

        // المحارف الاتجاهية غير المرئية تُرفض عند الحدّ ولا تُزال بصمت (فخ-23):
        // إسقاطها هنا مقصود لأن الاسم حقل عرض لا حقل توقيع.
        if (character is '\u200E' or '\u200F' or '\u202A' or '\u202B' or '\u202C' or '\u202D' or '\u202E'
            or '\u2066' or '\u2067' or '\u2068' or '\u2069' or '\u061C' or '\uFEFF')
        {
            return false;
        }

        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
        return category is not (UnicodeCategory.Format or UnicodeCategory.Surrogate or UnicodeCategory.PrivateUse
            or UnicodeCategory.OtherNotAssigned or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator);
    }
}
