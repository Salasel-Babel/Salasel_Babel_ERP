using Babel.SharedKernel;

namespace Babel.Ai.Boundary;

/// <summary>
/// <b>مفردات رفض الحدّ — وهي جُملٌ تُقال لا رموزٌ تُسجَّل.</b>
/// <para>
/// كل جملة <b>تسمّي الشكل باسمه</b> وتقول أين يُكتب بدلاً من هنا. والقاعدة نفسها التي
/// كتبها <c>VoiceRefusals</c>: رسالةٌ عامّة تجعل المستخدم يعيد المحاولة بالكلام نفسه
/// إلى ما لا نهاية.
/// </para>
/// <para>
/// <b>ولا وجود لرسالة «حُذف كذا»</b> — لأن لا وجود لحذف. المسار يرفض ولا يُنقّح:
/// نصٌّ مُنقَّح يُسلَّم إلى النموذج جملةً <b>لم يقلها صاحبها</b>، والنموذج لا يعلم أن
/// حقلاً نُزع فيملأ الفراغ بثقة — وهو بعينه ما يرفضه هذا المستودع في
/// <c>VoiceRefusals.SlotMissing</c>: «قيمةٌ مخمَّنة في مستندٍ يُرحَّل أسوأ من حقلٍ فارغ».
/// </para>
/// </summary>
public static class AgentBoundaryErrors
{
    /// <summary>صدر رموز الرفض كلّها.</summary>
    public const string CodePrefix = "ai.agent.identifier_refused.";

    /// <summary>
    /// الجملة التي تظهر في اللوحة عند أي رفض، فوق الجملة الخاصّة بالشكل. تُذكر هنا لا
    /// في الواجهة كي تكون العربية <b>مصدراً واحداً</b>، والواجهة تعرضها ولا تؤلّفها.
    /// </summary>
    public const string PanelRefusalAr =
        "لا أُرسل رقم الهوية إلى النموذج. اكتبه في الحقل على الشاشة — "
        + "والمسار هذا لا يمرّ به رقمُ هوية ولا آيبان ولا رقم تسجيل ضريبي، لا مقنَّعاً ولا كاملاً.";

    /// <summary>رقم هوية أو إقامة: عشر خانات تبدأ بـ١ أو ٢.</summary>
    public static readonly Error NationalId = new(
        CodePrefix + "national_id",
        "لا أُرسل رقم الهوية أو الإقامة إلى النموذج. اكتبه في حقله على الشاشة.",
        "A national or residency identity number is never sent to the model; type it in its own field on the screen.");

    /// <summary>رقم آيبان سعودي، متّصلاً كان أو مكتوباً مجموعاتٍ رباعية.</summary>
    public static readonly Error Iban = new(
        CodePrefix + "iban",
        "لا أُرسل رقم الآيبان.",
        "An IBAN is never sent to the model.");

    /// <summary>رقم تسجيل ضريبي سعودي.</summary>
    public static readonly Error Vat = new(
        CodePrefix + "vat",
        "لا أُرسل رقم التسجيل الضريبي — وهو خمس عشرة خانة تبدأ بـ٣ وتنتهي بـ٣.",
        "A VAT registration number is never sent to the model; it is fifteen digits beginning and ending with 3.");

    /// <summary>عشر خانات لا يُقطع بأيّهما هي — <b>ولا يُخمَّن</b>.</summary>
    public static readonly Error CommercialRegisterOrNationalId = new(
        CodePrefix + "cr_or_national_id",
        "عشر خانات متتالية: قد تكون سجلاً تجارياً أو رقم هوية. لا أُرسلها ولا أُخمّن أيّهما.",
        "Ten consecutive digits: this may be a commercial-register number or an identity number. It is not sent, and which one it is is not guessed.");

    /// <summary>رقم جوال سعودي.</summary>
    public static readonly Error Phone = new(
        CodePrefix + "phone",
        "لا أُرسل رقم الجوال.",
        "A mobile number is never sent to the model.");

    /// <summary>الشبكة الأخيرة: سلسلة رقمية طويلة لم يطابقها شكلٌ مُسمّى.</summary>
    public static readonly Error DigitRun = new(
        CodePrefix + "digit_run",
        "سلسلة رقمية طويلة تشبه معرّفاً. لا تمرّ من هنا.",
        "A long digit run that looks like an identifier does not pass this boundary.");

    /// <summary>
    /// قيمةٌ مقنَّعة. <b>والقناع ليس إذناً</b>: أربع خانات مع اسمٍ في الجملة نفسها
    /// تُعيد التعرّف على صاحبها. والقناع موضعه ورقة السؤال التي يرسمها الخادم ولا يراها
    /// النموذج، لا نسخةُ المحادثة.
    /// </summary>
    public static readonly Error MaskedValue = new(
        CodePrefix + "masked_value",
        "حتى القيمة المُقنَّعة لا تعبر: قناعٌ مع اسمٍ في الجملة نفسها يكفي للتعرّف على صاحبه. "
        + "والقناع يُعرض على الشاشة ولا يدخل نصّ المحادثة.",
        "Even a masked value does not cross: a mask beside a name re-identifies. Masks belong on the screen, never in the transcript.");

    /// <summary>ظرفٌ بلا جزء واحد — ونداءٌ بلا محتوى لا يُرسَل.</summary>
    public static readonly Error OutboundEmpty = new(
        "ai.agent.outbound_empty",
        "لا شيء يُرسَل: الظرف فارغ. ونداءٌ بلا محتوى يستهلك دوراً ويعود بجوابٍ عن لا شيء.",
        "Nothing to send: the envelope is empty.");
}
