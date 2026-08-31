using System.Text.RegularExpressions;
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
/// </summary>
public static partial class VoiceDisclosure
{
    /// <summary>القناع كما تكتبه وحدة الموارد البشرية: أربع نقاط ثم آخر أربعة محارف.</summary>
    public const string MaskPrefix = "••••";

    /// <summary>رقم هويةٍ أو إقامة: تسع خانات فأكثر متتالية.</summary>
    [GeneratedRegex(@"(?<![0-9])[0-9]{9,}(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex IdentityShape();

    /// <summary>آيبان سعودي: ‏SA ثم اثنتان وعشرون خانة.</summary>
    [GeneratedRegex(@"(?<![A-Za-z0-9])SA[0-9]{22}(?![0-9])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex IbanShape();

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

        if (IbanShape().IsMatch(text))
        {
            errors.Add(VoiceRefusals.MaskedReadRequired("رقم الآيبان"));
        }

        if (IdentityShape().IsMatch(text))
        {
            errors.Add(VoiceRefusals.MaskedReadRequired("رقم الهوية"));
        }

        return errors.Count == 0 ? Result.Success() : Result.Failure(errors);
    }
}
