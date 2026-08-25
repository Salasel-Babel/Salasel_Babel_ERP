using System.Globalization;
using System.Text;
using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Zatca.Qr;

/// <summary>وسم واحد في ترميز TLV، بقيمته الخام.</summary>
public readonly record struct QrTag(byte Tag, ReadOnlyMemory<byte> Value)
{
    public string AsText() => Encoding.UTF8.GetString(Value.Span);
}

/// <summary>
/// شكل قيمة الوسم على السلك. <b>ليس تفصيلاً تجميلياً</b>: الوسوم 6–9 تحمل مادة
/// تشفيرية، وكتابتها نصّاً مُرمَّزاً بدل بايتات خام (أو العكس) يُنتج رمزاً
/// <b>يُقرأ بنجاح ويحمل قيمة خاطئة</b> — وهو أسوأ ناتج ممكن، لأنه يبدو ناجحاً.
/// </summary>
public enum QrValueForm
{
    /// <summary>القيمة نصّ يُكتب ببايتات UTF-8 (اسم، رقم، تاريخ، أو نصّ base64).</summary>
    Utf8Text,

    /// <summary>القيمة بايتات خام تُكتب كما هي (‏DER، توقيع).</summary>
    RawBinary
}

/// <summary>
/// أي وسم يُكتب بأي شكل. <b>جدول واحد مُعلَن</b> بدل قرار مبعثر عند كل وسم.
/// </summary>
public sealed record ZatcaQrValueForms(
    QrValueForm InvoiceHash,
    QrValueForm Signature,
    QrValueForm PublicKey,
    QrValueForm CertificateSignature)
{
    /// <summary>
    /// الافتراضي المستعاد من قراءة المواصفة ومن تنفيذات مفتوحة المصدر.
    /// <b>غير متماثل عمداً</b>: البصمة والتوقيع نصّاً مُرمَّزاً، والمفتاح العام وتوقيع
    /// الشهادة بايتات خام. وهذا التفاوت هو أعلى ما في رمز QR خطراً بين ما لم يُتحقَّق منه.
    /// </summary>
    [Provisional("شكل قيمة كل وسم من الوسوم 6–9: نصّ مُرمَّز أم بايتات خام",
        DerivedFrom = "قراءة مواصفة رمز QR وتنفيذات مفتوحة المصدر — لا من الهيئة",
        Risk = ProvisionalRisk.Structural,
        VerifyBy = "مواصفة رمز QR المنشورة ومثالها المرجعي، ثم فكّ ترميز رمز صادر عن أداة الهيئة")]
    public static ZatcaQrValueForms Default { get; } = new(
        InvoiceHash: QrValueForm.Utf8Text,
        Signature: QrValueForm.Utf8Text,
        PublicKey: QrValueForm.RawBinary,
        CertificateSignature: QrValueForm.RawBinary);
}

/// <summary>
/// رمز الاستجابة السريعة بترميز TLV ثم base64.
/// <para/>
/// <b>ثلاثة أعطال في هذا الترميز تُنتج رمزاً «يعمل» وهو خاطئ</b>، ولذلك كلها مرفوضة
/// هنا بصوت عالٍ لا مُعالَجة بهدوء:
/// <list type="number">
///   <item>
///     <b>قيمة أطول من 255 بايت.</b> خانة الطول بايت واحد. قصّ القيمة لتدخل يُنتج
///     رمزاً يُقرأ ويحمل اسماً مبتوراً — وقصّ نصّ عربي عند بايت 255 قد يقع
///     <b>داخل محرف</b> فيُنتج UTF-8 غير صالح. <b>مرفوض.</b>
///   </item>
///   <item>
///     <b>ترتيب الوسوم.</b> القارئ يقرأ بالترتيب لا بالبحث عن الوسم، فترتيب مختلف
///     يعطي مبلغاً في موضع الضريبة. الترتيب هنا مبنيّ في الدالة لا مُمرَّر.
///   </item>
///   <item>
///     <b>وسم ناقص في المرحلة الثانية.</b> رمز بخمسة وسوم يُقرأ بنجاح تام؛ ونقصه
///     لا يظهر إلا عند متحقّق الهيئة. ولذلك المرحلة نوع صريح، لا عدد وسوم متروك للنيّة.
///   </item>
/// </list>
/// </summary>
public static class ZatcaQr
{
    /// <summary>أقصى طول قيمة يسعه بايت الطول الواحد.</summary>
    public const int MaximumValueLength = 255;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// المرحلة الأولى: خمسة وسوم. لا بصمة ولا توقيع ولا مفتاح عام — لأنها لا توجد بعد.
    /// </summary>
    public static string Phase1(
        string sellerNameAr,
        string sellerVatNumber,
        DateTimeOffset issuedAt,
        decimal grossTotal,
        decimal taxTotal) =>
        Encode(Phase1Tags(sellerNameAr, sellerVatNumber, issuedAt, grossTotal, taxTotal));

    /// <summary>
    /// المرحلة الثانية: الخمسة الأولى ومعها البصمة والتوقيع والمفتاح العام،
    /// ويُضاف توقيع الشهادة <b>للفاتورة المبسّطة وحدها</b>.
    /// </summary>
    public static string Phase2(
        string sellerNameAr,
        string sellerVatNumber,
        DateTimeOffset issuedAt,
        decimal grossTotal,
        decimal taxTotal,
        string invoiceHashBase64,
        string signatureBase64,
        ReadOnlyMemory<byte> publicKeyDer,
        ReadOnlyMemory<byte> certificateSignature,
        bool isSimplified,
        ZatcaQrValueForms? forms = null)
    {
        ZatcaQrValueForms shape = forms ?? ZatcaQrValueForms.Default;

        List<QrTag> tags = [.. Phase1Tags(sellerNameAr, sellerVatNumber, issuedAt, grossTotal, taxTotal)];

        tags.Add(new QrTag(6, Shape(invoiceHashBase64, default, shape.InvoiceHash, textOnly: true)));
        tags.Add(new QrTag(7, Shape(signatureBase64, default, shape.Signature, textOnly: true)));
        tags.Add(new QrTag(8, Shape(null, publicKeyDer, shape.PublicKey, textOnly: false)));

        // الوسم التاسع للمبسّطة وحدها. إضافته للقياسية أو حذفه من المبسّطة
        // يُنتج في الحالتين رمزاً يُقرأ بنجاح ويُرفض عند المتحقّق.
        if (isSimplified)
        {
            tags.Add(new QrTag(9, Shape(null, certificateSignature, shape.CertificateSignature, textOnly: false)));
        }

        return Encode(tags);
    }

    private static ReadOnlyMemory<byte> Shape(
        string? text, ReadOnlyMemory<byte> binary, QrValueForm form, bool textOnly) => form switch
        {
            QrValueForm.Utf8Text when text is not null => Utf8.GetBytes(text),
            QrValueForm.Utf8Text => Utf8.GetBytes(Convert.ToBase64String(binary.Span)),
            QrValueForm.RawBinary when textOnly && text is not null => Convert.FromBase64String(text),
            QrValueForm.RawBinary => binary,
            _ => throw new ArgumentOutOfRangeException(nameof(form), form, "شكل قيمة وسم غير معروف / unknown tag value form")
        };

    private static IEnumerable<QrTag> Phase1Tags(
        string sellerNameAr, string sellerVatNumber, DateTimeOffset issuedAt, decimal grossTotal, decimal taxTotal)
    {
        yield return new QrTag(1, Utf8.GetBytes(sellerNameAr));
        yield return new QrTag(2, Utf8.GetBytes(sellerVatNumber));
        yield return new QrTag(3, Utf8.GetBytes(Timestamp(issuedAt)));
        yield return new QrTag(4, Utf8.GetBytes(Documents.ZatcaAmounts.Render(grossTotal, "qr.gross_total")));
        yield return new QrTag(5, Utf8.GetBytes(Documents.ZatcaAmounts.Render(taxTotal, "qr.tax_total")));
    }

    /// <summary>
    /// الطابع الزمني داخل الرمز. <b>ثقافة ثابتة و UTC صريح</b>: تحت <c>ar-SA</c> يعطي
    /// التنسيق الافتراضي تاريخاً هجرياً يحمل بداخله <c>U+200F</c> — رمز يُقرأ بنجاح
    /// ويحمل تاريخاً لا يفهمه أحد.
    /// </summary>
    [Provisional("تنسيق الطابع الزمني داخل رمز QR ومنطقته الزمنية",
        DerivedFrom = "قراءة مواصفة رمز QR — لا من الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "مواصفة رمز QR المنشورة")]
    public static string Timestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    /// <summary>
    /// يُرمّز قائمة وسوم. <b>يرفض القيمة الأطول من 255 بايت ولا يقصّها</b>.
    /// </summary>
    public static string Encode(IEnumerable<QrTag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        List<byte> buffer = [];
        int index = 0;

        foreach (QrTag tag in tags)
        {
            index++;

            if (tag.Value.Length > MaximumValueLength)
            {
                throw new ZatcaQrException(FormattableString.Invariant($"قيمة الوسم {tag.Tag} (‏الترتيب {index}) طولها {tag.Value.Length} بايت، ") +
                    FormattableString.Invariant($"وخانة الطول في TLV بايت واحد لا يسع أكثر من {MaximumValueLength}. ") +
                    "القصّ ممنوع: يُنتج رمزاً يُقرأ بنجاح ويحمل قيمة مبتورة، وقصّ النصّ العربي " +
                    "قد يقع داخل محرف فيُنتج UTF-8 غير صالح. المعالجة تقع قبل هذا الحدّ. / " +
                    FormattableString.Invariant($"tag {tag.Tag} value is {tag.Value.Length} bytes; the TLV length byte holds at most {MaximumValueLength}. ") +
                    "Truncation is refused: it yields a QR that scans and is wrong.");
            }

            buffer.Add(tag.Tag);
            buffer.Add((byte)tag.Value.Length);
            buffer.AddRange(tag.Value.ToArray());
        }

        if (index == 0)
        {
            throw new ZatcaQrException("رمز QR بلا وسوم. رمز فارغ يُرمَّز بنجاح ولا يحمل شيئاً. / an empty QR encodes successfully and carries nothing.");
        }

        return Convert.ToBase64String(buffer.ToArray());
    }

    /// <summary>
    /// فكّ الترميز. موجود <b>لأن الاختبار الذي يتحقّق من أن السلسلة «تبدو base64» عديم
    /// القيمة هنا</b>: ما يجب إثباته هو أن الوسوم بترتيبها وأن كل طول يطابق قيمته.
    /// </summary>
    public static IReadOnlyList<QrTag> Decode(string base64)
    {
        byte[] bytes = Convert.FromBase64String(base64);
        List<QrTag> tags = [];
        int position = 0;

        while (position < bytes.Length)
        {
            if (position + 2 > bytes.Length)
            {
                throw new ZatcaQrException(string.Create(CultureInfo.InvariantCulture,
                    $"ترميز TLV مبتور عند البايت {position}: لا يتّسع لوسم وطول. / truncated TLV at byte {position}."));
            }

            byte tag = bytes[position];
            int length = bytes[position + 1];
            position += 2;

            if (position + length > bytes.Length)
            {
                throw new ZatcaQrException(string.Create(CultureInfo.InvariantCulture,
                    $"الوسم {tag} يعلن طولاً {length} ويتجاوز نهاية البيانات. / tag {tag} declares length {length} past the end."));
            }

            tags.Add(new QrTag(tag, bytes.AsMemory(position, length)));
            position += length;
        }

        return tags;
    }
}

/// <summary>عطل في ترميز رمز الاستجابة السريعة. يخرج بصوت عالٍ ولا يُنتج رمزاً ناقصاً.</summary>
public sealed class ZatcaQrException(string message) : Exception(message);
