using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Sales.Voice;

/// <summary>
/// <b>ما تُسهم به وحدة المبيعات من خطط منطوقة.</b>
/// <para>
/// <b>الخطّةُ الأولى هي طلبُ المالك حرفاً:</b> «سجّل سند قبض من شركة المسار الأمثل
/// <b>فإن لم تجدها أنشئ لها حساباً</b> ثم سند قبض بقيمة 20000 ريال بتاريخ اليوم».
/// أمرٌ واحد في فمه، وشيئان في النظام.
/// </para>
/// <para>
/// <b>ولماذا خطوتان لا ثلاث — والبحثُ ليس خطوة:</b> «ابحث عن العميل» <b>ليس له بابٌ
/// منشور</b>. لا <c>listCustomers</c> في العقد ولا بحثٌ باسم؛ و<c>readCustomer</c>
/// يطلب معرّفاً — أي يطلب جوابَ البحث نفسه. <b>والمسارُ المنطوق لا ينادي باباً على أي
/// حال</b> (‏<c>handoff.ts</c>). فالبحثُ يقع حيث كان يقع دائماً: على الشاشة، في مُنتقٍ،
/// بعين إنسان. وجعلُه «خطوة» كان سيكون <b>ادّعاءَ بحثٍ لا يقع</b> — ولذلك شرطُ الخطوة
/// الأولى <see cref="VoicePlanCondition.WhenHumanFindsNothing"/>: <b>الإنسان هو الذي
/// يجيب</b>، والخطّة تسأله بصوتها.
/// </para>
/// <para>
/// <b>وسقفُ المستند المرحَّل الواحد يجتازه هذا الطلب بلا تضييق:</b> إنشاءُ العميل لا
/// يُرحّل شيئاً، وسندُ القبض هو المستند الوحيد الذي سيُرحَّل — <b>بيد إنسانٍ على شاشة،
/// لا بنعمٍ منطوقة</b>.
/// </para>
/// </summary>
public sealed class SalesVoicePlans : IVoicePlanCatalogue
{
    /// <inheritdoc />
    public BabelModule Module => BabelModule.Sales;

    /// <inheritdoc />
    public IReadOnlyList<VoicePlan> Plans { get; } =
    [
        new VoicePlan(
            "accounting.customer_receipt.with_new_customer",
            VoiceSection.Accounting,
            BabelModule.Sales,
            "سند قبض من عميل — مع إنشائه إن لم يوجد",
            // الطلب — ما يريده الإنسان.
            ["سند قبض", "سجل سند قبض", "قبضت من العميل", "استلمت من العميل", "تحصيل من عميل"],
            // ‏**الشرط — وهو ما يجعلها خطّةً لا أمراً واحداً.** ومقاطعُ قصيرة بقصد:
            // الطلبُ والشرطُ لا يتجاوران في كلام الناس، وبينهما اسمُ العميل.
            ["فان لم تجدها", "فان لم تجده", "ان لم تجدها", "ان لم تجده",
             "وان لم يكن العميل موجودا", "والا انشئ", "فان لم يكن موجودا"],
            [
                new VoicePlanStep(
                    "create-customer",
                    "accounting.customer.add",
                    VoicePlanCondition.WhenHumanFindsNothing,
                    "إن لم تجد العميل، تُفتح شاشة العملاء باسمه مملوءاً.",
                    [
                        new VoiceSlotBinding("name", VoiceSlotSource.FromUtterance),
                    ],
                    // ‏**ما تطلبه الشاشةُ ولا يطلبه الصوت — يُقال جهراً قبل أن يبدأ.**
                    // وهو جوابُ «كان يفترض أن يطلب بيانات أكثر عن الشركة»: يطلبها،
                    // لكن حيث تُكتب لا حيث تُسمع.
                    ["رمز العميل", "حدّ الائتمان", "مهلة السداد"]),

                new VoicePlanStep(
                    "draft-receipt",
                    "accounting.customer_receipt.record",
                    VoicePlanCondition.Always,
                    "ثم مسوّدة سند القبض — تُراجَع على الشاشة، ويُرحّلها إنسانٌ بيده.",
                    [
                        // الاسم من الجملة نفسها لا من الخطوة الأولى: النصّ من الفم نفسه،
                        // ولا يمرّ معرّفٌ بين خطوتين في هذا النظام.
                        new VoiceSlotBinding("customer", VoiceSlotSource.FromUtterance),
                        new VoiceSlotBinding("amount", VoiceSlotSource.FromUtterance),
                        new VoiceSlotBinding("receivedOn", VoiceSlotSource.FromUtterance),
                        // ‏**وطريقةُ القبض تُسأل ولا تُخترَع.** غيابُها في جملة المالك رفضٌ
                        // صحيح، والخطّة لا تُعفي منه: تقف وتسأل باسم الشريحة.
                        new VoiceSlotBinding("method", VoiceSlotSource.AskedOfHuman),
                    ],
                    []),
            ]),
    ];
}
