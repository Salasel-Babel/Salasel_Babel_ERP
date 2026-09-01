using Babel.SharedKernel;

namespace Babel.Ai.Voice;

/// <summary>
/// أعطال <b>بناء</b> سجلّ الأسماء — تقع مرّةً عند التركيب لا عند كل نُطق، وتُسقط
/// التركيب. ودليلُ أسماءٍ نصفُه صالح أسوأ من دليلٍ يرفض أن يُبنى: الأوّل يحدّ الأسماء
/// تسعاً وتسعين مرّة ثم يكتب اسماً لم يُسجَّل.
/// </summary>
public static class VoiceEntityErrors
{
    /// <summary>دليلٌ لا يسمّي سجلّاً.</summary>
    /// <param name="module">الوحدة المُسهِمة.</param>
    public static Error DirectoryNamesNoRegister(string module) => new(
        "ai.voice.entities.directory_names_no_register",
        "الوحدة «" + module + "» تُسهم بدليل أسماء ولا تسمّي السجلّ الذي تنتمي إليه. "
        + "ودليلٌ بلا سجلّ لا يستطيع أن يحدّ شريحةً بعينها، فيصير أسماءً معلّقة في الهواء.",
        "Module '" + module + "' contributes a name directory that names no register.");

    /// <summary>اسمٌ فارغ في دليل.</summary>
    /// <param name="kind">السجلّ.</param>
    public static Error NameEmpty(string kind) => new(
        "ai.voice.entities.name_empty",
        "دليل «" + kind + "» يحمل اسماً فارغاً. والاسم الفارغ يطابق بادئةَ كل نافذة، "
        + "فيحدّ كل شريحةٍ عند الصفر ويجعل كلَّ طرفٍ مجهولاً.",
        "The '" + kind + "' directory carries an empty name, which prefixes every window.");

    /// <summary>اسمٌ يحمل مقطعاً رقمياً — رقم حسابٍ يعبر في ثوب اسم (القاعدة 2).</summary>
    /// <param name="kind">السجلّ.</param>
    /// <param name="name">الاسم.</param>
    public static Error NameCarriesALedgerCode(string kind, string name) => new(
        "ai.voice.entities.name_carries_a_ledger_code",
        "دليل «" + kind + "» يحمل «" + name + "» وفيه مقطع رقمي. "
        + "والقاعدة 2 تمنع الوحدة من تسمية حساب، ودليلُ الأسماء ليس استثناءً منها.",
        "The '" + kind + "' directory carries '" + name + "', which holds a numeric segment.");
}
