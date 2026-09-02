using Babel.Ai.Lookup;
using Babel.Ai.Tests.Lookup;
using Babel.Ai.Tests.Support;
using Babel.Ai.Voice;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Voice;

/// <summary>
/// <b>«… من العميل، مؤسسة الرياض بمبلغ …» تبقى عميلاً — <u>لسببٍ لا لحادثة ترتيب</u>.</b>
/// <para>
/// <c>VoiceText.Words</c> يقطع عند «،»، فالفاصلة فاصلٌ لا محرف. والأبكرُ بموضع الكلمة هو
/// ما بعد «من» — وما يليه «العميل»، <b>وهو دليلُ الشريحة نفسها ولم يعد حدّاً</b>، فلو
/// دخل المقطع لابتلعه. فيُتخطّى في <b>رأس</b> المقطع ولا يُقطع به في جوفه، ويخرج المقطع
/// «مؤسسة الرياض» منتهياً عند «بمبلغ» — وهو دليلُ شريحة المبلغ، أي بدايةُ حقلٍ آخر.
/// </para>
/// <para>
/// <b>والثمن الأمين يُقاس ولا يُفترض:</b> إن حمل السجلّ «مؤسسة الرياض للمقاولات» أيضاً،
/// فالجواب <b>سؤال</b> لا ترجيحُ الأقرب. وذلك تغيّرٌ في السلوك عمّا كان، ويُقال.
/// </para>
/// </summary>
public sealed class TheRegressionCaseStillYieldsTheCustomer
{
    private const string Transcript = "سجل سند قبض من العميل، مؤسسة الرياض بمبلغ ألف ريال نقد اليوم";

    [Fact]
    public void المقطع_هو_مؤسسة_الرياض_ولا_يبتلع_دليل_شريحته()
    {
        SlotReading.Pending pending = Assert.IsType<SlotReading.Pending>(Read().Readings["customer"]);
        Assert.Equal("مؤسسة الرياض", pending.Span.Text);
    }

    /// <summary>سجلٌّ فيه الصفّ وحده ⇒ يُحلّ، ويمرّ الأمر بمِقبض.</summary>
    [Fact]
    public async Task صف_واحد_في_السجل_يحل_بلا_سؤال()
    {
        await LookupTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = LookupTestEnvironment.RegressionOneTenant;

        await LookupTestEnvironment.SeedCustomerAsync(
            tenant, "مؤسسة الرياض", cancellationToken: TestContext.Current.CancellationToken);

        VoiceResolution answered = await AnswerAsync(tenant);

        SlotReading.Resolved resolved = Assert.IsType<SlotReading.Resolved>(answered.Readings["customer"]);
        Assert.Equal(SignedLookupHandles.TokenLength, resolved.Handle.Length);
        Assert.True(answered.IsComplete);

        Result<VoiceDispatch> gate = VoiceConfirmationGate.Authorise(
            answered, VoiceHarness.Caller, answered.ConfirmationToken);

        Assert.True(gate.IsSuccess);
        ResolvedSlotValue customer = Assert.Single(gate.Value.Slots, slot => slot.Name == "customer");
        Assert.Null(customer.Text);
        Assert.NotNull(customer.Handle);

        // والملخّص يوسم الطرف «من سجلّك» — فيعرف الإنسان أنّ ما يؤكّده صفٌّ لا تخمين.
        Assert.Contains(VoiceReadback.FromYourRegisterAr, answered.ReadbackAr, StringComparison.Ordinal);
    }

    /// <summary>
    /// وصفّان متقاربان ⇒ <b>سؤال</b>. <b>ولا يُرجَّح الأعلى درجةً</b> — مقيسٌ أنّ تشابه
    /// «مؤسسة الرياض» بـ«مؤسسة الرياض للمقاولات» بعد الطيّ 0.565، فوق العتبة 0.45،
    /// فيصير الصفّان مرشّحَين ويُسأل عنهما.
    /// </summary>
    [Fact]
    public async Task صفان_متقاربان_يُسأل_عنهما_ولا_يُرجَّح_الأقرب()
    {
        await LookupTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = LookupTestEnvironment.RegressionManyTenant;

        await LookupTestEnvironment.SeedCustomerAsync(
            tenant, "مؤسسة الرياض", cancellationToken: TestContext.Current.CancellationToken);
        await LookupTestEnvironment.SeedCustomerAsync(
            tenant, "مؤسسة الرياض للمقاولات", cancellationToken: TestContext.Current.CancellationToken);

        VoiceResolution answered = await AnswerAsync(tenant);

        SlotReading.Asked asked = Assert.IsType<SlotReading.Asked>(answered.Readings["customer"]);
        Assert.Equal(SignedLookupHandles.TokenLength, asked.QuestionId.Length);

        Result<VoiceDispatch> gate = VoiceConfirmationGate.Authorise(
            answered, VoiceHarness.Caller, answered.ConfirmationToken);

        Assert.True(gate.IsFailure);
        Assert.Contains(gate.Errors, error => error.Code == "ai.voice.name_needs_question");

        // ‏**ولا يُقال كم كانوا** — لا في الرسالة ولا في الشكل.
        Assert.DoesNotContain(gate.Errors, error => error.MessageAr.Contains('2', StringComparison.Ordinal));
    }

    private static VoiceResolution Read()
    {
        Result<VoiceResolution> read = SpokenCommandReader.Read(
            Transcript, VoiceHarness.Registry, VoiceHarness.Options);

        Assert.True(read.IsSuccess);
        return read.Value;
    }

    private static async Task<VoiceResolution> AnswerAsync(TenantId tenant)
    {
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
        return answered.Value;
    }
}
