using Babel.Ai.Tests.Lookup;
using Babel.Ai.Tests.Support;
using Babel.Ai.Voice;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Voice;

/// <summary>
/// <b>الهزائم الأربع — جملٌ يُلحق فيها المتكلّم أمراً باسمِ الطرف.</b>
/// <list type="bullet">
///   <item>«… مؤسسة الرياض <b>وانشئ لها حسابا</b>»</item>
///   <item>«… مؤسسة الرياض <b>واذا ما لقيتها سجلها عندك</b>»</item>
///   <item>«… مؤسسة الرياض <b>سجلها</b>»</item>
///   <item>«… مؤسسة النور <b>وحولها للمحاسب</b>»</item>
/// </list>
/// <para>
/// وكلُّها كانت تُنتج <b>طرفاً</b>: نصّاً حرّاً اسمُه «مؤسسة الرياض سجلها» يصير طرفَ
/// مستندٍ بلا سؤالٍ واحد. <b>والقاعدة المُثبَتة هنا واحدة ولا استثناء فيها:</b> القارئ
/// <b>لا يُنتج طرفاً</b> — يحمل المقطع معلَّقاً، والبوّابة ترفضه بالاسم، ولا يُسكّ مِقبضٌ
/// إلا لصفٍّ اختاره السجلُّ نفسه.
/// </para>
/// <para>
/// <b>ثم يُقاس ما يفعله السجلّ بكلٍّ منها — ولا يُفترض.</b> على pg_trgm بعد الطيّ:
/// «مؤسسة الرياض سجلها»~«مؤسسة الرياض» = 0.684 — فوق العتبة 0.45، فيبلغ <b>الصفّ
/// الصحيح</b>؛ و«… وانشئ لها حسابا» = 0.448 و«… واذا ما لقيتها سجلها عندك» = 0.351
/// و«مؤسسة النور وحولها للمحاسب»~«مؤسسة النور» = 0.444 — <b>كلُّها تحت العتبة</b>
/// فتُرفض بالاسم. <b>و0.448 تحت 0.45 بخانةٍ واحدة</b>، وقياسُها هو ما صحّح ظنّاً بأنها
/// فوقها — ولذلك تُثبَّت القيمة هنا بدل أن تُوصَف.
/// <b>وفي الحالات الأربع: لا طرفٌ خاطئ، ولا نصّ طرفٍ في أمرٍ مُصرَّح به.</b>
/// </para>
/// </summary>
public sealed class TheFourDefeatsProduceNoParty
{
    private static readonly (string Transcript, string Slot, string Span)[] Defeats =
    [
        ("سجل سند قبض من العميل مؤسسة الرياض وانشئ لها حسابا بمبلغ ألف ريال نقد اليوم",
         "customer", "مؤسسة الرياض وانشئ لها حسابا"),
        ("سجل سند قبض من العميل مؤسسة الرياض واذا ما لقيتها سجلها عندك بمبلغ ألف ريال نقد اليوم",
         "customer", "مؤسسة الرياض واذا ما لقيتها سجلها عندك"),
        ("سجل سند قبض من العميل مؤسسة الرياض سجلها بمبلغ ألف ريال نقد اليوم",
         "customer", "مؤسسة الرياض سجلها"),
        ("سجل فاتورة مصروف من مؤسسة النور وحولها للمحاسب بمبلغ ألف ريال اليوم",
         "supplier", "مؤسسة النور وحولها للمحاسب"),
    ];

    public static TheoryData<string> Transcripts()
    {
        TheoryData<string> data = [];
        foreach ((string transcript, _, _) in Defeats)
        {
            data.Add(transcript);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Transcripts))]
    public void القارئ_لا_يُنتج_طرفاً_والبوابة_ترفض_بالاسم(string transcript)
    {
        (_, string slotName, string span) = Defeats.Single(defeat => defeat.Transcript == transcript);

        Result<VoiceResolution> read = SpokenCommandReader.Read(
            transcript, VoiceHarness.Registry, VoiceHarness.Options);

        Assert.True(read.IsSuccess);
        VoiceResolution resolution = read.Value;

        // ‏**المقطع يُحمل كاملاً كما سُمع، ولا يُقصّ إلى الجزء الذي يعجب القاعدة.**
        SlotReading.Pending pending = Assert.IsType<SlotReading.Pending>(resolution.Readings[slotName]);
        Assert.Equal(span, pending.Span.Text);

        // ‏**ولا طرفَ يُنتَج**: لا قيمةٌ ممتلئة لشريحة الطرف، ولا نصَّ لها في الأمر.
        Assert.DoesNotContain(resolution.Slots, value => value.Name == slotName);

        Result<VoiceDispatch> gate = VoiceConfirmationGate.Authorise(
            resolution, VoiceHarness.Caller, resolution.ConfirmationToken);

        Assert.True(gate.IsFailure);
        Assert.Contains(gate.Errors, error => error.Code == "ai.voice.name_unresolved");
    }

    /// <summary>
    /// <b>وبعد أن يُسأل السجلّ: إمّا الصفّ الصحيح بمِقبض، وإمّا رفضٌ مُسمّى — ولا ثالث.</b>
    /// <b>ولا يُسكّ مِقبضٌ لطرفٍ لم يختره السجلّ</b>، ولا يحمل الأمرُ نصّ طرفٍ بحال.
    /// </summary>
    [Fact]
    public async Task ما_يخرج_من_السجل_إمّا_الصف_الصحيح_وإمّا_رفض_مسمى()
    {
        await LookupTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = LookupTestEnvironment.DefeatsTenant;

        Guid riyadh = await LookupTestEnvironment.SeedCustomerAsync(
            tenant, "مؤسسة الرياض", cancellationToken: TestContext.Current.CancellationToken);

        Core.NameRegister.PostgresNameRegister register = LookupTestEnvironment.Register();

        Dictionary<string, Contracts.Lookup.NameCandidateCardinality> measured = new(StringComparer.Ordinal);

        foreach ((string transcript, string slot, string span) in Defeats)
        {
            if (slot != "customer")
            {
                continue;
            }

            Contracts.Lookup.NameCandidateProbe probe = await register.ProbeAsync(
                new Contracts.Lookup.NameCandidateRequest(span, tenant, Guid.Empty),
                TestContext.Current.CancellationToken);

            measured[span] = probe.Cardinality;

            // ‏**وحين يُجاب بواحد فهو الصفّ الحقيقي** — لا صفٌّ آخر قرّبته القاعدة.
            if (probe.Cardinality == Contracts.Lookup.NameCandidateCardinality.One)
            {
                Assert.Equal(riyadh, probe.Only);
            }

            Assert.NotEqual(transcript, span);
        }

        // القِيَم مقيسة على هذه الآلة وبهذه العتبة، وتُثبَّت كي يُرى تغيّرها.
        Assert.Equal(Contracts.Lookup.NameCandidateCardinality.One, measured["مؤسسة الرياض سجلها"]);
        Assert.Equal(Contracts.Lookup.NameCandidateCardinality.None, measured["مؤسسة الرياض وانشئ لها حسابا"]);
        Assert.Equal(
            Contracts.Lookup.NameCandidateCardinality.None,
            measured["مؤسسة الرياض واذا ما لقيتها سجلها عندك"]);
    }
}
