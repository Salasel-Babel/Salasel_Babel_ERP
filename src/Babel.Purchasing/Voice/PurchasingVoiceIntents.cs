using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Purchasing.Voice;

/// <summary>
/// <b>ما تُسهم به وحدة المشتريات في المسار المنطوق — نصفُ قسم «المحاسبة».</b>
/// <para>
/// <b>القاعدة:</b> الصوت يبلغ <b>كل مسوّدة</b> ولا يبلغ <b>ترحيلاً واحداً</b>. فمن يقف
/// عند باب المستودع وفي يده ورقةُ مورد يقول أمرَ الشراء ومحضرَ الاستلام والفاتورة
/// والمرتجع، <b>ويتسلّم مسوّدةً يراجعها بعينه ويُرحّلها بيده</b>.
/// </para>
/// <para>
/// <b>وما تغيّر — مكتوباً لا مطموساً:</b> كان <b>أمر الشراء</b> ممنوعاً بحجّة أنه «يُكتب
/// بمقارنة عروضٍ ثلاثة»، و<b>تقادم الذمم الدائنة</b> بحجّة أنه «تقريرٌ يُقرأ بالعين».
/// وكلتا الحجّتين تصف <b>المراجعة</b> لا <b>الإملاء</b> — والمراجعةُ هي خطوة المسوّدة،
/// فلا تمنع النطق بل تُبرّره.
/// </para>
/// <para>
/// <b>والاستثناء الذي بقي:</b> <b>المطابقة الثلاثية</b> — وليست إملاءً أصلاً: هي مقابلةُ
/// أمرِ شراءٍ بمحضرِ استلامٍ بفاتورةِ مورد جنباً إلى جنب. <b>لا شيء فيها يُقال، وكلُّ
/// شيءٍ فيها يُقارَن</b>. ولا عملية منشورة لها في العقد أصلاً، فليس لها بابٌ يُبلَغ.
/// </para>
/// </summary>
public sealed class PurchasingVoiceIntents : IVoiceIntentCatalogue
{
    /// <inheritdoc />
    public BabelModule Module => BabelModule.Purchasing;

    /// <inheritdoc />
    public IReadOnlyList<VoiceIntent> Intents { get; } =
    [
        new VoiceIntent(
            "accounting.goods_receipt.draft",
            VoiceSection.Accounting,
            BabelModule.Purchasing,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "purchasing.goods_receipt.posted",
            "draftGoodsReceipt",
            "مسودة محضر استلام بضاعة",
            [
                "سجل استلام بضاعة", "محضر استلام بضاعة", "سجل محضر استلام", "وصلت البضاعة", "استلمت البضاعة",
            ],
            [
                new VoiceSlot("orderNumber", VoiceSlotKind.Code, "أمر الشراء", true,
                    ["بامر شراء", "على امر شراء", "امر الشراء"], []),
                new VoiceSlot("item", VoiceSlotKind.Text, "الصنف", true,
                    ["الصنف", "صنف", "للصنف"], []),
                new VoiceSlot("quantity", VoiceSlotKind.Quantity, "الكمية المستلمة", true,
                    ["كمية", "الكمية", "عدد", "العدد", "بمقدار"], []),
                new VoiceSlot("receivedOn", VoiceSlotKind.Date, "تاريخ الاستلام", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "accounting.payables_aging.query",
            VoiceSection.Accounting,
            BabelModule.Purchasing,
            VoiceIntentKind.Query,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "readPayablesAging",
            "تقادم الذمم الدائنة",
            [
                "تقادم الذمم الدائنة", "اعمار الذمم الدائنة", "تقادم الموردين", "كم علينا للموردين",
            ],
            [
                new VoiceSlot("asOf", VoiceSlotKind.Date, "تاريخ القطع", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "accounting.purchase_order.draft",
            VoiceSection.Accounting,
            BabelModule.Purchasing,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "createPurchaseOrder",
            "مسودة أمر شراء",
            [
                "افتح امر شراء", "سجل امر شراء", "اطلب من المورد", "اطلب بضاعة من المورد", "امر شراء جديد",
            ],
            [
                new VoiceSlot("supplier", VoiceSlotKind.Text, "المورد", true,
                    ["من المورد", "المورد", "مورد"], []),
                new VoiceSlot("item", VoiceSlotKind.Text, "الصنف", true,
                    ["الصنف", "صنف", "للصنف"], []),
                new VoiceSlot("quantity", VoiceSlotKind.Quantity, "الكمية المطلوبة", true,
                    ["كمية", "الكمية", "عدد", "العدد", "بمقدار"], []),
                new VoiceSlot("warehouse", VoiceSlotKind.Text, "المستودع", true,
                    ["المستودع", "مستودع", "المخزن", "مخزن"], []),
                new VoiceSlot("orderedOn", VoiceSlotKind.Date, "تاريخ الأمر", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "accounting.purchase_return.draft",
            VoiceSection.Accounting,
            BabelModule.Purchasing,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "purchasing.debit_note.posted",
            "draftPurchaseReturn",
            "مسودة مرتجع مشتريات",
            [
                "سجل مرتجع مشتريات", "مرتجع مشتريات", "اشعار مدين على المورد", "رجعت بضاعة للمورد",
            ],
            [
                new VoiceSlot("billNumber", VoiceSlotKind.Code, "فاتورة المورد", true,
                    ["على الفاتورة", "الفاتورة", "فاتورة"], []),
                new VoiceSlot("item", VoiceSlotKind.Text, "الصنف", true,
                    ["الصنف", "صنف", "للصنف"], []),
                new VoiceSlot("quantity", VoiceSlotKind.Quantity, "الكمية المرتجعة", true,
                    ["كمية", "الكمية", "عدد", "العدد", "بمقدار"], []),
                new VoiceSlot("issuedOn", VoiceSlotKind.Date, "تاريخ الإشعار", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "accounting.stock_bill.capture",
            VoiceSection.Accounting,
            BabelModule.Purchasing,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "draftStockBill",
            "التقاط فاتورة مشتريات مخزنية",
            [
                "سجل فاتورة مشتريات مخزنية", "فاتورة مشتريات مخزنية", "فاتورة بضاعة من المورد", "قيد فاتورة مخزنية",
            ],
            [
                new VoiceSlot("receiptNumber", VoiceSlotKind.Code, "محضر الاستلام", true,
                    ["على محضر استلام", "محضر الاستلام"], []),
                new VoiceSlot("supplier", VoiceSlotKind.Text, "المورد", true,
                    ["من المورد", "المورد", "مورد"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "الإجمالي شامل الضريبة", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمتها", "الاجمالي", "اجمالي", "المجموع"], []),
                new VoiceSlot("issuedOn", VoiceSlotKind.Date, "تاريخ الفاتورة", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "accounting.supplier_bill.capture",
            VoiceSection.Accounting,
            BabelModule.Purchasing,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "purchasing.invoice.expense.posted",
            "draftExpenseBill",
            "التقاط فاتورة مصروف من مورد",
            [
                "سجل فاتورة مصروف", "قيد فاتورة مصروف", "فاتورة مصروف", "ادخل فاتورة مورد", "اكتب فاتورة مورد", "عندي فاتورة مصروف",
            ],
            [
                new VoiceSlot("supplier", VoiceSlotKind.Text, "المورد", true,
                    ["من", "المورد", "مورد", "باسم", "لصالح"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "الإجمالي شامل الضريبة", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمتها", "قيمته", "الاجمالي", "اجمالي", "المجموع"], []),
                new VoiceSlot("taxRate", VoiceSlotKind.Number, "نسبة الضريبة", false,
                    ["ضريبة", "وضريبة", "الضريبة", "بنسبة"], []),
                new VoiceSlot("billNumber", VoiceSlotKind.Code, "رقم الفاتورة", false,
                    ["رقم", "برقم", "رقمها"], []),
                new VoiceSlot("issuedOn", VoiceSlotKind.Date, "تاريخ الإصدار", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "accounting.supplier_payment.record",
            VoiceSection.Accounting,
            BabelModule.Purchasing,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "purchasing.payment.posted",
            "draftSupplierPayment",
            "سند صرف لمورد",
            [
                "سجل سند صرف", "سند صرف", "صرفت للمورد", "سددت للمورد", "دفعت للمورد", "اصرف للمورد",
            ],
            [
                new VoiceSlot("supplier", VoiceSlotKind.Text, "المورد", true,
                    ["للمورد", "المورد", "مورد", "لصالح", "الى"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "المبلغ المدفوع", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمته", "قيمتها"], []),
                new VoiceSlot("method", VoiceSlotKind.Choice, "طريقة الدفع", true,
                    ["نقد", "تحويل", "شيك", "شبكة"], ["نقد", "تحويل", "شيك", "شبكة"]),
                new VoiceSlot("paidOn", VoiceSlotKind.Date, "تاريخ الصرف", true,
                    [], []),
            ],
            false,
            null),
    ];
}
