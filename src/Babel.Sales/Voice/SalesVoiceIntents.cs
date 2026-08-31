using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Sales.Voice;

/// <summary>
/// <b>ما تُسهم به وحدة المبيعات في المسار المنطوق — النصف الثاني من قسم «المحاسبة».</b>
/// <para>
/// <b>سند قبضٍ يُنطَق</b> لأن مَن يقبضه واقفٌ عند العميل وفي يده نقد أو جهاز شبكة، ولا
/// شاشة أمامه. <b>ورصيدُ عميلٍ يُنطَق</b> لأنه سؤالٌ بجوابٍ واحد يُقال في ثانيتين، ولا
/// يُقارَن بشيء.
/// </para>
/// <para>
/// <b>وما لم يُعطَ صوتاً:</b> <b>فاتورة المبيعات نفسها</b> — أسطرٌ بكمّياتٍ وأسعارٍ
/// وخصومٍ وضريبةٍ لكل سطر، ومراجعتُها سماعاً أطول من كتابتها؛ و<b>الإشعار الدائن</b>
/// لأنه يُبنى على فاتورةٍ تُختار من قائمة؛ و<b>تقادم الذمم المدينة</b> لأنه جدولُ
/// مقارنةٍ لا جواب.
/// </para>
/// </summary>
public sealed class SalesVoiceIntents : IVoiceIntentCatalogue
{
    /// <inheritdoc />
    public BabelModule Module => BabelModule.Sales;

    /// <inheritdoc />
    public IReadOnlyList<VoiceIntent> Intents { get; } =
    [
        new VoiceIntent(
            "accounting.customer_receipt.record",
            VoiceSection.Accounting,
            BabelModule.Sales,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "sales.receipt.posted",
            "سند قبض من عميل",
            "Record a customer receipt",
            [
                "سجل سند قبض", "سند قبض", "استلمت من العميل", "قبضت من العميل",
                "تحصيل من عميل", "حصلت من العميل",
            ],
            [
                new VoiceSlot("customer", VoiceSlotKind.Text, "العميل", "Customer", true,
                    ["العميل", "عميل", "من", "لصالح"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "المبلغ المقبوض", "Amount received", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمته", "قيمتها"], []),
                new VoiceSlot("method", VoiceSlotKind.Choice, "طريقة القبض", "Receipt method", true,
                    [], ["نقد", "تحويل", "شيك", "شبكة"]),
                new VoiceSlot("receivedOn", VoiceSlotKind.Date, "تاريخ القبض", "Received on", true, [], []),
            ],
            false,
            null,
            null),

        new VoiceIntent(
            "accounting.customer_balance.query",
            VoiceSection.Accounting,
            BabelModule.Sales,
            VoiceIntentKind.Query,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "رصيد عميل",
            "Customer balance",
            [
                "كم رصيد العميل", "رصيد العميل", "كم على العميل", "وش رصيد العميل",
                "كم باقي على العميل",
            ],
            [
                new VoiceSlot("customer", VoiceSlotKind.Text, "العميل", "Customer", true,
                    ["العميل", "عميل", "على", "حق"], []),
            ],
            false,
            null,
            null),
    ];
}
