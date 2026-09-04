using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.RealEstate.Voice;

/// <summary>
/// <b>ما تُسهم به وحدة العقارات في المسار المنطوق.</b>
/// <para>
/// مسؤولُ التحصيل يتنقّل بين وحداتٍ في مبنى ويقف عند بابٍ ويستلم نقداً أو شيكاً؛
/// وفنّيُّ الصيانة يقف على سلّم. والقاعدة فوق ذلك: الصوت يبلغ <b>كل مسوّدة</b> ولا
/// يبلغ <b>ترحيلاً</b>.
/// </para>
/// <para>
/// <b>وما تغيّر — مكتوباً لا مطموساً:</b> كان <b>عقد الإيجار</b> ممنوعاً من الصوت جملةً.
/// والفرق الذي أضاعه المنع: <b>تسجيلُ عقدٍ مُحرَّر شيء واعتمادُه للفوترة شيء آخر</b>.
/// فمسوّدةُ <b>قيد التسجيل</b> تُملى الآن (‏<c>draftLeaseRegistration</c>) — وهي إدخالُ
/// مرجعٍ لعقدٍ حُرِّر في منصّة إيجار، لا تحريرُ عقد — <b>والاعتماد للفوترة لا يُبلَغ
/// أبداً</b>: <c>approveLeaseRegistrationForBilling</c> فعلُه <c>approve</c> يمنعه حارسُ
/// الأفعال بالبناء، لأن الاعتماد قرارُ إنسانٍ يُتَّخذ بالعين واليد. وقراءةُ القيد على
/// الطرفين تبقى كما هي: <b>وجودُها هو الغرض</b>.
/// </para>
/// <para>
/// <b>وما لا يُبلَغ لأنه لا بابَ له في العقد المنشور:</b> سلسلةُ الشيكات الآجلة،
/// وتوريدُ صافي الإيراد للمالك، ومصادرةُ الوديعة — <b>ثلاثتُها أحداثٌ في مصفوفة
/// الترحيل بلا عمليةٍ منشورة تُنشئها</b>. وبابٌ لا وجود له لا يُخترَع، والقرار عند
/// مالك المنتج (خطة الصوت §9).
/// </para>
/// </summary>
public sealed class RealEstateVoiceIntents : IVoiceIntentCatalogue
{
    /// <inheritdoc />
    public BabelModule Module => BabelModule.RealEstate;

    /// <inheritdoc />
    public IReadOnlyList<VoiceIntent> Intents { get; } =
    [
        new VoiceIntent(
            "realestate.lease_registration.draft",
            VoiceSection.RealEstate,
            BabelModule.RealEstate,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "draftLeaseRegistration",
            "مسودة قيد تسجيل عقد إيجار",
            [
                "سجل عقد ايجار", "قيد عقد ايجار", "ارشف عقد ايجار", "مسودة تسجيل عقد ايجار",
            ],
            [
                new VoiceSlot("lessee", VoiceSlotKind.Text, "المستأجر", true,
                    ["للمستاجر", "المستاجر", "مستاجر"], []),
                new VoiceSlot("unit", VoiceSlotKind.Code, "الوحدة", true,
                    ["للوحدة", "الوحدة", "وحدة"], []),
                new VoiceSlot("totalRent", VoiceSlotKind.Money, "إجمالي الإيجار", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمتها", "الاجمالي", "اجمالي", "المجموع"], []),
                new VoiceSlot("startsOn", VoiceSlotKind.Date, "تاريخ البداية", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "realestate.maintenance_expense.record",
            VoiceSection.RealEstate,
            BabelModule.RealEstate,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "realestate.maintenance.company_expense",
            "draftExpenseBill",
            "مصروف صيانة على الشركة",
            [
                "سجل مصروف صيانة", "صيانة على الشركة", "فاتورة صيانة", "مصروف صيانة",
            ],
            [
                new VoiceSlot("unit", VoiceSlotKind.Code, "الوحدة", true,
                    ["للوحدة", "الوحدة", "وحدة"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "قيمة الصيانة", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمتها"], []),
                new VoiceSlot("spentOn", VoiceSlotKind.Date, "تاريخ الصرف", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "realestate.rent_invoice.draft",
            VoiceSection.RealEstate,
            BabelModule.RealEstate,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "realestate.rent_invoice.own_property",
            "draftRentInvoice",
            "مسودة فاتورة إيجار",
            [
                "سجل فاتورة ايجار", "اصدر فاتورة ايجار", "افتح فاتورة ايجار", "فاتورة ايجار",
            ],
            [
                new VoiceSlot("lease", VoiceSlotKind.Code, "عقد الإيجار", true,
                    ["للعقد", "العقد", "عقد"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "قيمة الفاتورة", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمتها", "الاجمالي", "اجمالي", "المجموع"], []),
                new VoiceSlot("taxRate", VoiceSlotKind.Number, "نسبة الضريبة", false,
                    ["ضريبة", "وضريبة", "بنسبة"], []),
                new VoiceSlot("issuedOn", VoiceSlotKind.Date, "تاريخ الإصدار", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "realestate.tenant_arrears.query",
            VoiceSection.RealEstate,
            BabelModule.RealEstate,
            VoiceIntentKind.Query,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "readTenantArrearsAging",
            "متأخرات مستأجر",
            [
                "كم متاخرات المستاجر", "متاخرات المستاجر", "كم على المستاجر", "وش متاخرات المستاجر",
            ],
            [
                new VoiceSlot("lessee", VoiceSlotKind.Text, "المستأجر", true,
                    ["المستاجر", "مستاجر", "على"], []),
            ],
            false,
            null),

        new VoiceIntent(
            "realestate.tenant_receipt.record",
            VoiceSection.RealEstate,
            BabelModule.RealEstate,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "realestate.collection.received",
            "draftTenantReceipt",
            "تحصيل من مستأجر",
            [
                "سجل تحصيل من مستاجر", "قبضت من المستاجر", "تحصيل ايجار", "استلمت ايجار", "حصلت من المستاجر",
            ],
            [
                new VoiceSlot("lessee", VoiceSlotKind.Text, "المستأجر", true,
                    ["من المستاجر", "المستاجر", "مستاجر", "من"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "المبلغ المحصَّل", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمته"], []),
                new VoiceSlot("method", VoiceSlotKind.Choice, "طريقة التحصيل", true,
                    ["نقد", "تحويل", "شيك", "شبكة"], ["نقد", "تحويل", "شيك", "شبكة"]),
                new VoiceSlot("receivedOn", VoiceSlotKind.Date, "تاريخ التحصيل", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "realestate.unit_status.query",
            VoiceSection.RealEstate,
            BabelModule.RealEstate,
            VoiceIntentKind.Query,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "readUnit",
            "حالة وحدة",
            [
                "حالة الوحدة", "وش وضع الوحدة", "الوحدة مؤجرة", "وضع الوحدة",
            ],
            [
                new VoiceSlot("unit", VoiceSlotKind.Code, "الوحدة", true,
                    ["للوحدة", "الوحدة", "وحدة"], []),
            ],
            false,
            null),
    ];
}
