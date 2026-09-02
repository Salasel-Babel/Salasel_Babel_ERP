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
/// <para>
/// <b>ورابعٌ أُضيف بعد عطلٍ مقيس: لا يقرّر أين ينتهي اسمُ طرف.</b> شريحةُ
/// <see cref="VoiceSlotKind.Entity"/> لم تعد تُقرأ قيمةً — يُحدَّد لها <b>مقطع</b>
/// (<see cref="SpokenSpans.Locate"/>) ويُسلَّم <see cref="SlotReading.Pending"/> إلى السجلّ
/// المحلّي. والسبب أن القارئ <b>لا يملك ما يميّز</b> بين مقطعين كلاهما اسمُ منشأةٍ صحيح
/// الشكل في الجملة نفسها، <b>والسجلّ يملكه</b>.
/// </para>
/// <para>
/// <b>وخامسٌ حُذف ولم يُصلَح: قراءةُ اسم الشركة المنطوق.</b> كانت <c>ReadCompany</c>
/// تجمع اسماً بالقاعدة نفسها ثم يُقارَن بـ<c>VoiceText.Same</c> باسم الشركة المفتوحة —
/// <b>حكمٌ على الهوية بتساوي نصّين</b>، وهو بعينه ما يرفضه هذا المستودع. والشركةُ في
/// الجلسة (<c>VoiceCaller.CompanyId</c>) ولا تُنطق في مسوّدة قطّ؛ فصار <b>وجودُ دليلِ
/// شركةٍ هو الإشارة، لا الاسمُ المُحلَّل</b>.
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
        string folded = VoiceText.Fold(transcript);
        IReadOnlyList<string> words = VoiceText.Words(transcript);

        Result<VoiceIntent> matched = Match(folded, registry, transcript);
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

        HashSet<string> boundaries = Boundaries(intent);

        // ‏**قراءةٌ واحدة لكل شريحة، والمجموعة هي التي تفرض ذلك.** ‏`Add` ترمي على مفتاحٍ
        // مكرَّر، و`VoiceIntentRegistry.Build` يرفض تكرار اسم الشريحة أصلاً — فالرمية
        // **غير بالغة إلّا بالعطل**: «دليلٌ لاحق يطمس رفضاً سابقاً» يصير استثناءً عند
        // السطر بعينه، لا طرفاً معقولاً يعبر البوّابة.
        Dictionary<string, SlotReading> readings = new(StringComparer.Ordinal);

        foreach (VoiceSlot slot in intent.Slots)
        {
            readings.Add(slot.Name, ReadSlot(slot, intent, words, boundaries, reading));
        }

        // ‏**الشركة: الدليل هو الإشارة، لا الاسم.** لا يُحلَّل اسمٌ ولا يُقارَن بشيء.
        bool companyCueHeard = CompanyCueHeard(words);

        IReadOnlyList<SpokenSlotValue> values = Filled(readings);
        string readbackAr = VoiceReadback.Arabic(intent, values, readings);

        // ‏**الحارس على ما يُنطَق نفسه**: قيمةٌ شخصية تسلّلت إلى الملخّص تُرفض هنا،
        // لا في الطبقة التي تنطقه — فالطبقة قد تُنسى، وهذه لا تُتجاوَز.
        Result disclosure = VoiceDisclosure.Guard(readbackAr);
        if (disclosure.IsFailure)
        {
            return Result<VoiceResolution>.Failure(disclosure.Errors);
        }

        return Result<VoiceResolution>.Success(new VoiceResolution(
            intent,
            readings,
            companyCueHeard,
            readbackAr,
            VoiceReadback.Token(intent, readings)));
    }

    /// <summary>الشرائح الممتلئة بترتيب إعلانها — مشتقّةٌ من القراءات لا محفوظةٌ بجانبها.</summary>
    private static IReadOnlyList<SpokenSlotValue> Filled(Dictionary<string, SlotReading> readings) =>
        [.. readings.Values.OfType<SlotReading.Filled>().Select(static filled => filled.Value)];

    /// <summary>
    /// هل نُطق دليلُ شركة؟ <b>ولا يُقرأ ما بعده.</b> الاسمُ المُحلَّل كان يُقارَن بتساوي
    /// نصّين باسم الشركة المفتوحة، وذلك حكمٌ على الهوية بالتخمين. ووجودُ الدليل وحده
    /// يكفي للرفض، والرفضُ يسمّي الشاشة التي يُبدَّل منها.
    /// </summary>
    private static bool CompanyCueHeard(IReadOnlyList<string> words)
    {
        foreach (string cue in CompanyCues)
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
                    return true;
                }
            }
        }

        return false;
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

    /// <summary>حدودُ المقاطع: كلمات الإيقاف العامّة، ودلائل كل شريحة في هذه النيّة.</summary>
    private static HashSet<string> Boundaries(VoiceIntent intent)
    {
        HashSet<string> boundaries = new(StopWords, StringComparer.Ordinal);

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

    /// <summary>
    /// قراءةُ شريحةٍ واحدة — <b>وتُعيد حالةً من مجموعةٍ مغلقة، لا قيمةً أو لا شيء</b>.
    /// <para>
    /// و<see cref="VoiceSlotKind.Entity"/> وحدها لا تُقرأ قيمةً: يُحدَّد لها مقطع ويُسلَّم
    /// <see cref="SlotReading.Pending"/>. وشريحةٌ لازمة لم يُنطق لها شيء تعود
    /// <see cref="SlotReading.Silent"/>، والبوابةُ — لا هذه الدالّة — هي التي تُحوّلها رفضاً.
    /// </para>
    /// </summary>
    private static SlotReading ReadSlot(
        VoiceSlot slot,
        VoiceIntent intent,
        IReadOnlyList<string> words,
        HashSet<string> boundaries,
        VoiceReadingOptions options)
    {
        if (slot.Kind == VoiceSlotKind.Entity)
        {
            SpokenSpan? span = SpokenSpans.Locate(slot, words, SpokenSpans.ForeignCues(intent, slot));

            // ‏مفتاح السجلّ مضمونٌ عند البناء (‏`RegisterNotStated`)، فلا افتراض هنا.
            return span is null
                ? new SlotReading.Silent()
                : new SlotReading.Pending(slot.Name, span, slot.RegisterKey!);
        }

        if (slot.Kind == VoiceSlotKind.Quantity)
        {
            return ReadQuantity(slot, words);
        }

        SpokenSlotValue? value = slot.Kind switch
        {
            VoiceSlotKind.Money or VoiceSlotKind.Number => ReadNumeric(slot, words),
            VoiceSlotKind.Date => ReadDate(slot, words, options),
            VoiceSlotKind.Choice => ReadChoice(slot, words),
            VoiceSlotKind.Code => ReadCode(slot, words, boundaries),
            VoiceSlotKind.Prose => ReadProse(slot, words, boundaries),

            // قيمةٌ خارج المفردات المغلقة ترتفع ولا تسقط إلى «نصّ حرّ»: صنفٌ جديد
            // يُقرأ نصّاً حرّاً هو بعينه الباب الذي أُغلق في هذا التحويل.
            _ => throw new ArgumentOutOfRangeException(
                nameof(slot), slot.Kind, "صنفُ شريحةٍ خارج المفردات المغلقة."),
        };

        return value is null ? new SlotReading.Silent() : new SlotReading.Filled(value);
    }

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
    private static SlotReading ReadQuantity(VoiceSlot slot, IReadOnlyList<string> words)
    {
        // ‏**مرورانِ لا مرورٌ واحد بحالةٍ تعبر <c>continue</c>.**
        // كان الشكل «سجّل ما سُمع بـ??= ثم تابعْ إلى الدليل التالي» — وهو <b>الشكل نفسه</b>
        // الذي جعل رفضَ مقطعٍ سبباً في قبول مقطعٍ آخر في مسار الأسماء. فلا يُترك هنا
        // ليُقتدى به: مرورٌ يبحث عن عددٍ بوحدة، ثم — إن لم يُوجد — مرورٌ يسمّي أول عددٍ
        // سُمع بلا وحدة. لكلِّ حلقةٍ مخرجٌ واحد، ولا حالةَ تُحمل بينهما.
        foreach (int at in CuePositions(slot, words))
        {
            if (at >= words.Count)
            {
                continue;
            }

            (string Text, int Next)? span = NumberSpan(words, at);
            if (span is null || ArabicSpokenNumber.Read(span.Value.Text).IsFailure)
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
                continue;
            }

            return new SlotReading.Filled(new SpokenSlotValue(
                slot.Name,
                ArabicSpokenNumber.Read(span.Value.Text).Value.ToString("0.####", CultureInfo.InvariantCulture),
                unit,
                span.Value.Text + " " + string.Join(' ', words.Skip(next).Take(width)),
                FieldProvenance.Spoken));
        }

        // ‏**والرفض حالةٌ من حالات القراءة لا قائمةٌ بجانبها.** كان العطل يُدفع في قائمة
        // ثالثة تُقرأ في البوّابة، وثلاث قوائم متوازية تستطيع أن تتناقض.
        foreach (int at in CuePositions(slot, words))
        {
            if (at >= words.Count)
            {
                continue;
            }

            (string Text, int Next)? span = NumberSpan(words, at);
            if (span is not null && ArabicSpokenNumber.Read(span.Value.Text).IsSuccess)
            {
                return new SlotReading.Refused(VoiceRefusals.UnitMissing(slot, span.Value.Text));
            }
        }

        return new SlotReading.Silent();
    }

    /// <summary>
    /// التاريخ: منطوقٌ إن قيل، وإلّا <b>من الإعدادات</b> بوسمٍ ظاهر — ولا يُخترَع
    /// حين لا يُحقَن تاريخُ اليوم أصلاً.
    /// </summary>
    private static SpokenSlotValue? ReadDate(VoiceSlot slot, IReadOnlyList<string> words, VoiceReadingOptions options)
    {
        foreach (string word in words)
        {
            if (options.Today is not null && string.Equals(VoiceText.Fold(word), VoiceDates.TodayWord, StringComparison.Ordinal))
            {
                return new SpokenSlotValue(slot.Name, options.Today, null, word, FieldProvenance.Spoken);
            }

            if (options.Today is not null && VoiceDates.YesterdayWords.Contains(VoiceText.Fold(word)))
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
    /// نصٌّ حرّ <b>لا يسمّي أحداً</b>: ما بين الدليل وأول حدّ — كلمةِ إيقاف، أو دليلِ شريحةٍ
    /// أخرى، أو عدد.
    /// <para>
    /// <b>وهذه هي القاعدة التي أنتجت العطل، وتبقى هنا عمداً وبالتباين مُسمّى:</b> «بيان
    /// القيد» و«سبب التغيير» سطران يقرؤهما إنسان على شاشة المسوّدة، وخطؤهما تجميليّ؛
    /// و«العميل» طرفٌ على مستندٍ يُرحَّل، وخطؤه <b>طرفٌ آخر</b>. وحارسٌ في
    /// <c>NoDraftIsBuiltFromASpokenName</c> يقيس على العقد المنشور أن لا شريحةً من هذا
    /// الصنف تُغذّي حقلَ معرّف — فلا يلتبس الصنفان ثانيةً بسهو.
    /// </para>
    /// </summary>
    private static SpokenSlotValue? ReadProse(VoiceSlot slot, IReadOnlyList<string> words, HashSet<string> boundaries)
    {
        foreach (int at in CuePositions(slot, words))
        {
            List<string> parts = [];

            for (int index = at; index < words.Count; index++)
            {
                string word = words[index];

                if (boundaries.Contains(VoiceText.Fold(word)) || VoiceUnits.IsUnit(word) || ArabicSpokenNumber.CanRead(word))
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
}
