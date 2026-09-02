using Babel.SharedKernel;

namespace Babel.Ai.Boundary;

/// <summary>
/// حكم الحدّ على نصّ. <b>حالتان لا ثالثة لهما</b> — ولا وجود لـ«مُنقَّح».
/// </summary>
public enum AgentScrubOutcome
{
    /// <summary>لا شكل معرّفٍ في النصّ؛ يعبر كما كتبه صاحبه.</summary>
    Clean = 1,

    /// <summary>شكلُ معرّفٍ واحد فأكثر؛ لا يُرسَل شيء، ويُقال للمستخدم ما وُجد.</summary>
    Refused = 2,
}

/// <summary>
/// <b>نتيجة الفحص — ولا ثالث لها.</b>
/// <para>
/// <b>لماذا لا يوجد <c>Redacted</c>:</b> لأنه لا يمكن إنشاؤه. النوع يحمل حالتين، ومن
/// أراد التنقيح لا يجد نوعاً يعبّر عنه. والقرار مُبرَّرٌ بسابقتين في هذا المستودع:
/// <c>ArabicNumerals</c> يرفض <c>١٢3</c> ولا يُطبّعه، و<c>VoiceErrors.DigitsAndWordsMixed</c>
/// يرفض «ألف و500» ولا يختار فرعاً. والتنقيح الصامت هو التطبيع الصامت نفسه، طبقةً أعلى.
/// </para>
/// <para>
/// <b>وثمنُ الخطأ غير متماثل:</b> رفضٌ كاذب يكلّف دورةً واحدة ويكتب المستخدم الرقم في
/// حقله على الشاشة؛ ورقمُ هويةٍ عبر لا يُستردّ. و<c>HrEmployee.identity</c> جُعل
/// <b>عاجزاً بنيوياً</b> عن حمل قيمةٍ غير مقنَّعة — فلا يصير الوكيلُ البابَ الذي أُغلق
/// في المخطّط.
/// </para>
/// </summary>
public sealed record AgentScrubVerdict
{
    internal AgentScrubVerdict(AgentScrubOutcome outcome, IReadOnlyList<Error> errors)
    {
        Outcome = outcome;
        Errors = errors;
    }

    /// <summary>الحكم.</summary>
    public AgentScrubOutcome Outcome { get; }

    /// <summary>
    /// أسباب الرفض — <b>واحدٌ لكل شكلٍ نطق</b>، بترتيب <see cref="AgentIdentifierShapes.All"/>،
    /// لا واحدٌ لكل مطابقة. وتُعاد كلّها لا أوّلها: من يُعيد الكتابة يريد أن يعرف كل ما
    /// وُجد في مرّة واحدة.
    /// </summary>
    public IReadOnlyList<Error> Errors { get; }

    /// <summary>هل عبر النصّ؟</summary>
    public bool IsClean => Outcome == AgentScrubOutcome.Clean;

    /// <summary>هل رُفض؟</summary>
    public bool IsRefused => Outcome == AgentScrubOutcome.Refused;
}
