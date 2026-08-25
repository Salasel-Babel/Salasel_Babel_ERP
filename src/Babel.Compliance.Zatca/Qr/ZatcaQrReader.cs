using System.Globalization;
using System.Text;

namespace Babel.Compliance.Zatca.Qr;

/// <summary>
/// مرحلة الرمز كما <b>تُقرأ من وسومه</b>، لا كما يُصرّح بها المستدعي.
/// <para/>
/// المُرمِّز يأخذ المرحلة نوعاً صريحاً كي لا يُنتج رمزاً ناقص الوسوم؛ والقارئ يشتقّها
/// من الوسوم الموجودة فعلاً، لأن الرمز الواصل إلينا كتبه <b>غيرنا</b> ولا وعد فيه.
/// </summary>
public enum ZatcaQrPhase
{
    /// <summary>خمسة وسوم: اسم البائع ورقمه الضريبي والطابع الزمني والإجمالي والضريبة.</summary>
    Phase1 = 1,

    /// <summary>ثمانية وسوم: الخمسة ومعها البصمة والتوقيع والمفتاح العام — فاتورة قياسية.</summary>
    Phase2Standard = 2,

    /// <summary>تسعة وسوم: الثمانية ومعها توقيع الشهادة — فاتورة مبسّطة.</summary>
    Phase2Simplified = 3,
}

/// <summary>
/// طول وسم واحد <b>بالبايت</b> كما أعلنته خانة الطول.
/// <para/>
/// البايت لا المحرف: الاسم العربي يكلّف بايتين لكل حرف تقريباً، فقارئ يقيس بالمحارف
/// يقرأ قيمةً منزاحة عن موضعها ويُخرج <b>نصّاً معقولاً وخاطئاً</b>
/// (‏<c>docs/evidence/traps.md#fakh-qr-tlv-length-byte-silently-truncates</c>).
/// </summary>
/// <param name="Tag">رقم الوسم.</param>
/// <param name="ByteLength">طول قيمته بالبايت.</param>
public readonly record struct ZatcaQrTagLength(byte Tag, int ByteLength);

/// <summary>
/// محتوى رمز الاستجابة السريعة مفكوكاً إلى حقول مُسمّاة.
/// <para/>
/// <b>هذه الحقول مُصدَّق عليها تشفيرياً في المرحلة الثانية</b> — أي أنها ليست قراءة
/// ضوئية ولا تخميناً: البائع ورقمه الضريبي والطابع الزمني والإجمالي والضريبة كلها
/// داخل الرمز الذي وقّعه المُصدِر. وما ليس فيه — سطور الفاتورة — يبقى قراءةً ضوئية.
/// </summary>
public sealed record ZatcaQrContents
{
    /// <summary>اسم البائع كما كتبه المُصدِر.</summary>
    public required string SellerName { get; init; }

    /// <summary>رقم التسجيل الضريبي للبائع.</summary>
    public required string SellerVatNumber { get; init; }

    /// <summary>لحظة الإصدار بالتوقيت العالمي.</summary>
    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>الإجمالي شامل الضريبة. <c>decimal</c> لا غير.</summary>
    public required decimal GrossTotal { get; init; }

    /// <summary>مبلغ ضريبة القيمة المضافة. <c>decimal</c> لا غير.</summary>
    public required decimal TaxTotal { get; init; }

    /// <summary>المرحلة المشتقّة من الوسوم الموجودة.</summary>
    public required ZatcaQrPhase Phase { get; init; }

    /// <summary>أطوال الوسوم بالبايت بترتيبها — أداة تشخيص لا زينة.</summary>
    public required IReadOnlyList<ZatcaQrTagLength> TagLengths { get; init; }

    /// <summary>بصمة الفاتورة نصّاً مُرمَّزاً، أو <c>null</c> في المرحلة الأولى.</summary>
    public string? InvoiceHashBase64 { get; init; }

    /// <summary>التوقيع نصّاً مُرمَّزاً، أو <c>null</c> في المرحلة الأولى.</summary>
    public string? SignatureBase64 { get; init; }

    /// <summary>المفتاح العام بايتات DER، وفارغ في المرحلة الأولى.</summary>
    public ReadOnlyMemory<byte> PublicKey { get; init; }

    /// <summary>توقيع الشهادة، وفارغ إلا في المبسّطة.</summary>
    public ReadOnlyMemory<byte> CertificateSignature { get; init; }

    /// <summary>هل الرمز يحمل مادة تشفيرية (المرحلة الثانية)؟</summary>
    public bool IsCryptographicallyAttested => Phase != ZatcaQrPhase.Phase1;
}

/// <summary>
/// فكّ رمز الاستجابة السريعة إلى حقول مُسمّاة — <b>عكس <see cref="ZatcaQr"/> بالضبط</b>.
/// <para/>
/// <b>لماذا هذا القارئ موجود:</b> فاتورة المورد الملتزمة تحمل الحقول الحرجة داخل رمزها،
/// فلا تحتاج قراءةً ضوئية أصلاً. القراءة الضوئية تبقى للسطور ولمستندٍ قديم أو غير ملتزم.
/// <para/>
/// <b>وثلاثة أعطال تجعل قارئاً «يعمل» وهو خاطئ، وكلها مرفوضة هنا بصوت عالٍ:</b>
/// <list type="number">
///   <item>
///     <b>القياس بالمحرف بدل البايت.</b> خانة الطول في TLV تعدّ <b>بايتات</b>، والاسم
///     العربي يكلّف بايتين لكل حرف. قارئ يتقدّم بعدد المحارف ينزلق داخل الوسم التالي
///     فيقرأ <b>رقماً ضريبياً مقطوعاً وتاريخاً معقولاً</b> ولا يشتكي.
///     المسافة هنا تُقطع ببايتات <c>ReadOnlyMemory&lt;byte&gt;</c> حصراً.
///   </item>
///   <item>
///     <b>فكّ UTF-8 المتساهل.</b> <c>Encoding.UTF8.GetString</c> يستبدل البايتات غير
///     الصالحة بمحرف <c>U+FFFD</c> <b>بصمت</b>، فاسمٌ قُصّ داخل محرف عربي يعود «اسماً»
///     فيه محرف استبدال واحد ويمرّ. القارئ هنا يستعمل مُفكِّكاً <b>يرمي</b> عند أول
///     بايت غير صالح.
///   </item>
///   <item>
///     <b>الترتيب مفترَض لا مفحوص.</b> القارئ يقرأ بالترتيب؛ فرمزٌ وسومه مبعثرة يضع
///     الضريبة موضع الإجمالي. الترتيب هنا <b>مفحوص</b>، والوسم المكرّر أو المجهول
///     أو الناقص كلها رفض.
///   </item>
/// </list>
/// </summary>
public static class ZatcaQrReader
{
    /// <summary>الوسوم الخمسة الإلزامية في كل مرحلة.</summary>
    public const int MandatoryTagCount = 5;

    /// <summary>عدد وسوم الفاتورة القياسية في المرحلة الثانية.</summary>
    public const int StandardTagCount = 8;

    /// <summary>عدد وسوم الفاتورة المبسّطة في المرحلة الثانية.</summary>
    public const int SimplifiedTagCount = 9;

    /// <summary>
    /// مُفكِّك <b>يرمي</b> ولا يستبدل. هذا هو الفرق بين رفضٍ مسموع واسمٍ فيه محرف استبدال.
    /// </summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>الترتيب الوحيد المقبول للوسوم.</summary>
    private static readonly byte[] ExpectedOrder = [1, 2, 3, 4, 5, 6, 7, 8, 9];

    /// <summary>
    /// يفكّ رمزاً ويعيد حقوله. يرمي <see cref="ZatcaQrException"/> على أي عيب،
    /// ولا يعيد قيمة مُخمَّنة إطلاقاً.
    /// </summary>
    /// <param name="base64">الرمز كما مُسح.</param>
    /// <param name="forms">شكل قيمة الوسوم 6–9. الافتراضي هو شكل المُرمِّز نفسه.</param>
    public static ZatcaQrContents Read(string base64, ZatcaQrValueForms? forms = null)
    {
        ArgumentNullException.ThrowIfNull(base64);

        ZatcaQrValueForms shape = forms ?? ZatcaQrValueForms.Default;
        IReadOnlyList<QrTag> tags = DecodeOrThrow(base64);

        EnsureOrderAndCompleteness(tags);

        Dictionary<byte, ReadOnlyMemory<byte>> byTag = tags.ToDictionary(static tag => tag.Tag, static tag => tag.Value);

        string sellerName = Text(byTag[1], 1, "اسم البائع", "seller name");
        string vatNumber = Text(byTag[2], 2, "الرقم الضريبي", "VAT registration number");
        string timestamp = Text(byTag[3], 3, "الطابع الزمني", "timestamp");
        string gross = Text(byTag[4], 4, "الإجمالي شامل الضريبة", "gross total");
        string tax = Text(byTag[5], 5, "مبلغ الضريبة", "tax total");

        ZatcaQrPhase phase = tags.Count switch
        {
            MandatoryTagCount => ZatcaQrPhase.Phase1,
            StandardTagCount => ZatcaQrPhase.Phase2Standard,
            _ => ZatcaQrPhase.Phase2Simplified,
        };

        return new ZatcaQrContents
        {
            SellerName = sellerName,
            SellerVatNumber = vatNumber,
            IssuedAt = Timestamp(timestamp),
            GrossTotal = Amount(gross, 4, "الإجمالي شامل الضريبة", "gross total"),
            TaxTotal = Amount(tax, 5, "مبلغ الضريبة", "tax total"),
            Phase = phase,
            TagLengths = [.. tags.Select(static tag => new ZatcaQrTagLength(tag.Tag, tag.Value.Length))],
            InvoiceHashBase64 = byTag.TryGetValue(6, out ReadOnlyMemory<byte> hash) ? AsText(hash, 6, shape.InvoiceHash) : null,
            SignatureBase64 = byTag.TryGetValue(7, out ReadOnlyMemory<byte> signature) ? AsText(signature, 7, shape.Signature) : null,
            PublicKey = byTag.TryGetValue(8, out ReadOnlyMemory<byte> key) ? AsBinary(key, 8, shape.PublicKey) : default,
            CertificateSignature = byTag.TryGetValue(9, out ReadOnlyMemory<byte> seal)
                ? AsBinary(seal, 9, shape.CertificateSignature)
                : default,
        };
    }

    private static IReadOnlyList<QrTag> DecodeOrThrow(string base64)
    {
        try
        {
            return ZatcaQr.Decode(base64);
        }
        catch (FormatException)
        {
            throw new ZatcaQrException(
                "الرمز ليس base64 صالحاً، فلا شيء فيه يُقرأ. والقارئ المتساهل هنا يُنتج بايتات "
                + "عشوائية تُفكَّك إلى حقول تبدو معقولة. / "
                + "the payload is not valid base64; a lenient reader would produce plausible fields from noise.");
        }
    }

    /// <summary>
    /// الوسوم بترتيبها، بلا تكرار ولا مجهول ولا نقص، وبعدد يقابل مرحلةً معروفة.
    /// </summary>
    private static void EnsureOrderAndCompleteness(IReadOnlyList<QrTag> tags)
    {
        if (tags.Count is not (MandatoryTagCount or StandardTagCount or SimplifiedTagCount))
        {
            throw new ZatcaQrException(
                FormattableString.Invariant($"الرمز يحمل {tags.Count} وسماً، والأشكال المعروفة ثلاثة: ")
                + FormattableString.Invariant($"{MandatoryTagCount} (المرحلة الأولى) أو {StandardTagCount} (قياسية) أو {SimplifiedTagCount} (مبسّطة). ")
                + "والعدد الآخر رمزٌ يُقرأ بنجاح وينقصه ما يرفضه المتحقّق. / "
                + FormattableString.Invariant($"the code carries {tags.Count} tags; only 5, 8 or 9 are known shapes."));
        }

        for (int index = 0; index < tags.Count; index++)
        {
            byte expected = ExpectedOrder[index];
            if (tags[index].Tag != expected)
            {
                throw new ZatcaQrException(
                    FormattableString.Invariant($"الوسم في الترتيب {index + 1} رقمه {tags[index].Tag} والمتوقّع {expected}. ")
                    + "القارئ يقرأ بالترتيب لا بالبحث، فترتيبٌ مختلف يضع مبلغاً في موضع الضريبة. / "
                    + FormattableString.Invariant($"tag at position {index + 1} is {tags[index].Tag}, expected {expected}."));
            }
        }
    }

    /// <summary>
    /// يقرأ قيمة وسم نصّاً. القيمة تُقطع <b>ببايتاتها</b>، وتُفكَّك بمُفكِّك يرمي.
    /// </summary>
    private static string Text(ReadOnlyMemory<byte> value, byte tag, string fieldAr, string fieldEn)
    {
        if (value.Length == 0)
        {
            throw new ZatcaQrException(
                FormattableString.Invariant($"الوسم {tag} ({fieldAr}) طوله صفر بايت. الحقل الفارغ يُقرأ بنجاح ولا يحمل شيئاً. / ")
                + FormattableString.Invariant($"tag {tag} ({fieldEn}) is zero bytes long."));
        }

        try
        {
            return StrictUtf8.GetString(value.Span);
        }
        catch (DecoderFallbackException)
        {
            throw new ZatcaQrException(
                FormattableString.Invariant($"الوسم {tag} ({fieldAr}) يحمل {value.Length} بايت ليست UTF-8 صالحة — ")
                + "وهذا ما يخلّفه القصّ عند حدّ 255 بايتاً حين يقع داخل محرف عربي. "
                + "والفكّ المتساهل يُعيد محرف استبدال ويمرّ. / "
                + FormattableString.Invariant($"tag {tag} ({fieldEn}) is not valid UTF-8; a lenient decoder would substitute U+FFFD and pass."));
        }
    }

    /// <summary>الطابع الزمني: تنسيق واحد وثقافة ثابتة و UTC صريح — عكس <see cref="ZatcaQr.Timestamp"/>.</summary>
    private static DateTimeOffset Timestamp(string value)
    {
        if (!DateTimeOffset.TryParseExact(
                value,
                "yyyy-MM-ddTHH:mm:ssZ",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            throw new ZatcaQrException(
                "الوسم 3 (الطابع الزمني) لا يطابق «yyyy-MM-ddTHH:mm:ssZ»: «" + value + "». "
                + "والقراءة بثقافة الجهاز تقبل تاريخاً هجرياً وتُعيد يوماً آخر. / "
                + "tag 3 (timestamp) does not match yyyy-MM-ddTHH:mm:ssZ: '" + value + "'.");
        }

        return parsed;
    }

    /// <summary>
    /// المبلغ <c>decimal</c> بثقافة ثابتة. الفاصلة العربية <c>U+066B</c> أو فاصل الآلاف
    /// أو الأسّ كلها رفض: كلها تُنتج رقماً «يُقرأ» ويختلف عمّا في الفاتورة.
    /// </summary>
    private static decimal Amount(string value, byte tag, string fieldAr, string fieldEn)
    {
        if (!decimal.TryParse(
                value,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out decimal parsed))
        {
            throw new ZatcaQrException(
                FormattableString.Invariant($"الوسم {tag} ({fieldAr}) ليس عدداً عشرياً بثقافة ثابتة: «{value}». / ")
                + FormattableString.Invariant($"tag {tag} ({fieldEn}) is not an invariant decimal: '{value}'."));
        }

        return parsed;
    }

    /// <summary>عكس تشكيل القيمة للوسمين 6 و7: القيمة نصّ مُرمَّز في الحالتين.</summary>
    private static string AsText(ReadOnlyMemory<byte> value, byte tag, QrValueForm form) => form switch
    {
        QrValueForm.Utf8Text => Text(value, tag, "قيمة نصّية", "text value"),
        QrValueForm.RawBinary => Convert.ToBase64String(value.Span),
        _ => throw new ArgumentOutOfRangeException(nameof(form), form, "شكل قيمة وسم غير معروف / unknown tag value form"),
    };

    /// <summary>عكس تشكيل القيمة للوسمين 8 و9: القيمة بايتات في الحالتين.</summary>
    private static ReadOnlyMemory<byte> AsBinary(ReadOnlyMemory<byte> value, byte tag, QrValueForm form) => form switch
    {
        QrValueForm.RawBinary => value,
        QrValueForm.Utf8Text => FromBase64(Text(value, tag, "قيمة ثنائية مُرمَّزة", "encoded binary value"), tag),
        _ => throw new ArgumentOutOfRangeException(nameof(form), form, "شكل قيمة وسم غير معروف / unknown tag value form"),
    };

    private static ReadOnlyMemory<byte> FromBase64(string text, byte tag)
    {
        try
        {
            return Convert.FromBase64String(text);
        }
        catch (FormatException)
        {
            throw new ZatcaQrException(
                FormattableString.Invariant($"الوسم {tag} مُعلَن نصّاً مُرمَّزاً وليس base64 صالحاً. / ")
                + FormattableString.Invariant($"tag {tag} is declared as encoded text but is not valid base64."));
        }
    }
}
