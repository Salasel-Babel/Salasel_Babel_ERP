using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Ai.Voice;

/// <summary>
/// أخطاء المسار المنطوق. على اصطلاح <see cref="Error"/> نفسه: رمزٌ تعتمد عليه الشيفرة،
/// ونصّان للعرض، والعربية هي الأصل.
/// <para>
/// <b>والقاعدة الحاكمة هنا واحدة:</b> ما لا يُقرأ يقيناً <b>يُرفض ويُسمّى</b>، ولا يُخمَّن.
/// رقمٌ مخمَّن في مسوّدة فاتورة أسوأ من حقل فارغ بما لا يُقاس: الفارغ يُملأ، والمخمَّن يُؤكَّد.
/// </para>
/// </summary>
public static class VoiceErrors
{
    /// <summary>لا كلام — التفريغ فارغ.</summary>
    public static readonly Error TranscriptEmpty = new(
        "ai.voice.transcript_empty",
        "لا نصّ في التفريغ. لم يُسمَع شيء، أو أُفلت الزرّ قبل أن يبدأ الكلام.",
        "The transcript is empty; nothing was heard, or the button was released before speech began.");

    /// <summary>التفريغ أطول من الحدّ.</summary>
    public static Error TranscriptTooLong(int length, int limit) => new(
        "ai.voice.transcript_too_long",
        "التفريغ " + Num(length) + " محرفاً وهو يتجاوز الحدّ " + Num(limit) + ". "
        + "والحدّ موضوع كي لا يصير حقلُ نصٍّ مفتوحاً باباً إلى نموذج بلا سقف.",
        "The transcript is " + Num(length) + " characters, beyond the limit of " + Num(limit) + ".");

    /// <summary>
    /// خلط بين نظامَي أرقام في عدد واحد. <b>مرفوض عمداً لا مُطبَّع</b>.
    /// </summary>
    public static Error MixedDigitSystems(string token) => new(
        "ai.voice.mixed_digit_systems",
        "العدد «" + token + "» يخلط نظامَي أرقام في كلمة واحدة (عربية-هندية أو ديفاناغرية مع لاتينية). "
        + "والخلط يُرفض ولا يُطبَّع: نصفُه لصقٌ من مصدر آخر، وتطبيعه الصامت يُنتج رقماً لا يعرف قائلُه أنه قاله.",
        "The number '" + token + "' mixes two digit systems in one token; this is refused, not normalised.");

    /// <summary>كلمة لا يعرفها قاموس الأعداد المنطوقة.</summary>
    public static Error UnknownNumberWord(string word) => new(
        "ai.voice.unknown_number_word",
        "الكلمة «" + word + "» ليست في قاموس الأعداد المنطوقة. "
        + "والقاموس مغلق عمداً: كلمةٌ تُقارَب بأقرب شبيه تُنتج رقماً مختلفاً بصمت.",
        "The word '" + word + "' is not in the closed spoken-number vocabulary.");

    /// <summary>تركيب عددي غير مقروء — مثل مئة بعد ألف بلا رابط.</summary>
    public static Error NumberNotComposable(string phrase) => new(
        "ai.voice.number_not_composable",
        "العبارة «" + phrase + "» لا تُركَّب عدداً واحداً مقروءاً. "
        + "وقيلت على الأرجح عددان متتاليان، فيُطلب من القائل إعادتهما مفصولين.",
        "The phrase '" + phrase + "' does not compose into a single readable number.");

    /// <summary>خلط أرقام وكلمات في عدد واحد.</summary>
    public static Error DigitsAndWordsMixed(string phrase) => new(
        "ai.voice.digits_and_words_mixed",
        "العبارة «" + phrase + "» تخلط أرقاماً وكلمات في عدد واحد. "
        + "و«ألف و500» تحتمل ألفاً وخمسمئة وتحتمل ألفاً ثم خمسمئة منفصلة، فتُرفض ولا يُختار أحد الاحتمالين.",
        "The phrase '" + phrase + "' mixes digits and number words in one value and is ambiguous.");

    /// <summary>لا مبلغ في الكلام.</summary>
    public static readonly Error NoAmountHeard = new(
        "ai.voice.no_amount_heard",
        "لا مبلغ في الكلام. المسوّدة تبقى بحقل مبلغ فارغ يملؤه الإنسان — ولا يُخترَع رقم.",
        "No amount was heard; the draft keeps an empty amount for a human to fill, and no number is invented.");

    /// <summary>مبلغ خارج المدى المعقول لفاتورة.</summary>
    public static Error AmountOutOfRange(decimal value) => new(
        "ai.voice.amount_out_of_range",
        "المبلغ المقروء " + Show(value) + " خارج المدى المقبول لمسوّدة. "
        + "وتفريغٌ صوتي يُنتج رقماً فلكياً من كلمة واحدة مُساء سماعُها، فالحدّ يقع هنا لا في الشاشة.",
        "The parsed amount " + Show(value) + " is outside the range accepted for a draft.");

    /// <summary>نسبة ضريبة خارج المدى.</summary>
    public static Error TaxRateOutOfRange(decimal value) => new(
        "ai.voice.tax_rate_out_of_range",
        "نسبة الضريبة المقروءة " + Show(value) + " خارج المدى [0، 1].",
        "The parsed tax rate " + Show(value) + " is outside [0, 1].");

    /// <summary>رمز حدث منطوق لا يعرفه القاموس المغلق.</summary>
    public static Error NoEventHeard(string transcript) => new(
        "ai.voice.no_event_heard",
        "لا حدث معروف في الكلام: «" + transcript + "». "
        + "والحدث يُختار من مصفوفة الترحيل بيد الإنسان، ولا يُخترَع من صياغة الجملة.",
        "No known event in the utterance: '" + transcript + "'.");

    private static string Show(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);
}
