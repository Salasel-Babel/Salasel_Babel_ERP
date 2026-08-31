using Babel.Contracts.Capture;
using Babel.Contracts.Voice;

namespace Babel.Ai.Voice;

/// <summary>
/// <b>الملخّص المرتدّ — وهو نصّ واحد يُقرأ ويُعرض معاً.</b>
/// <para>
/// <b>ولماذا نصٌّ واحد لا اثنان:</b> نصٌّ للنطق ونصٌّ للشاشة ينحرفان، فيسمع الأعمى
/// جملةً ويرى الأصمّ أخرى، ويؤكّد كلٌّ منهما ما لم يؤكّده الآخر. والتأكيد على أمرٍ
/// لم يُعرَض كاملاً ليس تأكيداً.
/// </para>
/// <para>
/// <b>ورمز التأكيد صورةٌ حتمية للأمر بعينه</b> — لا رقم عشوائي: تغيّرَ الأمرُ بعد
/// قراءته فتغيّر الرمز، فيُرفض التأكيد القديم بدل أن يُنفَّذ أمرٌ لم يسمعه أحد.
/// </para>
/// <para>
/// <b>وما لا يُقنَّع هنا:</b> ما نطقه المستخدم قبل ثانية. تقنيعُ اسمٍ قاله بصوته يجعل
/// الملخّص عديم الفائدة («الموظف: ••••امدي») ولا يحمي أحداً — فالغرفة سمعته أصلاً.
/// والتقنيع موضعُه <b>الجواب</b> لا الأمر، ويحرسه <see cref="VoiceDisclosure"/> على
/// كل نصٍّ يُنطَق.
/// </para>
/// </summary>
public static class VoiceReadback
{
    /// <summary>ما يُقال بعد الملخّص لكل عمليةٍ تُغيّر الحال.</summary>
    public const string ConfirmCallAr = "قل «تأكيد» أو اضغط زرّ التأكيد.";

    /// <summary>نظيرها الإنجليزي.</summary>
    public const string ConfirmCallEn = "Say 'confirm' or press the confirm button.";

    /// <summary>يبني الملخّص العربي.</summary>
    /// <param name="intent">النيّة.</param>
    /// <param name="slots">الشرائح الممتلئة.</param>
    public static string Arabic(VoiceIntent intent, IReadOnlyList<SpokenSlotValue> slots)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(slots);

        List<string> parts = [];

        foreach (SpokenSlotValue value in slots)
        {
            VoiceSlot? slot = intent.Slots.FirstOrDefault(candidate => candidate.Name == value.Name);
            string label = slot?.NameAr ?? value.Name;
            string unit = value.Unit is null ? string.Empty : " " + value.Unit;
            string source = value.Provenance == FieldProvenance.Defaulted ? " (من الإعدادات)" : string.Empty;
            parts.Add(label + ": " + value.Text + unit + source);
        }

        string body = parts.Count == 0 ? "بلا شرائح" : string.Join("، ", parts);
        string head = intent.NameAr + " — " + body + ".";

        return intent.RequiresConfirmation ? head + " " + ConfirmCallAr : head;
    }

    /// <summary>يبني الملخّص الإنجليزي.</summary>
    /// <param name="intent">النيّة.</param>
    /// <param name="slots">الشرائح.</param>
    public static string English(VoiceIntent intent, IReadOnlyList<SpokenSlotValue> slots)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(slots);

        List<string> parts = [];

        foreach (SpokenSlotValue value in slots)
        {
            VoiceSlot? slot = intent.Slots.FirstOrDefault(candidate => candidate.Name == value.Name);
            string label = slot?.NameEn ?? value.Name;
            string unit = value.Unit is null ? string.Empty : " " + value.Unit;
            parts.Add(label + ": " + value.Text + unit);
        }

        string body = parts.Count == 0 ? "no slots" : string.Join(", ", parts);
        string head = intent.NameEn + " — " + body + ".";

        return intent.RequiresConfirmation ? head + " " + ConfirmCallEn : head;
    }

    /// <summary>
    /// رمز التأكيد: صورةٌ نصّية مرتَّبة للأمر. <b>الترتيب باسم الشريحة</b> كي لا يتغيّر
    /// الرمز لأن المتكلّم قدّم المبلغ على التاريخ.
    /// </summary>
    /// <param name="intent">النيّة.</param>
    /// <param name="slots">الشرائح.</param>
    public static string Token(VoiceIntent intent, IReadOnlyList<SpokenSlotValue> slots)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(slots);

        IEnumerable<string> ordered = slots
            .OrderBy(static value => value.Name, StringComparer.Ordinal)
            .Select(static value => value.Name + "=" + value.Text + (value.Unit is null ? string.Empty : ":" + value.Unit));

        return intent.Id + "|" + string.Join(";", ordered);
    }
}
