using Babel.Compliance.Abstractions;
using Babel.Compliance.FakeProvider;
using Babel.Compliance.Model;
using Babel.Compliance.Pipeline;
using Babel.Compliance.Store;
using Babel.Compliance.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Babel.Compliance.Tests;

/// <summary>
/// اختبارات نسخة الإنتاج من المخزن: PostgreSQL حقيقي، وعدّاد بقفل صف، ومعاملة واحدة.
/// <para/>
/// <b>تُتخطّى تلقائياً</b> ما لم تُضبط <c>BABEL_COMPLIANCE_TEST_DB</c> — كي تبقى المجموعة
/// كلها قابلة للتشغيل بلا قاعدة بيانات وبلا اعتمادات. لا كلمة مرور في هذا المستودع.
/// <code>
/// export BABEL_COMPLIANCE_TEST_DB="Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres"
/// </code>
/// <para>
/// <b>والمتغيّر اتصال صيانة لا اسم قاعدة اختبار.</b> أسماء قواعد الاختبار تُشتقّ في
/// <see cref="TestDatabases"/> بلاحقةٍ خاصّة بكل عملية، فلا يثبّتها إعدادٌ منشور —
/// متغيّرٌ يحمل اسماً ثابتاً يُبطل اللاحقة بصمت ويعيد العطل كاملاً بينما الشيفرة
/// تبدو مُصلَحة (‏<c>docs/evidence/traps.md#fakh-test-databases-share-a-fixed-name-across-processes</c>).
/// </para>
/// </summary>
public class RelationalStoreTests
{
    private static string? Admin => Environment.GetEnvironmentVariable("BABEL_COMPLIANCE_TEST_DB");

    /// <summary>
    /// ينشئ قاعدةً <b>باسم خاصّ بهذه العملية</b>، وينشر مخطّط الالتزام فيها، ويُرجع مخزناً عليها.
    /// <para>
    /// <b>ما كان هنا قبلاً:</b> <c>drop database if exists {db} with (force)</c> على اسم
    /// <b>ثابت</b>. و<c>with (force)</c> يقطع جلسات <b>عمليات أخرى</b>، فعمليتان
    /// متزامنتان تُدمّر كلٌّ منهما تشغيل الأخرى. الاسم صار خاصّاً بالعملية في
    /// <see cref="TestDatabases"/>، و<b>لا إسقاط عند البدء</b> إطلاقاً.
    /// </para>
    /// </summary>
    private static async Task<EfComplianceStore> FreshStoreAsync(string db, TimeProvider clock, CancellationToken ct)
    {
        var admin = Admin!;
        var target = await TestDatabases.CreateAsync(admin, db, ct);
        await using (var conn = new NpgsqlConnection(target))
        {
            await conn.OpenAsync(ct);
            await using var ddl = new NpgsqlCommand(ComplianceSchema.CreateSql(), conn);
            await ddl.ExecuteNonQueryAsync(ct);
        }

        var options = new DbContextOptionsBuilder<ComplianceDbContext>().UseNpgsql(target).Options;
        return new EfComplianceStore(new PooledFactory(options), clock);
    }

    private sealed class PooledFactory(DbContextOptions<ComplianceDbContext> options)
        : IDbContextFactory<ComplianceDbContext>
    {
        public ComplianceDbContext CreateDbContext() => new(options);
    }

    [Fact]
    public async Task The_relational_store_runs_the_whole_pipeline_with_a_gapless_row_locked_counter()
    {
        if (Admin is null) return;   // بلا قاعدة بيانات: يُتخطّى بصمت
        var ct = TestContext.Current.CancellationToken;

        var clock = new ManualClock(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
        var db = TestDatabases.Pipeline;
        var store = await FreshStoreAsync(db, clock, ct);

        var authority = new FakeAuthority();
        using var provider = new FakeComplianceProvider(
            KeyCustody.SelfHeld, authority, clock, StatusProbeSupport.ByDocumentIdentity);
        var registry = new InMemoryIssuingUnitRegistry();
        var settings = new ComplianceSettings();
        var renderer = new Babel.Compliance.Canonical.ProvisionalDocumentRenderer();
        var factory = new ComplianceDocumentFactory(store, renderer, provider, registry, clock);
        var clearance = new ClearanceCoordinator(store, provider, registry, settings, clock);
        var reporting = new ReportingWorker(store, provider, registry, settings, clock);
        var service = new ComplianceService(factory, clearance, reporting, store, settings, clock);

        var csr = await provider.Onboarding.CreateSigningRequestAsync(
            new CsrRequest(Harness.Tenant, Harness.Unit, ComplianceEnvironment.Simulation,
                new CsrSubject("cn", "org", "ou", "SA", new Dictionary<string, string> { ["2.5.4.5"] = "1" }, "tpl")), ct);
        var grant = await provider.Onboarding.RequestProductionCertificateAsync(csr.Credential, ct);
        await registry.UpsertAsync(new IssuingUnitRegistration
        {
            Tenant = Harness.Tenant, IssuingUnit = Harness.Unit,
            Environment = ComplianceEnvironment.Simulation,
            DisplayNameAr = "نقطة بيع", DisplayNameEn = "POS",
            Credential = grant.Credential, Stage = OnboardingStage.Active
        }, ct);

        var ledger = new FakeLedger();
        ComplianceDocument Doc(string n, decimal net)
        {
            var tax = decimal.Round(net * 0.15m, 4);
            var entry = ledger.Post(Harness.Tenant, Harness.Unit, n, net, tax, net + tax, clock.GetUtcNow());
            return new ComplianceDocument(
                ComplianceDocumentId.New(), Guid.CreateVersion7(), Harness.Tenant, Harness.Unit,
                ComplianceDocumentKind.Invoice, ComplianceFlow.Clearance, n, clock.GetUtcNow(), "SAR",
                new PartyRef("بائع", "Seller", "300000000000003"),
                new PartyRef("مشترٍ", "Buyer", "310000000000003"),
                [new DocumentLine(1, "بند", "line", 1m, net, net, 0.15m, tax, net + tax)],
                new DocumentTotals(net, tax, net + tax), entry);
        }

        // خمسة مستندات، أحدها مرفوض — والعدّاد يجب أن يبقى بلا فجوات.
        for (var i = 1; i <= 5; i++)
        {
            var d = Doc($"INV-PG-{i}", 100.0000m * i);
            if (i == 3) authority.Script(d.DocumentUuid, FakeBehaviour.Reject);
            var r = await service.ClearAsync(d, ct);
            Assert.Equal(i == 3 ? ComplianceStatus.Rejected : ComplianceStatus.Accepted, r.Status);
        }

        var rows = await store.InTransactionAsync(
            (uow, t) => uow.ListAsync(new ComplianceQuery(Harness.Tenant, Limit: 100), t), ct);
        Assert.Equal(5, rows.Count);
        Assert.Equal(Enumerable.Range(1, 5).Select(i => (long)i), rows.Select(r => r.Counter));

        var head = await store.InTransactionAsync(
            (uow, t) => uow.GetChainHeadAsync(Harness.Tenant, Harness.Unit, t), ct);
        Assert.Equal(6, head!.NextCounter);

        // سلسلة سليمة عبر الصفوف المخزَّنة فعلاً في PostgreSQL.
        var expectedPrev = Babel.Compliance.Canonical.ComplianceCanonical.Genesis(Harness.Tenant, Harness.Unit);
        foreach (var r in rows.OrderBy(r => r.Counter))
        {
            Assert.Equal(expectedPrev, r.PreviousHash);
            expectedPrev = r.DocumentHash;
        }
        Assert.Equal(expectedPrev, head.HeadHash);

        // ومهلة غامضة تُحسم باستعلام الحالة، عبر المخزن العلائقي نفسه.
        var ambiguous = Doc("INV-PG-AMB", 640.0000m);
        authority.Script(ambiguous.DocumentUuid, FakeBehaviour.AmbiguousAfterAccept);
        Assert.Equal(ComplianceStatus.Ambiguous, (await service.ClearAsync(ambiguous, ct)).Status);
        Assert.Equal(ComplianceStatus.Accepted,
            (await service.ContinueClearanceAsync(ambiguous.DocumentId, ct)).Status);
        Assert.Equal(1, authority.AcceptancesFor(ambiguous.DocumentUuid));

        ledger.AssertUntouched();
    }

    /// <summary>
    /// <b>العدّاد صف مقفول لا <c>SEQUENCE</c>.</b> يُقاس الفرق مباشرةً على القاعدة نفسها:
    /// التسلسل يُضيّع الرقم عند التراجع، وصف العدّاد لا يفعل.
    /// </summary>
    [Fact]
    public async Task A_rolled_back_transaction_burns_a_sequence_value_but_never_a_counter_row_value()
    {
        if (Admin is null) return;
        var ct = TestContext.Current.CancellationToken;

        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var db = TestDatabases.Counter;
        var store = await FreshStoreAsync(db, clock, ct);
        var target = new NpgsqlConnectionStringBuilder(Admin!) { Database = db }.ConnectionString;

        // (أ) SEQUENCE: nextval ثم تراجع ⇒ الرقم ضاع نهائياً.
        await using var conn = new NpgsqlConnection(target);
        await conn.OpenAsync(ct);
        await using (var c = new NpgsqlCommand("create sequence s_demo", conn)) await c.ExecuteNonQueryAsync(ct);
        await using (var tx = await conn.BeginTransactionAsync(ct))
        {
            await using var c = new NpgsqlCommand("select nextval('s_demo')", conn, tx);
            Assert.Equal(1L, (long)(await c.ExecuteScalarAsync(ct))!);
            await tx.RollbackAsync(ct);
        }
        await using (var c = new NpgsqlCommand("select nextval('s_demo')", conn))
            Assert.Equal(2L, (long)(await c.ExecuteScalarAsync(ct))!);   // الرقم 1 ضاع

        // (ب) صف العدّاد: تراجع ⇒ لا شيء ضاع، لأن لا مستند وُجد أصلاً.
        await Assert.ThrowsAnyAsync<Exception>(() => store.InTransactionAsync(async (uow, t) =>
        {
            var slot = await uow.AllocateChainSlotAsync(Harness.Tenant, Harness.Unit, t);
            Assert.Equal(1, slot.Counter);
            throw new InvalidOperationException("تراجع مقصود");
        }, ct));

        var again = await store.InTransactionAsync(
            (uow, t) => uow.AllocateChainSlotAsync(Harness.Tenant, Harness.Unit, t), ct);
        Assert.Equal(1, again.Counter);   // لم يُحرق شيء
    }
}
