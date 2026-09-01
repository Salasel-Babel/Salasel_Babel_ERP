namespace Babel.Contracts.Voice;

/// <summary>
/// خطوةٌ واحدة في خطّة.
/// </summary>
/// <param name="StepId">معرّفها داخل الخطّة — فريدٌ فيها، وتشير إليه الروابط.</param>
/// <param name="IntentId">
/// <b>معرّفُ نيّةٍ منشورة في السجلّ — ولا معرّفُ عمليةٍ بحال.</b>
/// <para>
/// <b>وهذا هو الباب الذي لا يُفتح:</b> لو حملت الخطوةُ <c>OperationId</c> لصارت
/// الخطّةُ مكاناً ثانياً تُسمّى فيه العمليات المنشورة — ومكانٌ ثانٍ يعني حارساً ثانياً،
/// ويوماً يُكتب فيه <c>postCustomerReceipt</c> في بياناتٍ لا يقرؤها
/// <see cref="VoiceIntent.OperationId"/> ولا حارسُه. وبتسمية <b>النيّة</b> تصير العملية
/// مقروءةً من النيّة المُحلّاة، <b>وكلُّ نيّةٍ في السجلّ قد اجتازت حارسَ العمليات عند
/// البناء</b>. فلا شيء يُهرَّب لأن الخطّة <b>لا تملك أن تسمّي باباً</b>.
/// </para>
/// </param>
/// <param name="Condition">شرط تنفيذها.</param>
/// <param name="PurposeAr">
/// ما تفعله هذه الخطوة بالعربية كما يُقرأ في توجيه الخطّة. <b>وهو السجلّ لا ترجمتُه</b>
/// (‏ADR-0021 §6.3 · القاعدة 14).
/// </param>
/// <param name="Bindings">كيف تمتلئ شرائحها.</param>
/// <param name="ScreenAsksForAr">
/// <b>حقولٌ تطلبها شاشةُ هذه الخطوة ولا يطلبها الصوت</b> — بأسمائها العربية.
/// <para>
/// وهي جوابُ «كان يفترض أن يطلب بيانات أكثر»: يطلبها، <b>لكن على الشاشة</b>، ويقولها
/// بصوته في التوجيه قبل أن يبدأ. فرمزُ العميل هويّةٌ تحملها مستنداتُه المرحَّلة، ورمزٌ
/// منطوق رمزٌ سُمع خطأً؛ وحدُّ الائتمان ومهلةُ السداد سياسةٌ لا إملاء. <b>والصوت يملأ،
/// والشاشة تلتزم.</b>
/// </para>
/// </param>
public sealed record VoicePlanStep(
    string StepId,
    string IntentId,
    VoicePlanCondition Condition,
    string PurposeAr,
    IReadOnlyList<VoiceSlotBinding> Bindings,
    IReadOnlyList<string> ScreenAsksForAr);
