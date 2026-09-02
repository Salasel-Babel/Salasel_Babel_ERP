using Babel.SharedKernel;

namespace Babel.Ai.Voice;

/// <summary>
/// أعطال <b>بناء</b> السجلّ — لا أعطال كلام. تقع مرّةً عند التركيب لا عند كل نُطق،
/// <b>وتُسقط التركيب</b>: سجلٌّ نصفُه صالح أسوأ من سجلٍّ يرفض أن يُبنى.
/// </summary>
public static class VoiceCatalogueErrors
{
    /// <summary>معرّف نيّة تكرّر عبر وحدتين.</summary>
    /// <param name="intentId">المعرّف.</param>
    public static Error DuplicateIntentId(string intentId) => new(
        "ai.voice.catalogue.duplicate_intent",
        "معرّف النيّة «" + intentId + "» مُعلَن مرّتين. "
        + "وتغليبُ إحداهما بصمت يجعل الكلام ينفّذ نيّةَ وحدةٍ أخرى.",
        "Intent id '" + intentId + "' is declared twice; silently preferring one would run another module's intent.");

    /// <summary>شكل المعرّف مخالف — مقاطع لاتينية صغيرة تفصلها نقاط.</summary>
    /// <param name="intentId">المعرّف.</param>
    public static Error MalformedIntentId(string intentId) => new(
        "ai.voice.catalogue.malformed_intent_id",
        "معرّف النيّة «" + intentId + "» ليس على الشكل المُعلَن: مقاطع لاتينية صغيرة تفصلها نقاط.",
        "Intent id '" + intentId + "' does not match the declared shape.");

    /// <summary>نيّةٌ تُرحِّل بلا رمز حدث.</summary>
    /// <param name="intentId">المعرّف.</param>
    public static Error EventCodeMissing(string intentId) => new(
        "ai.voice.catalogue.event_code_missing",
        "النيّة «" + intentId + "» تُرحّل ولا تسمّي رمز حدث. "
        + "وترحيلٌ بلا رمز حدث لا تعرف المصفوفة ما تفعل به.",
        "Intent '" + intentId + "' posts to the ledger but names no event code.");

    /// <summary>رمز حدث ليس في مصفوفة الترحيل.</summary>
    /// <param name="intentId">المعرّف.</param>
    /// <param name="eventCode">الرمز.</param>
    public static Error EventCodeUnknown(string intentId, string eventCode) => new(
        "ai.voice.catalogue.event_code_unknown",
        "النيّة «" + intentId + "» تسمّي الحدث «" + eventCode + "» وهو ليس في مصفوفة الترحيل. "
        + "ورمزٌ مخترَع قيس في هذا المستودع وهو يُنتج ترحيلاً مكرَّراً صامتاً.",
        "Intent '" + intentId + "' names event '" + eventCode + "' which is not in the posting matrix.");

    /// <summary>نيّةٌ لا تُرحّل ومع ذلك تسمّي حدثاً.</summary>
    /// <param name="intentId">المعرّف.</param>
    /// <param name="eventCode">الرمز.</param>
    public static Error EventCodeNotExpected(string intentId, string eventCode) => new(
        "ai.voice.catalogue.event_code_not_expected",
        "النيّة «" + intentId + "» لا تُرحّل وتسمّي الحدث «" + eventCode + "». "
        + "وحدثٌ مُعلَن على مسارٍ لا يُرحّل يُقرأ لاحقاً أثراً محاسبياً لا وجود له.",
        "Intent '" + intentId + "' does not post yet names event '" + eventCode + "'.");

    /// <summary>
    /// نيّةٌ تسمّي حساباً — بالمقطع الرقمي أو باسم شريحة يسمّي دليل الحسابات.
    /// </summary>
    /// <param name="intentId">المعرّف.</param>
    /// <param name="what">ما التُقط.</param>
    public static Error NamesALedgerCode(string intentId, string what) => new(
        "ai.voice.catalogue.names_a_ledger_code",
        "النيّة «" + intentId + "» تسمّي «" + what + "» وهو رمز حساب أو اسمٌ يدلّ عليه. "
        + "والقاعدة 2 تمنع الوحدة من تسمية حساب، والمسار المنطوق ليس استثناءً منها.",
        "Intent '" + intentId + "' names '" + what + "', which is a ledger code or points at one.");

    /// <summary>نيّةٌ تنتظر قراراً ولا تسمّي القرار.</summary>
    /// <param name="intentId">المعرّف.</param>
    public static Error OwnerDecisionNotStated(string intentId) => new(
        "ai.voice.catalogue.owner_decision_not_stated",
        "النيّة «" + intentId + "» مُعلَنة «تنتظر قراراً» ولا تكتب القرار المنتظَر. "
        + "وانتظارٌ بلا اسمٍ لما يُنتظر ليس انتظاراً بل نسياناً.",
        "Intent '" + intentId + "' awaits an owner decision but does not state which.");

    /// <summary>نيّةٌ منشورة تحمل نصّ قرار منتظَر.</summary>
    /// <param name="intentId">المعرّف.</param>
    public static Error OwnerDecisionNotExpected(string intentId) => new(
        "ai.voice.catalogue.owner_decision_not_expected",
        "النيّة «" + intentId + "» منشورة وتحمل نصّ قرارٍ منتظَر — أحد الحقلين خاطئ.",
        "Intent '" + intentId + "' is published yet carries an owner-decision text.");

    /// <summary>
    /// <b>نيّةٌ تبلغ عمليةً ممنوعة على الصوت</b> — ترحيلاً أو توقيعاً أو اعتماداً،
    /// أو فعلاً لم يُصنَّف بعد.
    /// </summary>
    /// <param name="intentId">المعرّف.</param>
    /// <param name="operationId">العملية.</param>
    /// <param name="why">السبب كما سمّاه الحارس.</param>
    public static Error OperationNotReachableByVoice(string intentId, string operationId, string why) => new(
        "ai.voice.catalogue.operation_not_reachable",
        "النيّة «" + intentId + "» تبلغ العملية «" + operationId + "» — و" + why + ". "
        + "والصوت يبلغ المسوّدة ولا يبلغ الترحيل: مسوّدةٌ خاطئة تُلقى بلا ثمن، "
        + "وقيدٌ خاطئ يُكلّف قيداً عاكساً يبقى في السجلّ.",
        "Intent '" + intentId + "' reaches operation '" + operationId
        + "', which the voice path may never reach.");

    /// <summary>نيّةٌ منشورة لا تسمّي العملية التي تبلغها.</summary>
    /// <param name="intentId">المعرّف.</param>
    public static Error OperationNotStated(string intentId) => new(
        "ai.voice.catalogue.operation_not_stated",
        "النيّة «" + intentId + "» منشورة ولا تسمّي عمليةً منشورة تبلغها. "
        + "ونيّةٌ بلا عملية تنتهي إلى نداءٍ في الهواء: يُؤكَّد الأمر ثم لا يصل مستنداً.",
        "Intent '" + intentId + "' is published yet names no published operation to reach.");

    /// <summary>نيّةٌ تنتظر قراراً وتسمّي عمليةً — والعملية هي بعينها ما ينقص.</summary>
    /// <param name="intentId">المعرّف.</param>
    /// <param name="operationId">العملية.</param>
    public static Error OperationNotExpected(string intentId, string operationId) => new(
        "ai.voice.catalogue.operation_not_expected",
        "النيّة «" + intentId + "» تنتظر قراراً وتسمّي العملية «" + operationId + "». "
        + "وما ينتظره القرار هو العملية نفسها، فتسميتُها ادّعاءُ بابٍ لم يُفتح.",
        "Intent '" + intentId + "' awaits an owner decision yet names operation '" + operationId + "'.");

    /// <summary>نيّةٌ بلا عبارة إطلاق واحدة.</summary>
    /// <param name="intentId">المعرّف.</param>
    public static Error NoPhrases(string intentId) => new(
        "ai.voice.catalogue.no_phrases",
        "النيّة «" + intentId + "» بلا عبارة إطلاق واحدة — نيّةٌ لا يستطيع أحد أن ينطقها.",
        "Intent '" + intentId + "' declares no trigger phrase and can never be spoken.");

    /// <summary>
    /// شريحةُ طرفٍ لا تسمّي سجلَّها — <b>وذلك يُسقط البناء</b>.
    /// </summary>
    /// <param name="intentId">المعرّف.</param>
    /// <param name="slotName">اسم الشريحة.</param>
    public static Error RegisterNotStated(string intentId, string slotName) => new(
        "ai.voice.catalogue.register_not_stated",
        "الشريحة «" + slotName + "» في النيّة «" + intentId + "» تسمّي طرفاً ولا تسمّي السجلّ "
        + "الذي يُحلّ فيه اسمه. وشريحةٌ كهذه تبقى معلَّقةً أبداً: تُقرأ ولا تُحلّ، فلا تكتمل النيّة قطّ.",
        "Entity slot '" + slotName + "' in intent '" + intentId + "' names no register key; it could never resolve.");

    /// <summary>شريحةٌ ليست طرفاً وتسمّي سجلّاً.</summary>
    /// <param name="intentId">المعرّف.</param>
    /// <param name="slotName">اسم الشريحة.</param>
    /// <param name="registerKey">المفتاح المُعلَن.</param>
    public static Error RegisterNotExpected(string intentId, string slotName, string registerKey) => new(
        "ai.voice.catalogue.register_not_expected",
        "الشريحة «" + slotName + "» في النيّة «" + intentId + "» ليست طرفاً وتسمّي السجلّ «"
        + registerKey + "». والوعدُ بحلٍّ لا يقع أسوأ من غيابه: تُعرض الشريحة «تنتظر السجلّ» ولا ينظرها أحد.",
        "Slot '" + slotName + "' in intent '" + intentId + "' is not an entity yet names register '" + registerKey + "'.");

    /// <summary>مفتاح سجلٍّ خارج الشكل المُعلَن.</summary>
    /// <param name="intentId">المعرّف.</param>
    /// <param name="slotName">اسم الشريحة.</param>
    /// <param name="registerKey">المفتاح.</param>
    public static Error MalformedRegisterKey(string intentId, string slotName, string registerKey) => new(
        "ai.voice.catalogue.malformed_register_key",
        "مفتاح السجلّ «" + registerKey + "» في الشريحة «" + slotName + "» من النيّة «" + intentId
        + "» ليس على الشكل المُعلَن: لاتينيّ يبدأ بحرفٍ صغير بلا نقاط ولا فراغ.",
        "Register key '" + registerKey + "' on slot '" + slotName + "' of intent '" + intentId + "' is malformed.");

    /// <summary>سجلٌّ فارغ.</summary>
    public static readonly Error CatalogueEmpty = new(
        "ai.voice.catalogue.empty",
        "سجلّ النيّات فارغ. وسجلٌّ فارغ يجعل كل كلامٍ «لم أفهم» بلا سببٍ يظهر.",
        "The intent registry is empty; every utterance would refuse for no visible reason.");
}
