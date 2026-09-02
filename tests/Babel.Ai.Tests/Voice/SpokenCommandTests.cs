using System.Globalization;
using Babel.Ai.Tests.Support;
using Babel.Ai.Voice;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Voice;

/// <summary>
/// <b>القراءة — على المتجهات المشتركة، لا على أمثلةٍ تُكتب في الاختبار.</b>
/// <para>
/// كل نيّةٍ من العشرين لها هنا ثلاثة إثباتات: جملةٌ تُقرأ كاملة، وجملةٌ تنقصها شريحة
/// لازمة <b>فتُسمّى باسمها</b>، وجملةٌ لا تُفهَم أصلاً. والملفّ نفسه يقرؤه اختبارُ
/// المتصفّح، فانحرافُ التنفيذين يُحمِّر بوّابةً لا شاشةً.
/// </para>
/// </summary>
public sealed class SpokenCommandTests
{
    public static TheoryData<string> Utterances()
    {
        TheoryData<string> data = [];
        foreach (VectorUtterance vector in VoiceVectors.File.Utterances)
        {
            data.Add(vector.Transcript);
        }

        return data;
    }

    public static TheoryData<string> MissingCases()
    {
        TheoryData<string> data = [];
        foreach (VectorMissing vector in VoiceVectors.File.Missing)
        {
            data.Add(vector.Transcript);
        }

        return data;
    }

    public static TheoryData<string> Refusals()
    {
        TheoryData<string> data = [];
        foreach (VectorRefusal vector in VoiceVectors.File.Refusals)
        {
            data.Add(vector.Transcript);
        }

        return data;
    }

    [Fact]
    public void ملف_المتجهات_ليس_ضامراً()
    {
        // حارس لا فراغ: ملفٌّ فارغ يجعل كل ما تحته يمرّ بلا أن يقرأ شيئاً (فخ-43).
        Assert.True(VoiceVectors.File.Intents.Count >= 40);
        Assert.True(VoiceVectors.File.Utterances.Count >= 40);
        Assert.True(VoiceVectors.File.Missing.Count >= 40);
        Assert.True(VoiceVectors.File.Refusals.Count >= 3);
    }

    [Fact]
    public void ملف_المتجهات_يصف_السجل_الحقيقي_نيّةً_نيّة()
    {
        // ‏**والمتجهات تصف المنتج لا نسخةً منه**: نيّةٌ تُضاف في وحدةٍ ولا تُكتب هنا
        // تُحمِّر فوراً، فلا يوجد سجلّان ينحرفان.
        string[] actual = [.. VoiceHarness.Registry.Intents.Select(static intent => intent.Id)];
        string[] declared = [.. VoiceVectors.File.Intents.Select(static intent => intent.Id).Order(StringComparer.Ordinal)];

        Assert.Equal(declared, actual);

        foreach (VectorIntent declaredIntent in VoiceVectors.File.Intents)
        {
            Contracts.Voice.VoiceIntent? intent = VoiceHarness.Registry.Find(declaredIntent.Id);
            Assert.NotNull(intent);
            Assert.Equal(declaredIntent.Section, intent.Section.ToString());
            Assert.Equal(declaredIntent.Module, intent.Module.ToString());
            Assert.Equal(declaredIntent.Kind, intent.Kind.ToString());
            Assert.Equal(declaredIntent.Status, intent.Status.ToString());
            Assert.Equal(declaredIntent.LedgerEffect, intent.LedgerEffect.ToString());
            Assert.Equal(declaredIntent.EventCode, intent.EventCode);
            Assert.Equal(declaredIntent.OperationId, intent.OperationId);
            Assert.Equal(declaredIntent.RequiresConfirmation, intent.RequiresConfirmation);
            Assert.Equal(declaredIntent.ReadsPersonalData, intent.ReadsPersonalData);
            Assert.Equal(declaredIntent.NameAr, intent.NameAr);
            Assert.Equal(declaredIntent.Phrases, intent.Phrases);

            foreach (VectorSlot declaredSlot in declaredIntent.Slots)
            {
                Contracts.Voice.VoiceSlot slot = Assert.Single(intent.Slots, candidate => candidate.Name == declaredSlot.Name);
                Assert.Equal(declaredSlot.Kind, slot.Kind.ToString());
                Assert.Equal(declaredSlot.NameAr, slot.NameAr);
                Assert.Equal(declaredSlot.Required, slot.Required);
                Assert.Equal(declaredSlot.Cues, slot.Cues);
                Assert.Equal(declaredSlot.Choices, slot.Choices);
            }
            Assert.Equal([.. declaredIntent.Slots.Select(static slot => slot.Name)], [.. intent.Slots.Select(static slot => slot.Name)]);
        }
    }

    [Theory]
    [MemberData(nameof(Utterances))]
    public void المسار_السعيد_يستخرج_كل_شريحة_بقيمتها(string transcript)
    {
        VectorUtterance vector = VoiceVectors.File.Utterances.Single(candidate => candidate.Transcript == transcript);

        Result<VoiceResolution> read = SpokenCommandReader.Read(transcript, VoiceHarness.Registry, VoiceHarness.Options);

        Assert.True(read.IsSuccess, read.IsFailure ? read.Errors[0].MessageAr : string.Empty);
        VoiceResolution resolution = read.Value;

        Assert.Equal(vector.Intent, resolution.Intent.Id);
        Assert.Empty(resolution.MissingSlots);

        foreach ((string name, string expected) in vector.Slots)
        {
            SpokenSlotValue value = Assert.Single(resolution.Slots, candidate => candidate.Name == name);
            Assert.Equal(expected, value.Text);
        }

        foreach ((string name, string unit) in vector.Units ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            SpokenSlotValue value = Assert.Single(resolution.Slots, candidate => candidate.Name == name);
            Assert.Equal(unit, value.Unit);
        }

        // الملخّص يحمل اسم النيّة ولا يخرج فارغاً — وهو ما يُقرأ ويُعرض معاً.
        Assert.Contains(resolution.Intent.NameAr, resolution.ReadbackAr, StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, resolution.ConfirmationToken);
    }

    [Theory]
    [MemberData(nameof(MissingCases))]
    public void الشريحة_الناقصة_تُسمّى_ولا_تُخترَع(string transcript)
    {
        VectorMissing vector = VoiceVectors.File.Missing.Single(candidate => candidate.Transcript == transcript);

        // ‏**بلا حقنِ تاريخِ اليوم لا يُملأ حقلُ تاريخٍ إطلاقاً** — ولا ساعةَ جهازٍ داخل
        // المحرّك. والمتجه الذي يطلب ذلك يقيس القاعدة نفسها لا يستثني منها.
        VoiceReadingOptions options = vector.WithoutToday
            ? new VoiceReadingOptions(null, VoiceHarness.Options.StatutoryTaxRate)
            : VoiceHarness.Options;

        Result<VoiceResolution> read = SpokenCommandReader.Read(transcript, VoiceHarness.Registry, options);

        Assert.True(read.IsSuccess, read.IsFailure ? read.Errors[0].MessageAr : string.Empty);
        VoiceResolution resolution = read.Value;

        Assert.Equal(vector.Intent, resolution.Intent.Id);
        Assert.Equal(vector.Missing.Order(StringComparer.Ordinal), resolution.MissingSlots.Order(StringComparer.Ordinal));

        // ولا قيمةَ مُخترَعة مكان الناقص.
        foreach (string name in vector.Missing)
        {
            Assert.DoesNotContain(resolution.Slots, candidate => candidate.Name == name);
        }

        foreach (string code in vector.Faults ?? [])
        {
            Assert.Contains(resolution.Faults, fault => fault.Code == code);
        }

        // والبوابة ترفض، وتسمّي الشريحة باسمها العربي في الرسالة.
        Result<VoiceDispatch> gate = VoiceConfirmationGate.Authorise(
            resolution, VoiceHarness.Caller, resolution.ConfirmationToken);

        Assert.True(gate.IsFailure);

        if (resolution.Intent.Status == Contracts.Voice.VoiceIntentStatus.AwaitingOwnerDecision)
        {
            // ‏**والقرار المعلَّق يسبق النقص**: نيّةٌ لا تُنفَّذ أصلاً لا يُطلب من قائلها
            // أن يُكمل شرائحها، فيُقال له السبب الحقيقي لا سببٌ سيُتبعه سبب.
            Assert.Contains(gate.Errors, error => error.Code == "ai.voice.owner_decision_pending");
            return;
        }

        Assert.Contains(gate.Errors, error => error.Code == "ai.voice.slot_missing" || error.Code == "ai.voice.unit_missing");
        Assert.Contains(
            gate.Errors,
            error => error.MessageAr.StartsWith(VoiceRefusals.MissingAr, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public void ما_لا_يُفهَم_يُرفض_بالاسم_ولا_يُقارَب_بأقرب_شبيه(string transcript)
    {
        VectorRefusal vector = VoiceVectors.File.Refusals.Single(candidate => candidate.Transcript == transcript);

        Result<VoiceResolution> read = SpokenCommandReader.Read(transcript, VoiceHarness.Registry, VoiceHarness.Options);

        Assert.True(read.IsFailure);
        Assert.Contains(read.Errors, error => error.Code == vector.Code);
    }

    [Fact]
    public void جملة_تطابق_نيّتين_تُرفض_ولا_يُختار_أحدهما_بالقرعة()
    {
        // «سجل سند قبض» و«سجل سند صرف» عبارتان بطول واحد؛ وجملةٌ تحمل الاثنتين
        // تُنتج تعادلاً — والتعادل رفضٌ لا قرعة.
        Result<VoiceResolution> read = SpokenCommandReader.Read(
            "سجل سند قبض وسجل سند صرف", VoiceHarness.Registry, VoiceHarness.Options);

        Assert.True(read.IsFailure);
        Assert.Contains(read.Errors, error => error.Code == "ai.voice.intent_ambiguous");
    }

    [Fact]
    public void التفريغ_الأطول_من_الحد_يُرفض_بالحد_مُسمّى()
    {
        string long_ = new('ا', SpokenCommandReader.TranscriptLimit + 1);

        Result<VoiceResolution> read = SpokenCommandReader.Read(long_, VoiceHarness.Registry, VoiceHarness.Options);

        Assert.True(read.IsFailure);
        Assert.Contains(read.Errors, error => error.Code == "ai.voice.transcript_too_long");
    }

    [Fact]
    public void التاريخ_غير_المنطوق_يأتي_من_الإعدادات_بوسم_ظاهر_ولا_يُخترَع_بلا_حقن()
    {
        const string transcript = "سجل سند قبض من العميل مؤسسة الرياض بمبلغ ألف ريال نقد";

        VoiceResolution withToday = SpokenCommandReader
            .Read(transcript, VoiceHarness.Registry, VoiceHarness.Options).Value;
        SpokenSlotValue date = Assert.Single(withToday.Slots, slot => slot.Name == "receivedOn");
        Assert.Equal(Contracts.Capture.FieldProvenance.Defaulted, date.Provenance);
        Assert.Equal(VoiceHarness.Today, date.Text);

        // وبلا حقنٍ لتاريخ اليوم **لا يُملأ الحقل إطلاقاً** — لا ساعةَ جهازٍ في المحرّك.
        VoiceResolution without = SpokenCommandReader
            .Read(transcript, VoiceHarness.Registry, new VoiceReadingOptions()).Value;
        Assert.DoesNotContain(without.Slots, slot => slot.Name == "receivedOn");
        Assert.Contains("receivedOn", without.MissingSlots);
    }

    [Fact]
    public void الكمية_بلا_وحدة_تُرفض_ولا_تُفسَّر_بوحدة_الأساس()
    {
        VoiceResolution resolution = SpokenCommandReader.Read(
            "سجل جرد الصنف اسمنت كمية عشرين المستودع الرئيسي اليوم",
            VoiceHarness.Registry,
            VoiceHarness.Options).Value;

        Assert.Contains("quantity", resolution.MissingSlots);
        Assert.Contains(resolution.Faults, fault => fault.Code == "ai.voice.unit_missing");
        Assert.DoesNotContain(resolution.Slots, slot => slot.Name == "quantity");
    }

    [Fact]
    public void الوحدة_المركبة_تُقرأ_قبل_مفردها()
    {
        VoiceResolution cubic = SpokenCommandReader.Read(
            "سجل مستخلص عميل للعقد برج الشمال بند خرسانة كمية عشرة متر مكعب اليوم",
            VoiceHarness.Registry,
            VoiceHarness.Options).Value;

        Assert.Equal("M3", Assert.Single(cubic.Slots, slot => slot.Name == "quantity").Unit);

        VoiceResolution linear = SpokenCommandReader.Read(
            "سجل مستخلص عميل للعقد برج الشمال بند دهان كمية عشرة متر اليوم",
            VoiceHarness.Registry,
            VoiceHarness.Options).Value;

        Assert.Equal("M", Assert.Single(linear.Slots, slot => slot.Name == "quantity").Unit);
    }

    [Fact]
    public void رمز_التأكيد_يتغير_بتغير_الأمر_ولا_يتغير_بترتيب_الكلام()
    {
        VoiceResolution first = SpokenCommandReader.Read(
            "سجل سند قبض من العميل مؤسسة الرياض بمبلغ ألف ريال نقد اليوم",
            VoiceHarness.Registry, VoiceHarness.Options).Value;

        VoiceResolution other = SpokenCommandReader.Read(
            "سجل سند قبض من العميل مؤسسة الرياض بمبلغ ألفين ريال نقد اليوم",
            VoiceHarness.Registry, VoiceHarness.Options).Value;

        Assert.NotEqual(first.ConfirmationToken, other.ConfirmationToken);

        VoiceResolution again = SpokenCommandReader.Read(
            "سجل سند قبض من العميل مؤسسة الرياض بمبلغ ألف ريال نقد اليوم",
            VoiceHarness.Registry, VoiceHarness.Options).Value;

        Assert.Equal(first.ConfirmationToken, again.ConfirmationToken);
    }

    /// <summary>
    /// <b>الحدّان مُشتقّان من المتجهات لا مكتوبان بيد.</b>
    /// <para>
    /// رقمٌ يختاره كاتبٌ سحرٌ؛ ورقمٌ يُعاد حسابه من الملفّ الذي يصف المنتج <b>بياناتٌ
    /// مملوكة</b>. فإن احتاج متجهٌ جديد اسماً أطول، حمرّ هذا الإثبات وطلب تعديل الثابت —
    /// <b>وذلك فرقٌ يُراجَع</b>، لا سطرٌ يتحرّك بلا أن يراه أحد.
    /// </para>
    /// </summary>
    [Fact]
    public void حدّا_الاسم_والرمز_يُعاد_حسابهما_من_المتجهات()
    {
        Dictionary<(string Intent, string Slot), string> kinds = [];
        foreach (VectorIntent intent in VoiceVectors.File.Intents)
        {
            foreach (VectorSlot slot in intent.Slots)
            {
                kinds[(intent.Id, slot.Name)] = slot.Kind;
            }
        }

        int longestName = 0;
        int longestCode = 0;

        foreach (VectorUtterance vector in VoiceVectors.File.Utterances)
        {
            foreach ((string name, string value) in vector.Slots)
            {
                if (!kinds.TryGetValue((vector.Intent, name), out string? kind))
                {
                    continue;
                }

                int count = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

                if (string.Equals(kind, "Text", StringComparison.Ordinal))
                {
                    longestName = Math.Max(longestName, count);
                }
                else if (string.Equals(kind, "Code", StringComparison.Ordinal))
                {
                    longestCode = Math.Max(longestCode, count);
                }
            }
        }

        // حارس لا فراغ: صفرٌ هنا يعني أن المُحلِّل لم يقرأ شيئاً فمرّ (فخ-43).
        Assert.True(longestName > 0 && longestCode > 0);

        Assert.Equal(SpokenCommandReader.NameWordLimit, longestName);
        Assert.Equal(SpokenCommandReader.CodeWordLimit, longestCode);
    }

    /// <summary>
    /// <b>وحدّا المتصفّح هما حدّا الخادم حرفاً.</b> تنفيذان بحدَّين مختلفين ينحرفان،
    /// ولا يُكتشف انحرافهما إلا على شاشة صاحب المصلحة (‏ADR-0030 خامساً): يقبل المتصفّح
    /// اسماً ويرفضه الخادم، فيُقرأ الرفض عطلاً في الشبكة.
    /// </summary>
    [Fact]
    public void حدّا_المتصفّح_هما_حدّا_الخادم()
    {
        string browser = File.ReadAllText(RepositoryRoot.At("web/src/voice/command.ts"));

        Assert.Contains(
            "export const NAME_WORD_LIMIT = "
            + SpokenCommandReader.NameWordLimit.ToString(CultureInfo.InvariantCulture) + ";",
            browser,
            StringComparison.Ordinal);

        Assert.Contains(
            "export const CODE_WORD_LIMIT = "
            + SpokenCommandReader.CodeWordLimit.ToString(CultureInfo.InvariantCulture) + ";",
            browser,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>الفاصلة لا تُغيّر شيئاً — خاصّيةً لا قيمةً واحدة.</b>
    /// <para>
    /// حكمُ المقطع <b>لا يُقسّم الجملة ولا يُضيف حدّاً</b>: هو رأيٌ في مقطعٍ أنتجه
    /// المشيُ نفسه. وهذا الإثبات يقيس ذلك على المتجهات كلّها: توأمٌ بفاصلةٍ قبل كل
    /// قيمةٍ متوقّعة يجب أن يُنتج <b>قراءةً مطابقة حرفاً بحرف</b> — الشرائح، وما سُمع،
    /// والملخّص، ورمز التأكيد. فانحدارُ الترقيم الذي هزم محاولةً سابقة لا يعود صامتاً.
    /// </para>
    /// </summary>
    [Fact]
    public void الفاصلة_قبل_القيمة_لا_تُغيّر_القراءة_في_أي_متجه()
    {
        int measured = 0;

        foreach (VectorUtterance vector in VoiceVectors.File.Utterances)
        {
            VoiceResolution plain = SpokenCommandReader
                .Read(vector.Transcript, VoiceHarness.Registry, VoiceHarness.Options).Value;

            foreach ((string _, string value) in vector.Slots)
            {
                string needle = " " + value;
                if (!vector.Transcript.Contains(needle, StringComparison.Ordinal))
                {
                    continue;
                }

                int at = vector.Transcript.IndexOf(needle, StringComparison.Ordinal);
                string twin = string.Concat(
                    vector.Transcript.AsSpan(0, at),
                    "، ",
                    vector.Transcript.AsSpan(at + 1));

                Result<VoiceResolution> read = SpokenCommandReader
                    .Read(twin, VoiceHarness.Registry, VoiceHarness.Options);

                Assert.True(read.IsSuccess, twin);
                VoiceResolution comma = read.Value;
                measured++;

                Assert.Equal(plain.Intent.Id, comma.Intent.Id);
                Assert.Equal(plain.ReadbackAr, comma.ReadbackAr);
                Assert.Equal(plain.ConfirmationToken, comma.ConfirmationToken);
                Assert.Equal(plain.SpokenCompany, comma.SpokenCompany);
                Assert.Equal(plain.MissingSlots, comma.MissingSlots);
                Assert.Equal(
                    plain.Faults.Select(static fault => fault.Code),
                    comma.Faults.Select(static fault => fault.Code));
                Assert.Equal(
                    plain.Slots.Select(static slot => (slot.Name, slot.Text, slot.Unit, slot.Heard, slot.Provenance)),
                    comma.Slots.Select(static slot => (slot.Name, slot.Text, slot.Unit, slot.Heard, slot.Provenance)));
            }
        }

        Assert.True(measured >= 40, measured.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// <b>الثقب المُعلَن — يُكتب هنا قبل أن يجده أحد.</b>
    /// <para>
    /// حكمُ المقطع مرشّحٌ رخيص، <b>وله ثقبان لا يُسدّان بلا معجم</b>، وكلاهما مقيسٌ
    /// في هذا الإثبات لا موصوفٌ في تعليق:
    /// </para>
    /// <list type="number">
    ///   <item>
    ///     <b>فعلٌ بمفعوله يبدأ بشكل أداة التعريف</b> — «التقطها»، و«ألغها» بعد التجريد
    ///     «الغها». والأداة تُبرّئ «الركن» و«المساكن» من أن تُقرأ أفعالاً، <b>ولا قاعدة
    ///     إملائية</b> تفصل «التقطها» عن «التجارة» — ثالثُ حرفهما تاء في الاثنتين.
    ///   </item>
    ///   <item>
    ///     <b>ذيلُ إسنادٍ بلا ضمير متّصل داخل الحدّ</b> — «النور وحول المبلغ» ثلاث كلمات
    ///     كاسمٍ من ثلاث كلمات، ولا فرق في الرسم بينهما.
    ///   </item>
    ///   <item>
    ///     <b>الهاء المفردة مفعولاً</b> — «وسجله» و«قيده». وهي مُخرَجة <b>عمداً</b>: محرّك
    ///     التفريغ يكتب التاء المربوطة هاءً بلا قاعدة، فمنعُها يرفض «شركة صيانه» و«مؤسسة
    ///     تجاره» — <b>ورفضُ اسمٍ حقيقي عطلٌ آخر لا عطلٌ أصغر</b>.
    ///   </item>
    /// </list>
    /// <para>
    /// والجواب على الاثنين ليس بنداً يُضاف إلى قائمة — <b>فذلك بالضبط ما هُزم ثلاث مرّات
    /// في يومٍ واحد</b> (‏docs/evidence/traps.md#fakh-a-remedy-that-is-a-list-is-not-a-remedy) —
    /// بل <b>الطبقةُ الثانية</b>: الاسم لا يغادر شاشة المسوّدة إلا معرّفَ صفٍّ واحد،
    /// ويحرسه <see cref="VoiceNamesNeverReachTheDoor"/>: لا باب منشور يقبل اسماً منطوقاً.
    /// </para>
    /// </summary>
    [Fact]
    public void الثقب_المُعلَن_في_الطبقة_الأولى_مقيس_لا_موصوف()
    {
        // ١ · فعلٌ بمفعوله يبدأ بشكل الأداة — يمرّ من الطبقة الأولى، وهذا مُعلَن.
        VoiceResolution article = SpokenCommandReader.Read(
            "سجل سند قبض من العميل مؤسسة الرياض التقطها بمبلغ الف ريال نقد اليوم",
            VoiceHarness.Registry, VoiceHarness.Options).Value;

        Assert.Equal(
            "مؤسسة الرياض التقطها",
            Assert.Single(article.Slots, slot => slot.Name == "customer").Text);

        // ٢ · ذيلُ إسنادٍ بلا ضمير متّصل داخل الحدّ — يمرّ كذلك، وهذا مُعلَن.
        VoiceResolution tail = SpokenCommandReader.Read(
            "سجل سند قبض من العميل النور وحول المبلغ بمبلغ الف ريال نقد اليوم",
            VoiceHarness.Registry, VoiceHarness.Options).Value;

        Assert.Equal(
            "النور وحول المبلغ",
            Assert.Single(tail.Slots, slot => slot.Name == "customer").Text);

        // ٣ · الهاء المفردة مفعولاً — تمرّ كذلك، وثمنُ منعها أغلى من ثمن إقرارها.
        VoiceResolution he = SpokenCommandReader.Read(
            "سجل جرد الصنف اسمنت مقاوم وسجله كمية عشرين كيس المستودع الرئيسي اليوم",
            VoiceHarness.Registry, VoiceHarness.Options).Value;

        Assert.Equal(
            "اسمنت مقاوم وسجله",
            Assert.Single(he.Slots, slot => slot.Name == "item").Text);

        // ٤ · وثمنُ الحدّ مُعلَن كذلك: اسمٌ حقيقيّ من أربع كلمات **يُرفض ولا يُبتَر**.
        VoiceResolution long_ = SpokenCommandReader.Read(
            "سجل سند قبض من العميل شركة النور الأولى للمقاولات بمبلغ الف ريال نقد اليوم",
            VoiceHarness.Registry, VoiceHarness.Options).Value;

        Assert.Contains("customer", long_.MissingSlots);
        Assert.Contains(long_.Faults, fault => fault.Code == "ai.voice.name_not_bounded");

        // والثلاثة الأولى تقف عند الطبقة الثانية: الأبواب لا تقبل اسماً أصلاً.
        Assert.Equal("draftCustomerReceipt", article.Intent.OperationId);
        Assert.Equal(article.Intent.OperationId, tail.Intent.OperationId);
    }

    /// <summary>
    /// <b>استبدال الطرف الصامت — الرفض يُلزِم، ولا يُستبدَل بطرفٍ آخر.</b>
    /// <para>
    /// <b>ما وقع:</b> حكمُ المقطع كان يرفض المقطع الأوّل ثم <b>يخزّن الرفض ويمضي</b> إلى
    /// الدليل التالي (<c>refused ??= …; continue;</c>). فإن حملت الجملة طرفاً ثانياً —
    /// وهي تحمله دائماً في «من العميل ... لصالح ...» — أسعف الدليلُ التالي بمقطعٍ قصيرٍ
    /// مقبول، <b>فعاد طرفٌ آخر تماماً، و<c>Faults</c> فارغة، والقراءة كاملة</b>. فيُقرأ
    /// على المستخدم اسمُ عميلٍ لم ينطقه، ويحلّه صفٌّ واحد فيمضي إلى مسوّدة.
    /// </para>
    /// <para>
    /// <b>ولا تُنقذه الطبقة الثانية</b> (<see cref="VoiceNamesNeverReachTheDoor"/>): تلك
    /// تمنع <b>الاسم</b> من بلوغ الباب، وهذا اسمٌ <b>يُحَلّ إلى صفٍّ واحد بنجاح</b> —
    /// وهو الطرف الخطأ. الاستبدال الصامت يقع <b>قبل</b> الحلّ لا بعده.
    /// </para>
    /// <para>
    /// <b>والعلاج ليس قائمة</b> — لا فواصل جُملٍ تُعدَّد ولا كلماتٍ تُستثنى: الحكم على
    /// المقطع كما هو، <b>ثم يُلزِم</b>. مقطعٌ غير مبرَّر يُنهي البحث عن الشريحة برفضٍ
    /// مُسمّى؛ ولا يذهب القارئ يفتّش عن مقطعٍ آخر يعجبه في الجملة نفسها.
    /// </para>
    /// </summary>
    [Fact]
    public void المقطع_المرفوض_لا_يُستبدَل_صامتاً_بطرفٍ_آخر()
    {
        const string transcript =
            "سجل سند قبض من العميل شركة النور الاولى للمقاولات لصالح مؤسسة الرياض بمبلغ الف ريال نقد اليوم";

        VoiceResolution read = SpokenCommandReader
            .Read(transcript, VoiceHarness.Registry, VoiceHarness.Options).Value;

        // ‏١ · لا شريحة عميل تخرج بقيمة، والنقص مُسمّى.
        Assert.DoesNotContain(read.Slots, slot => slot.Name == "customer");
        Assert.Contains("customer", read.MissingSlots);
        Assert.False(read.IsComplete);

        // ‏٢ · والرفض مسموع: برمزه، وباسم المقطع الذي رُفض فعلاً — لا بمقطعٍ آخر.
        Error fault = Assert.Single(read.Faults, candidate => candidate.Code == "ai.voice.name_not_bounded");
        Assert.Contains("شركة النور الاولي للمقاولات", fault.MessageAr, StringComparison.Ordinal);

        // ‏٣ · **وهذا هو الحدّ**: الطرف الثاني لا يظهر في شيء يُقرأ أو يُنفَّذ عليه.
        Assert.DoesNotContain("مؤسسة الرياض", read.ReadbackAr, StringComparison.Ordinal);
        Assert.DoesNotContain(read.Slots, slot => slot.Text.Contains("الرياض", StringComparison.Ordinal));

        // ‏٤ · حارس لا فراغ: الرفض مقصورٌ على الشريحة المرفوضة — وليس سقوطاً عامّاً
        //      يجعل التأكيدات فوقه صحيحةً لسببٍ خاطئ (فخ-43).
        Assert.Equal("1000", Assert.Single(read.Slots, slot => slot.Name == "amount").Text);
        Assert.Equal("نقد", Assert.Single(read.Slots, slot => slot.Name == "method").Text);
        Assert.Equal(VoiceHarness.Today, Assert.Single(read.Slots, slot => slot.Name == "receivedOn").Text);
    }

    [Fact]
    public void معجم_الوحدات_ليس_ضامراً_ويرفض_ما_ليس_فيه()
    {
        Assert.True(VoiceUnits.Count >= 20, VoiceUnits.Count.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("CTN", VoiceUnits.CodeOf("كرتون"));
        Assert.Null(VoiceUnits.CodeOf("شوية"));
    }
}
