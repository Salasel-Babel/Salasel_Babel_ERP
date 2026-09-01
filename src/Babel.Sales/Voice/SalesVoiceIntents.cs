using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Sales.Voice;

/// <summary>
/// <b>ما تُسهم به وحدة المبيعات في المسار المنطوق — النصف الثاني من قسم «المحاسبة».</b>
/// <para>
/// <b>وقاعدةٌ واحدة تحكم ما هنا:</b> يبلغ الصوت <b>كل عملية إنشاء مسوّدة</b>، ولا يبلغ
/// <b>عملية ترحيلٍ واحدة</b>. فالكاشير يقول فاتورته وهو واقفٌ عند الزبون، ثم <b>تظهر
/// المسوّدة على الشاشة فيراجعها بعينه ويُرحّلها بيده</b>.
/// </para>
/// <para>
/// <b>وما تغيّر عن الحال السابقة — مكتوباً لا مطموساً:</b> كانت فاتورة المبيعات والإشعار
/// الدائن وتقادم الذمم المدينة <b>ممنوعةً من الصوت</b> بمعيارٍ يقول «الصوت يخسر حيث
/// يجب أن يُقارَن رقمٌ برقم على الشاشة». والمعيار كان يخلط <b>الإدخال بالصوت</b>
/// بـ<b>التنفيذ بالصوت</b>: ومراجعةُ الأرقام على الشاشة هي <b>خطوة المسوّدة بعينها</b>،
/// فالاعتراض يسقط بها لا يقوم عليها.
/// </para>
/// <para>
/// <b>ولماذا هذا آمن هنا بالذات:</b> المسوّدة لا تمسّ الدفتر، والدفترُ يُضاف إليه فقط.
/// فمسوّدةٌ خاطئة تُلقى بلا ثمن، وقيدٌ خاطئ يُكلّف <b>قيداً عاكساً وجيلاً ثانياً يبقيان</b>.
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
            "accounting.credit_note.draft",
            VoiceSection.Accounting,
            BabelModule.Sales,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "sales.credit_note.posted",
            "draftCreditNote",
            "مسودة إشعار دائن",
            [
                "سجل اشعار دائن", "اشعار دائن للعميل", "اشعار دائن", "مرتجع مبيعات", "العميل رجع البضاعة",
            ],
            [
                new VoiceSlot("customer", VoiceSlotKind.Text, "العميل", true,
                    ["للعميل", "على العميل", "العميل", "عميل"], []) { Entity = VoiceEntityKind.Customer },
                new VoiceSlot("invoiceNumber", VoiceSlotKind.Code, "الفاتورة الأصلية", true,
                    ["على الفاتورة", "الفاتورة", "فاتورة"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "قيمة الإشعار", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمتها", "الاجمالي", "اجمالي", "المجموع"], []),
                new VoiceSlot("issuedOn", VoiceSlotKind.Date, "تاريخ الإصدار", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "accounting.customer_balance.query",
            VoiceSection.Accounting,
            BabelModule.Sales,
            VoiceIntentKind.Query,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "readReceivablesAging",
            "رصيد عميل",
            [
                "كم رصيد العميل", "رصيد العميل", "كم على العميل", "وش رصيد العميل", "كم باقي على العميل",
            ],
            [
                new VoiceSlot("customer", VoiceSlotKind.Text, "العميل", true,
                    ["العميل", "عميل", "على", "حق"], []) { Entity = VoiceEntityKind.Customer },
            ],
            false,
            null),

        new VoiceIntent(
            "accounting.customer_receipt.record",
            VoiceSection.Accounting,
            BabelModule.Sales,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "sales.receipt.posted",
            "draftCustomerReceipt",
            "سند قبض من عميل",
            [
                "سجل سند قبض", "سند قبض", "استلمت من العميل", "قبضت من العميل", "تحصيل من عميل", "حصلت من العميل",
            ],
            [
                new VoiceSlot("customer", VoiceSlotKind.Text, "العميل", true,
                    ["العميل", "عميل", "من", "لصالح"], []) { Entity = VoiceEntityKind.Customer },
                new VoiceSlot("amount", VoiceSlotKind.Money, "المبلغ المقبوض", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمته", "قيمتها"], []),
                new VoiceSlot("method", VoiceSlotKind.Choice, "طريقة القبض", true,
                    ["نقد", "تحويل", "شيك", "شبكة"], ["نقد", "تحويل", "شيك", "شبكة"]),
                new VoiceSlot("receivedOn", VoiceSlotKind.Date, "تاريخ القبض", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "accounting.receivables_aging.query",
            VoiceSection.Accounting,
            BabelModule.Sales,
            VoiceIntentKind.Query,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "readReceivablesAging",
            "تقادم الذمم المدينة",
            [
                "تقادم الذمم المدينة", "اعمار الذمم المدينة", "تقادم العملاء", "كم على العملاء",
            ],
            [
                new VoiceSlot("asOf", VoiceSlotKind.Date, "تاريخ القطع", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "accounting.sales_invoice.draft",
            VoiceSection.Accounting,
            BabelModule.Sales,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "sales.invoice.posted",
            "draftSalesInvoice",
            "مسودة فاتورة مبيعات",
            [
                "سجل فاتورة مبيعات", "فاتورة مبيعات", "افتح فاتورة مبيعات", "بعت على العميل", "اكتب فاتورة للعميل", "بيع للعميل",
            ],
            [
                new VoiceSlot("customer", VoiceSlotKind.Text, "العميل", true,
                    ["على العميل", "للعميل", "العميل", "عميل"], []) { Entity = VoiceEntityKind.Customer },
                new VoiceSlot("amount", VoiceSlotKind.Money, "الإجمالي شامل الضريبة", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمتها", "الاجمالي", "اجمالي", "المجموع"], []),
                new VoiceSlot("taxRate", VoiceSlotKind.Number, "نسبة الضريبة", false,
                    ["ضريبة", "وضريبة", "بنسبة"], []),
                new VoiceSlot("invoiceNumber", VoiceSlotKind.Code, "رقم الفاتورة", false,
                    ["رقم", "برقم", "رقمها"], []),
                new VoiceSlot("issuedOn", VoiceSlotKind.Date, "تاريخ الإصدار", true,
                    [], []),
            ],
            false,
            null),
    ];
}
