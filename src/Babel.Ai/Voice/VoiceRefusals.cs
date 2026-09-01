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

    /// <summary>
    /// <b>مقطعٌ حرّ لا يُعرف أين ينتهي.</b> النافذة من دليل الشريحة إلى أوّل حدٍّ أعرضُ
    /// من أن تكون اسماً، ولا سجلّ يحدّها.
    /// <para>
    /// <b>ولماذا رفضٌ لا اقتطاع:</b> اقتطاعُ الاسم عند عرضٍ مختار تخمينٌ يلبس ثوب
    /// القاعدة، ويُنتج مستنداً <b>صحيح الشكل على طرفٍ آخر</b> — وهو أسوأ من الرفض،
    /// لأنه يمرّ. والجملةُ تسمّي ما لم يُفهم كي يُقال أمراً مستقلاً.
    /// </para>
    /// </summary>
    /// <param name="slot">الشريحة.</param>
    /// <param name="heard">النافذة كما سُمعت.</param>
    /// <param name="limit">أقصى عرضٍ يُقبل بلا سجلّ.</param>
    public static Error BoundaryNotFound(VoiceSlot slot, string heard, int limit)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return new Error(
            "ai.voice.slot_boundary_not_found",
            NotUnderstoodAr + " أين ينتهي " + slot.NameAr + ". سمعتُ «" + heard + "» — و"
            + Num(limit) + " كلمات حدُّ ما أقبله اسماً بلا سجلّ. "
            + "ولا أقتطع: اسمٌ مقطوع عند كلمةٍ اخترتُها أنا يصنع مستنداً صحيح الشكل على طرفٍ آخر. "
            + "قُل " + slot.NameAr + " وحده، ثم قُل الباقي أمراً مستقلاً.",
            "Cannot bound slot '" + slot.Name + "': heard '" + heard + "', beyond the "
            + Num(limit) + "-word limit that applies with no register; it is refused, not truncated.");
    }

    /// <summary>
    /// <b>اسمان مسجَّلان يبدآن المقطع بالطول نفسه</b> — فيُرفض ولا يُقترع، بالمقياس
    /// نفسه الذي يرفض نيّتين متعادلتين.
    /// </summary>
    /// <param name="slot">الشريحة.</param>
    /// <param name="heard">النافذة كما سُمعت.</param>
    public static Error BoundaryAmbiguous(VoiceSlot slot, string heard)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return new Error(
            "ai.voice.slot_boundary_ambiguous",
            NotUnderstoodAr + " أيَّ " + slot.NameAr + " تقصد في «" + heard + "»: "
            + "اسمان مسجَّلان يبدآن الكلام بالطول نفسه. "
            + "واختيارُ أحدهما بالصدفة يكتب المستند على طرفٍ لم تقصده، فيُطلب منك التخصيص.",
            "Two registered names of equal length prefix slot '" + slot.Name + "' in '" + heard
            + "'; the tie is refused, not drawn.");
    }

    /// <summary>
    /// اسمٌ ليس في السجلّ. <b>ولا يُقارَب بأقرب شبيه</b>: تقريبٌ في اسم طرفٍ يُنتج
    /// مستنداً صحيح الشكل على طرفٍ آخر.
    /// </summary>
    /// <param name="slot">الشريحة.</param>
    /// <param name="heard">ما سُمع.</param>
    public static Error NameNotInRegister(VoiceSlot slot, string heard)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return new Error(
            "ai.voice.name_not_in_register",
            NotUnderstoodAr + " «" + heard + "» في " + slot.NameAr + " — لا أجده في السجلّ. "
            + "ولا أُقارِبه بأقرب شبيه: تقريبٌ في اسم طرفٍ يكتب المستند على غيره ولا يراه أحد. "
            + "سجّله أوّلاً، ثم أعِد الأمر.",
            "'" + heard + "' is not in the register for slot '" + slot.Name
            + "'; no nearest-neighbour match is attempted.");
    }

    /// <summary>
    /// <b>فضلةٌ بقيت بعد الاسم المسجَّل</b> — تُسمّى ولا تُبتلع ولا تُلقى. وهي الخطّاف
    /// الذي يتعلّق به الأمر المركَّب حين يصير خطّةً متعدّدة الخطوات.
    /// </summary>
    /// <param name="slot">الشريحة.</param>
    /// <param name="name">الاسم كما سُجّل.</param>
    /// <param name="residue">ما بقي.</param>
    public static Error ResidueNotUnderstood(VoiceSlot slot, string name, string residue)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return new Error(
            "ai.voice.residue_not_understood",
            "فهمتُ " + slot.NameAr + ": " + name + ". ولم أفهم: «" + residue + "» — "
            + "قُلْه أمراً مستقلاً. وما لا يُفهم يُسمّى ولا يُبتلع في اسمٍ ولا يُلقى بصمت.",
            "Understood slot '" + slot.Name + "' as '" + name + "'; the residue '" + residue
            + "' is named, neither swallowed into the name nor discarded.");
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

    /// <summary>شركةٌ منطوقة غير الشركة المفتوحة.</summary>
    /// <param name="spoken">ما نُطق.</param>
    /// <param name="current">الشركة المفتوحة.</param>
    public static Error CompanyNotSwitched(string spoken, string current) => new(
        "ai.voice.company_not_switched",
        "قلتَ «" + spoken + "» والمفتوح الآن «" + current + "». "
        + "ولا أنتقل بين الشركات بالكلام داخل أمرٍ آخر: بدّل الشركة بخطوةٍ صريحة ثم أعِد الأمر.",
        "You said '" + spoken + "' while '" + current + "' is open; voice never switches company inside another command.");

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
