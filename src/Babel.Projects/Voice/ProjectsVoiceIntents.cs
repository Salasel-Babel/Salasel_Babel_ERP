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
/// <b>وما تغيّر — مكتوباً لا مطموساً:</b> كان <b>أمر التغيير</b> و<b>الإفراج عن المحتجز</b>
/// و<b>الضمانات</b> ممنوعةً من الصوت. والحجّة في الثلاثة كانت واحدة: «تُراجَع بالعين
/// قبل أن تمضي». وهي حجّةٌ صحيحة <b>ضدّ التنفيذ بالصوت</b> ولا شأن لها بالإملاء: المراجعة
/// بالعين هي <b>خطوة المسوّدة</b>، وقد صارت هي الحدّ. فالثلاثة تُملى، وتظهر مسوّدةً،
/// <b>ويُرحّلها إنسانٌ بيده على الشاشة</b>.
/// </para>
/// <para>
/// <b>وجدولُ الكميات وحده لا يُبلَغ</b> — لا لأنه ممنوع، بل لأن العقد المنشور لا يحمل
/// له عمليةَ إنشاءٍ أصلاً: فيه <c>readBoqItems</c> وحدها. وبابٌ لا وجود له لا يُخترَع.
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
            "contracting.change_order.draft",
            VoiceSection.Contracting,
            BabelModule.Projects,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "addChangeOrder",
            "مسودة أمر تغيير",
            [
                "سجل امر تغيير", "امر تغيير على العقد", "افتح امر تغيير", "امر تغيير",
            ],
            [
                new VoiceSlot("contract", VoiceSlotKind.Text, "العقد", true,
                    ["للعقد", "العقد", "عقد"], []),
                new VoiceSlot("reason", VoiceSlotKind.Text, "سبب التغيير", true,
                    ["بسبب", "السبب", "لان"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "قيمة التغيير", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمتها", "الاجمالي", "اجمالي", "المجموع"], []),
                new VoiceSlot("issuedOn", VoiceSlotKind.Date, "تاريخ الأمر", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "contracting.client_certificate.measure",
            VoiceSection.Contracting,
            BabelModule.Projects,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "projects.client_certificate.posted",
            "draftClientCertificate",
            "قياس بندٍ في مستخلص عميل",
            [
                "سجل مستخلص عميل", "مستخلص عميل", "قياس مستخلص", "سجل كمية منفذة", "قست في المستخلص", "اضف الى مستخلص العميل",
            ],
            [
                new VoiceSlot("contract", VoiceSlotKind.Text, "العقد", true,
                    ["عقد", "العقد", "للعقد", "بعقد"], []),
                new VoiceSlot("boqItem", VoiceSlotKind.Text, "بند جدول الكميات", true,
                    ["بند", "البند", "للبند", "بندي"], []),
                new VoiceSlot("quantity", VoiceSlotKind.Quantity, "الكمية المنفذة", true,
                    ["كمية", "الكمية", "بمقدار", "عدد", "منفذ"], []),
                new VoiceSlot("measuredOn", VoiceSlotKind.Date, "تاريخ القياس", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "contracting.contract_position.query",
            VoiceSection.Contracting,
            BabelModule.Projects,
            VoiceIntentKind.Query,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "readContractPosition",
            "موقف العقد",
            [
                "كم موقف العقد", "موقف العقد", "وضع العقد", "كم المنجز في العقد", "وش موقف العقد",
            ],
            [
                new VoiceSlot("contract", VoiceSlotKind.Text, "العقد", true,
                    ["عقد", "العقد", "للعقد"], []),
            ],
            false,
            null),

        new VoiceIntent(
            "contracting.guarantee.draft",
            VoiceSection.Contracting,
            BabelModule.Projects,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "addGuarantee",
            "مسودة ضمان بنكي",
            [
                "سجل ضمان بنكي", "سجل خطاب ضمان", "خطاب ضمان", "ضمان بنكي",
            ],
            [
                new VoiceSlot("contract", VoiceSlotKind.Text, "العقد", true,
                    ["للعقد", "العقد", "عقد"], []),
                new VoiceSlot("guaranteeNumber", VoiceSlotKind.Code, "رقم الضمان", true,
                    ["رقم الضمان", "الخطاب رقم", "الخطاب"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "قيمة الضمان", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمتها", "الاجمالي", "اجمالي", "المجموع"], []),
                new VoiceSlot("expiresOn", VoiceSlotKind.Date, "تاريخ الانتهاء", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "contracting.retention_collection.draft",
            VoiceSection.Contracting,
            BabelModule.Projects,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "projects.client_retention.collected",
            "draftRetentionCollection",
            "مسودة تحصيل محتجز من عميل",
            [
                "سجل تحصيل محتجز", "تحصيل محتجز من العميل", "تحصيل المحتجز", "قبضت المحتجز", "استلمت المحتجز",
            ],
            [
                new VoiceSlot("contract", VoiceSlotKind.Text, "العقد", true,
                    ["للعقد", "العقد", "عقد"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "المبلغ المُحصَّل", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمتها", "الاجمالي", "اجمالي", "المجموع"], []),
                new VoiceSlot("method", VoiceSlotKind.Choice, "طريقة التحصيل", true,
                    ["نقد", "تحويل", "شيك", "شبكة"], ["نقد", "تحويل", "شيك", "شبكة"]),
                new VoiceSlot("collectedOn", VoiceSlotKind.Date, "تاريخ التحصيل", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "contracting.retention_register.query",
            VoiceSection.Contracting,
            BabelModule.Projects,
            VoiceIntentKind.Query,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "readRetentionRegister",
            "كشف المحتجزات",
            [
                "كشف المحتجزات", "كم المحتجز في العقد", "وش المحتجز في العقد", "المحتجزات في العقد",
            ],
            [
                new VoiceSlot("contract", VoiceSlotKind.Text, "العقد", true,
                    ["للعقد", "العقد", "عقد"], []),
            ],
            false,
            null),

        new VoiceIntent(
            "contracting.retention_release.draft",
            VoiceSection.Contracting,
            BabelModule.Projects,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "projects.retention.released",
            "draftRetentionRelease",
            "مسودة إفراج عن محتجز",
            [
                "سجل افراج عن محتجز", "الافراج عن المحتجز", "افراج عن محتجز", "افرج عن المحتجز", "اطلق المحتجز",
            ],
            [
                new VoiceSlot("contract", VoiceSlotKind.Text, "العقد", true,
                    ["للعقد", "العقد", "عقد"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "المبلغ المُفرَج عنه", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمتها", "الاجمالي", "اجمالي", "المجموع"], []),
                new VoiceSlot("releasedOn", VoiceSlotKind.Date, "تاريخ الإفراج", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "contracting.subcontractor_advance.record",
            VoiceSection.Contracting,
            BabelModule.Projects,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "projects.subcontractor_advance.paid",
            "draftSubcontractorAdvance",
            "دفعة مقدمة لمقاول من الباطن",
            [
                "دفعة مقدمة لمقاول", "سلفة مقاول من الباطن", "صرفت دفعة مقدمة", "دفعة مقدمة للمقاول",
            ],
            [
                new VoiceSlot("subcontractor", VoiceSlotKind.Text, "المقاول من الباطن", true,
                    ["للمقاول", "المقاول", "مقاول", "لصالح"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "المبلغ", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمته"], []),
                new VoiceSlot("paidOn", VoiceSlotKind.Date, "تاريخ الصرف", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "contracting.subcontractor_certificate.measure",
            VoiceSection.Contracting,
            BabelModule.Projects,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "projects.subcontractor_certificate.posted",
            "draftSubcontractorCertificate",
            "قياس بندٍ في مستخلص مقاول من الباطن",
            [
                "مستخلص مقاول من الباطن", "مستخلص من الباطن", "سجل مستخلص مقاول", "قياس مقاول الباطن",
            ],
            [
                new VoiceSlot("subcontract", VoiceSlotKind.Text, "عقد الباطن", true,
                    ["عقد", "العقد", "للعقد"], []),
                new VoiceSlot("boqItem", VoiceSlotKind.Text, "بند جدول الكميات", true,
                    ["بند", "البند", "للبند"], []),
                new VoiceSlot("quantity", VoiceSlotKind.Quantity, "الكمية المنفذة", true,
                    ["كمية", "الكمية", "بمقدار", "عدد"], []),
                new VoiceSlot("measuredOn", VoiceSlotKind.Date, "تاريخ القياس", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "contracting.subcontractor_statement.query",
            VoiceSection.Contracting,
            BabelModule.Projects,
            VoiceIntentKind.Query,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "readSubcontractorStatement",
            "كشف حساب مقاول من الباطن",
            [
                "كشف حساب مقاول من الباطن", "كشف حساب المقاول", "كشف المقاول", "وش موقف المقاول",
            ],
            [
                new VoiceSlot("subcontractor", VoiceSlotKind.Text, "المقاول من الباطن", true,
                    ["للمقاول", "المقاول", "مقاول"], []),
            ],
            false,
            null),
    ];
}
