namespace Babel.Ai.Voice;

/// <summary>
/// <b>معجم الوحدات المنطوقة — مغلق.</b> يحوّل كلمةً عربية إلى رمز وحدةٍ لاتيني.
/// <para>
/// <b>وما لا يفعله هذا المعجم أهمّ ممّا يفعله:</b> لا يتحقّق أن الوحدة صالحةٌ لهذا
/// الصنف — لا يستطيع، ولا يجوز أن يستطيع: وحدةُ الذكاء لا تعرف وحدة المخزون
/// (القاعدة 3). فهو يقول «قيل كرتون» ولا يقول «للصنف كرتون». والتحقّق يقع في
/// الوحدة المالكة عند التنفيذ، على معامل التحويل المُعلَن للصنف نفسه.
/// </para>
/// <para>
/// <b>ولا وحدةَ افتراضية.</b> غيابُ الوحدة رفضٌ مُسمّى، لا سقوطٌ إلى وحدة الأساس:
/// «عشرين» في مستودعٍ فيه الصنف بالحبّة والكرتون فرقُها اثنا عشر ضعفاً، والفرق
/// يُضرب في تكلفة الوحدة فيصل إلى المال.
/// </para>
/// </summary>
public static class VoiceUnits
{
    private static readonly Dictionary<string, string> Lexicon = Build();

    private static Dictionary<string, string> Build()
    {
        Dictionary<string, string> source = new(StringComparer.Ordinal)
        {
            ["حبة"] = "EA", ["حبات"] = "EA", ["حبه"] = "EA", ["قطعة"] = "EA", ["قطع"] = "EA", ["وحدة"] = "EA",
            ["علبة"] = "BOX", ["علب"] = "BOX", ["صندوق"] = "BOX", ["صناديق"] = "BOX",
            ["كرتون"] = "CTN", ["كراتين"] = "CTN", ["كرتونة"] = "CTN",
            ["كيس"] = "BAG", ["أكياس"] = "BAG", ["شوال"] = "BAG",
            ["طبلية"] = "PAL", ["بالتة"] = "PAL", ["بالت"] = "PAL",
            ["كيلو"] = "KG", ["كيلوجرام"] = "KG", ["كجم"] = "KG",
            ["طن"] = "TON", ["أطنان"] = "TON",
            ["لتر"] = "L", ["لترات"] = "L",
            ["متر"] = "M", ["أمتار"] = "M",
            ["متر مربع"] = "M2", ["مربع"] = "M2",
            ["متر مكعب"] = "M3", ["مكعب"] = "M3", ["مكعبة"] = "M3",
            ["يوم"] = "DAY", ["أيام"] = "DAY",
            ["ساعة"] = "HR", ["ساعات"] = "HR",
            ["لفة"] = "ROL", ["رول"] = "ROL",
        };

        Dictionary<string, string> keyed = new(StringComparer.Ordinal);
        foreach ((string word, string code) in source)
        {
            keyed[VoiceText.Fold(word)] = code;
        }

        return keyed;
    }

    /// <summary>عدد المداخل — يقرؤه حارس اللافراغ.</summary>
    public static int Count => Lexicon.Count;

    /// <summary>هل هذه الكلمة وحدة؟</summary>
    /// <param name="word">الكلمة كما نُطقت.</param>
    public static bool IsUnit(string word) => word is not null && Lexicon.ContainsKey(VoiceText.Fold(word));

    /// <summary>رمز الوحدة، أو <c>null</c> إن لم تكن في المعجم المغلق.</summary>
    /// <param name="word">الكلمة.</param>
    public static string? CodeOf(string word) =>
        word is not null && Lexicon.TryGetValue(VoiceText.Fold(word), out string? code) ? code : null;

    /// <summary>
    /// رمز وحدةٍ من كلمتين — <b>وهو ليس ترفاً</b>: «متر مكعب» و«متر مربع» و«متر» ثلاث
    /// وحدات مختلفة تبدأ بالكلمة نفسها، وقراءةُ الأولى منها «متراً» تُدخل خرسانةً
    /// بمقدارٍ يقلّ عن الحقيقة بمرتبتين.
    /// </summary>
    /// <param name="first">الكلمة الأولى.</param>
    /// <param name="second">الكلمة الثانية.</param>
    public static string? CodeOfPair(string first, string second) =>
        first is not null && second is not null
        && Lexicon.TryGetValue(VoiceText.Fold(first) + " " + VoiceText.Fold(second), out string? code)
            ? code
            : null;
}
