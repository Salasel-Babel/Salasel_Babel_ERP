using Babel.Ai.Lookup;
using Babel.Contracts.Lookup;
using Babel.Core.NameRegister;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Lookup;

/// <summary>
/// <b>ثلاثة أجوبة لا رابع لها، على جدول العملاء الحقيقيّ.</b>
/// <para>
/// صفرٌ ⇒ <c>none</c> · واحدٌ بالضبط ⇒ <c>resolved</c> ومعه مِقبض · اثنان فأكثر ⇒
/// <c>needs_question</c>. <b>ولا قاعدة «أفضل تطابق»</b>: مقيس أن «محمد علي القحطاني»
/// و«محمد أحمد القحطاني» يبلغان مع «محمد القحطاني» درجتين مختلفتين، ومع ذلك <b>لا
/// يُختار أعلاهما</b> — يُسأل. واختيار الأعلى بالصدفة يُنفّذ عمليةً لم تُطلَب، وهو نصّ
/// <c>VoiceRefusals.Ambiguous</c> في هذا المستودع.
/// </para>
/// </summary>
public sealed class TheLookupAnswersNoneOneOrAmbiguous
{
    /// <summary>لا مطابق ⇒ <c>none</c>، وبلا مِقبض وبلا معرّف ورقة.</summary>
    [Fact]
    public async Task NothingMatchesSoTheAnswerIsNone()
    {
        await LookupTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = LookupTestEnvironment.NoMatchTenant;

        await LookupTestEnvironment.SeedCustomerAsync(
            tenant, "مؤسسة الغامدي للمقاولات", cancellationToken: TestContext.Current.CancellationToken);

        NameLookupResult answer = await ResolveAsync("شركة المسار الأمثل", tenant);

        Assert.Equal(NameLookupOutcome.None, answer.Outcome);
        Assert.Null(answer.Handle);
        Assert.Null(answer.QuestionId);
    }

    /// <summary>
    /// مطابقٌ واحد ⇒ <c>resolved</c> ومِقبض. والاسم المكتوب يختلف رسماً عن المخزَّن —
    /// «الامثل» بلا همزة، و«شركه» بالهاء — فالطيّ هو ما جعله مطابقاً.
    /// </summary>
    [Fact]
    public async Task ExactlyOneMatchesSoTheAnswerCarriesAHandle()
    {
        await LookupTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = LookupTestEnvironment.SingleMatchTenant;

        Guid expected = await LookupTestEnvironment.SeedCustomerAsync(
            tenant, "شركة المسار الأمثل", cancellationToken: TestContext.Current.CancellationToken);
        await LookupTestEnvironment.SeedCustomerAsync(
            tenant, "مؤسسة الغامدي للمقاولات", cancellationToken: TestContext.Current.CancellationToken);

        NameLookupResult answer = await ResolveAsync("شركه المسار الامثل", tenant);

        Assert.Equal(NameLookupOutcome.Resolved, answer.Outcome);
        Assert.NotNull(answer.Handle);
        Assert.Null(answer.QuestionId);

        // والمِقبض يفكّ إلى الصفّ نفسه داخل الجلسة نفسها — سلطةُ تسميةٍ لا سلطةُ فعل.
        Result<RedeemedLookupHandle> redeemed = LookupTestEnvironment.Handles().Redeem(
            answer.Handle!, LookupHandlePurpose.Entity, tenant, Company, Session);

        Assert.True(redeemed.IsSuccess);
        Assert.Equal(expected, redeemed.Value.Subject);
    }

    /// <summary>اثنان فأكثر ⇒ سؤال، ومعرّف الورقة مِقبضٌ غرضه سؤال لا كِيان.</summary>
    [Fact]
    public async Task TwoOrMoreMatchSoTheAnswerAsks()
    {
        await LookupTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = LookupTestEnvironment.AmbiguousTenant;

        await LookupTestEnvironment.SeedCustomerAsync(
            tenant, "محمد علي القحطاني", cancellationToken: TestContext.Current.CancellationToken);
        await LookupTestEnvironment.SeedCustomerAsync(
            tenant, "محمد أحمد القحطاني", cancellationToken: TestContext.Current.CancellationToken);

        NameLookupResult answer = await ResolveAsync("محمد القحطاني", tenant);

        Assert.Equal(NameLookupOutcome.NeedsQuestion, answer.Outcome);
        Assert.Null(answer.Handle);
        Assert.NotNull(answer.QuestionId);

        // ‏**معرّف الورقة لا يُفكّ كِياناً** — الغرض داخل البايتات الموقَّعة.
        Result<RedeemedLookupHandle> asEntity = LookupTestEnvironment.Handles().Redeem(
            answer.QuestionId!, LookupHandlePurpose.Entity, tenant, Company, Session);

        Assert.True(asEntity.IsFailure);
        Assert.Equal("ai.lookup.handle_purpose_mismatch", asEntity.Errors[0].Code);
    }

    /// <summary>طرفٌ مُوقَف ليس مرشّحاً — والعمود يُمرَّر في الوصف لا يُفترض.</summary>
    [Fact]
    public async Task AnInactivePartyIsNotACandidate()
    {
        await LookupTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = LookupTestEnvironment.InactiveTenant;

        await LookupTestEnvironment.SeedCustomerAsync(
            tenant, "شركة المسار الأمثل", cancellationToken: TestContext.Current.CancellationToken);
        await LookupTestEnvironment.SeedCustomerAsync(
            tenant, "شركة المسار الامثل", isActive: false, cancellationToken: TestContext.Current.CancellationToken);

        NameLookupResult answer = await ResolveAsync("شركة المسار الأمثل", tenant);

        // لولا شرط السريان لكان الجواب سؤالاً — فالصفّان يطويان إلى المفتاح نفسه.
        Assert.Equal(NameLookupOutcome.Resolved, answer.Outcome);
    }

    /// <summary>
    /// <b>ورقة السؤال تُرسم من البيانات المحلّية — ومنفذها منفصلٌ عن منفذ السبر.</b>
    /// المنفذ الذي يُعيد أسماءً اسمه يقول ذلك، والمنفذ الذي يمرّ منه النموذج لا يملك دالّةً تُعيدها.
    /// </summary>
    [Fact]
    public async Task TheSheetSourceReturnsNamesAndTheProbeSourceHasNoWayTo()
    {
        await LookupTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = LookupTestEnvironment.SheetTenant;

        await LookupTestEnvironment.SeedCustomerAsync(
            tenant, "محمد علي القحطاني", cancellationToken: TestContext.Current.CancellationToken);
        await LookupTestEnvironment.SeedCustomerAsync(
            tenant, "محمد أحمد القحطاني", cancellationToken: TestContext.Current.CancellationToken);

        PostgresNameRegister register = LookupTestEnvironment.Register();

        IReadOnlyList<NameCandidate> sheet = await ((INameCandidateSheetSource)register).ListForSheetAsync(
            new NameCandidateRequest("محمد القحطاني", tenant, Company),
            LookupTestEnvironment.Options.QuestionSheetCap,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, sheet.Count);
        Assert.All(sheet, static candidate => Assert.False(string.IsNullOrWhiteSpace(candidate.LabelAr)));

        // والمنفذ الآخر لا يحمل عضواً واحداً يُعيد اسماً.
        Assert.DoesNotContain(
            typeof(INameCandidateSource).GetMethods(),
            static method => method.ReturnType.ToString().Contains("NameCandidate>", StringComparison.Ordinal));
    }

    private static Guid Company => new("c0000000-0000-4000-8000-000000000001");

    private static Guid Session => new("5e551000-0000-4000-8000-000000000001");

    private static async Task<NameLookupResult> ResolveAsync(string text, TenantId tenant)
    {
        NameRegisterLookup lookup = new(
            [LookupTestEnvironment.Register()],
            LookupTestEnvironment.Handles(),
            LookupTestEnvironment.Options);

        Result<NameLookupResult> answer = await lookup.ResolveAsync(
            "customer",
            text,
            new LookupSession(tenant, Company, Session),
            TestContext.Current.CancellationToken);

        Assert.True(answer.IsSuccess, answer.IsFailure ? answer.Errors[0].ToString() : string.Empty);
        return answer.Value;
    }
}
