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
    /// <b>العلاج المُتاح — يُذكر في الأشكال «المجرّدة» وحدها، وهي التي تُنتج الإنذار الكاذب.</b>
    /// <para>
    /// <b>ولماذا يلزم نصٌّ ثانٍ أصلاً:</b> «عشر خاناتٍ تبدأ بـ١» هو شكلُ رقم هوية
    /// <b>وشكلُ مبلغٍ بمليار ونصف كُتب بلا فواصل</b> معاً، ولا شيء محلّيّ يفرّق بينهما.
    /// فالرفض صحيح — لكن «اكتبه في حقله على الشاشة» جملةٌ <b>غير قابلة للتنفيذ</b> حين
    /// يكون الرقم هو المبلغَ الذي طُلب من الوكيل أن يتصرّف به: لا حقل على الشاشة لشيءٍ
    /// لم يُفتح بعد. فتُقال الصورتان معاً ويُقال العلاج: الفواصل تُعيد كتابة المبلغ نصّاً
    /// <b>يعبر</b> (‏<c>1,500,000,000</c> لا تُلمّ نقطتُه ولا فاصلتُه في شكلٍ مجرَّد)،
    /// والشرطات تُعيد كتابة رقم المستند (‏<c>INV-2026-000412</c>). <b>ورسالةٌ تسمّي
    /// المخرج تُنهي الدورة، ورسالةٌ تسمّي التهمة وحدها تُعيدها.</b>
    /// </para>
    /// </summary>
    public const string AmountRemedyAr =
        "وإن كان مبلغاً أو رقمَ مستند فأعد كتابته بفواصله أو بشرطاته — 1,500,000,000 "
        + "و INV-2026-000412 يعبران كما هما.";

    /// <summary>الإنجليزية المقابلة لـ<see cref="AmountRemedyAr"/>.</summary>
    public const string AmountRemedyEn =
        "If it is an amount or a document number, write it with separators or dashes: "
        + "1,500,000,000 and INV-2026-000412 both pass unchanged.";

    /// <summary>
    /// الجملة التي تظهر في اللوحة عند أي رفض، فوق الجملة الخاصّة بالشكل. تُذكر هنا لا
    /// في الواجهة كي تكون العربية <b>مصدراً واحداً</b>، والواجهة تعرضها ولا تؤلّفها.
    /// <para>
    /// <b>وهي ليست ثابتاً معلّقاً:</b> ملفّ اللغة العربية في المتصفّح يحمل نصَّها
    /// حرفاً بحرف (‏<c>agent.boundary.panelRefusal</c>)، وحارسٌ يقرأ ذلك الملفّ نفسه
    /// ويطابقه بهذا الثابت — على منوال <c>TheBrowserCatalogueMirrorsTheServer</c>. وثابتٌ
    /// لا يقرؤه شيء يُحرَّر أو يُفرَّغ أو يُحذف <b>بلا أن يحمرّ شيء</b>، ويظلّ يُقال إن
    /// هنا مصدراً واحداً وليس كذلك. وصياغتُها لا تسمّي شكلاً بعينه: صدرٌ يقول «الهوية»
    /// فوق جملةِ آيبانٍ يتّهم غير الجاني.
    /// </para>
    /// </summary>
    public const string PanelRefusalAr =
        "لا يعبر هذا الطريق ما شكلُه معرّف — لا هويةً ولا آيباناً ولا رقمَ تسجيلٍ ضريبي "
        + "ولا جوّالاً، لا مقنَّعاً ولا كاملاً. اكتبه في حقله على الشاشة.";

    /// <summary>رقم هوية أو إقامة: عشر خانات تبدأ بـ١ أو ٢.</summary>
    public static readonly Error NationalId = new(
        CodePrefix + "national_id",
        "لا أُرسل رقم الهوية أو الإقامة إلى النموذج. اكتبه في حقله على الشاشة. "
        + AmountRemedyAr,
        "A national or residency identity number is never sent to the model; type it in its own field on the screen. "
        + AmountRemedyEn);

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
        "عشر خانات متتالية: قد تكون سجلاً تجارياً أو رقم هوية. لا أُرسلها ولا أُخمّن أيّهما. "
        + AmountRemedyAr,
        "Ten consecutive digits: this may be a commercial-register number or an identity number. "
        + "It is not sent, and which one it is is not guessed. " + AmountRemedyEn);

    /// <summary>رقم جوال سعودي.</summary>
    public static readonly Error Phone = new(
        CodePrefix + "phone",
        "لا أُرسل رقم الجوال.",
        "A mobile number is never sent to the model.");

    /// <summary>الشبكة الأخيرة: سلسلة رقمية طويلة لم يطابقها شكلٌ مُسمّى.</summary>
    public static readonly Error DigitRun = new(
        CodePrefix + "digit_run",
        "سلسلة رقمية طويلة تشبه معرّفاً. لا تمرّ من هنا. " + AmountRemedyAr,
        "A long digit run that looks like an identifier does not pass this boundary. " + AmountRemedyEn);

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

    /// <summary>
    /// موضعُ جزءٍ خارج المفردات المغلقة. <b>يُرفض ولا يُقرأ افتراضاً</b>.
    /// </summary>
    public static readonly Error OutboundPartKindUndefined = new(
        "ai.agent.outbound_part_kind_undefined",
        "موضعُ الجزء خارج المفردات المغلقة. مسارٌ جديد إلى النموذج يُضاف عضواً في "
        + "المفردات ويُفحص، ولا يُمرَّر برقمٍ لا معنى له.",
        "The outbound part kind is outside the closed vocabulary; a new route to the model "
        + "adds a member and is inspected, it is not smuggled through as an unnamed number.");

    /// <summary>ظرفٌ بلا جزء واحد — ونداءٌ بلا محتوى لا يُرسَل.</summary>
    public static readonly Error OutboundEmpty = new(
        "ai.agent.outbound_empty",
        "لا شيء يُرسَل: الظرف فارغ. ونداءٌ بلا محتوى يستهلك دوراً ويعود بجوابٍ عن لا شيء.",
        "Nothing to send: the envelope is empty.");
}
