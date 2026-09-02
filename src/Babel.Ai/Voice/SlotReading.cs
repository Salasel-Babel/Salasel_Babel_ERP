using Babel.SharedKernel;

namespace Babel.Ai.Voice;

/// <summary>
/// <b>ما قرأه القارئ لشريحةٍ واحدة — مجموعةٌ مغلقة بأربع حالات، وليس فيها «تابع».</b>
/// <para>
/// <b>العطل كان في تدفّق التحكّم لا في القاعدة.</b> كان القارئ يجرّب مواضع الدلائل واحداً
/// بعد آخر ويعود بأول ما أنتج قيمة؛ وكل طبقةٍ تُضاف فوقه تُغري بالشكل نفسه — «سجّل الرفض
/// ثم <c>continue</c> إلى الدليل التالي» — فيصير <b>رفضُ مقطعٍ سبباً في قبول مقطعٍ آخر</b>،
/// ويخرج طرفٌ مختلف على المستند بلا عطلٍ واحد.
/// </para>
/// <para>
/// <b>والعلاج نوعٌ لا اتّفاق.</b> ليس في هذه المجموعة حالةٌ تعني «لم أقرّر بعد»:
/// <see cref="Pending"/> ليست «واصلْ البحث» بل <b>«هذا المقطع نهائيّ، وعلى حالٍّ أن يجيب
/// عنه مرّةً واحدة»</b>. وحلقةُ المواضع تعيش <b>تحت</b> هذا النوع، داخل
/// <see cref="SpokenSpans.Locate"/>، وتوقيعُها يُعيد <see cref="SpokenSpan"/> ولا يستطيع أن
/// يحمل رفضاً. فحين يوجد <see cref="Refused"/> تكون الحلقة قد انتهت — <b>ولا موضع نحويّ
/// يُكتب فيه <c>continue</c> بعده</b>. وذلك خاصّية بنية لا قاعدة مراجعة.
/// </para>
/// </summary>
public abstract record SlotReading
{
    private SlotReading()
    {
    }

    /// <summary>لم يُنطق لهذه الشريحة دليل — ولا تُخترَع لها قيمة.</summary>
    public sealed record Silent : SlotReading;

    /// <summary>قيمةٌ مقروءة — عددٌ أو تاريخٌ أو اختيارٌ أو رمزٌ أو نصٌّ لا يسمّي أحداً.</summary>
    /// <param name="Value">القيمة.</param>
    public sealed record Filled(SpokenSlotValue Value) : SlotReading;

    /// <summary>
    /// مقطعٌ سُمع في موضع طرفٍ ولم يُحلّ بعد. <b>وليست «واصلْ»</b>: المقطع نهائيّ،
    /// والسجلّ المُسمّى هو الذي يجيب عنه مرّةً واحدة.
    /// </summary>
    /// <param name="Slot">اسم الشريحة.</param>
    /// <param name="Span">المقطع كما سُمع.</param>
    /// <param name="RegisterKey">السجلّ الذي يُسأل.</param>
    public sealed record Pending(string Slot, SpokenSpan Span, string RegisterKey) : SlotReading;

    /// <summary>
    /// طرفٌ أجاب عنه السجلّ بصفٍّ واحد. <b>والقيمة مِقبضٌ لا اسم</b>؛ والمقطع يُحفَظ
    /// معه كي يرى الإنسان <b>ما قاله</b> — لا كي يُكتب في مستند.
    /// <para>
    /// <b>ولا اسمَ من السجلّ هنا عمداً:</b> المنفذ الذي يُعيد أسماءً هو
    /// المنفذ الذي يُعيد أسماءً، و<c>Babel.Ai</c> ممنوعةٌ من تسميته بحارسٍ قائم
    /// (‏<c>TheNameSheetIsNeverReachableFromTheAgent</c>). فتسميةُ الصفّ المطابَق على
    /// الشاشة فعلُ طبقة التركيب، لا فعلُ هذا المشروع.
    /// </para>
    /// </summary>
    /// <param name="Slot">اسم الشريحة.</param>
    /// <param name="Handle">المِقبض المعتم.</param>
    /// <param name="Span">المقطع الذي سُئل به.</param>
    public sealed record Resolved(string Slot, string Handle, SpokenSpan Span) : SlotReading;

    /// <summary>
    /// أجاب السجلُّ بأكثر من واحد، <b>فوُرِقت ورقةُ سؤال</b>. ومعرّف الورقة مِقبضٌ غرضُه
    /// <c>Question</c> لا <c>Entity</c>، فلا يُفتدى صفّاً لو رُدَّ في موضع كِيان.
    /// <b>ولا يُقال كم كان المرشّحون</b> — العدد لا يُحذف من الجواب، هو لا يُحسب.
    /// </summary>
    /// <param name="Slot">اسم الشريحة.</param>
    /// <param name="QuestionId">معرّف الورقة.</param>
    /// <param name="Span">المقطع الذي سُئل به.</param>
    public sealed record Asked(string Slot, string QuestionId, SpokenSpan Span) : SlotReading;

    /// <summary>رفضٌ مُسمّى لهذه الشريحة — ولا يُتجاوَز بمحاولةٍ أخرى.</summary>
    /// <param name="Error">سبب الرفض كاملاً برسالته.</param>
    public sealed record Refused(Error Error) : SlotReading;
}
