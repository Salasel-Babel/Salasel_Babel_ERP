using System.Security.Cryptography;
using System.Text;
using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Zatca.Canonicalization;

/// <summary>
/// كيف يُكتب <b>ناتج</b> دالة التجزئة داخل المستند. ثلاثة مواضع في مستند واحد،
/// و<b>ليست كلها بالترميز نفسه</b> — وهذا بالضبط ما يجعلها فئة عطل قائمة بذاتها.
/// <para/>
/// <b>لماذا تعداد مُعلَن بدل ثابت مدفون:</b> عدم التماثل بين المواضع الثلاثة هو
/// <b>أعلى بند خطراً في هذا المشروع كله</b> بين ما لم يُتحقَّق منه. جعلُه تعداداً
/// يعني أن تصحيحه — يوم تُقرأ المواصفة — سطر واحد ومتجه ذهبي جديد،
/// لا مطاردة عبر ملفات التوقيع.
/// </summary>
public enum DigestEncoding
{
    /// <summary>‏<c>base64(بايتات البصمة الخام)</c> — اثنتان وثلاثون بايتاً تصير 44 محرفاً.</summary>
    RawDigestBase64,

    /// <summary>
    /// ‏<c>base64(النصّ الستّ‌عشري للبصمة)</c> — أربع وستون محرفاً ستّ‌عشرياً تصير 88 محرفاً.
    /// <b>دورة ترميز زائدة مقصودة</b>، لا سهو: من يفكّها مرة واحدة يحصل على نصّ ستّ‌عشري
    /// يبدو معقولاً، فيبني عليه ويفشل عند الجهة بلا رسالة تشرح السبب.
    /// </summary>
    HexDigestBase64
}

/// <summary>
/// <b>الطريق الوحيد إلى دالة التجزئة في مسار الهيئة.</b> لا يُستدعى <c>SHA256</c> مباشرةً
/// في أي موضع آخر من هذا المشروع.
/// <para/>
/// <b>وفخّ المجال الأول محبوس هنا بالنوع لا بالتعليق</b>
/// (‏<c>docs/evidence/traps.md#fakh-double-hashing</c>): كل دالة تُعيد
/// <see cref="byte"/><c>[]</c> خاماً حين يكون الناتج مقصوداً للتوقيع، وتُعيد
/// <see cref="string"/> حين يكون الناتج مقصوداً <b>للكتابة داخل المستند فقط</b>.
/// النصّ لا يُمرَّر إلى موقِّع أبداً، والبايتات الخام لا تُكتب في المستند أبداً.
/// </summary>
public static class ZatcaDigests
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>بصمة خام. <b>هذه وحدها هي ما يُمرَّر إلى موقِّع.</b></summary>
    public static byte[] Sha256(ReadOnlySpan<byte> bytes) => SHA256.HashData(bytes);

    /// <summary>
    /// يكتب بصمة داخل المستند بالترميز المطلوب. <b>لا يُجزّئ</b> — يستقبل بصمة محسوبة،
    /// كي لا يوجد في هذا الملف طريق يجزّئ ما هو مُجزَّأ سلفاً.
    /// </summary>
    public static string Render(ReadOnlySpan<byte> digest, DigestEncoding encoding) => encoding switch
    {
        DigestEncoding.RawDigestBase64 => Convert.ToBase64String(digest),
        DigestEncoding.HexDigestBase64 => Convert.ToBase64String(
            Utf8.GetBytes(Convert.ToHexString(digest).ToLowerInvariant())),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "ترميز بصمة غير معروف / unknown digest encoding")
    };

    /// <summary>
    /// بصمة الشهادة كما تُكتب في <c>xades:CertDigest</c>.
    /// <para/>
    /// <b>المُجزَّأ ليس بايتات DER بل نصّ base64 لها.</b> أي أن هناك دورة ترميز قبل
    /// التجزئة ودورة بعدها. وهو نفس شكل العطل الذي يوثّقه
    /// <c>docs/evidence/traps.md#fakh-double-base64-in-binary-security-token</c>
    /// في رمز الأمان الثنائي، واقعاً هنا في موضع ثانٍ مختلف.
    /// </summary>
    [Provisional("ما الذي يُجزَّأ بالضبط لبصمة الشهادة: بايتات DER أم نصّ base64 لها، وبأي ترميز يُكتب الناتج",
        DerivedFrom = "قراءة المواصفة وتنفيذات مفتوحة المصدر — لا من الهيئة",
        Risk = ProvisionalRisk.Structural,
        VerifyBy = "مواصفة الختم التشفيري: تعريف xades:CertDigest ومثالها المرجعي")]
    public static byte[] CertificateDigestInput(ReadOnlySpan<byte> certificateDer) =>
        Utf8.GetBytes(Convert.ToBase64String(certificateDer));

    /// <summary>
    /// رمز الأمان الثنائي: <b>base64 لـbase64 لـDER</b> — دورتا فكّ ترميز للوصول إلى الشهادة.
    /// يُبنى هنا مرة واحدة كي لا يُعاد ارتكاب الخطأ في المصادقة وفي التوقيع معاً.
    /// </summary>
    [Provisional("ترميز رمز الأمان الثنائي (base64 مزدوج فوق DER)",
        DerivedFrom = "تنفيذات مفتوحة المصدر مستقلة — لا من الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "توثيق ترويسة رمز الأمان الثنائي في مواصفة الواجهة")]
    public static string BinarySecurityToken(ReadOnlySpan<byte> certificateDer) =>
        Convert.ToBase64String(Encoding.ASCII.GetBytes(Convert.ToBase64String(certificateDer)));
}

/// <summary>
/// الترميز المستعمل في كل موضع من المواضع الثلاثة. سجل واحد يُمرَّر، لا ثوابت متفرّقة.
/// <para/>
/// <b>القيم الافتراضية أدناه هي أخطر ما في هذا المشروع بين ما لم يُتحقَّق منه</b>، لأن
/// خطأً في أيٍّ منها يُنتج مستنداً يتحقّق محلياً بنجاح تام ويُرفض عند الجهة بلا رسالة
/// تشرح السبب — وهو فخّ التجزئة المزدوجة نفسه في ثوب الترميز.
/// </summary>
public sealed record ZatcaDigestPolicy(
    DigestEncoding InvoiceReference,
    DigestEncoding SignedPropertiesReference,
    DigestEncoding CertificateDigest)
{
    /// <summary>
    /// الافتراضي المستعاد من قراءة المواصفة ومن تنفيذات مفتوحة المصدر.
    /// <b>عدم التماثل مقصود في هذا الافتراضي</b>: مرجع الفاتورة بالبصمة الخام،
    /// والآخران بالنصّ الستّ‌عشري. وهذا التفاوت نفسه هو ما يجب أن يُقرأ من المواصفة
    /// قبل أول إرسال حقيقي، لأنه غير قابل للتخمين ولا يُكشف إلا بالرفض.
    /// </summary>
    [Provisional("ترميز كل من المواضع الثلاثة، وعدم التماثل بينها",
        DerivedFrom = "قراءة المواصفة وتنفيذات مفتوحة المصدر — لا من الهيئة",
        Risk = ProvisionalRisk.Structural,
        VerifyBy = "المثال المرجعي الموقَّع المنشور مع مواصفة الختم التشفيري، ثم إثبات في البيئة الاختبارية")]
    public static ZatcaDigestPolicy Default { get; } = new(
        InvoiceReference: DigestEncoding.RawDigestBase64,
        SignedPropertiesReference: DigestEncoding.HexDigestBase64,
        CertificateDigest: DigestEncoding.HexDigestBase64);

    /// <summary>
    /// البديل المتماثل: الثلاثة بالبصمة الخام. موجود كي يكون تبديل الفرضية
    /// <b>سطراً واحداً ومتجهاً ذهبياً</b>، لا إعادة كتابة لمسار التوقيع.
    /// </summary>
    public static ZatcaDigestPolicy AllRaw { get; } = new(
        DigestEncoding.RawDigestBase64,
        DigestEncoding.RawDigestBase64,
        DigestEncoding.RawDigestBase64);
}
