using System.Buffers;
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
/// <b>ولماذا يُطبَّع هنا وهذا المستودع يرفض التطبيع الصامت:</b> القاعدة المكتوبة في
/// <c>ArabicNumerals</c> — «الرفض لا التطبيع الصامت» — قاعدةُ <b>مخزنٍ ومطابقة</b>: ما
/// يُخزَّن أو يُجزَّأ أو يُطابَق به موردٌ لا يُحوَّل بصمت، لأن المحوَّل يصير غير ما كتبه
/// الإنسان. وهذا الملفّ <b>ليس مخزناً ولا مطابقةً</b>: ناتجه يُفحَص ثم يُرمى، والذي يخرج
/// إلى النموذج هو <b>النصّ الأصلي كما كتبه صاحبه حرفاً بحرف</b>. والطيّ عند <b>كاشف</b>
/// يزيد ما يُلتقط ولا ينقصه — وأسوأ أثره إنذارٌ كاذب ثمنه دورةٌ واحدة؛ والطيّ عند
/// <b>مخزن</b> يُنتج قيمةً لا يعرف قائلُها أنه قالها. فالفرق في الموضع لا في الذوق.
/// </para>
/// <para>
/// وجدول أنظمة الأرقام <b>لا يُعاد كتابته هنا</b>: يُقرأ من <see cref="ArabicNumerals.SystemOf(char)"/>
/// — الأنظمة الأربعة نفسها (لاتيني · عربي-هندي · فارسي موسَّع · ديفاناغري).
/// </para>
/// </summary>
internal static class AgentBoundaryText
{
    /// <summary>التطويل <c>U+0640</c> — زينةٌ خطّية بلا معنى، وفاصلٌ ممتاز لمن يُخفي رقماً.</summary>
    public const char Tatweel = '\u0640';

    /// <summary>
    /// محارف التحكّم الاتجاهي وعرض الصفر — <b>المجموعة نفسها</b> التي يُعدّدها
    /// <c>SaudiVatNumber.InvisibleControls</c> و<c>ComplianceText.InvisibleControls</c>.
    /// وهي مُعادة هنا لأن كليهما <b>خارج ما تستطيع هذه الوحدة الإشارة إليه</b> (القاعدة 3:
    /// ‏<c>Babel.Ai</c> لا ترى <c>Babel.Purchasing</c> ولا <c>Babel.Compliance</c>)،
    /// والتكرار مقصود ومحروس باختبار اتّفاقٍ يقرأ الحقل الأصلي بالانعكاس.
    /// </summary>
    public static readonly char[] InvisibleControls =
    [
        '\u200E', '\u200F', '\u061C',
        '\u202A', '\u202B', '\u202C', '\u202D', '\u202E',
        '\u2066', '\u2067', '\u2068', '\u2069',
        '\u200B', '\u200C', '\u200D', '\uFEFF',
    ];

    /// <summary>
    /// <b>المسافات بأنواعها</b> — تفصل خانتين ولا تفصل عددين، فتُلمّ لكل شكلٍ يحتمل القطع.
    /// </summary>
    public static readonly char[] WhitespaceJoiners =
    [
        '\u0020', '\u00A0', '\u2007', '\u2009', '\u202F',
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
        '\u002D', '\u2010', '\u2011', '\u2012', '\u2013', '\u2014', '\u005F',
    ];

    /// <summary>
    /// <b>ما ليس فاصلاً وإن بدا كذلك:</b> النقطة والفاصلة وفاصل الآلاف العربي <c>U+066C</c>
    /// والشرطة المائلة. لمّها يحوّل مبلغاً مكتوباً <c>12,345,678.90</c> إلى عشر خانات
    /// متّصلة تبدأ بـ<c>1</c>؛ ويحوّل تاريخاً <c>01/09/2026</c> إلى سلسلة. وكلاهما نصٌّ
    /// عادي في نظام محاسبة، وإنذارٌ كاذب عليه ثمنٌ يُدفع كل يوم.
    /// </summary>
    public static readonly char[] NotJoiners = ['.', ',', '\u066C', '/'];

    private static readonly SearchValues<char> Invisible = SearchValues.Create(InvisibleControls);

    private static readonly SearchValues<char> Whitespace = SearchValues.Create(WhitespaceJoiners);

    private static readonly SearchValues<char> WhitespaceAndDashes =
        SearchValues.Create([.. WhitespaceJoiners, .. DashJoiners]);

    /// <summary>
    /// يطوي النصّ للفحص وحده: توحيد <c>NFC</c>، ثم نزع غير المرئي والتطويل، ثم ردّ كل
    /// رقمٍ في أي نظام إلى نظيره اللاتيني. <b>الناتج لا يُخزَّن ولا يُرسَل.</b>
    /// </summary>
    /// <param name="text">النصّ كما ورد.</param>
    public static string Fold(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string normalised;
        try
        {
            normalised = text.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            // نصّ يحمل نقطة ترميز غير صالحة: يُفحص كما ورد. ولا يُرمى الاستثناء إلى
            // الأعلى — حارسٌ يسقط بخطأ برمجي عند نصٍّ مشوَّه يصير باباً لا حارساً.
            normalised = text;
        }

        StringBuilder folded = new(normalised.Length);

        foreach (char character in normalised)
        {
            if (character == Tatweel || Invisible.Contains(character))
            {
                continue;
            }

            int system = ArabicNumerals.SystemOf(character);
            folded.Append(system > 0 ? (char)('0' + (character - system)) : character);
        }

        return folded.ToString();
    }

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

        SearchValues<char> joiners = tolerance switch
        {
            AgentSplitTolerance.Whitespace => Whitespace,
            AgentSplitTolerance.WhitespaceAndDashes => WhitespaceAndDashes,
            _ => throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "لا لمَّ بلا احتمال قطع."),
        };

        StringBuilder joined = new(folded.Length);
        int index = 0;

        while (index < folded.Length)
        {
            char current = folded[index];

            if (joiners.Contains(current) && joined.Length > 0 && char.IsAsciiDigit(joined[^1]))
            {
                int after = index;
                while (after < folded.Length && joiners.Contains(folded[after]))
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
}
