using System.Globalization;
using Babel.Contracts.Capture;
using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Ai.Voice;

/// <summary>
/// <b>قارئ الأمر المنطوق — حتمي، بلا شبكة، وبلا نموذج.</b>
/// <para>
/// المسار الحتمي هو <b>ما يعمل حين لا يعمل شيء</b>: في مستودعٍ بلا تغطية، وعلى موقع
/// صبٍّ بلا شبكة، وفي عرضٍ شبكتُه مغلقة. والنموذج — إن رُكِّب — يُضيف الحالة العامّة
/// فوقه ولا يصير شرطاً لظهور الأثر (‏ADR-0030 عاشراً).
/// </para>
/// <para>
/// <b>وما يعيده هذا القارئ ليس إذناً.</b> يعيد <b>ما فهم</b> — ومعه ما نقص باسمه —
/// كي تمتلئ الشاشة والمستخدم ما زال يتكلّم. والباب الوحيد إلى التنفيذ هو
/// <see cref="VoiceConfirmationGate"/>.
/// </para>
/// <para>
/// <b>وثلاثة أشياء لا يفعلها بحال:</b> لا يخترع قيمةً لشريحةٍ لم تُنطق؛ ولا يختار بين
/// نيّتين متطابقتين؛ ولا يفترض وحدةَ قياس. وكلٌّ من الثلاثة يُنتج مستنداً صحيح الشكل
/// بمعنى آخر — وهو أخبث ما يمكن أن يُنتجه مسارُ إدخال.
/// </para>
/// </summary>
public static class SpokenCommandReader
{
    /// <summary>أقصى طول تفريغ يُقبل. حدٌّ كي لا يصير حقلُ نصٍّ مفتوحاً باباً بلا سقف.</summary>
    public const int TranscriptLimit = 600;

    /// <summary>أقصى عدد شرائح في أمرٍ واحد.</summary>
    public const int SlotLimit = 12;

    /// <summary>أقصى عدد كلماتٍ في رمزٍ منطوق.</summary>
    private const int CodeWordLimit = 4;

    /// <summary>كلماتٌ موصِّلة تُتخطّى قبل الرمز: «الوحدة <b>رقم</b> اثنتي عشرة».</summary>
    private static readonly HashSet<string> Connectors = new(
        new[] { "رقم", "برقم", "رقمها", "هو", "هي" }.Select(VoiceText.Fold),
        StringComparer.Ordinal);

    /// <summary>دلائل الشركة المنطوقة — <b>مغلقة ومركّبة</b>، فلا تلتقط «شركة النور» اسمَ مورد.</summary>
    private static readonly string[] CompanyCues =
        [.. new[] { "في شركة", "بشركة", "لشركة", "في منشأة", "بمنشأة", "لمنشأة", "على شركة" }.Select(VoiceText.Fold)];

    /// <summary>كلمات إيقاف عامّة: تُنهي مقطع النصّ الحرّ ولا تدخل فيه.</summary>
    private static readonly HashSet<string> StopWords = new(
        new[]
        {
            "و", "في", "على", "من", "الى", "عن", "مع", "ثم",
            "بمبلغ", "مبلغ", "بقيمة", "قيمتها", "قيمته", "الاجمالي", "اجمالي", "المجموع",
            "ريال", "ريالا", "ريالات", "ريالين", "هللة",
            "بتاريخ", "تاريخ", "اليوم", "امس", "البارحة", "أمس",
            "رقم", "رقمها", "برقم", "كمية", "الكمية", "عدد", "العدد",
            "ضريبة", "الضريبة", "وضريبة", "بالمئة", "بالمائة", "المئة", "المائة",
            "تأكيد", "الغاء", "إلغاء",
        }.Select(VoiceText.Fold),
        StringComparer.Ordinal);

    /// <summary>
    /// <b>كواسرُ المقاطع — أدواتُ الشرط وأفعالُ الأمر.</b>
    /// <para>
    /// <b>ولماذا هي امتدادٌ لمبدأ <see cref="StopWords"/> لا مبدأٌ ثانٍ:</b> «من» و«في»
    /// و«على» و«ثم» ليست في تلك القائمة لأنها تُعلّم حقولاً، بل لأنها <b>كلماتٌ
    /// وظيفية</b> — والاسم لا يبتلع كلمةً وظيفية. وأداةُ الشرط وفعلُ الأمر من هذا
    /// الصنف بعينه. فالعطل لم يكن نقصاً في مبدأٍ بل نقصاً <b>في القائمة على مبدئها</b>.
    /// </para>
    /// <para>
    /// <b>والقياس الذي أوجبها</b>: «سجل سند قبض من شركة المسار الامثل <b>فان لم تجدها
    /// انشيء لها حسابا</b> ثم سند قبض…» — قرأ العميلَ اسماً من ثلاث عشرة كلمة، فيه شرطٌ
    /// وفعلُ أمر. والاسم عبارةٌ اسمية: أداةُ شرطٍ أو فعلُ أمرٍ داخله يعني أن الاسم
    /// انتهى قبل كلمة.
    /// </para>
    /// <para>
    /// <b>وخطرُها مُعلَن ومكشوف لا مطمور:</b> اسمٌ مشروع يحمل «لو» أو «الا» يُقصّ.
    /// ولذلك <b>لا يُقصّ بصمت</b>: ما سقط يُحمل في
    /// <see cref="SpokenSlotValue.Dropped"/> ويُعرض بجانب الحقل، فيرى الإنسان القصّة
    /// الخاطئة بعينه بدل أن يوقّع عليها.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> ClauseBreakers = new(
        new[]
        {
            // أدوات الشرط والاستثناء
            "ان", "اذا", "فان", "فاذا", "لو", "ولو", "والا", "الا", "وان", "لم", "لما", "متى", "إن",
            // أفعال الأمر التي تبدأ أمراً ثانياً
            "انشئ", "انشيء", "انشاء", "سجل", "اصرف", "اضف", "افتح", "حول", "اطلع", "سو", "سوي", "اعمل",
        }.Select(VoiceText.Fold),
        StringComparer.Ordinal);

    /// <summary>
    /// حدودُ <b>الاسم</b> حين لا تكون هناك نيّةٌ مطابَقة — كلماتُ الإيقاف وكواسرُ
    /// المقاطع معاً. يقرؤها <see cref="ReadCompany"/>، وهي نفسها الأساسُ الذي تبني
    /// عليه <see cref="Boundaries"/> دلائلَ الشرائح.
    /// </summary>
    private static readonly HashSet<string> NameBoundaries =
        new(StopWords.Concat(ClauseBreakers), StringComparer.Ordinal);

    /// <summary>كلمات التاريخ النسبي.</summary>
    private static readonly string TodayWord = VoiceText.Fold("اليوم");

    private static readonly string[] YesterdayWords =
        [.. new[] { "امس", "أمس", "البارحة" }.Select(VoiceText.Fold)];

    /// <summary>كلمات تدلّ على النسبة المئوية.</summary>
    private static readonly HashSet<string> PercentWords = new(
        new[] { "بالمئة", "بالمائة", "المئة", "المائة", "٪", "%" }.Select(VoiceText.Fold),
        StringComparer.Ordinal);

    /// <summary>
    /// يقرأ جملةً واحدة أمراً واحداً.
    /// </summary>
    /// <param name="transcript">التفريغ كما ورد من المتصفّح أو كما كُتب يدوياً.</param>
    /// <param name="registry">سجلّ النيّات.</param>
    /// <param name="options">ما يُحقن كي تكون القراءة حتمية.</param>
    public static Result<VoiceResolution> Read(
        string transcript,
        VoiceIntentRegistry registry,
        VoiceReadingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(registry);

        if (string.IsNullOrWhiteSpace(transcript))
        {
            return Result<VoiceResolution>.Failure(VoiceErrors.TranscriptEmpty);
        }

        if (transcript.Length > TranscriptLimit)
        {
            return Result<VoiceResolution>.Failure(
                VoiceErrors.TranscriptTooLong(transcript.Length, TranscriptLimit));
        }

        VoiceReadingOptions reading = options ?? new VoiceReadingOptions();

        Result<VoiceIntent> matched = Match(VoiceText.Fold(transcript), registry, transcript);
        if (matched.IsFailure)
        {
            return Result<VoiceResolution>.Failure(matched.Errors);
        }

        VoiceIntent intent = matched.Value;

        if (intent.Slots.Count > SlotLimit)
        {
            return Result<VoiceResolution>.Failure(
                VoiceRefusals.TooManySlots(intent.Slots.Count, SlotLimit));
        }

        return Fill(intent, transcript, reading);
    }

    /// <summary>
    /// <b>يقرأ نصّاً في نيّةٍ بعينها — بلا مطابقة.</b>
    /// <para>
    /// <b>ولماذا بلا مطابقة، وهذا هو بيت القصيد:</b> حين تقف خطوةٌ لأن شريحةً تنقصها،
    /// يُسأل الإنسان عنها باسمها فيقول «نقد» أو «خمسة آلاف». <b>وتمريرُ ذلك على
    /// <see cref="Match"/> كارثة</b>: «خمسة آلاف» لا تحمل عبارةَ إطلاقٍ فتُرفض
    /// «لم أفهم» وقد فهمت تماماً؛ أو — أسوأ — تحمل كلمةً تُطابق نيّةً أخرى فيُملأ حقلٌ
    /// في مستندٍ لم يطلبه أحد. فالجواب يُقرأ <b>في النيّة التي سألت وحدها</b>.
    /// </para>
    /// <para>
    /// وما عدا المطابقة فكلُّ شيء كما هو: نفسُ قرّاء الشرائح، ونفسُ الحدود، ونفسُ
    /// الملخّص، ونفسُ الرمز، ونفسُ حارس الإفشاء. <b>ولا بابَ ثانياً إلى التنفيذ</b> —
    /// ما يخرج من هنا يمرّ من <see cref="VoiceConfirmationGate"/> كما يمرّ ما يخرج من
    /// <see cref="Read"/>.
    /// </para>
    /// </summary>
    /// <param name="intent">النيّة التي سألت.</param>
    /// <param name="transcript">جوابُ الإنسان.</param>
    /// <param name="options">ما يُحقن كي تكون القراءة حتمية.</param>
    public static Result<VoiceResolution> ReadInto(
        VoiceIntent intent,
        string transcript,
        VoiceReadingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (string.IsNullOrWhiteSpace(transcript))
        {
            return Result<VoiceResolution>.Failure(VoiceErrors.TranscriptEmpty);
        }

        if (transcript.Length > TranscriptLimit)
        {
            return Result<VoiceResolution>.Failure(
                VoiceErrors.TranscriptTooLong(transcript.Length, TranscriptLimit));
        }

        if (intent.Slots.Count > SlotLimit)
        {
            return Result<VoiceResolution>.Failure(
                VoiceRefusals.TooManySlots(intent.Slots.Count, SlotLimit));
        }

        return Fill(intent, transcript, options ?? new VoiceReadingOptions());
    }

    /// <summary>
    /// يملأ شرائح نيّةٍ من نصّ. <b>مشتركٌ بين <see cref="Read"/> و<see cref="ReadInto"/>
    /// كي لا يوجد قارئان ينحرفان</b>: الفرق بينهما المطابقةُ وحدها.
    /// </summary>
    private static Result<VoiceResolution> Fill(
        VoiceIntent intent,
        string transcript,
        VoiceReadingOptions reading)
    {
        IReadOnlyList<string> words = VoiceText.Words(transcript);

        List<SpokenSlotValue> values = [];
        List<string> missing = [];
        List<Error> faults = [];

        HashSet<string> boundaries = Boundaries(intent);

        foreach (VoiceSlot slot in intent.Slots)
        {
            SpokenSlotValue? value = ReadSlot(slot, words, boundaries, reading, faults);

            if (value is not null)
            {
                values.Add(value);
            }
            else if (slot.Required)
            {
                missing.Add(slot.Name);
            }
        }

        string? company = ReadCompany(words);

        string readbackAr = VoiceReadback.Arabic(intent, values);

        // ‏**الحارس على ما يُنطَق نفسه**: قيمةٌ شخصية تسلّلت إلى الملخّص تُرفض هنا،
        // لا في الطبقة التي تنطقه — فالطبقة قد تُنسى، وهذه لا تُتجاوَز.
        Result disclosure = VoiceDisclosure.Guard(readbackAr);
        if (disclosure.IsFailure)
        {
            return Result<VoiceResolution>.Failure(disclosure.Errors);
        }

        return Result<VoiceResolution>.Success(new VoiceResolution(
            intent,
            values,
            missing,
            faults,
            company,
            readbackAr,
            VoiceReadback.Token(intent, values)));
    }

    /// <summary>
    /// يطابق نيّةً واحدة. <b>الأطول يفوز</b> كي لا تبتلع عبارةٌ عامّة عبارةً أخصّ —
    /// و<b>تعادلُ نيّتين رفضٌ لا قرعة</b>.
    /// </summary>
    private static Result<VoiceIntent> Match(string folded, VoiceIntentRegistry registry, string transcript)
    {
        int best = 0;
        List<VoiceIntent> winners = [];

        foreach (VoiceIntent intent in registry.Intents)
        {
            int score = 0;

            foreach (string phrase in intent.Phrases)
            {
                string needle = VoiceText.Fold(phrase);
                if (needle.Length > score && folded.Contains(needle, StringComparison.Ordinal))
                {
                    score = needle.Length;
                }
            }

            if (score == 0)
            {
                continue;
            }

            if (score > best)
            {
                best = score;
                winners.Clear();
                winners.Add(intent);
            }
            else if (score == best)
            {
                winners.Add(intent);
            }
        }

        if (winners.Count == 0)
        {
            return Result<VoiceIntent>.Failure(VoiceRefusals.NotUnderstood(transcript));
        }

        if (winners.Count > 1)
        {
            return Result<VoiceIntent>.Failure(
                VoiceRefusals.Ambiguous(transcript, [.. winners.Select(static intent => intent.Id)]));
        }

        return Result<VoiceIntent>.Success(winners[0]);
    }

    /// <summary>
    /// حدودُ المقاطع: كلماتُ الإيقاف، <b>وكواسرُ المقاطع</b>، ودلائلُ كل شريحة في هذه
    /// النيّة. وتُستهلك في <see cref="ReadText"/> و<see cref="ReadCode"/> معاً، فالكاسر
    /// يبلغ الاثنين بإضافةٍ واحدة.
    /// </summary>
    private static HashSet<string> Boundaries(VoiceIntent intent)
    {
        HashSet<string> boundaries = new(NameBoundaries, StringComparer.Ordinal);

        foreach (VoiceSlot slot in intent.Slots)
        {
            foreach (string cue in slot.Cues)
            {
                foreach (string word in VoiceText.Words(cue))
                {
                    boundaries.Add(VoiceText.Fold(word));
                }
            }
        }

        return boundaries;
    }

    private static SpokenSlotValue? ReadSlot(
        VoiceSlot slot,
        IReadOnlyList<string> words,
        HashSet<string> boundaries,
        VoiceReadingOptions options,
        List<Error> faults)
        => slot.Kind switch
        {
            VoiceSlotKind.Money or VoiceSlotKind.Number => ReadNumeric(slot, words),
            VoiceSlotKind.Quantity => ReadQuantity(slot, words, faults),
            VoiceSlotKind.Date => ReadDate(slot, words, options),
            VoiceSlotKind.Choice => ReadChoice(slot, words),
            VoiceSlotKind.Code => ReadCode(slot, words, boundaries),
            _ => ReadText(slot, words, boundaries),
        };

    /// <summary>
    /// مواضع ما بعد كل دليلٍ من دلائل الشريحة، بترتيب إعلانها ثم بترتيب ورودها.
    /// <para>
    /// <b>ولماذا كل المواضع لا أوّلها:</b> «على ستة أقساط» فيها دليلان لشريحةٍ واحدة —
    /// «أقساط» و«على» — والأوّل يقع <b>بعد</b> العدد فلا يُنتج شيئاً. وقارئٌ يكتفي بأوّل
    /// دليلٍ يجده يُعلن «ينقصني عدد الأقساط» وقد قيلت. فيُجرَّب كلُّ موضع حتى يُنتج
    /// أحدُها قيمة، <b>وإن لم يُنتج أيٌّ منها فالشريحة ناقصة حقاً</b>.
    /// </para>
    /// </summary>
    private static IEnumerable<int> CuePositions(VoiceSlot slot, IReadOnlyList<string> words)
    {
        foreach (string cue in slot.Cues)
        {
            IReadOnlyList<string> parts = VoiceText.Words(cue);
            if (parts.Count == 0)
            {
                continue;
            }

            for (int index = 0; index + parts.Count <= words.Count; index++)
            {
                bool hit = true;

                for (int offset = 0; offset < parts.Count; offset++)
                {
                    if (!VoiceText.Same(words[index + offset], parts[offset]))
                    {
                        hit = false;
                        break;
                    }
                }

                if (hit)
                {
                    yield return index + parts.Count;
                }
            }
        }
    }

    /// <summary>يجمع أطول مقطع عددي يبدأ عند الموضع، ويقشر واو العطف الأخيرة.</summary>
    private static (string Text, int Next)? NumberSpan(IReadOnlyList<string> words, int from)
    {
        int end = from;

        while (end < words.Count && (ArabicSpokenNumber.CanRead(words[end]) || words[end] == "و"))
        {
            end++;
        }

        while (end > from && words[end - 1] == "و")
        {
            end--;
        }

        return end == from ? null : (string.Join(' ', words.Skip(from).Take(end - from)), end);
    }

    private static SpokenSlotValue? ReadNumeric(VoiceSlot slot, IReadOnlyList<string> words)
    {
        foreach (int at in CuePositions(slot, words))
        {
            if (at >= words.Count)
            {
                continue;
            }

            (string Text, int Next)? span = NumberSpan(words, at);
            if (span is null)
            {
                continue;
            }

            Result<decimal> read = ArabicSpokenNumber.Read(span.Value.Text);
            if (read.IsFailure)
            {
                continue;
            }

            decimal value = read.Value;

            // «خمسة عشر بالمئة» نسبةٌ لا عدد: القسمة تقع هنا لا في الشاشة.
            if (span.Value.Next < words.Count && PercentWords.Contains(VoiceText.Fold(words[span.Value.Next])))
            {
                value /= 100m;
            }

            return new SpokenSlotValue(
                slot.Name,
                value.ToString("0.####", CultureInfo.InvariantCulture),
                null,
                span.Value.Text,
                FieldProvenance.Spoken);
        }

        return null;
    }

    /// <summary>
    /// الكمّية <b>ووحدتُها معاً</b>. عددٌ بلا وحدةٍ ليس كمّية: يُسجَّل عطلاً مُسمّى،
    /// وتبقى الشريحة فارغة — <b>ولا يُفترض أن المقصود وحدة الأساس</b>.
    /// </summary>
    private static SpokenSlotValue? ReadQuantity(VoiceSlot slot, IReadOnlyList<string> words, List<Error> faults)
    {
        string? heardWithoutUnit = null;

        foreach (int at in CuePositions(slot, words))
        {
            if (at >= words.Count)
            {
                continue;
            }

            (string Text, int Next)? span = NumberSpan(words, at);
            if (span is null)
            {
                continue;
            }

            Result<decimal> read = ArabicSpokenNumber.Read(span.Value.Text);
            if (read.IsFailure)
            {
                continue;
            }

            // ‏**الوحدة المركّبة تُجرَّب أولاً**: «متر مكعب» تبدأ بـ«متر»، وأخذُ الأولى
            // وحدها يُدخل خرسانةً بمقدارٍ يقلّ عن الحقيقة بمرتبتين.
            int next = span.Value.Next;
            string? unit = next + 1 < words.Count ? VoiceUnits.CodeOfPair(words[next], words[next + 1]) : null;
            int width = unit is null ? 1 : 2;

            if (unit is null && next < words.Count)
            {
                unit = VoiceUnits.CodeOf(words[next]);
            }

            if (unit is null)
            {
                heardWithoutUnit ??= span.Value.Text;
                continue;
            }

            string spokenUnit = string.Join(' ', words.Skip(next).Take(width));

            return new SpokenSlotValue(
                slot.Name,
                read.Value.ToString("0.####", CultureInfo.InvariantCulture),
                unit,
                span.Value.Text + " " + spokenUnit,
                FieldProvenance.Spoken);
        }

        if (heardWithoutUnit is not null)
        {
            faults.Add(VoiceRefusals.UnitMissing(slot, heardWithoutUnit));
        }

        return null;
    }

    /// <summary>
    /// التاريخ: منطوقٌ إن قيل، وإلّا <b>من الإعدادات</b> بوسمٍ ظاهر — ولا يُخترَع
    /// حين لا يُحقَن تاريخُ اليوم أصلاً.
    /// </summary>
    private static SpokenSlotValue? ReadDate(VoiceSlot slot, IReadOnlyList<string> words, VoiceReadingOptions options)
    {
        foreach (string word in words)
        {
            if (options.Today is not null && string.Equals(VoiceText.Fold(word), TodayWord, StringComparison.Ordinal))
            {
                return new SpokenSlotValue(slot.Name, options.Today, null, word, FieldProvenance.Spoken);
            }

            if (options.Today is not null && YesterdayWords.Contains(VoiceText.Fold(word)))
            {
                return new SpokenSlotValue(slot.Name, Shift(options.Today, -1), null, word, FieldProvenance.Spoken);
            }

            if (DateOnly.TryParseExact(word, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly exact))
            {
                return new SpokenSlotValue(
                    slot.Name,
                    exact.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    null,
                    word,
                    FieldProvenance.Spoken);
            }
        }

        return options.Today is null
            ? null
            : new SpokenSlotValue(slot.Name, options.Today, null, string.Empty, FieldProvenance.Defaulted);
    }

    private static string Shift(string iso, int days) =>
        DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
            ? date.AddDays(days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : iso;

    /// <summary>اختيارٌ من قائمة مغلقة. ما ليس فيها لا يُقارَب بأقرب شبيه.</summary>
    private static SpokenSlotValue? ReadChoice(VoiceSlot slot, IReadOnlyList<string> words)
    {
        foreach (string choice in slot.Choices)
        {
            foreach (string word in words)
            {
                if (VoiceText.Same(word, choice))
                {
                    return new SpokenSlotValue(slot.Name, choice, null, word, FieldProvenance.Spoken);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// رمزٌ أو رقمُ مستندٍ أو موقعٍ في رفّ: <b>يقبل العدد داخله</b> — «رف ثلاثة» و«شقة
    /// اثنتي عشرة» رمزان لا نصّان ولا عددان. ويتخطّى «رقم» الموصِّلة قبل القيمة.
    /// </summary>
    private static SpokenSlotValue? ReadCode(VoiceSlot slot, IReadOnlyList<string> words, HashSet<string> boundaries)
    {
        foreach (int start in CuePositions(slot, words))
        {
            int at = start;

            while (at < words.Count && Connectors.Contains(VoiceText.Fold(words[at])))
            {
                at++;
            }

            List<string> parts = [];

            for (int index = at; index < words.Count && parts.Count < CodeWordLimit; index++)
            {
                string word = words[index];

                if (boundaries.Contains(VoiceText.Fold(word)) || VoiceUnits.IsUnit(word))
                {
                    break;
                }

                parts.Add(word);
            }

            if (parts.Count > 0)
            {
                string text = string.Join(' ', parts);
                return new SpokenSlotValue(slot.Name, text, null, text, FieldProvenance.Spoken);
            }
        }

        return null;
    }

    /// <summary>
    /// نصٌّ حرّ: ما بين الدليل وأول حدّ — كلمةِ إيقاف، أو <b>كاسرِ مقطع</b>، أو دليلِ
    /// شريحةٍ أخرى، أو عدد. <b>وما قُصّ عند كاسرٍ يُحمَل ولا يُطرح</b>.
    /// </summary>
    private static SpokenSlotValue? ReadText(VoiceSlot slot, IReadOnlyList<string> words, HashSet<string> boundaries)
    {
        foreach (int at in CuePositions(slot, words))
        {
            List<string> parts = [];
            int stop = words.Count;

            for (int index = at; index < words.Count; index++)
            {
                string word = words[index];

                if (boundaries.Contains(VoiceText.Fold(word)) || VoiceUnits.IsUnit(word) || ArabicSpokenNumber.CanRead(word))
                {
                    stop = index;
                    break;
                }

                parts.Add(word);
            }

            if (parts.Count > 0)
            {
                string text = string.Join(' ', parts);
                return new SpokenSlotValue(
                    slot.Name, text, null, text, FieldProvenance.Spoken, DroppedTail(words, stop, boundaries));
            }
        }

        return null;
    }

    /// <summary>
    /// <b>ما كان سيُبتلَع لولا كواسرُ المقاطع</b> — يُحسب حين يقع التوقّف على كاسر،
    /// ويمشي بالقاعدة <b>القديمة</b> وحدها حتى أول حدٍّ ليس كاسراً.
    /// <para>
    /// وهو الفرقُ بعينه بين القارئ قبل الإصلاح وبعده، معروضاً على الشاشة بدل أن
    /// يُستنتَج من غيابه.
    /// </para>
    /// </summary>
    private static string? DroppedTail(IReadOnlyList<string> words, int stop, HashSet<string> boundaries)
    {
        if (stop >= words.Count || !ClauseBreakers.Contains(VoiceText.Fold(words[stop])))
        {
            return null;
        }

        List<string> tail = [];

        for (int index = stop; index < words.Count; index++)
        {
            string word = words[index];
            string folded = VoiceText.Fold(word);

            if ((boundaries.Contains(folded) && !ClauseBreakers.Contains(folded))
                || VoiceUnits.IsUnit(word)
                || ArabicSpokenNumber.CanRead(word))
            {
                break;
            }

            tail.Add(word);
        }

        return tail.Count == 0 ? null : string.Join(' ', tail);
    }

    /// <summary>
    /// اسم شركةٍ نُطق داخل الأمر. <b>الدليل مركّب عمداً</b> («في شركة» لا «شركة»)،
    /// كي لا يُقرأ اسمُ موردٍ يبدأ بكلمة «شركة» انتقالاً بين المنشآت.
    /// </summary>
    private static string? ReadCompany(IReadOnlyList<string> words)
    {
        foreach (string cue in CompanyCues)
        {
            IReadOnlyList<string> parts = VoiceText.Words(cue);

            for (int index = 0; index + parts.Count <= words.Count; index++)
            {
                bool hit = true;

                for (int offset = 0; offset < parts.Count; offset++)
                {
                    if (!VoiceText.Same(words[index + offset], parts[offset]))
                    {
                        hit = false;
                        break;
                    }
                }

                if (!hit)
                {
                    continue;
                }

                List<string> name = [];

                for (int at = index + parts.Count; at < words.Count; at++)
                {
                    // ‏**واسمُ المنشأة يُقصّ بالمجموعة نفسها**: هذا الماشي لا يقرأ
                    // <c>Boundaries</c> لأنه يعمل قبل أن تُعرف الشريحة — فيقرأ أساسَها.
                    if (NameBoundaries.Contains(VoiceText.Fold(words[at])) || ArabicSpokenNumber.CanRead(words[at]))
                    {
                        break;
                    }

                    name.Add(words[at]);
                }

                if (name.Count > 0)
                {
                    return string.Join(' ', name);
                }
            }
        }

        return null;
    }
}
