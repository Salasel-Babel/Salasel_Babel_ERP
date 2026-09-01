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

    /// <summary>
    /// <b>القصّ يُقاس، فلا يكون صامتاً.</b> متجهٌ يُعلن ذيلاً مقصوصاً يُحمِّر إن قُصّ
    /// غيرُه أو إن لم يُقصّ شيء؛ وشريحةٌ لا يُعلن لها ذيلٌ يجب ألّا تحمل واحداً —
    /// وإلّا كان الإصلاح يقصّ أسماءً لم يُقصد قصُّها.
    /// </summary>
    private static void AssertDropped(VoiceResolution resolution, IReadOnlyDictionary<string, string>? declared)
    {
        IReadOnlyDictionary<string, string> expected =
            declared ?? new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (SpokenSlotValue value in resolution.Slots)
        {
            if (expected.TryGetValue(value.Name, out string? tail))
            {
                Assert.Equal(tail, value.Dropped);
            }
            else
            {
                Assert.Null(value.Dropped);
            }
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

        AssertDropped(resolution, vector.Dropped);

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

        // ‏**وما امتلأ يُقاس أيضاً**: نقصُ حقلٍ لا يُعفي بقيّةَ الجملة من الصحّة، وجملةٌ
        // ينقصها حقلٌ واحد وقرأت الباقيَ خطأً تمرّ اليوم بلا أن يراها أحد.
        foreach ((string name, string expected) in vector.Slots ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            SpokenSlotValue value = Assert.Single(resolution.Slots, candidate => candidate.Name == name);
            Assert.Equal(expected, value.Text);
        }

        AssertDropped(resolution, vector.Dropped);

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

    [Fact]
    public void معجم_الوحدات_ليس_ضامراً_ويرفض_ما_ليس_فيه()
    {
        Assert.True(VoiceUnits.Count >= 20, VoiceUnits.Count.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("CTN", VoiceUnits.CodeOf("كرتون"));
        Assert.Null(VoiceUnits.CodeOf("شوية"));
    }
}
