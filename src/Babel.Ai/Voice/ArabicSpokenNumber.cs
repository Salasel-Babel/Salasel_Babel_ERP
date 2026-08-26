using System.Globalization;
using System.Text;
using Babel.SharedKernel;

namespace Babel.Ai.Voice;

/// <summary>
/// <b>قارئ الأعداد العربية المنطوقة.</b> «ألف وخمسمئة» ← <c>1500</c>.
/// <para>
/// <b>ولماذا هذا ملفّ قائم بذاته وليس تعبيراً نمطياً في الواجهة:</b> لأن العدد المنطوق
/// ليس أرقاماً أصلاً. مُفرِّغُ الكلام في المتصفّح يعيد <c>ألف وخمسمئة</c> نصّاً، ولا يوجد
/// <c>decimal.TryParse</c> يقرؤه. والبديل الوحيد عن قارئ مُعلَن هو أن يقرأه نموذج —
/// أي أن يصير أهمّ رقم في الفاتورة <b>استنتاجاً احتمالياً</b> بدل أن يكون قراءةً حتمية
/// قابلة لإعادة التشغيل ولاختبار متجهات ذهبية.
/// </para>
/// <para>
/// <b>القاموس مغلق</b> على مثال المفردات المغلقة في <c>Suggestions</c>: كلمةٌ خارجه تُرفض
/// باسمها ولا تُقارَب بأقرب شبيه. و«خمسة عشر» و«خمسين» متجاوران صوتياً وفرقهما 35؛
/// مقاربةٌ صامتة بينهما تُنتج فاتورة صحيحة الشكل بمبلغ آخر.
/// </para>
/// <para>
/// <b>ولا يُخلَط رقمٌ بكلمة في عدد واحد:</b> «ألف و500» تحتمل 1500 وتحتمل عددين متتاليين،
/// فتُرفض ولا يُختار أحد الاحتمالين.
/// </para>
/// </summary>
public static class ArabicSpokenNumber
{
    /// <summary>الآحاد وما يلحق بها من صيغ التذكير والتأنيث والنصب.</summary>
    private static readonly Dictionary<string, decimal> Units = Keyed(new()
    {
        ["صفر"] = 0m,
        ["واحد"] = 1m, ["واحدة"] = 1m, ["أحد"] = 1m, ["احد"] = 1m, ["إحدى"] = 1m, ["احدى"] = 1m,
        ["اثنا"] = 2m, ["اثني"] = 2m, ["اثنان"] = 2m, ["اثنين"] = 2m, ["إثنين"] = 2m, ["اثنتان"] = 2m, ["اثنتين"] = 2m, ["ثنتين"] = 2m,
        ["ثلاثة"] = 3m, ["ثلاث"] = 3m, ["ثلاثه"] = 3m,
        ["أربعة"] = 4m, ["اربعة"] = 4m, ["أربع"] = 4m, ["اربع"] = 4m, ["اربعه"] = 4m,
        ["خمسة"] = 5m, ["خمس"] = 5m, ["خمسه"] = 5m,
        ["ستة"] = 6m, ["ست"] = 6m, ["سته"] = 6m,
        ["سبعة"] = 7m, ["سبع"] = 7m, ["سبعه"] = 7m,
        ["ثمانية"] = 8m, ["ثمان"] = 8m, ["ثماني"] = 8m, ["ثمانيه"] = 8m,
        ["تسعة"] = 9m, ["تسع"] = 9m, ["تسعه"] = 9m,
        ["عشرة"] = 10m, ["عشر"] = 10m, ["عشره"] = 10m,
    });

    /// <summary>العقود.</summary>
    private static readonly Dictionary<string, decimal> Tens = Keyed(new()
    {
        ["عشرون"] = 20m, ["عشرين"] = 20m,
        ["ثلاثون"] = 30m, ["ثلاثين"] = 30m,
        ["أربعون"] = 40m, ["اربعون"] = 40m, ["أربعين"] = 40m, ["اربعين"] = 40m,
        ["خمسون"] = 50m, ["خمسين"] = 50m,
        ["ستون"] = 60m, ["ستين"] = 60m,
        ["سبعون"] = 70m, ["سبعين"] = 70m,
        ["ثمانون"] = 80m, ["ثمانين"] = 80m,
        ["تسعون"] = 90m, ["تسعين"] = 90m,
    });

    /// <summary>المئات المركّبة كما تُنطق كلمةً واحدة.</summary>
    private static readonly Dictionary<string, decimal> Hundreds = Keyed(new()
    {
        ["مئة"] = 100m, ["مائة"] = 100m, ["مية"] = 100m,
        ["مئتان"] = 200m, ["مئتين"] = 200m, ["مائتان"] = 200m, ["مائتين"] = 200m, ["ميتين"] = 200m,
        ["ثلاثمئة"] = 300m, ["ثلاثمائة"] = 300m,
        ["أربعمئة"] = 400m, ["اربعمئة"] = 400m, ["أربعمائة"] = 400m, ["اربعمائة"] = 400m,
        ["خمسمئة"] = 500m, ["خمسمائة"] = 500m,
        ["ستمئة"] = 600m, ["ستمائة"] = 600m,
        ["سبعمئة"] = 700m, ["سبعمائة"] = 700m,
        ["ثمانمئة"] = 800m, ["ثمانمائة"] = 800m, ["ثمنمئة"] = 800m,
        ["تسعمئة"] = 900m, ["تسعمائة"] = 900m,
    });

    /// <summary>
    /// المضاعِفات. <b>«ألفان» و«ألفين» قيمة لا مضاعِف</b>: من عامَلها مضاعِفاً قرأ
    /// «ألفين وخمسمئة» ‏<c>2 × 1000 + 500</c> صحيحاً بالصدفة، ثم قرأ «ألفين» وحدها صفراً.
    /// </summary>
    private static readonly Dictionary<string, decimal> Scales = Keyed(new()
    {
        ["ألف"] = 1_000m, ["الف"] = 1_000m, ["آلاف"] = 1_000m, ["الاف"] = 1_000m,
        ["مليون"] = 1_000_000m, ["ملايين"] = 1_000_000m, ["مليونين"] = 1_000_000m,
        ["مليار"] = 1_000_000_000m, ["مليارات"] = 1_000_000_000m,
    });

    /// <summary>قيم قائمة بذاتها لا تُضرَب فيما قبلها.</summary>
    private static readonly Dictionary<string, decimal> Standalone = Keyed(new()
    {
        ["ألفان"] = 2_000m, ["ألفين"] = 2_000m, ["الفان"] = 2_000m, ["الفين"] = 2_000m,
    });

    /// <summary>كسور منطوقة تُضاف إلى ما قبلها: «ألف ونص».</summary>
    private static readonly Dictionary<string, decimal> Fractions = Keyed(new()
    {
        ["نص"] = 0.5m, ["نصف"] = 0.5m, ["النص"] = 0.5m, ["النصف"] = 0.5m,
        ["ربع"] = 0.25m, ["الربع"] = 0.25m,
        ["ثلث"] = 0.3333m, ["الثلث"] = 0.3333m,
    });

    /// <summary>كلمات تفصل الصحيح عن الكسر العشري.</summary>
    private static readonly HashSet<string> DecimalMarkers =
        KeyedSet(["فاصلة", "فاصل", "فاصلا", "نقطة", "نقطه"]);

    /// <summary>كلمات تُتجاهَل داخل العدد ولا تحمل قيمة.</summary>
    private static readonly HashSet<string> Ignorable =
        new(StringComparer.Ordinal) { "و", string.Empty };

    /// <summary>
    /// يقرأ عبارةً عدداً واحداً. يقبل الأرقام وحدها، والكلمات وحدها — <b>ولا يقبل خلطهما</b>.
    /// </summary>
    /// <param name="phrase">العبارة كما نُطقت.</param>
    public static Result<decimal> Read(string phrase)
    {
        ArgumentNullException.ThrowIfNull(phrase);

        string trimmed = phrase.Trim();
        if (trimmed.Length == 0)
        {
            return Result<decimal>.Failure(VoiceErrors.NumberNotComposable(phrase));
        }

        Result<string> normalised = ArabicNumerals.Normalise(trimmed);
        if (normalised.IsFailure)
        {
            return Result<decimal>.Failure(normalised.Errors);
        }

        string[] words = Split(normalised.Value);
        if (words.Length == 0)
        {
            return Result<decimal>.Failure(VoiceErrors.NumberNotComposable(phrase));
        }

        bool anyDigits = words.Any(static word => word.Any(char.IsAsciiDigit));
        bool anyWords = words.Any(IsValuedWord);

        if (anyDigits && anyWords)
        {
            return Result<decimal>.Failure(VoiceErrors.DigitsAndWordsMixed(phrase));
        }

        return anyDigits ? ReadDigits(words, phrase) : ReadWords(words, phrase);
    }

    /// <summary>هل هذه العبارة عدد مقروء؟ سؤال يُجاب بلا رمي استثناء.</summary>
    /// <param name="phrase">العبارة.</param>
    public static bool CanRead(string phrase) => phrase is not null && Read(phrase).IsSuccess;

    private static bool IsValuedWord(string word) =>
        Units.ContainsKey(word) || Tens.ContainsKey(word) || Hundreds.ContainsKey(word)
        || Scales.ContainsKey(word) || Standalone.ContainsKey(word) || Fractions.ContainsKey(word);

    /// <summary>
    /// يفصل الكلمات، <b>ويقشر واو العطف الملتصقة</b> — «وخمسمئة» تصل من المُفرِّغ كلمة واحدة.
    /// والقشر مشروط بأن يبقى بعده كلمة معروفة، وإلا فُقدت «واحد» فصارت «احد».
    /// </summary>
    private static string[] Split(string text)
    {
        List<string> words = [];

        foreach (string raw in text.Split([' ', '\t', '\n', '\r', '،', '،'], StringSplitOptions.RemoveEmptyEntries))
        {
            string word = Strip(raw);
            if (word.Length == 0 || Ignorable.Contains(word))
            {
                continue;
            }

            if (!IsValuedWord(word) && !DecimalMarkers.Contains(word)
                && word.Length > 1 && word[0] == 'و'
                && (IsValuedWord(word[1..]) || DecimalMarkers.Contains(word[1..])))
            {
                word = word[1..];
            }

            words.Add(word);
        }

        return [.. words];
    }

    /// <summary>
    /// يمرّر مفاتيح القاموس بالتجريد نفسه الذي يمرّ به الكلام.
    /// <para>
    /// <b>وهذا ليس ترتيباً بل ضمانة:</b> قاموسٌ مفاتيحُه غير مُجرَّدة يفقد «إحدى» بعد أن
    /// يصير الكلام «احدي»، فتُرفض كلمة عربية صحيحة برسالة «ليست في القاموس» — وهو أسوأ
    /// أصناف العطل: الرسالة صحيحة والسبب في الحارس نفسه.
    /// </para>
    /// </summary>
    private static Dictionary<string, decimal> Keyed(Dictionary<string, decimal> source)
    {
        Dictionary<string, decimal> keyed = new(StringComparer.Ordinal);

        foreach ((string word, decimal value) in source)
        {
            keyed[Strip(word)] = value;
        }

        return keyed;
    }

    private static HashSet<string> KeyedSet(IReadOnlyList<string> source) =>
        new(source.Select(Strip), StringComparer.Ordinal);

    /// <summary>يزيل التشكيل والتطويل ويوحّد الألف والتاء المربوطة الشائعتين في التفريغ.</summary>
    private static string Strip(string word)
    {
        StringBuilder output = new(word.Length);

        foreach (char character in word)
        {
            if (character is >= 'ً' and <= 'ْ' or 'ـ' or 'ٰ')
            {
                continue;
            }

            output.Append(character switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ى' => 'ي',
                _ => character,
            });
        }

        return output.ToString();
    }

    private static Result<decimal> ReadDigits(string[] words, string phrase)
    {
        string joined = string.Concat(words);

        return decimal.TryParse(
            joined,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out decimal value)
            ? Result<decimal>.Success(value)
            : Result<decimal>.Failure(VoiceErrors.NumberNotComposable(phrase));
    }

    private static Result<decimal> ReadWords(string[] words, string phrase)
    {
        int marker = Array.FindIndex(words, DecimalMarkers.Contains);

        if (marker < 0)
        {
            return Compose(words, phrase);
        }

        Result<decimal> whole = Compose(words[..marker], phrase);
        if (whole.IsFailure)
        {
            return whole;
        }

        Result<decimal> fraction = ComposeFraction(words[(marker + 1)..], phrase);
        return fraction.IsFailure
            ? Result<decimal>.Failure(fraction.Errors)
            : Result<decimal>.Success(whole.Value + fraction.Value);
    }

    /// <summary>
    /// ما بعد «فاصلة» يُقرأ <b>سلسلة أرقام</b> لا عدداً: «فاصلة صفر خمسة» ← <c>0.05</c>،
    /// وقراءتُها عدداً تعطي <c>0.5</c> — وهو عُشر القيمة. وحين لا تكون كلها آحاداً
    /// («فاصلة خمسة وسبعين») تُقرأ عدداً وتُوضع بعدد خاناته.
    /// </summary>
    private static Result<decimal> ComposeFraction(string[] words, string phrase)
    {
        if (words.Length == 0)
        {
            return Result<decimal>.Failure(VoiceErrors.NumberNotComposable(phrase));
        }

        if (words.All(word => Units.TryGetValue(word, out decimal unit) && unit <= 9m))
        {
            string digits = string.Concat(words.Select(word => Units[word].ToString("0", CultureInfo.InvariantCulture)));
            return Result<decimal>.Success(decimal.Parse("0." + digits, CultureInfo.InvariantCulture));
        }

        Result<decimal> composed = Compose(words, phrase);
        if (composed.IsFailure)
        {
            return composed;
        }

        string text = composed.Value.ToString("0", CultureInfo.InvariantCulture);
        return Result<decimal>.Success(decimal.Parse("0." + text, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// التركيب: مُراكِمٌ جارٍ ومجموع. المئة تضرب ما قبلها، والألف فما فوق يُرحِّل
    /// المُراكِم إلى المجموع — وهي القاعدة نفسها في كل لغة تعدّ بالمنازل.
    /// </summary>
    private static Result<decimal> Compose(string[] words, string phrase)
    {
        if (words.Length == 0)
        {
            return Result<decimal>.Failure(VoiceErrors.NumberNotComposable(phrase));
        }

        decimal total = 0m;
        decimal current = 0m;
        decimal fraction = 0m;
        bool anything = false;

        foreach (string word in words)
        {
            if (Fractions.TryGetValue(word, out decimal part))
            {
                fraction += part;
                anything = true;
                continue;
            }

            if (Standalone.TryGetValue(word, out decimal standalone))
            {
                total += current + standalone;
                current = 0m;
                anything = true;
                continue;
            }

            if (Scales.TryGetValue(word, out decimal scale))
            {
                total += (current == 0m ? 1m : current) * scale;
                current = 0m;
                anything = true;
                continue;
            }

            if (Hundreds.TryGetValue(word, out decimal hundred))
            {
                // «ثلاث مئة» منطوقة كلمتين: المئة تضرب المُراكِم إن وُجد.
                current = hundred == 100m && current > 0m ? current * 100m : current + hundred;
                anything = true;
                continue;
            }

            if (Tens.TryGetValue(word, out decimal ten))
            {
                current += ten;
                anything = true;
                continue;
            }

            if (Units.TryGetValue(word, out decimal unit))
            {
                current += unit;
                anything = true;
                continue;
            }

            return Result<decimal>.Failure(VoiceErrors.UnknownNumberWord(word));
        }

        return anything
            ? Result<decimal>.Success(total + current + fraction)
            : Result<decimal>.Failure(VoiceErrors.NumberNotComposable(phrase));
    }
}
