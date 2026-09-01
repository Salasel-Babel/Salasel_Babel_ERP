using System.Globalization;
using Babel.Contracts.Voice;

namespace Babel.Ai.Voice;

/// <summary>
/// <b>توجيهُ الخطّة — نصٌّ واحد يُعرض ويُنطَق، ولا يأذن بشيء.</b>
/// <para>
/// <b>ولماذا يوجد أصلاً:</b> التأكيدُ في هذا النظام <b>لكل خطوةٍ على حدة</b>، وهو
/// مفروضٌ لا مُختار — رمزُ التأكيد صورةٌ حتمية لأمرٍ واحد، ورمزٌ يُحسب مقدَّماً على قيمٍ
/// لم توجد بعد إمّا أن يُسقطها فيُبطل المعنى الذي وُجد <c>ConfirmationMismatch</c>
/// ليحميه، أو يُعاد حسابه فلا يكون تأكيداً واحداً. <b>لكن ثلاثةَ ملخّصاتٍ بلا جملةٍ
/// جامعة انحدارٌ في الفهم</b>: يسمع الإنسان ثلاثة أوامر ولا يسمع الخطّة. فهذا النصّ
/// يقول الخطّة كلَّها <b>مرّةً واحدة، قبل أن تبدأ</b>.
/// </para>
/// <para>
/// <b>وهو توجيهٌ لا إذن — وهذا هو الفرق الذي يجب ألّا يذوب.</b> لا يحمل رمزاً، ولا
/// يُصاحبه زرُّ تأكيد، ولا يُنفَّذ به شيء. وزرٌّ بجانبه يُعلّم الناس أن يضغطوا على ما
/// لم يقرأوه، فيصير التأكيدُ الحقيقي بعده عادةً لا قراءة.
/// </para>
/// <para>
/// <b>ويُقال فيه ما تطلبه الشاشةُ ولا يطلبه الصوت</b> — بأسمائه. فمن يسمع «وتطلب
/// شاشتُه رمزاً وحدَّ ائتمانٍ ومهلةَ سداد» يعرف قبل أن يبدأ أين ينتهي الكلام ويبدأ
/// الكتابة.
/// </para>
/// </summary>
public static class VoicePlanReadback
{
    /// <summary>الأرقام العربية-الهندية للترقيم — «الخطوة ٢ من ٣» تُقرأ كما تُكتب.</summary>
    private static readonly string[] Ordinals = ["٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩"];

    /// <summary>يرقّم عدداً صغيراً بالأرقام العربية-الهندية.</summary>
    /// <param name="value">العدد.</param>
    public static string Numeral(int value)
    {
        string digits = value.ToString(CultureInfo.InvariantCulture);
        System.Text.StringBuilder output = new(digits.Length);

        foreach (char digit in digits)
        {
            output.Append(char.IsAsciiDigit(digit) ? Ordinals[digit - '0'] : digit.ToString());
        }

        return output.ToString();
    }

    /// <summary>
    /// جملةُ التوجيه كاملةً — <b>نصٌّ واحد، جُمَلُه هي بعينها بنودُ القائمة المرقّمة على
    /// الشاشة</b>، فلا يسمع الأعمى غير ما يقرأ الأصمّ.
    /// </summary>
    /// <param name="plan">الخطّة.</param>
    /// <param name="steps">جُمَل الخطوات كما بناها <see cref="StepSentence"/>، بترتيبها.</param>
    public static string Arabic(VoicePlan plan, IReadOnlyList<string> steps)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(steps);

        string head = plan.NameAr + " — خطّة من " + Numeral(steps.Count) + " خطوات.";

        return steps.Count == 0
            ? head + " ولا يُرحَّل شيء بالصوت."
            : head + " " + string.Join(" ", steps) + " ولا يُرحَّل شيء بالصوت.";
    }

    /// <summary>
    /// جملةُ خطوةٍ واحدة داخل التوجيه: رقمُها، وغرضُها، وما تطلبه شاشتُها ولا يطلبه الصوت.
    /// </summary>
    /// <param name="step">الخطوة.</param>
    /// <param name="ordinal">ترتيبها من واحد.</param>
    public static string StepSentence(VoicePlanStep step, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(step);

        string sentence = "(" + Numeral(ordinal) + ") " + step.PurposeAr;

        return step.ScreenAsksForAr.Count == 0
            ? sentence
            : sentence + " وتطلب شاشتُه: " + string.Join("، ", step.ScreenAsksForAr) + ".";
    }

    /// <summary>
    /// ترويسةُ الملخّص المرتدّ لخطوةٍ بعينها — <b>تُلصَق أمام
    /// <see cref="VoiceReadback.Arabic"/> بلا تغييره</b>. فبوابةُ التأكيد ورمزُها يبقيان
    /// على أمرٍ واحد كما كانا.
    /// </summary>
    /// <param name="ordinal">ترتيب الخطوة من واحد.</param>
    /// <param name="total">عدد الخطوات.</param>
    public static string StepPrefix(int ordinal, int total) =>
        "الخطوة " + Numeral(ordinal) + " من " + Numeral(total) + " — ";
}
