using Babel.Ai.Boundary;
using Babel.SharedKernel;

namespace Babel.Ai.Voice;

/// <summary>
/// <b>حارس الإفشاء — ما يُنطَق يُسمعه من في الغرفة.</b>
/// <para>
/// وحدة الموارد البشرية تُخرج الهوية <b>مُقنَّعة</b>: آخر أربعة محارف وحدها، وما قبلها
/// نقاطٌ بعددٍ ثابت لا يساوي الطول (‏<c>EmployeeService.Mask</c>). والمسار المنطوق
/// <b>يشدّد</b> القاعدة ولا يخفّفها: الشاشة تُرى بزاويةٍ واحدة، والصوت يُسمع في الغرفة
/// كلّها — فقيمةٌ تخرج مُقنَّعة على السلك ويُعاد تركيبها في جملةٍ منطوقة تكون قد سُرِّبت
/// إلى من لا يملك الشاشة أصلاً.
/// </para>
/// <para>
/// <b>والحارس يعمل على النصّ المنطوق نفسه</b>، لا على نيّة من كتبه: يرفض أي سلسلةٍ
/// تشبه رقم هويةٍ أو آيباناً غير مُقنَّع، فيلتقط التسريب حتى لو جاء من مسارٍ لم يُقصد.
/// </para>
/// <para>
/// <b>ولا يحمل هذا الملفّ شكلَيه بنفسه بعد اليوم.</b> كان يحمل <c>SA[0-9]{22}</c>
/// المتّصل وحده، وهو <b>يفوته</b> الصيغة التي يكتبها الناس فعلاً —
/// <c>SA03 8000 0000 6080 1016 7519</c> — ويفوته الرقم المكتوب بأرقام عربية-هندية أو
/// بينه تطويل. فصار يقرأ <see cref="AgentIdentifierShapes.Iban"/> و
/// <see cref="AgentIdentifierShapes.DigitRun"/> أنفسهما: <b>تعريفٌ واحد لا تعريفان</b>،
/// لأن تعريفَين لِـ«ما شكلُه آيبان» ينحرفان — وقد انحرفا فعلاً، وهذه الفقرة سجلُّ
/// الانحراف. والقناع يمرّ من هنا كما كان يمرّ (الشكل السابع لا يُسأل عنه في هذا المسار):
/// المسار المنطوق يمنع <b>القيمة الكاملة</b>، والمِصفاة إلى النموذج تمنع <b>القناع أيضاً</b>
/// لأن قناعاً في نسخة محادثة يُعاد التعرّف به.
/// </para>
/// </summary>
public static class VoiceDisclosure
{
    /// <summary>القناع كما تكتبه وحدة الموارد البشرية: أربع نقاط ثم آخر أربعة محارف.</summary>
    public const string MaskPrefix = "••••";

    /// <summary>يُقنّع قيمةً شخصية بالقاعدة نفسها التي تُقنّع بها وحدة الموارد البشرية.</summary>
    /// <param name="value">القيمة.</param>
    public static string Mask(string? value) =>
        value is { Length: > 4 } ? MaskPrefix + value[^4..] : MaskPrefix;

    /// <summary>
    /// يفحص نصّاً قبل نُطقه أو عرضه. يُعيد فشلاً حين يحمل قيمةً شخصية غير مُقنَّعة.
    /// </summary>
    /// <param name="text">النصّ المُزمَع نُطقه.</param>
    public static Result Guard(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        List<Error> errors = [];

        if (AgentIdentifierShapes.Iban.Matches(text))
        {
            errors.Add(VoiceRefusals.MaskedReadRequired("رقم الآيبان"));
        }

        if (AgentIdentifierShapes.DigitRun.Matches(text))
        {
            errors.Add(VoiceRefusals.MaskedReadRequired("رقم الهوية"));
        }

        return errors.Count == 0 ? Result.Success() : Result.Failure(errors);
    }
}
