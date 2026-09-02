using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Hr.Voice;

/// <summary>
/// <b>ما تُسهم به وحدة الموارد البشرية في المسار المنطوق.</b>
/// <para>
/// <b>القاعدة:</b> الصوت يبلغ <b>كل مسوّدة</b> ولا يبلغ <b>ترحيلاً</b>. فمسيّر الرواتب
/// و<b>صرفُه</b> وسدادُ التأمينات ومخصّصُ نهاية الخدمة وتصفيتُها تُملى كلّها، وتظهر
/// مسوّدةً على الشاشة، <b>وتُراجَع صفّاً صفّاً ثم تُرحَّل بيدٍ</b>. وما كان يُقال —
/// «قراءةٌ مرتدّة لمئتَي قسيمة ليست تأكيداً بل طقس» — <b>صحيحٌ تماماً</b>، ولذلك لم
/// تعد القراءة المرتدّة هي الحارس: <b>الحارس صار الشاشة</b>.
/// </para>
/// <para>
/// <b>والاستثناء الذي بقي هنا وحده بين الأقسام — ولسببٍ آخر غير التعقيد:</b> لا يُنطَق
/// <b>معرّفٌ شخصي</b>: رقم هوية ولا آيبان. <b>وهذا سببُ خصوصية لا سببُ صعوبة</b>: الصوت
/// يُسمَع في الغرفة كلّها بينما الشاشة تُرى بزاويةٍ واحدة. فالمسوّدة تُملى، والمعرّفُ لا.
/// ولذلك <b>لا نيّة هنا تحمل شريحةَ هويةٍ ولا آيبان</b> — والقناعُ بعده ثلاث طبقات:
/// الوحدة تُخرج آخر أربعة محارف وحدها، والملخّص يُقنّع مرّةً ثانية، ثم يمرّ على حارسٍ
/// يرفض أي سلسلةٍ تشبه هويةً أو آيباناً غير مُقنَّع.
/// </para>
/// <para>
/// <b>وإنهاء الخدمة نفسه لا يُبلَغ</b>: <c>terminateEmployee</c> فعلٌ لا يُعكَس — وهو
/// ممنوعٌ بالبناء لا بالانضباط (حارسُ الأفعال يُسقط البناء). <b>وأثرُه المالي يُملى
/// كاملاً</b> عبر مسوّدة تصفية نهاية الخدمة.
/// </para>
/// </summary>
public sealed class HrVoiceIntents : IVoiceIntentCatalogue
{
    /// <inheritdoc />
    public BabelModule Module => BabelModule.Hr;

    /// <inheritdoc />
    public IReadOnlyList<VoiceIntent> Intents { get; } =
    [
        new VoiceIntent(
            "hr.employee.query",
            VoiceSection.HumanResources,
            BabelModule.Hr,
            VoiceIntentKind.Query,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "readEmployee",
            "بطاقة موظف — مُقنَّعة",
            [
                "بيانات الموظف", "كرت الموظف", "ملف الموظف", "بطاقة الموظف", "وش بيانات الموظف",
            ],
            [
                new VoiceSlot("employee", VoiceSlotKind.Entity, "الموظف", true,
                    ["الموظف", "موظف", "عن"], [], "employee"),
            ],
            true,
            null),

        new VoiceIntent(
            "hr.employee_advance.record",
            VoiceSection.HumanResources,
            BabelModule.Hr,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "draftEmployeeAdvance",
            "سلفة موظف",
            [
                "سجل سلفة موظف", "سلفة للموظف", "اصرف سلفة", "سلفة موظف", "ابغى اسجل سلفة",
            ],
            [
                new VoiceSlot("employee", VoiceSlotKind.Entity, "الموظف", true,
                    ["للموظف", "الموظف", "موظف", "لصالح"], [], "employee"),
                new VoiceSlot("amount", VoiceSlotKind.Money, "مبلغ السلفة", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمتها"], []),
                new VoiceSlot("instalments", VoiceSlotKind.Number, "عدد الأقساط", false,
                    ["اقساط", "قسط", "على"], []),
                new VoiceSlot("grantedOn", VoiceSlotKind.Date, "تاريخ الصرف", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "hr.employee_deduction.record",
            VoiceSection.HumanResources,
            BabelModule.Hr,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "recordEmployeeDeduction",
            "خصم على موظف",
            [
                "سجل خصم على الموظف", "خصم على الموظف", "جزاء على الموظف", "سجل جزاء",
            ],
            [
                new VoiceSlot("employee", VoiceSlotKind.Entity, "الموظف", true,
                    ["الموظف", "موظف", "على"], [], "employee"),
                new VoiceSlot("amount", VoiceSlotKind.Money, "مبلغ الخصم", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمته"], []),
                new VoiceSlot("effectiveOn", VoiceSlotKind.Date, "تاريخ الاستحقاق", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "hr.end_of_service_provision.draft",
            VoiceSection.HumanResources,
            BabelModule.Hr,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "hr.end_of_service.accrual",
            "draftEndOfServiceProvision",
            "مسودة مخصص نهاية الخدمة",
            [
                "سجل مخصص نهاية الخدمة", "مخصص نهاية الخدمة", "استحقاق نهاية الخدمة", "احتساب المخصص",
            ],
            [
                new VoiceSlot("periodCode", VoiceSlotKind.Code, "رمز الفترة", true,
                    ["لفترة", "الفترة", "لشهر", "عن شهر"], []),
                new VoiceSlot("accruedOn", VoiceSlotKind.Date, "تاريخ الاستحقاق", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "hr.end_of_service_settlement.draft",
            VoiceSection.HumanResources,
            BabelModule.Hr,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "hr.end_of_service.settlement",
            "draftEndOfServiceSettlement",
            "مسودة تصفية نهاية خدمة",
            [
                "سجل تصفية نهاية الخدمة", "تصفية نهاية الخدمة", "مستحقات نهاية الخدمة", "صرف نهاية الخدمة",
            ],
            [
                new VoiceSlot("employee", VoiceSlotKind.Entity, "الموظف", true,
                    ["للموظف", "الموظف", "موظف"], [], "employee"),
                new VoiceSlot("amount", VoiceSlotKind.Money, "المستحق", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمتها", "الاجمالي", "اجمالي", "المجموع"], []),
                new VoiceSlot("method", VoiceSlotKind.Choice, "طريقة الصرف", true,
                    ["نقد", "تحويل", "شيك", "شبكة"], ["نقد", "تحويل", "شيك", "شبكة"]),
                new VoiceSlot("settledOn", VoiceSlotKind.Date, "تاريخ التصفية", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "hr.payroll_payment.draft",
            VoiceSection.HumanResources,
            BabelModule.Hr,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "hr.payroll.payment",
            "draftPayrollPayment",
            "مسودة صرف رواتب",
            [
                "سجل صرف الرواتب", "اصرف الرواتب", "صرف الرواتب", "دفع الرواتب",
            ],
            [
                new VoiceSlot("runNumber", VoiceSlotKind.Code, "المسير", true,
                    ["لمسير", "المسير رقم", "المسير"], []),
                new VoiceSlot("method", VoiceSlotKind.Choice, "طريقة الصرف", true,
                    ["نقد", "تحويل", "شيك", "شبكة"], ["نقد", "تحويل", "شيك", "شبكة"]),
                new VoiceSlot("paidOn", VoiceSlotKind.Date, "تاريخ الصرف", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "hr.payroll_run.draft",
            VoiceSection.HumanResources,
            BabelModule.Hr,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "hr.payroll.accrual",
            "draftPayrollRun",
            "مسودة مسير رواتب",
            [
                "جهز مسير الرواتب", "افتح مسير رواتب", "سوي مسير الرواتب", "مسير الرواتب", "مسير رواتب",
            ],
            [
                new VoiceSlot("periodCode", VoiceSlotKind.Code, "رمز الفترة", true,
                    ["لفترة", "الفترة", "لشهر", "عن شهر"], []),
                new VoiceSlot("preparedOn", VoiceSlotKind.Date, "تاريخ الإعداد", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "hr.social_insurance_payment.draft",
            VoiceSection.HumanResources,
            BabelModule.Hr,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "hr.social_insurance.payment",
            "draftSocialInsurancePayment",
            "مسودة سداد تأمينات اجتماعية",
            [
                "سجل سداد التامينات", "سداد التامينات الاجتماعية", "سداد التامينات", "دفعت التامينات",
            ],
            [
                new VoiceSlot("periodCode", VoiceSlotKind.Code, "رمز الفترة", true,
                    ["لفترة", "الفترة", "لشهر", "عن شهر"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "المبلغ المسدَّد", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمتها", "الاجمالي", "اجمالي", "المجموع"], []),
                new VoiceSlot("method", VoiceSlotKind.Choice, "طريقة السداد", true,
                    ["نقد", "تحويل", "شيك", "شبكة"], ["نقد", "تحويل", "شيك", "شبكة"]),
                new VoiceSlot("paidOn", VoiceSlotKind.Date, "تاريخ السداد", true,
                    [], []),
            ],
            false,
            null),
    ];
}
