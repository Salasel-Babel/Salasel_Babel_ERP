using System.Globalization;
using System.Text;

namespace Babel.Compliance.Canonical;

/// <summary>
/// سياسة النص عند حدّ الالتزام. <b>تُطبَّق مرة واحدة عند الدخول، ويُخزَّن الشكل المطبَّع</b>،
/// ثم تُجزَّأ البايتات المخزَّنة. التطبيع عند التجزئة وحدها خطأ: يجعل ما في القاعدة
/// مختلفاً عمّا وُقِّع عليه.
/// <para/>
/// ثلاث قواعد، كلها مقيسة في 02-architecture §8.3:
/// <list type="number">
///   <item>NFC: «أ» بالشكل المركّب U+0623 وبالشكل المفكّك U+0627+U+0654 يجب أن تعطيا البصمة نفسها.</item>
///   <item>محارف التحكم الاتجاهي (U+200E/U+200F/U+202A–U+202E …) تُزال — غير مرئية وتغيّر البصمة.</item>
///   <item>الأرقام العربية-الهندية والشرقية <b>تُرفض</b> في الحقول المُجزَّأة، لا تُحوَّل بصمت.</item>
/// </list>
/// </summary>
public static class ComplianceText
{
    /// <summary>محارف التحكم الاتجاهي وعرض الصفر التي تتسلل من طبقات الواجهة ومن النسخ واللصق.</summary>
    public static readonly char[] InvisibleControls =
    [
        '‎', '‏', '؜',
        '‪', '‫', '‬', '‭', '‮',
        '⁦', '⁧', '⁨', '⁩',
        '​', '‌', '‍', '﻿'
    ];

    public static bool ContainsInvisibleControl(string s) => s.AsSpan().IndexOfAny(InvisibleControls) >= 0;

    /// <summary>الأرقام العربية-الهندية U+0660–U+0669 والشرقية U+06F0–U+06F9.</summary>
    public static bool ContainsNonAsciiDigit(string s)
    {
        foreach (var ch in s)
            if ((ch >= '٠' && ch <= '٩') || (ch >= '۰' && ch <= '۹'))
                return true;
        return false;
    }

    /// <summary>
    /// التطبيع عند الحدّ. يُستدعى مرة واحدة على المستند الوارد، ويُخزَّن الناتج.
    /// يرمي عند وجود رقم غير ASCII — الرفض مقصود، لأن التحويل الصامت يجعل مبلغاً
    /// مكتوباً بأرقام هندية يُجزَّأ بشكل مختلف عمّا يراه المستخدم.
    /// </summary>
    public static string Normalise(string? value, string fieldName)
    {
        var s = value ?? string.Empty;
        if (ContainsNonAsciiDigit(s))
            throw new CanonicalisationException(
                $"الحقل «{fieldName}» يحتوي أرقاماً عربية-هندية أو شرقية. " +
                "الحقول المُجزَّأة تقبل أرقام ASCII فقط؛ العرض بالأرقام الهندية شأن طبقة العرض وحدها. / " +
                $"Field '{fieldName}' carries Arabic-Indic or Eastern-Arabic digits; hashed fields accept ASCII digits only.");

        var normalised = s.Normalize(NormalizationForm.FormC);
        if (!ContainsInvisibleControl(normalised)) return normalised;

        var sb = new StringBuilder(normalised.Length);
        foreach (var ch in normalised)
            if (Array.IndexOf(InvisibleControls, ch) < 0) sb.Append(ch);
        return sb.ToString();
    }

    /// <summary>
    /// <b>الفخّ ع-4:</b> التطبيع للبحث (الهمزات، التاء المربوطة، التطويل، التشكيل)
    /// شيء آخر تماماً، ونتيجته تذهب في <b>عمود منفصل</b>. تشغيله على الحقل الموقَّع يكسر السلسلة.
    /// هذه الدالة موجودة هنا فقط لتحمل التحذير في مكان يراه من يبحث عن «تطبيع».
    /// </summary>
    public static string SearchFold(string value) =>
        throw new NotSupportedException(
            "تطبيع البحث لا يُطبَّق على الحقول الموقَّعة أبداً (02-architecture §8.3 ع-4). " +
            "يعيش في وحدة البحث وفي عمود منفصل. / Search folding must never touch signed fields.");
}

public sealed class CanonicalisationException(string message) : Exception(message);
