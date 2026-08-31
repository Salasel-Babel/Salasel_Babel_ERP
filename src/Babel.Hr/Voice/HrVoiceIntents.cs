using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Hr.Voice;

/// <summary>
/// <b>ما تُسهم به وحدة الموارد البشرية في المسار المنطوق.</b>
/// <para>
/// <b>وهذا أضيقُ الأقسام صوتاً عن قصد.</b> الموارد البشرية تلمس بياناً شخصياً في كل
/// شاشة تقريباً، و<b>الصوت يُسمَع في الغرفة كلّها بينما الشاشة تُرى بزاويةٍ واحدة</b>.
/// فما بقي هنا ثلاث نيّاتٍ يقولها موظفُ شؤونٍ واقفٌ عند البوابة أو مشرفٌ في الورشة،
/// ولا تحمل واحدةٌ منها رقم هوية ولا آيباناً.
/// </para>
/// <para>
/// <b>وقراءة بيانات الموظف مُقنَّعة على هذا المسار بحكم البناء</b>: القيمة تخرج من
/// الوحدة بآخر أربعة محارف وحدها، والملخّص المنطوق يُقنّعها مرّةً ثانية، ثم يمرّ على
/// حارسٍ يرفض أي سلسلةٍ تشبه هويةً أو آيباناً غير مُقنَّع. ثلاث طبقات لأن التسريب هنا
/// لا يُسترجَع.
/// </para>
/// <para>
/// <b>وما لم يُعطَ صوتاً، ولماذا:</b>
/// <list type="bullet">
///   <item>
///     <b>مسيّر الرواتب واعتماده</b> — عمليةٌ تمسّ كل عاملٍ في المنشأة دفعةً واحدة،
///     وتُراجَع صفّاً صفّاً على شاشة قبل الاعتماد. وقراءةٌ مرتدّة لمئتَي قسيمة ليست
///     تأكيداً بل طقساً.
///   </item>
///   <item>
///     <b>إنهاء الخدمة</b> — قرارٌ يُغيّر حياة إنسان ويُوقَّع على ورق. وجعلُه جملةً
///     منطوقة يجعل خطأً في التفريغ يُنهي خدمة شخصٍ آخر يحمل اسماً مشابهاً.
///   </item>
///   <item>
///     <b>تسجيل الموظف وبياناته الشخصية</b> — رقم الهوية والآيبان يُمليان محرفاً محرفاً،
///     والتفريغ الصوتي يُخطئ في الرقم كلّه لا في محرفه. ولا يُنطَق أيٌّ منهما هنا أصلاً.
///   </item>
///   <item>
///     <b>صرف الرواتب وسداد التأمينات</b> — دفعٌ لجماعةٍ لا لفرد، ومكانُه المكتب.
///   </item>
/// </list>
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
            "hr.employee_advance.record",
            VoiceSection.HumanResources,
            BabelModule.Hr,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "سلفة موظف",
            "Employee advance",
            [
                "سجل سلفة موظف", "سلفة للموظف", "اصرف سلفة", "سلفة موظف",
                "ابغى اسجل سلفة",
            ],
            [
                new VoiceSlot("employee", VoiceSlotKind.Text, "الموظف", "Employee", true,
                    ["للموظف", "الموظف", "موظف", "لصالح"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "مبلغ السلفة", "Advance amount", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمتها"], []),
                new VoiceSlot("instalments", VoiceSlotKind.Number, "عدد الأقساط", "Instalments", false,
                    ["اقساط", "قسط", "على"], []),
                new VoiceSlot("grantedOn", VoiceSlotKind.Date, "تاريخ الصرف", "Granted on", true, [], []),
            ],
            false,
            null,
            null),

        new VoiceIntent(
            "hr.employee_deduction.record",
            VoiceSection.HumanResources,
            BabelModule.Hr,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "خصم على موظف",
            "Employee deduction",
            [
                "سجل خصم على الموظف", "خصم على الموظف", "جزاء على الموظف", "سجل جزاء",
            ],
            [
                new VoiceSlot("employee", VoiceSlotKind.Text, "الموظف", "Employee", true,
                    ["الموظف", "موظف", "على"], []),
                new VoiceSlot("amount", VoiceSlotKind.Money, "مبلغ الخصم", "Deduction amount", true,
                    ["بمبلغ", "مبلغ", "بقيمة", "قيمته"], []),
                new VoiceSlot("effectiveOn", VoiceSlotKind.Date, "تاريخ الاستحقاق", "Effective on", true, [], []),
            ],
            false,
            null,
            null),

        new VoiceIntent(
            "hr.employee.query",
            VoiceSection.HumanResources,
            BabelModule.Hr,
            VoiceIntentKind.Query,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "بطاقة موظف — مُقنَّعة",
            "Employee card — masked",
            [
                "بيانات الموظف", "كرت الموظف", "ملف الموظف", "بطاقة الموظف",
                "وش بيانات الموظف",
            ],
            [
                new VoiceSlot("employee", VoiceSlotKind.Text, "الموظف", "Employee", true,
                    ["الموظف", "موظف", "عن"], []),
            ],
            true,
            null,
            null),
    ];
}
