using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Purchasing.Voice;

/// <summary>
/// <b>ما تُسهم به وحدة المشتريات في المسار المنطوق — نصفُ قسم «المحاسبة».</b>
/// <para>
/// <b>ولماذا هذه الاثنتان دون غيرهما:</b> المعيار واحد في كل الأقسام — <b>الصوت يفوز
/// حيث اليدان مشغولتان أو العينان في مكانٍ آخر، ويخسر حيث يجب أن يُقارَن رقمٌ برقم على
/// الشاشة</b>. ومحاسبُ المشتريات يقف عند باب المستودع وفي يده ورقةُ مورد، أو يقف عند
/// الصرّاف وفي يده سندٌ ونقد — فهاتان تكسبان.
/// </para>
/// <para>
/// <b>وما لم يُعطَ صوتاً هنا، ولماذا:</b>
/// <list type="bullet">
///   <item>
///     <b>أمر الشراء</b> — يُكتب بمقارنة عروضٍ ثلاثة جنباً إلى جنب، وهو عملُ عينٍ لا أذن.
///   </item>
///   <item>
///     <b>المطابقة الثلاثية</b> — جوهرها فرقٌ بين ثلاثة أرقام، ونطقُها يُخفي الفرق نفسه.
///   </item>
///   <item>
///     <b>توزيع المصاريف الإضافية</b> — نِسَبٌ على أسطر، والنسبةُ المنطوقة على عشرة
///     أسطر لا تُراجَع سماعاً.
///   </item>
///   <item>
///     <b>تقادم الذمم الدائنة</b> — تقريرٌ يُقرأ بالعين، ونطقُ ستّ خانات عمرية عبثٌ.
///   </item>
/// </list>
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
            "accounting.supplier_bill.capture",
            VoiceSection.Accounting,
            BabelModule.Purchasing,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "purchasing.invoice.expense.posted",
            "التقاط فاتورة مصروف من مورد",
            [
                "سجل فاتورة مصروف", "قيد فاتورة مصروف", "فاتورة مصروف", "ادخل فاتورة مورد",
                "اكتب فاتورة مورد", "عندي فاتورة مصروف",
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
                new VoiceSlot("issuedOn", VoiceSlotKind.Date, "تاريخ الإصدار", true, [], []),
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
            "سند صرف لمورد",
            [
                "سجل سند صرف", "سند صرف", "صرفت للمورد", "سددت للمورد", "دفعت للمورد",
                "اصرف للمورد",
            ],
            [
                new VoiceSlot("supplier", VoiceSlotKind.Text, "المورد", true,
                    ["للمورد", "المورد", "مورد", "لصالح", "الى"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "المبلغ المدفوع", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمته", "قيمتها"], []),
                new VoiceSlot("method", VoiceSlotKind.Choice, "طريقة الدفع", true,
                    [], ["نقد", "تحويل", "شيك", "شبكة"]),
                new VoiceSlot("paidOn", VoiceSlotKind.Date, "تاريخ الصرف", true, [], []),
            ],
            false,
            null),
    ];
}
