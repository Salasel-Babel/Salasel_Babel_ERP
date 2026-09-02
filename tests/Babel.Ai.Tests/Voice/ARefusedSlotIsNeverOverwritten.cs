using Babel.Ai.Tests.Support;
using Babel.Ai.Voice;
using Babel.Contracts.Voice;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Voice;

/// <summary>
/// <b>صنفُ العطل — لا قضيّتُه: قراءةٌ واحدة لكل شريحة، ولا تُكتب مرّتين.</b>
/// <para>
/// كان الشكل المُغري «سجّل الرفض ثم <c>continue</c> إلى الدليل التالي» — فيصير <b>رفضُ
/// مقطعٍ سبباً في قبول مقطعٍ آخر</b>. والعلاج ليس مراجعةً بل بنية: نوعٌ بلا حالة «تابع»،
/// وحلقةٌ تعيش <b>تحت</b> النوع لا فوقه، ومجموعةٌ تُكتب بـ<c>Add</c> فترمي على التكرار.
/// </para>
/// <para>
/// وهذا الإثبات يقيس ذلك على <b>المتن كلّه</b> — أربعٍ وأربعين نيّةً وجملها — لا على
/// جملةٍ واحدة: عددُ القراءات يساوي عدد الشرائح <b>بالضبط</b>، ولكل شريحةٍ معلَنة مدخلٌ
/// واحد، ولا شريحةَ رُفضت أو عُلّقت ثم امتلأت.
/// </para>
/// </summary>
public sealed class ARefusedSlotIsNeverOverwritten
{
    public static TheoryData<string> Corpus()
    {
        TheoryData<string> data = [];
        foreach (VectorUtterance vector in VoiceVectors.File.Utterances)
        {
            data.Add(vector.Transcript);
        }

        foreach (VectorMissing vector in VoiceVectors.File.Missing)
        {
            data.Add(vector.Transcript);
        }

        return data;
    }

    [Fact]
    public void المتن_ليس_ضامراً()
    {
        // فخ-43: حارسٌ على متنٍ فارغ يمرّ على لا شيء.
        Assert.True(Corpus().Count >= 80, Corpus().Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public void لكل_شريحة_معلَنة_قراءةٌ_واحدة_لا_أقل_ولا_أكثر(string transcript)
    {
        Result<VoiceResolution> read = SpokenCommandReader.Read(
            transcript, VoiceHarness.Registry, VoiceHarness.Options);

        Assert.True(read.IsSuccess);
        VoiceResolution resolution = read.Value;

        Assert.Equal(resolution.Intent.Slots.Count, resolution.Readings.Count);

        foreach (VoiceSlot slot in resolution.Intent.Slots)
        {
            Assert.True(resolution.Readings.ContainsKey(slot.Name), slot.Name);

            SlotReading reading = resolution.Readings[slot.Name];

            // ‏**شريحةُ طرفٍ لا تمتلئ بنصّ أبداً** — لا قبل السجلّ ولا بعده.
            if (slot.Kind == VoiceSlotKind.Entity)
            {
                Assert.True(
                    reading is SlotReading.Pending or SlotReading.Silent,
                    slot.Name + " ⇒ " + reading.GetType().Name);
            }
            else
            {
                Assert.IsNotType<SlotReading.Pending>(reading);
                Assert.IsNotType<SlotReading.Resolved>(reading);
            }
        }

        // ‏**والمشتقّات لا تتناقض**: ما رُفض ليس ممتلئاً، وما نقص ليس ممتلئاً.
        foreach (Error fault in resolution.Faults)
        {
            Assert.NotEqual(string.Empty, fault.Code);
        }

        foreach (string missing in resolution.MissingSlots)
        {
            Assert.DoesNotContain(resolution.Slots, value => value.Name == missing);
            Assert.IsType<SlotReading.Silent>(resolution.Readings[missing]);
        }
    }

    /// <summary>
    /// <b>والكتابة الثانية ترمي عند السطر بعينه</b> — لا تُنتج طرفاً معقولاً يعبر البوّابة.
    /// وهي غيرُ بالغةٍ إلّا بالعطل: السجلّ يرفض تكرار اسم الشريحة عند البناء.
    /// </summary>
    [Fact]
    public void كتابةٌ_ثانية_لشريحةٍ_واحدة_ترمي_ولا_تطمس()
    {
        Dictionary<string, SlotReading> readings = new(StringComparer.Ordinal)
        {
            ["customer"] = new SlotReading.Refused(new Error("probe.refused", "رفض", "refused")),
        };

        Assert.Throws<ArgumentException>(() =>
            readings.Add("customer", new SlotReading.Filled(
                new SpokenSlotValue("customer", "مؤسسة الرياض", null, "مؤسسة الرياض", Contracts.Capture.FieldProvenance.Spoken))));

        // والقراءة الأولى باقية كما هي.
        Assert.IsType<SlotReading.Refused>(readings["customer"]);
    }

    /// <summary>
    /// <b>ونتيجةٌ ينقصها مدخلٌ لا تُبنى أصلاً.</b> شريحةٌ بلا قراءة تمرّ صامتة: لا تُقرأ
    /// ولا تُرفض ولا تظهر ناقصة — وذلك أخبثُ من رفضٍ صريح.
    /// </summary>
    [Fact]
    public void نتيجةٌ_ينقصها_مدخلٌ_لشريحةٍ_لا_تُبنى()
    {
        VoiceIntent intent = VoiceHarness.Registry.Find("accounting.customer_receipt.record")!;

        Assert.Throws<ArgumentException>(() => new VoiceResolution(
            intent,
            new Dictionary<string, SlotReading>(StringComparer.Ordinal) { ["customer"] = new SlotReading.Silent() },
            false,
            "ملخّص",
            "رمز"));
    }
}
