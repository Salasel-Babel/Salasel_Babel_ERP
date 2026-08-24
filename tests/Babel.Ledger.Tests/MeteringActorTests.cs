using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Core.Entitlement;
using Babel.Core.Metering;
using Babel.Ledger.Posting;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ledger.Tests;

/// <summary>
/// <b>محور الفوترة «مستخدم فاعل في الفترة» يقيس المستخدم الحقيقي لا فاعل النظام.</b>
/// <para>
/// المالك اختار «المستخدم الفاعل خلال الفترة» محوراً للتسعير. وبوابة الترحيل كانت تُمرّر
/// <see cref="UserId.SystemActor"/> إلى منفِّذ الاستحقاق مكان <c>request.Actor</c>، فيُسجّل
/// <c>RecordUserActivityAsync</c> المعرّف الاصطناعي نفسه عن كل ترحيل في النظام. والعطل
/// <b>صامت تماماً</b>: الترحيل ينجح، والقيد متوازن، والسلسلة سليمة، ولا رسالة واحدة —
/// ثم يقرأ تقرير «المستخدمون الفاعلون» <b>مستخدماً واحداً</b> مهما عمل من الناس، ويظهر
/// الخطأ أول مرّة على فاتورة اشتراك.
/// </para>
/// <para>
/// و<c>ReverseAsync</c> كان يُمرّر <c>request.Actor</c> على السطر المقابل تماماً — فالمساران
/// كانا يختلفان، وهو ما يجعل الخطأ خطأً لا اختياراً.
/// </para>
/// </summary>
[Collection("ledger")]
public sealed class MeteringActorTests : IAsyncLifetime
{
    /// <summary>دفتر مستقل: العدّاد والسلسلة بنطاق (شركة × دفتر × سنة).</summary>
    private const string Book = "METERING";

    private static readonly UserId Sara = new(new Guid("5a5a5a5a-0000-4000-8000-000000000001"));
    private static readonly UserId Khalid = new(new Guid("cbcbcbcb-0000-4000-8000-000000000002"));

    private LedgerHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await LedgerHarness.CreateAsync(TestContext.Current.CancellationToken);
        await LedgerTestEnvironment.EnsureCounterAsync(
            LedgerTestEnvironment.TenantA, Book, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · شخصان يُرحّلان ⇒ محور المستخدم يحمل شخصين
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task ترحيلان_من_شخصين_مختلفين_يُقاسان_فاعلَين_مختلفين_لا_فاعل_نظام_واحداً()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        InMemoryUsageStore usage = new();
        PostingService posting = new(
            new EntitlementEnforcer(new AlwaysEntitledService(), usage, TimeProvider.System),
            _harness.Runtime);

        Result<PostingReceipt> first = await posting.PostAsync(Entry(Sara, "SARA"), token);
        Result<PostingReceipt> second = await posting.PostAsync(Entry(Khalid, "KHALID"), token);

        Proof.Require(first.IsSuccess, "ترحيل الشخص الأول نجح", Describe(first));
        Proof.Require(second.IsSuccess, "ترحيل الشخص الثاني نجح", Describe(second));

        IReadOnlyCollection<UserId> active = await usage.GetActiveUsersAsync(
            new TenantId(LedgerTestEnvironment.TenantA),
            BillingPeriod.FromInstant(DateTimeOffset.UtcNow),
            token);

        string names = string.Join(" · ", active.Select(static user => user.ToString()));

        Proof.Require(
            active.Contains(Sara) && active.Contains(Khalid),
            "محور «المستخدم الفاعل» يحمل الشخصين معاً",
            $"الفاعلون المقيسون: {names}");

        Proof.Require(
            !active.Contains(UserId.SystemActor),
            "فاعل النظام لا يظهر على محور الفوترة عن ترحيل بدأه إنسان",
            $"الفاعلون المقيسون: {names}");

        Assert.Contains(Sara, active);
        Assert.Contains(Khalid, active);
        Assert.DoesNotContain(UserId.SystemActor, active);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · القرار الاستحقاقي نفسه لم يتغيّر — تغيّر **من يُسجَّل** لا **ما يُسمح به**
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task تغيير_الفاعل_المقيس_لا_يغيّر_قرار_الاستحقاق_نفسه()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        // وحدة مقروءة فقط: الاشتراك انقضى. الترحيل مرفوض — بأي فاعل كان.
        InMemoryUsageStore usage = new();
        PostingService posting = new(
            new EntitlementEnforcer(new ReadOnlyService(), usage, TimeProvider.System),
            _harness.Runtime);

        Result<PostingReceipt> byPerson = await posting.PostAsync(Entry(Sara, "RO-SARA"), token);
        Result<PostingReceipt> bySystem = await posting.PostAsync(
            Entry(UserId.SystemActor, "RO-SYSTEM"), token);

        Proof.Require(
            byPerson.IsFailure && byPerson.Errors[0].Code == "entitlement.read_only",
            "الوحدة المقروءة فقط ترفض ترحيل الإنسان",
            Describe(byPerson));

        Proof.Require(
            bySystem.IsFailure && bySystem.Errors[0].Code == "entitlement.read_only",
            "والوحدة نفسها ترفض ترحيل فاعل النظام بالرمز نفسه — القرار لا يقرأ الفاعل",
            Describe(bySystem));

        Assert.Equal(byPerson.Errors[0].Code, bySystem.Errors[0].Code);

        // ولا نشاط مستخدم يُسجَّل عن استدعاء مرفوض: القياس بعد السماح لا قبله.
        IReadOnlyCollection<UserId> active = await usage.GetActiveUsersAsync(
            new TenantId(LedgerTestEnvironment.TenantA),
            BillingPeriod.FromInstant(DateTimeOffset.UtcNow),
            token);

        Proof.Require(
            active.Count == 0,
            "الاستدعاء المرفوض لا يُقاس على محور المستخدم",
            FormattableString.Invariant($"عدد الفاعلين المقيسين: {active.Count}"));

        Assert.Empty(active);
    }

    private static PostingRequest Entry(UserId actor, string tag) => Requests
        .RentInvoice(
            LedgerTestEnvironment.TenantA,
            tag + "-" + Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture)[..8],
            1_000.0000m,
            150.0000m,
            new DateOnly(2026, 3, 10)) with
    {
        Book = Book,
        Actor = actor,
    };

    private static string Describe<T>(Result<T> result) => result.IsSuccess
        ? "نجح"
        : string.Join(" | ", result.Errors.Select(static e => e.Code + ": " + e.MessageAr));

    /// <summary>استحقاق كامل لكل وحدة — الاستحقاق نفسه مُختبَر في <c>Babel.Core.Tests</c>.</summary>
    private sealed class AlwaysEntitledService : IEntitlementService
    {
        public ValueTask<EntitlementSet> GetAsync(TenantId tenant, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("لا يُستعمل في هذا الاختبار.");

        public ValueTask<EntitlementState> GetStateAsync(
            TenantId tenant, BabelModule module, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(EntitlementState.Entitled);

        public ValueTask<Result<EntitlementSet>> ApplyAsync(
            EntitlementChangeRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("لا يُستعمل في هذا الاختبار.");
    }

    /// <summary>اشتراك منقضٍ: قراءة كاملة ولا كتابة.</summary>
    private sealed class ReadOnlyService : IEntitlementService
    {
        public ValueTask<EntitlementSet> GetAsync(TenantId tenant, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("لا يُستعمل في هذا الاختبار.");

        public ValueTask<EntitlementState> GetStateAsync(
            TenantId tenant, BabelModule module, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(EntitlementState.ReadOnly);

        public ValueTask<Result<EntitlementSet>> ApplyAsync(
            EntitlementChangeRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("لا يُستعمل في هذا الاختبار.");
    }
}
