using System.Globalization;
using System.Text;
using Babel.Ai.Voice;

namespace Babel.Ai.Boundary;

/// <summary>
/// <b>ما يفعله الحدّ بالنصّ <u>قبل</u> أن يفحصه — ولا شيء ممّا يفعله يخرج إلى النموذج.</b>
/// <para>
/// المعرّف الذي يُكتب كما هو يلتقطه أيّ نمط. والذي يهرب هو المعرّف <b>المشوَّه</b>:
/// ‏<c>١٠٩٢٨٣٧٤٦٥</c> بأرقام عربية-هندية، و<c>1092ـ837465</c> بتطويل بينها، و
/// <c>1092‍837465</c> بواصلٍ عديم العرض، و<c>1092 837465</c> مقطوعاً بمسافة.
/// أربع صور لرقمٍ واحد، ونمطٌ يقرأ اللاتيني المتّصل وحده يمرّرها كلّها.
/// </para>
/// <para>
/// <b>والدرس المدفوع ثمنه مرّتين: فئةٌ لا قائمة.</b> كانت هذه المحارف قائمةً يدوية من
/// ستّة عشر محرفاً، فكان <c>1092&#x00AD;837465</c> بشرطةٍ ليّنة (‏<c>U+00AD</c>) — وهي ما
/// يدسّه <b>لصقٌ عاديّ من PDF أو Word</b> لا مهاجم — يعبر <b>نظيفاً</b> على كل مسار.
/// وكذلك <c>U+2060</c> و<c>U+2061‑2064</c> و<c>U+180E</c> و<c>U+034F</c> ومحدِّدات الصور
/// ‏<c>U+FE00+</c> ومحارف C0/C1 كلّها. والمستودع كان قد كتب العلاج نصّاً في
/// <c>TextRules</c>: «الاعتماد على الفئة لا على قائمة يدوية يغلق الثغرة التي تتركها
/// القوائم المُعدَّدة». فالطيّ من اليوم يقرأ <b>فئة يونيكود</b>
/// (‏<c>Cf · Cc · Mn · Me · Mc · Cs · Co · Cn</c>) ولا يقرأ جدولاً يكتبه إنسان.
/// </para>
/// <para>
/// <b>وكذلك الأرقام:</b> كان الطيّ يعرف أربعة أنظمة، فكان رقمُ هويةٍ بأرقام
/// <b>عريضة</b> (‏<c>U+FF10-FF19</c> — ضغطةُ مفتاحٍ واحدة في أي مُدخِل ياباني، ولا يُفرَّق
/// عنها بالعين) أو بنغالية أو تايلندية يعبر نظيفاً. فصار الطيّ يردّ <b>كل رقمٍ عشريّ في
/// يونيكود</b> (‏<c>Nd</c>) إلى نظيره اللاتيني، ويوحّد بـ<c>NFKC</c> لا <c>NFC</c> —
/// وطبقتان لا واحدة، فلو صار التوحيد لا-عملية في بيئةٍ بلا عولمة بقي ردّ الأرقام قائماً.
/// و<see cref="ArabicNumerals.SystemOf(char)"/> يبقى المرجع لأنظمته الأربعة، والطيّ
/// <b>أوسع منه بحكم البناء</b> — وهذا هو الاتجاه الصحيح لتفاوتٍ بين كاشفٍ ومُطبِّع.
/// </para>
/// <para>
/// <b>ولماذا يُطبَّع هنا وهذا المستودع يرفض التطبيع الصامت:</b> القاعدة المكتوبة في
/// <c>ArabicNumerals</c> — «الرفض لا التطبيع الصامت» — قاعدةُ <b>مخزنٍ ومطابقة</b>: ما
/// يُخزَّن أو يُجزَّأ أو يُطابَق به موردٌ لا يُحوَّل بصمت، لأن المحوَّل يصير غير ما كتبه
/// الإنسان. وهذا الملفّ <b>ليس مخزناً ولا مطابقةً</b>: ناتجه يُفحَص ثم يُرمى، والذي يخرج
/// إلى النموذج هو <b>النصّ الأصلي كما كتبه صاحبه حرفاً بحرف</b>. والطيّ عند <b>كاشف</b>
/// يزيد ما يُلتقط ولا ينقصه — وأسوأ أثره إنذارٌ كاذب ثمنه دورةٌ واحدة؛ والطيّ عند
/// <b>مخزن</b> يُنتج قيمةً لا يعرف قائلُها أنه قالها. فالفرق في الموضع لا في الذوق.
/// </para>
/// </summary>
internal static class AgentBoundaryText
{
    /// <summary>التطويل <c>U+0640</c> — زينةٌ خطّية بلا معنى، وفاصلٌ ممتاز لمن يُخفي رقماً.</summary>
    public const char Tatweel = 'ـ';

    /// <summary>
    /// محارف التحكّم الاتجاهي وعرض الصفر التي <b>تُعدّدها الوحدات المالكة</b> —
    /// ‏<c>SaudiVatNumber.InvisibleControls</c> و<c>ComplianceText.InvisibleControls</c>.
    /// وهي مُعادة هنا لأن كليهما <b>خارج ما تستطيع هذه الوحدة الإشارة إليه</b> (القاعدة 3:
    /// ‏<c>Babel.Ai</c> لا ترى <c>Babel.Purchasing</c> ولا <c>Babel.Compliance</c>)،
    /// والتكرار مقصود ومحروس باختبار اتّفاقٍ يقرأ الحقل الأصلي بالانعكاس.
    /// <para>
    /// <b>وهي ليست ما يطويه هذا الملفّ، بل الحدّ الأدنى منه.</b> الطيّ يستعمل
    /// <see cref="IsStripped(System.Text.Rune)"/> — وهو أوسع بحكم الفئة — وحارسٌ يُثبت
    /// أن كل محرفٍ في هذه القائمة مطويٌّ فعلاً، فيبقى الاتّفاق حمّالاً ولا يصير زينة.
    /// </para>
    /// </summary>
    public static readonly char[] InvisibleControls =
    [
        '‎', '‏', '؜',
        '‪', '‫', '‬', '‭', '‮',
        '⁦', '⁧', '⁨', '⁩',
        '​', '‌', '‍', '﻿',
    ];

    /// <summary>
    /// <b>الشرطات والشرطة السفلية</b> — تُلمّ للأشكال <b>المُرتكِزة</b> وحدها (آيبان · ضريبي ·
    /// جوّال)، ولا تُلمّ لشكلَي «عشر خانات مجرّدة».
    /// <para>
    /// <b>والسبب مقيس على نصٍّ حقيقي:</b> رقم الفاتورة <c>INV-2026-000412</c> يصير بعد لمّ
    /// الشرطات <c>2026000412</c> — عشر خانات تبدأ بـ<c>2</c>، أي «رقم هوية» مزعوم. ورقمُ
    /// أمرٍ أو فاتورةٍ بهذا الشكل أكثر ورودًا في نظام محاسبة بما لا يُقاس من هويةٍ يكتبها
    /// صاحبها بشرطات. أمّا <c>050-123-4567</c> فيبقى ملتقَطاً لأن الجوّال <b>مُرتكِز</b>
    /// ببادئته.
    /// </para>
    /// </summary>
    public static readonly char[] DashJoiners =
    [
        '-', '‐', '‑', '‒', '–', '—', '_',
    ];

    /// <summary>
    /// <b>النقطة والفاصلة وفاصل الآلاف العربي والمائلة</b> — تُلمّ <b>للأشكال المُرتكِزة
    /// وحدها</b> ولا تُلمّ لغيرها أبداً.
    /// <para>
    /// <b>والتمييز هو بعينه حجّة صاحب الشرطات مطبَّقةً على النقطة:</b> لمّها في شكلٍ
    /// <b>مجرَّد</b> يحوّل مبلغاً مكتوباً <c>12,345,678.90</c> إلى عشر خانات متّصلة تبدأ
    /// بـ<c>1</c>، ويحوّل تاريخاً <c>01/09/2026</c> إلى سلسلة — وكلاهما نصٌّ عادي في نظام
    /// محاسبة. أمّا في شكلٍ <b>مُرتكِز</b> فلا: <c>SA03.8000.0000.6080.1016.7519</c> اثنتان
    /// وعشرون خانة خلف <c>SA</c>، و<c>05.12.34.56.78</c> جوّالٌ ببادئته، و
    /// <c>300.123.456.789.003</c> خمس عشرة خانة بين ٣ و٣. <b>ولا مبلغَ ولا تاريخَ يتنكّر
    /// في أيٍّ من الثلاثة</b> — فالثمن الذي بُرِّرَ به الاستثناء غيرُ مستحقٍّ هنا،
    /// وتركُ الباب مفتوحاً كان تناقضاً مع مبدأ الملفّ نفسه لا تطبيقاً له.
    /// </para>
    /// </summary>
    public static readonly char[] AnchoredOnlyJoiners = ['.', ',', '٬', '،', '/'];

    /// <summary>
    /// <b>هل يُنزع هذا المحرف قبل الفحص؟</b> — بالفئة لا بجدول.
    /// <para>
    /// ‏<c>Cf</c> تضمّ الشرطة الليّنة وواصل الكلمات وكل التحكّم الاتجاهي؛ و<c>Mn/Me/Mc</c>
    /// تضمّ التشكيل ومحدِّدات الصور وواصل العناقيد؛ و<c>Cc</c> تضمّ محارف التحكّم — عدا
    /// ما هو فراغٌ منها، فذاك <b>يُلمّ</b> لا يُنزع كي يبقى قطعُ السطر بين خانتين مُلتقَطاً
    /// بالقاعدة نفسها التي تلتقط المسافة. و<c>Cn/Co/Cs</c> غيرُ المُسنَدة والخاصّة
    /// والبديلة المفردة تُنزع لأنها لا تحمل معنى يُقرأ ولا يجوز أن تحمل فاصلاً.
    /// </para>
    /// </summary>
    /// <param name="rune">نقطة الترميز.</param>
    public static bool IsStripped(Rune rune)
    {
        if (rune.Value == Tatweel)
        {
            return true;
        }

        // الفراغ يُلمّ ولا يُنزع: نزعُه هنا كان يجعل «١٢٣٤٥\n٦٧٨٩٠» عشر خاناتٍ في
        // الطبقة الأولى — أي إنذاراً كاذباً على جدولٍ عاديّ — بينما لمُّه يُبقيه محكوماً
        // باحتمال القطع الذي يُقنَّن لكل شكلٍ على حدة.
        if (IsWhitespaceJoiner(rune))
        {
            return false;
        }

        return Rune.GetUnicodeCategory(rune) is UnicodeCategory.Format
            or UnicodeCategory.Control
            or UnicodeCategory.NonSpacingMark
            or UnicodeCategory.EnclosingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.Surrogate
            or UnicodeCategory.PrivateUse
            or UnicodeCategory.OtherNotAssigned;
    }

    /// <summary>
    /// <b>الفراغ بكل أنواعه</b> — تفصل خانتين ولا تفصل عددين، فتُلمّ لكل شكلٍ يحتمل القطع.
    /// <para>
    /// وكانت خمسة محارف أفقية، فكان <c>1092&#x0009;837465</c> بجدولةٍ و<c>1092\n837465</c>
    /// بقطع سطرٍ يعبران نظيفَين — <b>وقطعُ السطر بين خانتين هو ما يقع فعلاً في جسم
    /// <c>tool_result</c></b>، وهو الموضع الذي يسمّيه هذا الملفّ نفسه أخطر المواضع.
    /// والمجموعة الكاملة كانت مكتوبة في التجميعة نفسها
    /// (‏<c>ArabicNameFold.IsFoldedWhitespace</c>)، فصارت هنا فئةً لا نسخة.
    /// </para>
    /// </summary>
    /// <param name="rune">نقطة الترميز.</param>
    public static bool IsWhitespaceJoiner(Rune rune)
        => rune.Value is >= 0x0009 and <= 0x000D or 0x0085
        || Rune.GetUnicodeCategory(rune) is UnicodeCategory.SpaceSeparator
            or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator;

    /// <summary>
    /// يطوي النصّ للفحص وحده: توحيد <c>NFKC</c>، ثم نزع ما تنزعه
    /// <see cref="IsStripped(System.Text.Rune)"/>، ثم ردّ كل رقمٍ عشريّ في يونيكود إلى
    /// نظيره اللاتيني. <b>الناتج لا يُخزَّن ولا يُرسَل.</b>
    /// </summary>
    /// <param name="text">النصّ كما ورد.</param>
    public static string Fold(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string normalised;
        try
        {
            // ‏**NFKC لا NFC**: التوافقيّ وحده يطبق الأرقام العريضة وأشكال العرض العربية
            // على أصولها. ولا يُبنى عليه وحده — ردّ الأرقام أدناه طبقةٌ ثانية مستقلّة.
            normalised = text.Normalize(NormalizationForm.FormKC);
        }
        catch (ArgumentException)
        {
            // نصّ يحمل نقطة ترميز غير صالحة: يُفحص كما ورد. ولا يُرمى الاستثناء إلى
            // الأعلى — حارسٌ يسقط بخطأ برمجي عند نصٍّ مشوَّه يصير باباً لا حارساً.
            normalised = text;
        }

        StringBuilder folded = new(normalised.Length);
        Span<char> buffer = stackalloc char[2];

        foreach (Rune rune in normalised.EnumerateRunes())
        {
            if (IsStripped(rune))
            {
                continue;
            }

            int digit = DecimalDigitOf(rune);
            if (digit >= 0)
            {
                folded.Append((char)('0' + digit));
                continue;
            }

            int written = rune.EncodeToUtf16(buffer);
            folded.Append(buffer[..written]);
        }

        return folded.ToString();
    }

    /// <summary>
    /// قيمة الرقم العشريّ في <b>أي</b> نظام كتابة، أو <c>-1</c> إن لم يكن رقماً عشرياً.
    /// <b>ولا تُقرأ من جدول</b>: الفئة <c>Nd</c> هي التعريف، فنظامُ كتابةٍ يُضاف إلى
    /// يونيكود غداً مطويٌّ اليوم.
    /// </summary>
    /// <param name="rune">نقطة الترميز.</param>
    public static int DecimalDigitOf(Rune rune)
        => Rune.GetUnicodeCategory(rune) == UnicodeCategory.DecimalDigitNumber
            ? (int)Rune.GetNumericValue(rune)
            : -1;

    /// <summary>
    /// يلمّ الخانات المقطوعة: يحذف كل فاصلٍ يقع <b>بين خانتين</b>، ويترك ما عداه.
    /// <c>«SA03 8000 …»</c> و<c>«1092 837465»</c> يعودان متّصلين، و<c>«سجّل 100 قطعة»</c>
    /// لا يتغيّر لأن ما بعد الفاصل ليس خانة.
    /// </summary>
    /// <param name="folded">نصٌّ مطويّ بـ<see cref="Fold(string)"/>.</param>
    /// <param name="tolerance">أيّ فواصل تُلمّ.</param>
    public static string Join(string folded, AgentSplitTolerance tolerance)
    {
        ArgumentNullException.ThrowIfNull(folded);

        if (tolerance is not (AgentSplitTolerance.Whitespace
            or AgentSplitTolerance.WhitespaceAndDashes
            or AgentSplitTolerance.AnchoredSeparators))
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "لا لمَّ بلا احتمال قطع.");
        }

        StringBuilder joined = new(folded.Length);
        int index = 0;

        while (index < folded.Length)
        {
            char current = folded[index];

            if (IsJoiner(current, tolerance) && joined.Length > 0 && char.IsAsciiDigit(joined[^1]))
            {
                int after = index;
                while (after < folded.Length && IsJoiner(folded[after], tolerance))
                {
                    after++;
                }

                if (after < folded.Length && char.IsAsciiDigit(folded[after]))
                {
                    index = after;
                    continue;
                }
            }

            joined.Append(current);
            index++;
        }

        return joined.ToString();
    }

    /// <summary>
    /// هل يُلمّ هذا المحرف عند هذه الدرجة؟ والدرجات تراكمية: الأوسع يضمّ الأضيق.
    /// <b>ويُقرأ على محرفٍ واحد بأمان</b> لأن كل الفواصل — الفراغ والشرطات والنقطة —
    /// في المستوى الأساسي، فلا يقع فاصلٌ في زوج بدائل.
    /// </summary>
    private static bool IsJoiner(char character, AgentSplitTolerance tolerance)
    {
        if (char.IsSurrogate(character))
        {
            return false;
        }

        Rune rune = new(character);

        if (IsWhitespaceJoiner(rune))
        {
            return true;
        }

        if (tolerance == AgentSplitTolerance.Whitespace)
        {
            return false;
        }

        if (Array.IndexOf(DashJoiners, character) >= 0)
        {
            return true;
        }

        return tolerance == AgentSplitTolerance.AnchoredSeparators
            && Array.IndexOf(AnchoredOnlyJoiners, character) >= 0;
    }
}
