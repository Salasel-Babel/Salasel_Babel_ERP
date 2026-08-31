using System.Globalization;
using System.Text;

namespace Babel.Canonicalization;

/// <summary>
/// قواعد النص المُوقَّع.
///
/// <b>المبدأ المعماري الذي تقوم عليه كل هذه المكتبة:</b>
///
/// <code>
///   الحدّ (boundary)  : CleanForInput()   يُحوِّل.  يُستدعى مرّة واحدة عند دخول البيانات.
///   المُجزِّئ (hasher) : RequireCanonical() يتحقّق فقط. لا يُحوِّل أبداً.
/// </code>
///
/// لماذا هذا الفصل ليس تجميلاً:
/// لو طبّع المُجزِّئ النص عند التجزئة، لصار بإمكان أي شخص تعديل النص <b>المخزَّن</b>
/// بطريقة يمحوها التطبيع (إدراج U+200F، تفكيك الهمزة، إضافة BOM) دون أن تتغيّر البصمة.
/// عندها لا تربط البصمة ما هو مخزَّن، بل تربط «صورة» عنه — وهذا بالضبط ما يجعل
/// تنفيذات XMLDSig تسقط.
///
/// <b>القاعدة: البصمة تربط البايتات المخزَّنة نفسها، حرفاً بحرف.</b>
/// ولذلك المُجزِّئ <b>يرفض</b> ولا يُصلح.
///
/// The hasher validates and never transforms. If it normalised, an attacker could
/// edit stored text in ways normalisation erases and the hash would still verify:
/// the hash would bind a projection of the record, not the record.
/// </summary>
public static class TextRules
{
    /// <summary>سقف طول النص المُوقَّع بالمحارف. حدّ عاقل يمنع مستندات مُفخّخة.</summary>
    public const int MaxTextLength = 65_536;

    // =====================================================================
    //  الفحص — يُستدعى من المُجزِّئ. لا يُحوِّل.
    // =====================================================================

    /// <summary>
    /// يتحقّق أن النص صالح للتجزئة كما هو، أو يرمي. <b>لا يُعدّل النص إطلاقاً.</b>
    /// يعيد النص نفسه ليسهل التسلسل في الاستدعاءات.
    /// </summary>
    public static string RequireCanonical(string value, string? field = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        CanonicalRuntime.EnsureSupported();

        if (value.Length > MaxTextLength)
            throw new CanonicalizationException(CanonErrors.TextTooLong,
                $"طول النص {value.Length} يتجاوز الحدّ {MaxTextLength}.", -1, field);

        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];

            // --- أزواج البدائل: يجب أن تكون مكتملة، وإلا انفجر Normalize لاحقاً ---
            if (char.IsSurrogate(ch))
            {
                if (!char.IsHighSurrogate(ch) || i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                    throw new CanonicalizationException(CanonErrors.TextLoneSurrogate,
                        $"بديل غير مقترن U+{(int)ch:X4}. String.Normalize يرمي ArgumentException عليه.", i, field);

                var cp = char.ConvertToUtf32(ch, value[i + 1]);
                RequireCodePointAllowed(cp, i, field);
                i++;
                continue;
            }

            RequireCodePointAllowed(ch, i, field);
        }

        // --- NFC: يُفحص، ولا يُطبَّق ---
        // ملاحظة: IsNormalized نفسه يكذب في وضع العولمة الثابتة؛ ولذلك
        // CanonicalRuntime.EnsureSupported() أعلاه شرط لازم قبل الوصول إلى هنا.
        if (!value.IsNormalized(NormalizationForm.FormC))
            throw new CanonicalizationException(CanonErrors.TextNotNfc,
                "النص ليس بالشكل NFC. طبّعه عند الحدّ بـ TextRules.CleanForInput ثم خزّن الشكل المطبَّع. " +
                "المُجزِّئ لا يطبّع، لأن التطبيع عند التجزئة يفكّ ارتباط البصمة بما هو مخزَّن.",
                -1, field);

        return value;
    }

    private static void RequireCodePointAllowed(int cp, int index, string? field)
    {
        // ---- U+0000: لا تستطيع PostgreSQL تخزينه في text أصلاً (مقيس: 22021) ----
        if (cp == 0)
            throw new CanonicalizationException(CanonErrors.TextNul,
                "المحرف U+0000 ممنوع: PostgreSQL ترفض تخزينه في نوع text (خطأ 22021)، " +
                "فتصير القيمة مُجزَّأة وغير قابلة للتخزين معاً.", index, field);

        // ---- CR: سطر جديد له شكل لفظي واحد فقط وهو LF ----
        if (cp == '\r')
            throw new CanonicalizationException(CanonErrors.TextCarriageReturn,
                "المحرف U+000D (CR) ممنوع. نهايات الأسطر مُوحَّدة إلى LF وحده؛ استخدم CleanForInput عند الحدّ. " +
                "وإلا فإن نفس البيان المكتوب على Windows وعلى Linux يعطي بصمتين.", index, field);

        // ---- بقية محارف التحكم C0/C1 عدا LF ----
        if (cp != '\n' && (cp < 0x20 || (cp >= 0x7F && cp <= 0x9F)))
            throw new CanonicalizationException(CanonErrors.TextControlChar,
                $"محرف تحكّم U+{cp:X4} ممنوع في نص مُوقَّع (المسموح من C0 هو LF وحده).", index, field);

        // ---- التطويل U+0640 وأشكال الألف: مسموحة عمداً. انظر ArabicSearch. ----

        // ---- فئة Cf كاملة: كل محارف التحكّم الاتجاهي وغير المرئية ----
        // U+200E, U+200F, U+202A..U+202E, U+2066..U+2069, U+061C, U+200B..U+200D,
        // U+FEFF, U+00AD ... جميعها Cf. الاعتماد على الفئة لا على قائمة يدوية
        // يغلق الثغرة التي تتركها القوائم المُعدَّدة.
        var cat = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(cp), 0);
        if (cat == UnicodeCategory.Format)
            throw new CanonicalizationException(CanonErrors.TextFormatControl,
                $"محرف تنسيق غير مرئي U+{cp:X4} ({DescribeFormatChar(cp)}) ممنوع في نص مُوقَّع. " +
                "السياسة: رفض عند الحدّ، لا إزالة عند التجزئة — الإزالة تجعل إدراج هذه المحارف " +
                "في نص مخزَّن غير مكشوف. استخدم TextRules.CleanForInput عند إدخال البيانات.",
                index, field);

        // ---- فواصل الأسطر والفقرات في Unicode: نهاية سطر ثالثة غير LF ----
        if (cat is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
            throw new CanonicalizationException(CanonErrors.TextControlChar,
                $"فاصل سطر/فقرة Unicode U+{cp:X4} ممنوع. نهاية السطر الوحيدة هي LF. " +
                "U+2028 و U+2029 يأتيان من لصق محتوى من محرّرات نصوص ومن HTML.", index, field);

        // ---- مسافات غير ASCII: فرق غير مرئي بصرياً، ويصمد عبر PostgreSQL (مقيس) ----
        if (cat == UnicodeCategory.SpaceSeparator && cp != 0x20)
            throw new CanonicalizationException(CanonErrors.TextNonAsciiSpace,
                $"مسافة غير ASCII U+{cp:X4} ممنوعة. المسافة الوحيدة المسموحة هي U+0020. " +
                "U+00A0 تأتي من النسخ من Word وتبدو مطابقة تماماً على الشاشة.", index, field);

        // ---- أرقام عربية-هندية وشرقية: تبدو صحيحة وتُجزَّأ خطأ ----
        if (IsNonAsciiDigit(cp))
            throw new CanonicalizationException(CanonErrors.TextNonAsciiDigit,
                $"رقم غير ASCII U+{cp:X4} ممنوع في حقل مُوقَّع. العرض بالأرقام العربية-الهندية " +
                "شأن طبقة العرض وحدها؛ المخزَّن والمُجزَّأ بأرقام ASCII.", index, field);

        // ---- أشكال العرض العربية: تصمد عبر NFC ولا يصلحها إلا NFKC (مقيس) ----
        // U+FEFB (ﻻ) يبقى كما هو بعد NFC، وNFKC وحده يفكّه إلى U+0644 U+0627.
        // مصدره الشائع: النسخ من ملفات PDF.
        if (IsArabicPresentationForm(cp))
            throw new CanonicalizationException(CanonErrors.TextPresentationForm,
                $"شكل عرض عربي U+{cp:X4} ممنوع. هذه الأشكال تصمد أمام NFC ولا يفكّها إلا NFKC؛ " +
                "مصدرها المعتاد النسخ من PDF، وهي مطابقة بصرياً للحروف العادية. " +
                "CleanForInput يفكّها.", index, field);

        // ---- محارف غير مخصّصة: تعريفها قد يتغيّر مع إصدار Unicode القادم ----
        if (IsNoncharacter(cp))
            throw new CanonicalizationException(CanonErrors.TextNoncharacter,
                $"محرف غير حرف (noncharacter) U+{cp:X4} ممنوع.", index, field);

        if (cat == UnicodeCategory.PrivateUse)
            throw new CanonicalizationException(CanonErrors.TextPrivateUse,
                $"محرف من نطاق الاستخدام الخاص U+{cp:X4} ممنوع: معناه يعتمد على الخط المستخدم.",
                index, field);
    }

    private static string DescribeFormatChar(int cp) => cp switch
    {
        0x061C => "ARABIC LETTER MARK",
        0x200B => "ZERO WIDTH SPACE",
        0x200C => "ZERO WIDTH NON-JOINER",
        0x200D => "ZERO WIDTH JOINER",
        0x200E => "LEFT-TO-RIGHT MARK",
        0x200F => "RIGHT-TO-LEFT MARK",
        0x202A => "LEFT-TO-RIGHT EMBEDDING",
        0x202B => "RIGHT-TO-LEFT EMBEDDING",
        0x202C => "POP DIRECTIONAL FORMATTING",
        0x202D => "LEFT-TO-RIGHT OVERRIDE",
        0x202E => "RIGHT-TO-LEFT OVERRIDE",
        0x2066 => "LEFT-TO-RIGHT ISOLATE",
        0x2067 => "RIGHT-TO-LEFT ISOLATE",
        0x2068 => "FIRST STRONG ISOLATE",
        0x2069 => "POP DIRECTIONAL ISOLATE",
        0x00AD => "SOFT HYPHEN",
        0xFEFF => "ZERO WIDTH NO-BREAK SPACE / BOM",
        _ => "Cf"
    };

    /// <summary>هل هذه نقطة رمز رقم عشري خارج ASCII؟</summary>
    public static bool IsNonAsciiDigit(int cp)
        => CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(cp), 0) == UnicodeCategory.DecimalDigitNumber
           && !(cp >= '0' && cp <= '9');

    /// <summary>أشكال العرض العربية أ و ب (بما فيها الروابط مثل U+FEFB).</summary>
    public static bool IsArabicPresentationForm(int cp)
        => (cp >= 0xFB50 && cp <= 0xFDFF) || (cp >= 0xFE70 && cp <= 0xFEFE);

    private static bool IsNoncharacter(int cp)
        => (cp >= 0xFDD0 && cp <= 0xFDEF) || ((cp & 0xFFFE) == 0xFFFE);

    /// <summary>هل يمرّ النص من <see cref="RequireCanonical"/> بلا رفض؟</summary>
    public static bool IsCanonical(string value)
    {
        try { RequireCanonical(value); return true; }
        catch (CanonicalizationException) { return false; }
    }

    /// <summary>يعيد أول مشكلة في النص أو <c>null</c> — للاستخدام في رسائل التحقق للمستخدم.</summary>
    public static CanonicalizationException? Inspect(string value, string? field = null)
    {
        try { RequireCanonical(value, field); return null; }
        catch (CanonicalizationException ex) { return ex; }
    }

    // =====================================================================
    //  التحويل — يُستدعى عند الحدّ، مرّة واحدة، قبل التخزين. لا يُستدعى عند التجزئة.
    // =====================================================================

    /// <summary>
    /// <b>تنظيف الحدّ.</b> يُستدعى مرّة واحدة، عند دخول القيمة من الواجهة أو الاستيراد،
    /// و<b>يُخزَّن ناتجه</b>. ثم لا يُستدعى ثانية أبداً على قيمة مخزَّنة موقَّعة.
    ///
    /// ما يفعله، بهذا الترتيب بالضبط:
    ///   1. يوحّد نهايات الأسطر: CRLF و CR منفرداً و U+2028 و U+2029 -> LF.
    ///   2. يحذف كل محارف فئة Cf (بما فيها U+200E/U+200F/U+202A–U+202E وBOM).
    ///   3. يحوّل كل مسافة Zs غير U+0020 إلى U+0020.
    ///   4. يحوّل الأرقام العربية-الهندية والشرقية إلى أرقام ASCII.
    ///   5. يفكّ أشكال العرض العربية بـ NFKC <b>على تلك المحارف وحدها</b>
    ///      (NFKC على النص كله يدمّر أشياء أخرى، مثل ﷼ -> «ريال»).
    ///   6. يحذف محارف التحكّم الأخرى عدا LF.
    ///   7. يطبّع النتيجة إلى NFC.
    ///
    /// كل خطوة هنا <b>تغيّر بيانات المستخدم</b> — ولذلك تُستدعى قبل التوقيع فقط،
    /// ويُعرض الناتج للمستخدم قبل الحفظ حيثما أمكن.
    /// </summary>
    public static string CleanForInput(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        CanonicalRuntime.EnsureSupported();

        var sb = new StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];

            // 1. نهايات الأسطر
            if (ch == '\r')
            {
                if (i + 1 < value.Length && value[i + 1] == '\n') i++;
                sb.Append('\n');
                continue;
            }
            if (ch == '\n') { sb.Append('\n'); continue; }

            int cp;
            var width = 1;
            if (char.IsHighSurrogate(ch) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                cp = char.ConvertToUtf32(ch, value[i + 1]);
                width = 2;
            }
            else if (char.IsSurrogate(ch))
            {
                // بديل غير مقترن: يُحذف. لا يمكن تخزينه ولا تطبيعه.
                continue;
            }
            else
            {
                cp = ch;
            }

            i += width - 1;

            var cat = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(cp), 0);

            // 2. Cf يُحذف
            if (cat == UnicodeCategory.Format) continue;

            // 6. بقية محارف التحكم تُحذف
            if (cp < 0x20 || (cp >= 0x7F && cp <= 0x9F)) continue;

            // محارف غير أحرف واستخدام خاص تُحذف
            if (IsNoncharacter(cp) || cat == UnicodeCategory.PrivateUse) continue;

            // 3. المسافات وفواصل الأسطر
            if (cat is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
            { sb.Append('\n'); continue; }
            if (cat == UnicodeCategory.SpaceSeparator) { sb.Append(' '); continue; }

            // 4. الأرقام
            if (cp >= 0x0660 && cp <= 0x0669) { sb.Append((char)('0' + (cp - 0x0660))); continue; }
            if (cp >= 0x06F0 && cp <= 0x06F9) { sb.Append((char)('0' + (cp - 0x06F0))); continue; }
            if (IsNonAsciiDigit(cp))
            {
                var d = CharUnicodeInfo.GetDecimalDigitValue(char.ConvertFromUtf32(cp), 0);
                if (d >= 0) { sb.Append((char)('0' + d)); continue; }
            }

            // 5. أشكال العرض العربية: NFKC على هذا المحرف وحده
            if (IsArabicPresentationForm(cp))
            {
                sb.Append(char.ConvertFromUtf32(cp).Normalize(NormalizationForm.FormKC));
                continue;
            }

            sb.Append(char.ConvertFromUtf32(cp));
        }

        // 7. NFC
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// نسخة من <see cref="CleanForInput"/> تُبلّغ عمّا غيّرته — لعرضه للمستخدم
    /// قبل الحفظ، ولتسجيله في سجل التدقيق.
    /// </summary>
    public static (string Cleaned, IReadOnlyList<string> Changes) CleanForInputVerbose(string value)
    {
        var cleaned = CleanForInput(value);
        var changes = new List<string>();
        if (cleaned == value) return (cleaned, changes);

        if (value.Contains('\r')) changes.Add("توحيد نهايات الأسطر إلى LF");
        var seen = new HashSet<int>();
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (char.IsHighSurrogate(ch) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1])) i++;
            int cp = char.IsHighSurrogate(ch) && i < value.Length && char.IsLowSurrogate(value[i])
                ? char.ConvertToUtf32(ch, value[i]) : ch;
            if (!seen.Add(cp)) continue;
            var cat = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(cp), 0);
            if (cat == UnicodeCategory.Format) changes.Add($"حذف محرف تنسيق غير مرئي U+{cp:X4} ({DescribeFormatChar(cp)})");
            else if (cat == UnicodeCategory.SpaceSeparator && cp != 0x20) changes.Add($"استبدال مسافة U+{cp:X4} بمسافة ASCII");
            else if (IsNonAsciiDigit(cp)) changes.Add($"تحويل رقم U+{cp:X4} إلى رقم ASCII");
            else if (IsArabicPresentationForm(cp)) changes.Add($"تفكيك شكل عرض عربي U+{cp:X4}");
        }
        if (!value.IsNormalizedSafe()) changes.Add("تطبيع إلى NFC");
        return (cleaned, changes);
    }

    private static bool IsNormalizedSafe(this string s)
    {
        try { return s.IsNormalized(NormalizationForm.FormC); }
        catch (ArgumentException) { return false; }
    }
}
