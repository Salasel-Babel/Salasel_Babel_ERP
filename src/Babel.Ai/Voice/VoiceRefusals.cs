using System.Globalization;
using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Ai.Voice;

/// <summary>
/// <b>مفردات الرفض المنطوقة.</b> أربع جُمل يسمعها المستخدم أكثر من غيرها، وكلٌّ منها
/// <b>تسمّي ما ينقص بعينه</b> ولا تكتفي بأن تقول «خطأ».
/// <list type="bullet">
///   <item><b>لم أفهم</b> — لم تُطابَق نيّةٌ واحدة، أو طُوبقت نيّتان.</item>
///   <item><b>ينقصني كذا</b> — النيّة مفهومة وشريحةٌ لازمة غائبة، فتُسمّى باسمها العربي.</item>
///   <item><b>هذه العملية تحتاج تأكيداً</b> — كل ما يكتب أو يحرّك أو يصرف.</item>
///   <item><b>لا أملك صلاحية</b> — الاستحقاق أو الدور يمنع، والمنع يُقال ولا يُخفى.</item>
/// </list>
/// <para>
/// <b>ولماذا ملفٌّ قائم بذاته:</b> رسائل الرفض هي <b>واجهة المنتج</b> في المسار المنطوق
/// لا حاشيته. مستخدمٌ يعمل بيدين مشغولتين لا يقرأ شاشة، فالجملة المنطوقة هي كل ما يصله؛
/// وجملةٌ عامّة («تعذّر إتمام العملية») تجعله يعيد المحاولة بالكلام نفسه إلى ما لا نهاية.
/// </para>
/// </summary>
public static class VoiceRefusals
{
    /// <summary>صدر جملة «لم أفهم» كما تُنطق.</summary>
    public const string NotUnderstoodAr = "لم أفهم";

    /// <summary>صدر جملة «ينقصني».</summary>
    public const string MissingAr = "ينقصني";

    /// <summary>صدر جملة التأكيد.</summary>
    public const string NeedsConfirmationAr = "هذه العملية تحتاج تأكيداً";

    /// <summary>صدر جملة الصلاحية.</summary>
    public const string NotPermittedAr = "لا أملك صلاحية";

    /// <summary>لم تُطابَق نيّة واحدة.</summary>
    /// <param name="transcript">التفريغ كما ورد.</param>
    public static Error NotUnderstood(string transcript) => new(
        "ai.voice.intent_not_understood",
        NotUnderstoodAr + ". قلتَ: «" + transcript + "» — ولا يطابق أمراً أعرفه. "
        + "أعِد الجملة بفعلٍ في أوّلها: «سجّل» أو «اصرف» أو «كم».",
        "I did not understand: '" + transcript + "' matches no known command.");

    /// <summary>طُوبقت نيّتان فأكثر — <b>ولا تُختار إحداهما</b>.</summary>
    /// <param name="transcript">التفريغ.</param>
    /// <param name="intentIds">النيّات المتطابقة.</param>
    public static Error Ambiguous(string transcript, IReadOnlyList<string> intentIds)
    {
        ArgumentNullException.ThrowIfNull(intentIds);
        return new Error(
            "ai.voice.intent_ambiguous",
            NotUnderstoodAr + " أيّهما تقصد. «" + transcript + "» يطابق "
            + string.Join(" و", intentIds) + ". "
            + "واختيارُ أحدهما بالصدفة يُنفّذ عمليةً لم تُطلَب، فيُطلب منك التخصيص.",
            "Ambiguous: '" + transcript + "' matches " + string.Join(", ", intentIds) + "; no branch is chosen.");
    }

    /// <summary>شريحةٌ لازمة غائبة — تُسمّى باسمها العربي.</summary>
    /// <param name="intent">النيّة.</param>
    /// <param name="slot">الشريحة الناقصة.</param>
    public static Error SlotMissing(VoiceIntent intent, VoiceSlot slot)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(slot);
        return new Error(
            "ai.voice.slot_missing",
            MissingAr + " " + slot.NameAr + " في «" + intent.NameAr + "». "
            + "ولا يُخترَع: قيمةٌ مخمَّنة في مستندٍ يُرحَّل أسوأ من حقلٍ فارغ.",
            "Missing slot '" + slot.Name + "' for intent '" + intent.Id + "'; it is refused, not invented.");
    }

    /// <summary>كمّية بلا وحدة — والوحدة جزء القيمة لا زينتها.</summary>
    /// <param name="slot">الشريحة.</param>
    /// <param name="heard">ما سُمع.</param>
    public static Error UnitMissing(VoiceSlot slot, string heard)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return new Error(
            "ai.voice.unit_missing",
            MissingAr + " وحدة " + slot.NameAr + ": سمعتُ «" + heard + "» بلا وحدة. "
            + "وللصنف الواحد أكثر من وحدة، وفرقُ التفسير بين الحبّة والكرتون يصل إلى المال — فقُل الوحدة.",
            "Missing the unit of slot '" + slot.Name + "': heard '" + heard + "' with no unit.");
    }

    /// <summary>قيمةٌ خارج قائمة مغلقة.</summary>
    /// <param name="slot">الشريحة.</param>
    /// <param name="heard">ما سُمع.</param>
    public static Error ChoiceNotInList(VoiceSlot slot, string heard)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return new Error(
            "ai.voice.choice_not_in_list",
            NotUnderstoodAr + " «" + heard + "» في " + slot.NameAr + ". "
            + "والقائمة مغلقة: " + string.Join(" · ", slot.Choices) + ".",
            "'" + heard + "' is not in the closed list for slot '" + slot.Name + "'.");
    }

    /// <summary>عمليةٌ تُغيّر الحال بلا تأكيد.</summary>
    /// <param name="intent">النيّة.</param>
    public static Error ConfirmationRequired(VoiceIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return new Error(
            "ai.voice.confirmation_required",
            NeedsConfirmationAr + ": «" + intent.NameAr + "». "
            + "اسمع الملخّص أو اقرأه، ثم قل «تأكيد» أو اضغط زرّ التأكيد. "
            + "وما يكتب في الدفتر أو يحرّك مخزوناً أو يصرف لإنسان لا يمرّ بلا هذه الخطوة — بلا استثناء.",
            "Confirmation required for intent '" + intent.Id + "'; nothing that writes, moves stock or pays a person passes without it.");
    }

    /// <summary>تأكيدٌ لا يطابق الأمر الذي قُرئ.</summary>
    public static readonly Error ConfirmationMismatch = new(
        "ai.voice.confirmation_mismatch",
        "التأكيد لا يطابق الأمر المقروء. تغيّر الأمر بعد قراءته، فبَطَل التأكيد. "
        + "والقبول هنا يعني تنفيذ أمرٍ لم يسمعه أحد.",
        "The confirmation does not match the command that was read back; it is refused.");

    /// <summary>الاستحقاق أو الدور يمنع.</summary>
    /// <param name="intent">النيّة.</param>
    public static Error NotPermitted(VoiceIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return new Error(
            "ai.voice.not_permitted",
            NotPermittedAr + " «" + intent.NameAr + "» في هذه المنشأة. "
            + "والمسار المنطوق لا يفتح باباً مغلقاً على الشاشة: هو مدخلٌ آخر إلى الصلاحيات نفسها.",
            "Not permitted: intent '" + intent.Id + "'. Voice is another door to the same entitlements, never a wider one.");
    }

    /// <summary>
    /// نُطق دليلُ شركةٍ داخل أمرٍ آخر. <b>ولا يُسمّى ما نُطق</b>: تسميتُه تقتضي تحليلَ
    /// اسمٍ من الكلام، وتحليلُ الاسم هو بعينه ما حُذف من هذا المسار — وحكمُ الهوية
    /// بتساوي نصّين هو ما كان يُبنى عليه.
    /// </summary>
    public static readonly Error CompanyNotSwitched = new(
        "ai.voice.company_not_switched",
        "لا أنتقل بين المنشآت بالكلام داخل أمرٍ آخر. المنشأة المفتوحة هي التي يُنفَّذ فيها الأمر — "
        + "بدّلها من شاشة المنشآت ثم أعِد قول الأمر.",
        "Voice never switches company inside another command; switch it on the companies screen, then say the command again.");

    /// <summary>
    /// طرفٌ لم يُحلّ بعد — <b>والرفض هنا هو الفرق كلّه</b>: كان القارئ يمرّر المقطع نصّاً
    /// فيصير طرفَ المستند، وصار المقطع يُسأل عنه السجلّ ولا يمرّ قبل الجواب.
    /// </summary>
    /// <param name="slot">الشريحة.</param>
    /// <param name="heard">المقطع كما سُمع.</param>
    public static Error NameUnresolved(VoiceSlot slot, string heard)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return new Error(
            "ai.voice.name_unresolved",
            slot.NameAr + " لم يُحلّ بعد: سمعتُ «" + heard + "» ولم أسأل السجلّ عنه. "
            + "ولا أختار طرفاً بالتخمين — اسمٌ قريبٌ من اسمٍ آخر يُنشئ مستنداً على طرفٍ لم يُقصد.",
            "Slot '" + slot.Name + "' heard '" + heard + "' and is not resolved against the register yet.");
    }

    /// <summary>الاسم ليس في السجلّ — ولا يُقرَّب إلى أقربه.</summary>
    /// <param name="slot">الشريحة.</param>
    /// <param name="heard">المقطع كما سُمع.</param>
    public static Error NameNotInRegister(VoiceSlot slot, string heard)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return new Error(
            "ai.voice.name_not_in_register",
            "لا أجد «" + heard + "» في سجلّ " + slot.NameAr + " في هذه المنشأة. "
            + "أضِفه من شاشته ثم أعِد قول الأمر — ولا أُنشئه بالكلام ولا أختار أقرب شبيهٍ له.",
            "'" + heard + "' is not in the register for slot '" + slot.Name + "' in this tenant; nothing is guessed.");
    }

    /// <summary>أكثر من مطابق — <b>ولا يُقال كم</b>، ولا يُختار أعلاهم تشابهاً.</summary>
    /// <param name="slot">الشريحة.</param>
    /// <param name="heard">المقطع كما سُمع.</param>
    public static Error NameNeedsQuestion(VoiceSlot slot, string heard)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return new Error(
            "ai.voice.name_needs_question",
            "أيّ " + slot.NameAr + " تقصد بـ«" + heard + "»؟ اختر من الورقة المعروضة. "
            + "واختيارُ أحدهم بالصدفة يُنشئ مستنداً على طرفٍ لم يُطلَب.",
            "Slot '" + slot.Name + "' heard '" + heard + "' and needs a question sheet; no best match is chosen.");
    }

    /// <summary>نيّةٌ تنتظر قراراً من مالك المنتج.</summary>
    /// <param name="intent">النيّة.</param>
    public static Error OwnerDecisionPending(VoiceIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return new Error(
            "ai.voice.owner_decision_pending",
            "فهمتُ «" + intent.NameAr + "» ولا أنفّذها: " + (intent.OwnerDecisionAr ?? string.Empty)
            + " والامتناع هنا مقصود — تنفيذُها يقتضي اختراع تفسيرٍ لم يقرّره أحد.",
            "Understood intent '" + intent.Id + "' but will not execute it; the Arabic message names the missing owner decision.");
    }

    /// <summary>محاولة نُطقِ قيمةٍ شخصية غير مُقنَّعة.</summary>
    /// <param name="what">وصف القيمة.</param>
    public static Error MaskedReadRequired(string what) => new(
        "ai.voice.masked_read_required",
        "لا أنطق " + what + " كاملاً. القراءة المُقنَّعة وحدها تخرج من هذا المسار: "
        + "والصوت يُسمَع في غرفةٍ فيها غير صاحب البيان، والشاشة لا تُسمَع.",
        "Refusing to speak " + what + " in full; only the masked read leaves this path.");

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>عدد الشرائح المقروءة يتجاوز الحدّ — حارسٌ على الكلام الطويل.</summary>
    /// <param name="count">العدد.</param>
    /// <param name="limit">الحدّ.</param>
    public static Error TooManySlots(int count, int limit) => new(
        "ai.voice.too_many_slots",
        "الأمر يحمل " + Num(count) + " شريحة وهو يتجاوز الحدّ " + Num(limit) + ".",
        "The command carries " + Num(count) + " slots, beyond the limit of " + Num(limit) + ".");
}
