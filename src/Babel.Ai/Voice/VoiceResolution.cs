using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Ai.Voice;

/// <summary>
/// ما فهمه المحرّك من جملةٍ واحدة: النيّة، وما امتلأ، وما نقص، والملخّص المرتدّ، ورمزه.
/// <para>
/// <b>وهي ليست إذناً بالتنفيذ.</b> القراءة تنجح ومعها شرائح ناقصة عمداً — كي تمتلئ
/// الشاشة أمام المستخدم وهو يتكلّم، وكي يرى <b>ما نقص باسمه</b> بدل شاشةٍ فارغة
/// ورسالةِ خطأ. والرفضُ يقع في <see cref="VoiceConfirmationGate"/>، وهو الباب الوحيد
/// إلى التنفيذ.
/// </para>
/// </summary>
/// <param name="Intent">النيّة المطابَقة.</param>
/// <param name="Slots">ما امتلأ من شرائح.</param>
/// <param name="MissingSlots">أسماء الشرائح اللازمة التي لم تُسمع.</param>
/// <param name="Faults">
/// أعطالٌ وقعت أثناء القراءة — <b>كاملةً برسائلها</b> لا برموزها وحدها: من يعمل
/// بيدين مشغولتين يسمع الجملة ولا يقرأ رمزاً.
/// </param>
/// <param name="SpokenCompany">اسم شركةٍ نُطق داخل الأمر، إن نُطق.</param>
/// <param name="ReadbackAr">الملخّص المرتدّ بالعربية — يُقرأ ويُعرض معاً.</param>
/// <param name="ReadbackEn">الملخّص بالإنجليزية.</param>
/// <param name="ConfirmationToken">
/// رمز التأكيد: صورةٌ نصّية حتمية للأمر بعينه. تأكيدٌ برمزٍ آخر يُرفض، فلا يُنفَّذ
/// أمرٌ تغيّر بعد أن قُرئ على المستخدم.
/// </param>
public sealed record VoiceResolution(
    VoiceIntent Intent,
    IReadOnlyList<SpokenSlotValue> Slots,
    IReadOnlyList<string> MissingSlots,
    IReadOnlyList<Error> Faults,
    string? SpokenCompany,
    string ReadbackAr,
    string ReadbackEn,
    string ConfirmationToken)
{
    /// <summary>هل امتلأت كل الشرائح اللازمة؟</summary>
    public bool IsComplete => MissingSlots.Count == 0;
}
