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
/// <b>ولا ملخّص إنجليزيّ بجانبه.</b> الملخّص نصّ عرض، والعربية سجلُّه؛ وزوجٌ ثابت
/// <c>ar</c>/<c>en</c> في حقل عرض ممنوعٌ بنصّ ADR-0021 §6.3 بند 2 وتفرضه القاعدة 14.
/// ولغةٌ ثالثة تُضاف صفّاً في جدول الترجمات، لا عموداً ثانياً هنا — وهذا بالضبط ما
/// يعجز عنه الزوج الثابت بنيوياً.
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

    /// <summary>وسمُ الطرف الذي طابق صفّاً واحداً في السجلّ المحلّي.</summary>
    public const string FromYourRegisterAr = "من سجلّك";

    /// <summary>وسمُ الطرف الذي لم يُحلّ بعد — يُعرض ولا يُبتلع.</summary>
    public const string NotResolvedYetAr = "لم يُحلّ بعد";

    /// <summary>وسمُ الطرف الذي وُرِقت له ورقةُ سؤال. <b>ولا يُقال كم كان المرشّحون.</b></summary>
    public const string WhichOneAr = "أيّهم تقصد؟";

    /// <summary>يبني الملخّص العربي.</summary>
    /// <param name="intent">النيّة.</param>
    /// <param name="slots">الشرائح الممتلئة.</param>
    public static string Arabic(VoiceIntent intent, IReadOnlyList<SpokenSlotValue> slots)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(slots);

        return Compose(intent, [.. slots.Select(value => Part(intent, value))]);
    }

    /// <summary>
    /// يبني الملخّص من القراءات كلّها — <b>والأطراف تُوسَم ولا تُخفى</b>.
    /// <para>
    /// وطرفٌ حُلّ يظهر بالمقطع الذي قاله المستخدم ومعه «<c>من سجلّك</c>»، وطرفٌ معلَّق
    /// يظهر بمقطعه ومعه «<c>لم يُحلّ بعد</c>». <b>ولا يُخترع اسمٌ من السجلّ هنا</b>:
    /// المنفذ الذي يُعيد أسماءً محظورٌ على هذا المشروع بحارسٍ قائم، وتسميةُ الصفّ
    /// المطابَق على الشاشة فعلُ طبقة التركيب.
    /// </para>
    /// </summary>
    /// <param name="intent">النيّة.</param>
    /// <param name="slots">الشرائح الممتلئة.</param>
    /// <param name="readings">القراءات كلّها.</param>
    public static string Arabic(
        VoiceIntent intent,
        IReadOnlyList<SpokenSlotValue> slots,
        IReadOnlyDictionary<string, SlotReading> readings)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(readings);

        List<string> parts = [];

        foreach (VoiceSlot slot in intent.Slots)
        {
            if (!readings.TryGetValue(slot.Name, out SlotReading? reading))
            {
                continue;
            }

            switch (reading)
            {
                case SlotReading.Filled filled:
                    parts.Add(Part(intent, filled.Value));
                    break;

                case SlotReading.Resolved resolved:
                    parts.Add(slot.NameAr + ": " + resolved.Span.Text + " (" + FromYourRegisterAr + ")");
                    break;

                case SlotReading.Pending pending:
                    parts.Add(slot.NameAr + ": " + pending.Span.Text + " (" + NotResolvedYetAr + ")");
                    break;

                case SlotReading.Asked asked:
                    parts.Add(slot.NameAr + ": " + asked.Span.Text + " (" + WhichOneAr + ")");
                    break;

                default:
                    break;
            }
        }

        return Compose(intent, parts);
    }

    private static string Part(VoiceIntent intent, SpokenSlotValue value)
    {
        VoiceSlot? slot = intent.Slots.FirstOrDefault(candidate => candidate.Name == value.Name);
        string label = slot?.NameAr ?? value.Name;
        string unit = value.Unit is null ? string.Empty : " " + value.Unit;
        string source = value.Provenance == FieldProvenance.Defaulted ? " (من الإعدادات)" : string.Empty;
        return label + ": " + value.Text + unit + source;
    }

    private static string Compose(VoiceIntent intent, List<string> parts)
    {
        string body = parts.Count == 0 ? "بلا شرائح" : string.Join("، ", parts);
        string head = intent.NameAr + " — " + body + ".";

        return intent.RequiresConfirmation ? head + " " + ConfirmCallAr : head;
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

    /// <summary>
    /// رمز التأكيد من القراءات كلّها. <b>وحالةُ الطرف جزءٌ منه</b>: معلَّقٌ يكتب مقطعه،
    /// ومحلولٌ يكتب مِقبضه — فحلُّ شريحةٍ <b>يغيّر الرمز</b> ويُبطل تأكيداً قيل قبله.
    /// وذلك مقصود: تأكيدٌ على أمرٍ لم يكن قد اكتمل ليس تأكيداً.
    /// </summary>
    /// <param name="intent">النيّة.</param>
    /// <param name="readings">القراءات.</param>
    public static string Token(VoiceIntent intent, IReadOnlyDictionary<string, SlotReading> readings)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(readings);

        List<string> ordered = [];

        foreach (string name in readings.Keys.OrderBy(static key => key, StringComparer.Ordinal))
        {
            switch (readings[name])
            {
                case SlotReading.Filled filled:
                    ordered.Add(name + "=" + filled.Value.Text
                        + (filled.Value.Unit is null ? string.Empty : ":" + filled.Value.Unit));
                    break;

                case SlotReading.Resolved resolved:
                    ordered.Add(name + "@" + resolved.Handle);
                    break;

                case SlotReading.Pending pending:
                    ordered.Add(name + "?" + pending.Span.Text);
                    break;

                case SlotReading.Asked asked:
                    ordered.Add(name + "!" + asked.QuestionId);
                    break;

                default:
                    break;
            }
        }

        return intent.Id + "|" + string.Join(";", ordered);
    }
}
