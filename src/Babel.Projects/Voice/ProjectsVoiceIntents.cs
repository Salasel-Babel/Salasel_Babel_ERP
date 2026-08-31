using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Projects.Voice;

/// <summary>
/// <b>ما تُسهم به وحدة المشاريع في المسار المنطوق — قسم «المقاولات».</b>
/// <para>
/// <b>وهذا القسم أقوى مواضع الصوت في المنتج كلّه.</b> مهندسُ الموقع يقف عند صبّةٍ
/// جارية، وفي يده شريطُ قياسٍ وخوذة، ولا يستطيع أن يُخرج جهازاً ويكتب. والقياس
/// <b>يُنطَق لحظة وقوعه أو يُكتب من الذاكرة مساءً</b> — والثاني هو مصدرُ أغلب الفروق
/// في المستخلصات.
/// </para>
/// <para>
/// <b>وما لم يُعطَ صوتاً:</b> <b>جدول الكميات نفسه</b> — يُبنى بنوداً وأسعارَ وحدةٍ
/// وتُراجَع أرقامُه عمودياً؛ و<b>أمر التغيير</b> لأنه تعديلُ عقدٍ يُقارَن بالأصل بنداً
/// بنداً؛ و<b>الإفراج عن المحتجز</b> لأنه قرارُ إدارةٍ يُتّخذ على مكتبٍ لا على موقع؛
/// و<b>الضمانات</b> لأن قيمتها وتواريخها تُقرأ من ورقةٍ بنكية بالعين.
/// </para>
/// </summary>
public sealed class ProjectsVoiceIntents : IVoiceIntentCatalogue
{
    /// <inheritdoc />
    public BabelModule Module => BabelModule.Projects;

    /// <inheritdoc />
    public IReadOnlyList<VoiceIntent> Intents { get; } =
    [
        new VoiceIntent(
            "contracting.client_certificate.measure",
            VoiceSection.Contracting,
            BabelModule.Projects,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "projects.client_certificate.posted",
            "قياس بندٍ في مستخلص عميل",
            "Measure a client certificate line",
            [
                "سجل مستخلص عميل", "مستخلص عميل", "قياس مستخلص", "سجل كمية منفذة",
                "قست في المستخلص", "اضف الى مستخلص العميل",
            ],
            [
                new VoiceSlot("contract", VoiceSlotKind.Text, "العقد", "Contract", true,
                    ["عقد", "العقد", "للعقد", "بعقد"], []),
                new VoiceSlot("boqItem", VoiceSlotKind.Text, "بند جدول الكميات", "BoQ item", true,
                    ["بند", "البند", "للبند", "بندي"], []),
                new VoiceSlot("quantity", VoiceSlotKind.Quantity, "الكمية المنفذة", "Executed quantity", true,
                    ["كمية", "الكمية", "بمقدار", "عدد", "منفذ"], []),
                new VoiceSlot("measuredOn", VoiceSlotKind.Date, "تاريخ القياس", "Measured on", true, [], []),
            ],
            false,
            null,
            null),

        new VoiceIntent(
            "contracting.subcontractor_certificate.measure",
            VoiceSection.Contracting,
            BabelModule.Projects,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "projects.subcontractor_certificate.posted",
            "قياس بندٍ في مستخلص مقاول من الباطن",
            "Measure a subcontractor certificate line",
            [
                "مستخلص مقاول من الباطن", "مستخلص من الباطن", "سجل مستخلص مقاول",
                "قياس مقاول الباطن",
            ],
            [
                new VoiceSlot("subcontract", VoiceSlotKind.Text, "عقد الباطن", "Subcontract", true,
                    ["عقد", "العقد", "للعقد"], []),
                new VoiceSlot("boqItem", VoiceSlotKind.Text, "بند جدول الكميات", "BoQ item", true,
                    ["بند", "البند", "للبند"], []),
                new VoiceSlot("quantity", VoiceSlotKind.Quantity, "الكمية المنفذة", "Executed quantity", true,
                    ["كمية", "الكمية", "بمقدار", "عدد"], []),
                new VoiceSlot("measuredOn", VoiceSlotKind.Date, "تاريخ القياس", "Measured on", true, [], []),
            ],
            false,
            null,
            null),

        new VoiceIntent(
            "contracting.subcontractor_advance.record",
            VoiceSection.Contracting,
            BabelModule.Projects,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "projects.subcontractor_advance.paid",
            "دفعة مقدمة لمقاول من الباطن",
            "Pay a subcontractor advance",
            [
                "دفعة مقدمة لمقاول", "سلفة مقاول من الباطن", "صرفت دفعة مقدمة",
                "دفعة مقدمة للمقاول",
            ],
            [
                new VoiceSlot("subcontractor", VoiceSlotKind.Text, "المقاول من الباطن", "Subcontractor", true,
                    ["للمقاول", "المقاول", "مقاول", "لصالح"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "المبلغ", "Amount", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمته"], []),
                new VoiceSlot("paidOn", VoiceSlotKind.Date, "تاريخ الصرف", "Paid on", true, [], []),
            ],
            false,
            null,
            null),

        new VoiceIntent(
            "contracting.contract_position.query",
            VoiceSection.Contracting,
            BabelModule.Projects,
            VoiceIntentKind.Query,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "موقف العقد",
            "Contract position",
            [
                "كم موقف العقد", "موقف العقد", "وضع العقد", "كم المنجز في العقد",
                "وش موقف العقد",
            ],
            [
                new VoiceSlot("contract", VoiceSlotKind.Text, "العقد", "Contract", true,
                    ["عقد", "العقد", "للعقد"], []),
            ],
            false,
            null,
            null),
    ];
}
