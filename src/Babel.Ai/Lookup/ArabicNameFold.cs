using System.Text;

namespace Babel.Ai.Lookup;

/// <summary>
/// <b>طيّ الاسم العربي قبل مقارنته — والطيّ للبحث وحده، لا للتوقيع ولا للترحيل.</b>
/// <para>
/// التحذير مكتوب أصلاً في هذا المستودع، في
/// <c>Babel.Compliance.Canonical.ComplianceText.SearchFold</c>: دالّةٌ لا تفعل شيئاً إلا أن
/// ترمي، ونصُّها «تطبيع البحث لا يُطبَّق على الحقول الموقَّعة أبداً… يعيش في وحدة البحث وفي
/// <b>عمود منفصل</b>». وهذا هو ذلك العمود المنفصل وتلك الوحدة.
/// </para>
/// <para>
/// <b>ولماذا الطيّ أصلاً؟ لأن التشابه الثلاثي الخام يُخطئ أسماءً صحيحة.</b> مقيس على هذا
/// الجهاز (‏PostgreSQL 16.13، <c>pg_trgm 1.6</c>) قبل الطيّ وبعده:
/// <c>أحمد~احمد</c> ‏0.250 → 1.000 · <c>آدم~ادم</c> ‏0.143 → 1.000 ·
/// <c>مُحَمَّدٌ~محمد</c> ‏0.071 → 1.000 · <c>محمــــد~محمد</c> ‏0.300 → 1.000 ·
/// <c>فاطمة~فاطمه</c> ‏0.500 → 1.000 · <c>يحيى~يحيي</c> ‏0.429 → 1.000 ·
/// <c>مؤسسة~موسسه</c> ‏0.091 → 1.000 · <c>شركة المسار الامثل~شركة المسار الأمثل</c> ‏0.700 → 1.000.
/// أي أن <c>أحمد</c> بهمزةٍ واحدة كان <b>يسقط دون العتبة الافتراضية 0.3 فلا يُعثر عليه إطلاقاً</b>،
/// وذلك عطلٌ صامت: لا رسالة، بل «لا نتائج».
/// </para>
/// <para>
/// <b>وما يبقى مفروقاً بعد الطيّ يبقى مفروقاً — مقيس كذلك:</b>
/// <c>محمد علي القحطاني~محمد القحطاني</c> ‏0.778 · <c>القحطاني~القحطان</c> ‏0.700 ·
/// <c>محمد القحطاني~محمد الغامدي</c> ‏0.350 · <c>الرياض~رياض</c> ‏0.333. فالطيّ يوحّد
/// <b>الرسم</b> ولا يوحّد <b>الأسماء</b>، ولذلك لا تُنزع أداة التعريف «ال».
/// </para>
/// <para>
/// <b>وهذه النسخة بلغة C# لا تقرّر مطابقةً واحدة.</b> المطابقة تجري كلّها في قاعدة البيانات
/// بدالّة <c>babel.fold_arabic</c>، فتعريفٌ واحد يحكم العمود المخزَّن ونصّ الاستعلام معاً.
/// هذه النسخة لقاعدة «السبر» (مفتاحان أحدهما بادئة الآخر) ولاختبارات لا تحتاج قاعدة بيانات —
/// <b>ويحرسها إثباتٌ يُشغّل الاثنتين على متن واحد ويطلب تطابقاً حرفياً</b>. فتعريفان لا
/// يتّفقان أسوأ من تعريفٍ واحد ناقص.
/// </para>
/// </summary>
public static class ArabicNameFold
{
    /// <summary>
    /// المحارف غير المرئية التي تُحذف قبل أي مقارنة — نفس المجموعة التي يعدّدها
    /// <c>SaudiVatNumber.InvisibleControls</c> و<c>ComplianceText.InvisibleControls</c>.
    /// <para>
    /// ولا يُستدعى أيٌّ منهما: الأول <c>internal</c> في <c>Babel.Purchasing</c> والثاني في
    /// <c>Babel.Compliance</c>، ولا تستطيع <c>Babel.Ai</c> الإشارة إلى واحدةٍ منهما
    /// (القاعدة 3). <b>والتكرار مقصود ومحروس</b> بإثباتٍ يقارن المجموعتين.
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
    /// يطوي النصّ إلى مفتاح البحث: تطبيع NFC، ثم حذف التطويل والتشكيل والمحارف غير
    /// المرئية، ثم توحيد الألف والهمزات والتاء المربوطة والألف المقصورة، ثم توحيد
    /// الأرقام العربية-الهندية والشرقية إلى اللاتينية، ثم خفض اللاتينية، ثم طيّ الفراغ.
    /// </summary>
    /// <param name="value">النصّ كما كتبه المستخدم أو كما هو في السجلّ.</param>
    /// <returns>مفتاح البحث. لا يُخزَّن في حقل موقَّع ولا يُعرض للمستخدم.</returns>
    public static string Fold(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string normalised = value.Normalize(NormalizationForm.FormC);
        StringBuilder output = new(normalised.Length);
        bool pendingSpace = false;

        foreach (char character in normalised)
        {
            if (IsRemoved(character))
            {
                continue;
            }

            if (IsFoldedWhitespace(character))
            {
                // الفراغ يُطوى إلى فراغٍ واحد، ولا يُكتب قبل أوّل محرف — فلا حاجة إلى تشذيبٍ بعدُ.
                pendingSpace = output.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                output.Append(' ');
                pendingSpace = false;
            }

            output.Append(Map(character));
        }

        return output.ToString();
    }

    /// <summary>
    /// المفتاح الضيّق: الطيّ نفسه وقد نُزع منه كل فراغ.
    /// <para>
    /// <b>وهو ليس ترفاً:</b> مقيس أن <c>عبدالله~عبد الله</c> يبقى عند 0.545 بعد الطيّ
    /// الكامل — دون أي عتبةٍ معقولة — ويصير تساوياً تامّاً على هذا المفتاح. وهو أشيع
    /// اختلافٍ في الأسماء السعودية المركّبة.
    /// </para>
    /// </summary>
    /// <param name="value">النصّ كما ورد.</param>
    public static string FoldTight(string value) => Fold(value).Replace(" ", string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// هل مفتاحُ أحدِهما بادئةٌ صارمة للآخر؟ <b>علامةُ سبرٍ لا علامةُ تشابه</b>:
    /// «محمد» ثم «محمد ع» ثم «محمد عل» بحثٌ ثنائي في السجلّ، لا استفسارٌ عن اسم.
    /// <para>
    /// القاعدة نفسها تُطبَّق في حالة الدور (‏<c>AgentTurnState</c>) وليست من مِلك هذا الملفّ؛
    /// وهو يقدّم القياس وحده.
    /// </para>
    /// </summary>
    /// <param name="first">النصّ الأول.</param>
    /// <param name="second">النصّ الثاني.</param>
    public static bool OneFoldsToAStrictPrefixOfTheOther(string first, string second)
    {
        string a = Fold(first);
        string b = Fold(second);

        return a.Length != b.Length
            && (a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal));
    }

    /// <summary>محارف تُحذف: التطويل، والتشكيل، والعلامات القرآنية، وغير المرئي.</summary>
    private static bool IsRemoved(char character)
        => character == 'ـ'                            // التطويل
        || character is >= 'ً' and <= 'ٟ'          // الحركات والشدّة والسكون
        || character == 'ٰ'                             // الألف الخنجرية
        || character is >= 'ۖ' and <= 'ۭ'          // العلامات القرآنية
        || Array.IndexOf(InvisibleControls, character) >= 0;

    /// <summary>
    /// توحيد الرسم: الألف وأخواتها، والياء والهاء والواو، والأرقام، واللاتينية.
    /// <para>
    /// <b>والخفض على ‏ASCII وحدها عمداً، لا <c>lower()</c>.</b> مقيس على هذا الجهاز:
    /// <c>lower(text)</c> مُعلَنة <c>IMMUTABLE</c> في <c>pg_proc</c> (‏<c>provolatile = 'i'</c>)
    /// <b>وهي مع ذلك تابعة لترتيب المقارنة</b> — فيقبلها PostgreSQL في عمودٍ مولَّد مخزَّن
    /// بلا اعتراض، ويصير محتوى العمود والفهرس المبنيّ عليه تابعاً لترتيب مقارنة القاعدة.
    /// نسخةٌ تُستعاد على خادمٍ بترتيبٍ آخر تحمل مفاتيح مختلفة <b>بلا خطأ ولا سطر سجلّ</b>.
    /// و<c>translate</c> على ‏ASCII بديلٌ لا يعرف الترتيب أصلاً.
    /// </para>
    /// </summary>
    private static char Map(char character) => character switch
    {
        'أ' or 'إ' or 'آ' or 'ٱ' or 'ٲ' or 'ٳ' => 'ا', // أ إ آ ٱ ٲ ٳ → ا
        'ى' => 'ي',                                                             // ى → ي
        'ة' => 'ه',                                                             // ة → ه
        'ؤ' => 'و',                                                             // ؤ → و
        'ئ' => 'ي',                                                             // ئ → ي
        >= '٠' and <= '٩' => (char)('0' + (character - '٠')),               // ٠-٩
        >= '۰' and <= '۹' => (char)('0' + (character - '۰')),               // ۰-۹
        >= 'A' and <= 'Z' => (char)(character + 32),                             // اللاتينية إلى الصغيرة، ‏ASCII وحدها
        _ => character,
    };

    /// <summary>
    /// الفراغ الذي يُطوى — <b>مُعدَّد صراحةً، لا <c>char.IsWhiteSpace</c> ولا <c>\s</c></b>.
    /// <para>
    /// المجموعتان ليستا واحدة: <c>\s</c> في تعبير PostgreSQL النمطي هو الفراغ اللاتيني
    /// وحده، و<c>char.IsWhiteSpace</c> يشمل U+00A0 و U+2000–U+200A و U+3000. فتعريفٌ لكلٍّ
    /// بلغته يُنتج مفتاحين مختلفين لنصٍّ واحد فيه مسافةٌ غير فاصلة — وهي أشيع ما يُلصَق من
    /// مستندٍ أو من صفحة. والقائمة هنا هي بعينها ما يطابقه صنف المحارف في نصّ الهجرة.
    /// </para>
    /// </summary>
    private static bool IsFoldedWhitespace(char character)
        => character is >= '\u0009' and <= '\u000D'
        || character is '\u0020' or '\u0085' or '\u00A0' or '\u1680'
        || character is >= '\u2000' and <= '\u200A'
        || character is '\u2028' or '\u2029' or '\u202F' or '\u205F' or '\u3000';
}
