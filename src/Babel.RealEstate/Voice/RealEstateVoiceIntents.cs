using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.RealEstate.Voice;

/// <summary>
/// <b>ما تُسهم به وحدة العقارات في المسار المنطوق.</b>
/// <para>
/// مسؤولُ التحصيل يتنقّل بين وحداتٍ في مبنى، ويقف عند بابٍ ويستلم نقداً أو شيكاً؛
/// وفنّيُّ الصيانة يقف على سلّم. وكلاهما بيدين مشغولتين وعينين في مكانٍ آخر — وهذا
/// بعينه معيارُ إعطاء الصوت.
/// </para>
/// <para>
/// <b>وما لم يُعطَ صوتاً، ولماذا:</b>
/// <list type="bullet">
///   <item>
///     <b>توقيع عقد الإيجار</b> — عقدٌ بمُدّةٍ ودفعاتٍ ووديعةٍ وشروطِ إخلاء، ويُقرأ
///     على الطرفين قبل التوقيع. ونطقُه اختصارٌ لخطوةٍ وجودُها هو الغرض.
///   </item>
///   <item>
///     <b>الشيكات الآجلة</b> — إيداعٌ وتحصيلٌ وارتجاعٌ بتواريخ استحقاق، وسلسلةٌ تُتابَع
///     على جدول لا بالأذن.
///   </item>
///   <item>
///     <b>توريد صافي الإيراد للمالك</b> — حسابٌ يقابل عمولةً ومصاريفَ نيابةً، ويُراجَع
///     بالعين قبل التوريد.
///   </item>
///   <item>
///     <b>مصادرة الوديعة</b> — قرارٌ خلافي بين طرفين، ولا يُتَّخذ بجملةٍ منطوقة.
///   </item>
/// </list>
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
            "realestate.tenant_receipt.record",
            VoiceSection.RealEstate,
            BabelModule.RealEstate,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "realestate.collection.received",
            "تحصيل من مستأجر",
            "Record a tenant collection",
            [
                "سجل تحصيل من مستاجر", "قبضت من المستاجر", "تحصيل ايجار", "استلمت ايجار",
                "حصلت من المستاجر",
            ],
            [
                new VoiceSlot("lessee", VoiceSlotKind.Text, "المستأجر", "Lessee", true,
                    ["من المستاجر", "المستاجر", "مستاجر", "من"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "المبلغ المحصَّل", "Amount collected", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمته"], []),
                new VoiceSlot("method", VoiceSlotKind.Choice, "طريقة التحصيل", "Collection method", true,
                    [], ["نقد", "تحويل", "شيك", "شبكة"]),
                new VoiceSlot("receivedOn", VoiceSlotKind.Date, "تاريخ التحصيل", "Received on", true, [], []),
            ],
            false,
            null,
            null),

        new VoiceIntent(
            "realestate.maintenance_expense.record",
            VoiceSection.RealEstate,
            BabelModule.RealEstate,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "realestate.maintenance.company_expense",
            "مصروف صيانة على الشركة",
            "Maintenance expense borne by the company",
            [
                "سجل مصروف صيانة", "صيانة على الشركة", "فاتورة صيانة", "مصروف صيانة",
            ],
            [
                new VoiceSlot("unit", VoiceSlotKind.Code, "الوحدة", "Unit", true,
                    ["للوحدة", "الوحدة", "وحدة"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "قيمة الصيانة", "Maintenance amount", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمتها"], []),
                new VoiceSlot("spentOn", VoiceSlotKind.Date, "تاريخ الصرف", "Spent on", true, [], []),
            ],
            false,
            null,
            null),

        new VoiceIntent(
            "realestate.tenant_arrears.query",
            VoiceSection.RealEstate,
            BabelModule.RealEstate,
            VoiceIntentKind.Query,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "متأخرات مستأجر",
            "Tenant arrears",
            [
                "كم متاخرات المستاجر", "متاخرات المستاجر", "كم على المستاجر",
                "وش متاخرات المستاجر",
            ],
            [
                new VoiceSlot("lessee", VoiceSlotKind.Text, "المستأجر", "Lessee", true,
                    ["المستاجر", "مستاجر", "على"], []),
            ],
            false,
            null,
            null),

        new VoiceIntent(
            "realestate.unit_status.query",
            VoiceSection.RealEstate,
            BabelModule.RealEstate,
            VoiceIntentKind.Query,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "حالة وحدة",
            "Unit status",
            [
                "حالة الوحدة", "وش وضع الوحدة", "الوحدة مؤجرة", "وضع الوحدة",
            ],
            [
                new VoiceSlot("unit", VoiceSlotKind.Code, "الوحدة", "Unit", true,
                    ["للوحدة", "الوحدة", "وحدة"], []),
            ],
            false,
            null,
            null),
    ];
}
