using Babel.Ai.Lookup;
using Babel.Ai.Tests.Lookup;
using Babel.Ai.Tests.Support;
using Babel.Ai.Voice;
using Babel.Contracts.Lookup;

using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Voice;

/// <summary>
/// <b>العطل بعينه، على قاعدةٍ حقيقية: طرفٌ ثالث لا يُختار، ولا يُخمَّن أصلاً.</b>
/// <para>
/// الجملة تحمل <b>اسمَي منشأةٍ صحيحَي الشكل</b> في موضعٍ واحد: «من العميل <b>شركة النور
/// الأولى للمقاولات</b> لصالح <b>مؤسسة الرياض</b>». وشريحةُ <c>customer</c> تُعلن أربعة
/// دلائل، منها «العميل» و«لصالح» — <b>وكلاهما دليلُها هي</b>. وكان القارئ يطوي دلائل كل
/// الشرائح في مجموعة حدودٍ واحدة فيصير «لصالح» حدّاً يقطع اسمَها، ثم يجرّب المواضع
/// <b>بترتيب إعلان الدلائل</b> ويعود بأوّل ما أنتج كلمات.
/// </para>
/// <para>
/// <b>فكان الجواب صحيحاً بحادثةِ ترتيبٍ في مصفوفة</b> — لا بقاعدة. تُعاد المصفوفة، أو
/// تُضاف طبقةٌ تُعيد النظر في المواضع، فيخرج «مؤسسة الرياض» طرفاً للمستند <b>بلا عطلٍ
/// واحد وبوّابةٌ تقبل</b>. وهذا الإثبات يقيس أنّ القرار لم يعد للقارئ أصلاً.
/// </para>
/// </summary>
public sealed class TheThirdPartyIsNeverChosen
{
    private const string Transcript =
        "سجل سند قبض من العميل شركة النور الاولى للمقاولات لصالح مؤسسة الرياض بمبلغ الف ريال نقد اليوم";

    /// <summary>المقطع كما يخرج من <see cref="SpokenSpans"/> — <b>واحدٌ كاملٌ لا اثنان</b>.</summary>
    private const string WholeSpan = "شركة النور الاولي للمقاولات لصالح مؤسسة الرياض";

    /// <summary>الطرف الذي كانت إعادةُ ترتيب الدلائل تُخرجه — <b>ولا يُخرَج اليوم بحال</b>.</summary>
    private const string TheOtherParty = "مؤسسة الرياض";

    [Fact]
    public void القارئ_يحمل_المقطع_كاملاً_ولا_يقطعه_عند_دليل_شريحته_نفسها()
    {
        VoiceResolution resolution = Read();

        SlotReading.Pending pending = Assert.IsType<SlotReading.Pending>(resolution.Readings["customer"]);

        // ‏**مقطعٌ واحد، لا اثنان يُفاضَل بينهما.**
        Assert.Equal(WholeSpan, pending.Span.Text);
        Assert.Equal("customer", pending.RegisterKey);

        // ‏**والانحدار بعينه، مقيساً بالقيمة**: لا يمتلئ العميل بـ«مؤسسة الرياض» أبداً.
        Assert.DoesNotContain(resolution.Slots, slot => slot.Name == "customer");
        Assert.DoesNotContain(resolution.Readings.Values.OfType<SlotReading.Filled>(),
            filled => filled.Value.Text == TheOtherParty);
        Assert.Empty(resolution.Faults);
    }

    [Fact]
    public void البوابة_ترفض_قبل_السؤال_ولا_تُنتج_أمراً()
    {
        VoiceResolution resolution = Read();

        Result<VoiceDispatch> gate = VoiceConfirmationGate.Authorise(
            resolution, VoiceHarness.Caller, resolution.ConfirmationToken);

        Assert.True(gate.IsFailure);
        Error unresolved = Assert.Single(gate.Errors, error => error.Code == "ai.voice.name_unresolved");
        Assert.Contains("العميل", unresolved.MessageAr, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>والسجلّ — لا القارئ — هو الذي يسمّي الطرف.</b> ويُزرع الطرفان معاً في المنشأة
    /// نفسها، فيُقاس أنّ المقطع الكامل يبلغ <b>صفّ شركة النور</b> و<b>لا يبلغ صفّ مؤسسة
    /// الرياض</b> — والمقطع الآخر، وهو ما كانت إعادةُ الترتيب تُخرجه، يبلغ الصفّ الآخر.
    /// فالمقطعان <b>يميّزهما السجلّ ولا يميّزهما القارئ</b>، وذلك هو التحويل كلّه.
    /// </summary>
    [Fact]
    public async Task السجل_هو_الذي_يميز_المقطعين_لا_القارئ()
    {
        await LookupTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = LookupTestEnvironment.ThirdPartyTenant;

        Guid noor = await LookupTestEnvironment.SeedCustomerAsync(
            tenant, "شركة النور الأولى للمقاولات", cancellationToken: TestContext.Current.CancellationToken);
        Guid riyadh = await LookupTestEnvironment.SeedCustomerAsync(
            tenant, "مؤسسة الرياض", cancellationToken: TestContext.Current.CancellationToken);

        Core.NameRegister.PostgresNameRegister register = LookupTestEnvironment.Register();

        NameCandidateProbe whole = await register.ProbeAsync(
            new NameCandidateRequest(WholeSpan, tenant, Guid.Empty), TestContext.Current.CancellationToken);

        Assert.Equal(NameCandidateCardinality.One, whole.Cardinality);
        Assert.Equal(noor, whole.Only);
        Assert.NotEqual(riyadh, whole.Only);

        // والمقطع الذي كان الترتيب يُخرجه يبلغ الصفّ الآخر — فالفرق حقيقيّ ومقيس.
        NameCandidateProbe other = await register.ProbeAsync(
            new NameCandidateRequest(TheOtherParty, tenant, Guid.Empty), TestContext.Current.CancellationToken);

        Assert.Equal(NameCandidateCardinality.One, other.Cardinality);
        Assert.Equal(riyadh, other.Only);
    }

    /// <summary>
    /// وبعد السؤال: مِقبضٌ يمرّ، <b>ولا نصَّ لطرفٍ في الأمر المُصرَّح به</b>.
    /// </summary>
    [Fact]
    public async Task بعد_السؤال_يمر_مقبض_ولا_يمر_اسم()
    {
        await LookupTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = LookupTestEnvironment.ThirdPartyAnsweredTenant;

        await LookupTestEnvironment.SeedCustomerAsync(
            tenant, "شركة النور الأولى للمقاولات", cancellationToken: TestContext.Current.CancellationToken);

        NameRegisterLookup lookup = new(
            [LookupTestEnvironment.Register()],
            LookupTestEnvironment.Handles(),
            LookupTestEnvironment.Options);

        Result<VoiceResolution> answered = await SpokenNameResolver.ResolveAsync(
            Read(),
            lookup,
            new LookupSession(tenant, Guid.Empty, Guid.CreateVersion7()),
            TestContext.Current.CancellationToken);

        Assert.True(answered.IsSuccess);

        SlotReading.Resolved resolved = Assert.IsType<SlotReading.Resolved>(answered.Value.Readings["customer"]);
        Assert.Equal(SignedLookupHandles.TokenLength, resolved.Handle.Length);

        Result<VoiceDispatch> gate = VoiceConfirmationGate.Authorise(
            answered.Value, VoiceHarness.Caller, answered.Value.ConfirmationToken);

        Assert.True(gate.IsSuccess);

        ResolvedSlotValue customer = Assert.Single(gate.Value.Slots, slot => slot.Name == "customer");
        Assert.True(customer.IsEntity);
        Assert.Null(customer.Text);
        Assert.Equal(resolved.Handle, customer.Handle);

        // ‏**ولا اسمَ طرفٍ في الأمر كلّه** — لا «مؤسسة الرياض» ولا «شركة النور».
        Assert.DoesNotContain(gate.Value.Slots, slot => slot.Text is not null && slot.Text.Contains("مؤسسة", StringComparison.Ordinal));
        Assert.DoesNotContain(gate.Value.Slots, slot => slot.Text is not null && slot.Text.Contains("شركة", StringComparison.Ordinal));
    }

    private static VoiceResolution Read()
    {
        Result<VoiceResolution> read = SpokenCommandReader.Read(
            Transcript, VoiceHarness.Registry, VoiceHarness.Options);

        Assert.True(read.IsSuccess);
        return read.Value;
    }
}
