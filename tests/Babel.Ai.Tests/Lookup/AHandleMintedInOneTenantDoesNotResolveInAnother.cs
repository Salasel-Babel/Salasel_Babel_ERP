using Babel.Ai.Lookup;
using Babel.Contracts.Lookup;
using Babel.Core.NameRegister;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Lookup;

/// <summary>
/// <b>مِقبضٌ أُصدر في منشأةٍ لا يُفكّ في أخرى — بطبقتين مستقلّتين، وكلتاهما تُجرَّب هنا.</b>
/// <para>
/// وهو نصّ ما يحتجّ به <c>SignedAttachmentTickets</c> في هذا المستودع: «فلو سُرّبت تذكرة
/// كاملة واستُعملت في جلسة مستأجر آخر، سقطت عند المقارنة؛ ولو سقطت المقارنة سهواً، سقط
/// النداء عند المخزن لأن المستأجر جزء من المفتاح هناك. طبقتان مستقلّتان، لا واحدة مكرَّرة.»
/// </para>
/// <para>
/// <b>والسيناريو مُصمَّم ليكون أقسى ما يمكن:</b> المنشأتان تحملان عميلاً <b>بالاسم نفسه</b>،
/// فلو كان الفكّ يجري بالاسم أو كان النطاق يُقرأ من المِقبض نفسه لنجح العبور بلا أثر.
/// </para>
/// </summary>
public sealed class AHandleMintedInOneTenantDoesNotResolveInAnother
{
    private static Guid Company => new("c0000000-0000-4000-8000-000000000003");

    private static Guid Session => new("5e551000-0000-4000-8000-000000000003");

    /// <summary>الطبقة الأولى: الاسترداد يقارن بنطاق <b>الجلسة</b> فيرفض.</summary>
    [Fact]
    public async Task TheHandleIsRefusedWhenRedeemedUnderAnotherTenant()
    {
        await LookupTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);

        TenantId here = LookupTestEnvironment.MintedHereTenant;
        TenantId elsewhere = LookupTestEnvironment.MintedElsewhereTenant;

        // الاسم نفسه في المنشأتين: العبور لو وقع لوجد صفّاً مطابقاً في الجهة الأخرى.
        Guid mine = await LookupTestEnvironment.SeedCustomerAsync(
            here, "شركة المسار الأمثل", cancellationToken: TestContext.Current.CancellationToken);
        Guid theirs = await LookupTestEnvironment.SeedCustomerAsync(
            elsewhere, "شركة المسار الأمثل", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEqual(mine, theirs);

        SignedLookupHandles handles = LookupTestEnvironment.Handles();
        NameRegisterLookup lookup = new([LookupTestEnvironment.Register()], handles, LookupTestEnvironment.Options);

        Result<NameLookupResult> minted = await lookup.ResolveAsync(
            "customer",
            "شركة المسار الأمثل",
            new LookupSession(here, Company, Session),
            TestContext.Current.CancellationToken);

        Assert.True(minted.IsSuccess);
        Assert.Equal(NameLookupOutcome.Resolved, minted.Value.Outcome);

        string token = minted.Value.Handle!;

        // في منشأته: يُفكّ.
        Assert.True(handles.Redeem(token, LookupHandlePurpose.Entity, here, Company, Session).IsSuccess);

        // في منشأةٍ أخرى: يُرفض — والرسالة لا تقول «لمنشأةٍ أخرى»، فذلك بذاته تأكيدُ وجود.
        Result<RedeemedLookupHandle> crossed =
            handles.Redeem(token, LookupHandlePurpose.Entity, elsewhere, Company, Session);

        Assert.True(crossed.IsFailure);
        Assert.Equal("ai.lookup.handle_out_of_scope", crossed.Errors[0].Code);

        // وكذلك شركةٌ أخرى، وجلسةٌ أخرى — الحقول الثلاثة داخل التوقيع.
        Assert.True(handles.Redeem(token, LookupHandlePurpose.Entity, here, Guid.NewGuid(), Session).IsFailure);
        Assert.True(handles.Redeem(token, LookupHandlePurpose.Entity, here, Company, Guid.NewGuid()).IsFailure);
    }

    /// <summary>
    /// الطبقة الثانية، مستقلّة عن الأولى: <b>حتى لو تُخطّيت المقارنة</b>، الصفّ لا يوجد.
    /// السبر بمنشأةٍ أخرى على الاسم نفسه يجد صفّها هي، لا صفّ الأولى.
    /// </summary>
    [Fact]
    public async Task EvenWithoutTheComparisonTheRowIsNotThere()
    {
        await LookupTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);

        TenantId here = LookupTestEnvironment.SecondLayerHereTenant;
        TenantId elsewhere = LookupTestEnvironment.SecondLayerElsewhereTenant;

        Guid mine = await LookupTestEnvironment.SeedCustomerAsync(
            here, "شركة الأفق الواسع", cancellationToken: TestContext.Current.CancellationToken);
        Guid theirs = await LookupTestEnvironment.SeedCustomerAsync(
            elsewhere, "شركة الأفق الواسع", cancellationToken: TestContext.Current.CancellationToken);

        PostgresNameRegister register = LookupTestEnvironment.Register();

        NameCandidateProbe fromHere = await register.ProbeAsync(
            new NameCandidateRequest("شركة الافق الواسع", here, Company), TestContext.Current.CancellationToken);
        NameCandidateProbe fromElsewhere = await register.ProbeAsync(
            new NameCandidateRequest("شركة الافق الواسع", elsewhere, Company), TestContext.Current.CancellationToken);

        Assert.Equal(NameCandidateCardinality.One, fromHere.Cardinality);
        Assert.Equal(NameCandidateCardinality.One, fromElsewhere.Cardinality);

        Assert.Equal(mine, fromHere.Only);
        Assert.Equal(theirs, fromElsewhere.Only);

        // ‏**ولا يبلغ أيٌّ منهما صفّ الآخر** — المنشأة جزء من شرط الاستعلام لا زينةٌ بجانبه.
        Assert.NotEqual(fromElsewhere.Only, fromHere.Only);
    }

    /// <summary>
    /// <b>وطول المِقبض ثابتٌ مهما حمل</b> — فلا يُقاس منه غرضٌ ولا موضوع ولا وجودُ صفّ.
    /// </summary>
    [Fact]
    public void EveryHandleIsTheSameLength()
    {
        SignedLookupHandles handles = LookupTestEnvironment.Handles();
        TenantId tenant = LookupTestEnvironment.HandleLengthTenant;

        List<int> lengths = [];
        foreach (LookupHandlePurpose purpose in (LookupHandlePurpose[])
            [LookupHandlePurpose.Entity, LookupHandlePurpose.Question])
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Result<string> token = handles.Issue(
                    purpose, tenant, Company, Session, Guid.NewGuid(), TimeSpan.FromMinutes(1));

                Assert.True(token.IsSuccess);
                lengths.Add(token.Value.Length);
            }
        }

        Assert.Single(lengths.Distinct());
        Assert.Equal(SignedLookupHandles.TokenLength, lengths[0]);
    }

    /// <summary>مِقبضٌ عُبث ببايتة منه لا يُفكّ — والتوقيع يُتحقَّق قبل قراءة حقلٍ واحد.</summary>
    [Fact]
    public void ATamperedHandleIsRefusedBeforeAnyFieldIsRead()
    {
        SignedLookupHandles handles = LookupTestEnvironment.Handles();
        TenantId tenant = LookupTestEnvironment.TamperedHandleTenant;

        Result<string> token = handles.Issue(
            LookupHandlePurpose.Entity, tenant, Company, Session, Guid.NewGuid(), TimeSpan.FromMinutes(1));

        Assert.True(token.IsSuccess);

        char[] tampered = token.Value.ToCharArray();
        tampered[4] = tampered[4] == 'A' ? 'B' : 'A';

        Result<RedeemedLookupHandle> redeemed = handles.Redeem(
            new string(tampered), LookupHandlePurpose.Entity, tenant, Company, Session);

        Assert.True(redeemed.IsFailure);
        Assert.Equal("ai.lookup.handle_not_signed", redeemed.Errors[0].Code);
    }

    /// <summary>مدّةٌ فوق السقف تُرفض ولا تُقصّ بصمت — والقصّ الصامت يجعل الرفض يبدو قبولاً.</summary>
    [Fact]
    public void AnOverCapLifetimeIsRefusedNotClamped()
    {
        SignedLookupHandles handles = LookupTestEnvironment.Handles();

        Result<string> token = handles.Issue(
            LookupHandlePurpose.Entity,
            LookupTestEnvironment.LifetimeCapTenant,
            Company,
            Session,
            Guid.NewGuid(),
            LookupTestEnvironment.Options.HandleLifetimeCap + TimeSpan.FromSeconds(1));

        Assert.True(token.IsFailure);
        Assert.Equal("ai.lookup.handle_lifetime_refused", token.Errors[0].Code);
    }
}
