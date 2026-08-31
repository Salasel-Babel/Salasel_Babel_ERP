namespace Babel.Contracts.Voice;

/// <summary>
/// شريحةٌ تُستخرج من الكلام: اسمُها، وصنفُها، والكلماتُ التي تدلّ عليها، وهل هي لازمة.
/// <para>
/// <b>والدلائل عربية عامّية قبل أن تكون فصحى.</b> «كم عندي» أكثرُ ما يُقال فعلاً من
/// «ما رصيد الصنف»، ومَن يبني القاموس على الفصحى وحدها يبني نظاماً يعمل في العرض
/// التوضيحي ولا يعمل في المستودع.
/// </para>
/// </summary>
/// <param name="Name">اسم الشريحة — مفتاحٌ لاتيني ثابت تعتمد عليه الشيفرة والواجهة.</param>
/// <param name="Kind">صنفها.</param>
/// <param name="NameAr">اسمها العربي كما يُقرأ على المستخدم.</param>
/// <param name="NameEn">اسمها الإنجليزي.</param>
/// <param name="Required">هل غيابُها رفضٌ؟ الشريحة غير اللازمة تُترك فارغة ولا تُخترَع.</param>
/// <param name="Cues">
/// كلمات دالّة تسبق القيمة في الكلام — «من» و«للمورد» و«بمبلغ» و«حقّ». وتُقرأ
/// <b>مجرّدةً من التشكيل</b> كما يقرأ قاموس الأعداد.
/// </param>
/// <param name="Choices">
/// القائمة المغلقة حين يكون الصنف <see cref="VoiceSlotKind.Choice"/> — وفارغة لغيره.
/// </param>
public sealed record VoiceSlot(
    string Name,
    VoiceSlotKind Kind,
    string NameAr,
    string NameEn,
    bool Required,
    IReadOnlyList<string> Cues,
    IReadOnlyList<string> Choices);
