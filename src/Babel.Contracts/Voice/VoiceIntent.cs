using Babel.SharedKernel;

namespace Babel.Contracts.Voice;

/// <summary>
/// نيّةٌ منطوقة تُسهم بها وحدةٌ في السجلّ. <b>وهي بيانات لا شيفرة</b>: لا مُنفِّذ فيها،
/// ولا مرجعَ إلى خدمة، ولا اسمَ جدول.
/// <para>
/// <b>ولهذا يعيش هذا النوع في العقد لا في وحدة الذكاء:</b> الوحدات الأفقية ممنوعة من
/// أن يعرف بعضها بعضاً (القاعدة 3)، ووحدةُ الذكاء واحدة منها. فلو عاشت النيّات فيها
/// لَاحتاجت مرجعاً إلى كل وحدة تُسهم بنيّة — أي انقلاب اتجاه الاعتماد. والاتجاه
/// مقلوبٌ هنا بالبناء: الوحدة تعرف <b>العقد</b>، ووحدةُ الذكاء تعرف <b>العقد</b>،
/// ولا تعرف إحداهما الأخرى في أي اتجاه.
/// </para>
/// </summary>
/// <param name="Id">
/// معرّف النيّة — مقاطع لاتينية صغيرة تفصلها نقاط، فريدٌ عبر كل الوحدات.
/// </param>
/// <param name="Section">القسم الذي يراه المستخدم.</param>
/// <param name="Module">الوحدة المالكة — للاستحقاق والقياس، لا للملاحة.</param>
/// <param name="Kind">صنف النيّة، وهو ما يُملي التأكيد.</param>
/// <param name="Status">حالها: منشورة أو تنتظر قراراً.</param>
/// <param name="LedgerEffect">أثرها على الدفتر.</param>
/// <param name="EventCode">
/// رمز الحدث في مصفوفة الترحيل — إلزامي حين <see cref="VoiceLedgerEffect.Posts"/>،
/// وفارغٌ فيما عداه. <b>ولا رقم حساب هنا بحال</b>.
/// </param>
/// <param name="NameAr">
/// اسم النيّة بالعربية كما يُقرأ في الملخّص المرتدّ. <b>وهو السجلّ لا ترجمتُه</b>،
/// ولا نصف إنجليزيّ بجانبه: زوج <c>ar</c>/<c>en</c> ثابت عاجزٌ بنيوياً عن اللغة
/// الثالثة، ونصُّ ADR-0021 §6.3 بند 2 يمنع إدخال زوجٍ جديد في حقل عرض — وتفرضه
/// القاعدة 14 بدل أن تُذكّر به. والترجمة صفٌّ حين تُطلب، لا عمود.
/// </param>
/// <param name="Phrases">
/// عبارات الإطلاق كما تُنطق — <b>فصحى وعامّية خليجية معاً</b>، ومُجرَّدة من التشكيل.
/// </param>
/// <param name="Slots">الشرائح المطلوبة.</param>
/// <param name="ReadsPersonalData">
/// هل تقرأ بياناً شخصياً؟ حين تكون <c>true</c> يُقنَّع المقروء ولا يُنطَق كاملاً.
/// </param>
/// <param name="OwnerDecisionAr">
/// القرار الذي ينتظره مالك المنتج — <b>إلزامي</b> حين تكون الحال
/// <see cref="VoiceIntentStatus.AwaitingOwnerDecision"/>، وفارغ فيما عداها.
/// </param>
public sealed record VoiceIntent(
    string Id,
    VoiceSection Section,
    BabelModule Module,
    VoiceIntentKind Kind,
    VoiceIntentStatus Status,
    VoiceLedgerEffect LedgerEffect,
    string? EventCode,
    string NameAr,
    IReadOnlyList<string> Phrases,
    IReadOnlyList<VoiceSlot> Slots,
    bool ReadsPersonalData,
    string? OwnerDecisionAr)
{
    /// <summary>
    /// هل تحتاج تأكيداً صريحاً قبل التنفيذ؟ <b>مُشتقّة من الصنف لا مُعلَنة</b>:
    /// حقلٌ يُكتب بيدٍ يُنسى مرّة، والنسيان هنا قيدٌ في الدفتر بلا قراءة مرتدّة.
    /// </summary>
    public bool RequiresConfirmation => Kind == VoiceIntentKind.StateChange;
}
