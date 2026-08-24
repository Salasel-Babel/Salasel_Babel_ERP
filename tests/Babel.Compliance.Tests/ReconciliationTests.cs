using Babel.Compliance.Abstractions;
using Babel.Compliance.FakeProvider;
using Babel.Compliance.Model;
using Babel.Compliance.Pipeline;
using Babel.Compliance.Reconciliation;
using Babel.Compliance.Store;
using Xunit;

namespace Babel.Compliance.Tests;

/// <summary>
/// المطابقة بين ما رحّله الدفتر وما بناه الالتزام وما أقرّت به الجهة.
/// <b>هذه هي الجهة التي تكتشف ما فات كل شيء آخر.</b>
/// </summary>
public class ReconciliationTests
{
    private static (DateTimeOffset From, DateTimeOffset To) Window(Harness h) =>
        (h.Clock.GetUtcNow().AddDays(-1), h.Clock.GetUtcNow().AddDays(1));

    [Fact]
    public async Task A_clean_period_produces_no_findings_and_a_zero_tax_gap()
    {
        using var h = new Harness(KeyCustody.SelfHeld);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        for (var i = 1; i <= 3; i++)
            await h.Service.ClearAsync(h.NewDocument(ComplianceFlow.Clearance, $"INV-A{i}", net: 400.0000m),
                TestContext.Current.CancellationToken);

        var (from, to) = Window(h);
        var report = await h.Reconciler.RunAsync(Harness.Tenant, from, to, TestContext.Current.CancellationToken);

        Assert.True(report.IsClean, string.Join("\n", report.Findings.Select(f => f.SummaryAr)));
        Assert.Equal(3, report.Totals.LedgerDocuments);
        Assert.Equal(3, report.Totals.Accepted);
        Assert.Equal(0m, report.Totals.TaxGap);
        Assert.Equal(180.0000m, report.Totals.AcceptedTaxTotal);   // 3 × 60.0000
    }

    [Fact]
    public async Task A_posted_entry_with_no_compliance_document_is_reported_as_critical()
    {
        using var h = new Harness(KeyCustody.SelfHeld);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        // قيد مُرحَّل لم يمرّ بالالتزام إطلاقاً — الحالة التي تُفقد الفواتير بصمت.
        h.Ledger.Post(Harness.Tenant, Harness.Unit, "INV-ORPHAN", 1000.0000m, 150.0000m, 1150.0000m, h.Clock.GetUtcNow());

        var (from, to) = Window(h);
        var report = await h.Reconciler.RunAsync(Harness.Tenant, from, to, TestContext.Current.CancellationToken);

        var finding = Assert.Single(report.Findings, f => f.Kind == FindingKind.PostedButNeverBuilt);
        Assert.Equal(FindingSeverity.Critical, finding.Severity);
        Assert.Equal(150.0000m, finding.ExpectedAmount);
        Assert.Contains("INV-ORPHAN", finding.SummaryAr, StringComparison.Ordinal);
        Assert.Equal(150.0000m, report.Totals.TaxGap);
    }

    [Fact]
    public async Task An_unresolved_ambiguity_becomes_a_first_class_finding_with_a_human_next_step()
    {
        using var h = new Harness(KeyCustody.SelfHeld, StatusProbeSupport.NotSupported);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        var doc = h.NewDocument(ComplianceFlow.Clearance, "INV-AMB");
        h.Authority.Script(doc.DocumentUuid, FakeBehaviour.AmbiguousAfterAccept);
        await h.Service.ClearAsync(doc, TestContext.Current.CancellationToken);

        var (from, to) = Window(h);
        var report = await h.Reconciler.RunAsync(Harness.Tenant, from, to, TestContext.Current.CancellationToken);

        var finding = Assert.Single(report.Findings, f => f.Kind == FindingKind.UnresolvedAmbiguity);
        Assert.Contains("لا يُعاد الإرسال يدوياً", finding.NextStepAr, StringComparison.Ordinal);
        Assert.Contains("do not resubmit by hand", finding.NextStepEn, StringComparison.Ordinal);
        Assert.NotEmpty(report.NeedingHuman);
        Assert.Equal(1, report.Totals.Unresolved);

        // الضريبة المعزولة ظاهرة كرقم واحد للمدير المالي.
        Assert.Equal(150.0000m, report.Totals.QuarantinedTaxTotal);
        Assert.Equal(150.0000m, report.Totals.TaxGap);
    }

    [Fact]
    public async Task A_broken_chain_link_halts_issuance_on_that_unit()
    {
        using var h = new Harness(KeyCustody.SelfHeld);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        var first = h.NewDocument(ComplianceFlow.Clearance, "INV-C1");
        var second = h.NewDocument(ComplianceFlow.Clearance, "INV-C2");
        await h.Service.ClearAsync(first, TestContext.Current.CancellationToken);
        await h.Service.ClearAsync(second, TestContext.Current.CancellationToken);

        // عبث مباشر بالمخزن: تُعاد كتابة رابط السلسلة على المستند الثاني.
        await h.Store.InTransactionAsync(async (uow, ct) =>
        {
            var live = (await uow.GetAsync(second.DocumentId, ct))!;
            var tampered = new ComplianceRecord
            {
                DocumentId = live.DocumentId, DocumentUuid = live.DocumentUuid, Tenant = live.Tenant,
                IssuingUnit = live.IssuingUnit, Environment = live.Environment, Kind = live.Kind, Flow = live.Flow,
                DocumentNumber = live.DocumentNumber, JournalEntry = live.JournalEntry, IssuedAt = live.IssuedAt,
                Counter = live.Counter, PreviousHash = new byte[32], DocumentHash = live.DocumentHash,
                FrozenPayload = live.FrozenPayload, SealState = live.SealState,
                SubmissionFingerprint = live.SubmissionFingerprint, NetTotal = live.NetTotal,
                TaxTotal = live.TaxTotal, GrossTotal = live.GrossTotal, Status = live.Status,
                Version = live.Version, SettledAt = live.SettledAt, QueuedAt = live.QueuedAt
            };
            await uow.UpdateAsync(tampered, ct);
        }, TestContext.Current.CancellationToken);

        var (from, to) = Window(h);
        var report = await h.Reconciler.RunAsync(Harness.Tenant, from, to, TestContext.Current.CancellationToken);

        var broken = Assert.Single(report.Findings, f => f.Kind == FindingKind.ChainBroken);
        Assert.Equal(FindingSeverity.Blocking, broken.Severity);
        Assert.NotEmpty(report.Blocking);

        // والوحدة موقوفة فعلاً: أي بناء لاحق يرمي.
        var next = h.NewDocument(ComplianceFlow.Clearance, "INV-C3");
        await Assert.ThrowsAsync<ChainHaltedException>(() =>
            h.Service.ClearAsync(next, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task An_amount_mismatch_between_entry_and_document_is_caught_by_exact_decimal_comparison()
    {
        using var h = new Harness(KeyCustody.SelfHeld);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        var doc = h.NewDocument(ComplianceFlow.Clearance, "INV-M1", net: 1000.0000m);
        // نبني المستند بضريبة تختلف عن ضريبة القيد بمقدار 0.0001 فقط.
        var skewed = doc with
        {
            Totals = new DocumentTotals(1000.0000m, 150.0001m, 1150.0001m),
            Lines = [doc.Lines[0] with { TaxAmount = 150.0001m, GrossAmount = 1150.0001m }]
        };
        await h.Service.ClearAsync(skewed, TestContext.Current.CancellationToken);

        var (from, to) = Window(h);
        var report = await h.Reconciler.RunAsync(Harness.Tenant, from, to, TestContext.Current.CancellationToken);

        var finding = Assert.Single(report.Findings, f => f.Kind == FindingKind.AmountMismatch);
        Assert.Equal(150.0000m, finding.ExpectedAmount);
        Assert.Equal(150.0001m, finding.ObservedAmount);
        Assert.Contains("إشعار", finding.NextStepAr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Findings_are_persisted_and_can_be_closed_by_a_named_human()
    {
        using var h = new Harness(KeyCustody.SelfHeld);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);
        h.Ledger.Post(Harness.Tenant, Harness.Unit, "INV-ORPHAN2", 200.0000m, 30.0000m, 230.0000m, h.Clock.GetUtcNow());

        var (from, to) = Window(h);
        var report = await h.Reconciler.RunAsync(Harness.Tenant, from, to, TestContext.Current.CancellationToken);
        var open = await h.Store.InTransactionAsync(
            (uow, ct) => uow.OpenFindingsAsync(Harness.Tenant, ct), TestContext.Current.CancellationToken);
        Assert.Single(open);

        await h.Store.InTransactionAsync((uow, ct) => uow.ResolveFindingAsync(
            report.Findings[0].FindingId, "المحاسب", "بُني المستند يدوياً", "document built by hand", ct),
            TestContext.Current.CancellationToken);

        var stillOpen = await h.Store.InTransactionAsync(
            (uow, ct) => uow.OpenFindingsAsync(Harness.Tenant, ct), TestContext.Current.CancellationToken);
        Assert.Empty(stillOpen);
    }

    [Fact]
    public async Task A_human_decision_is_the_only_exit_from_the_human_queue_and_it_is_recorded()
    {
        using var h = new Harness(KeyCustody.SelfHeld, StatusProbeSupport.NotSupported);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        var doc = h.NewDocument(ComplianceFlow.Clearance, "INV-HUM");
        h.Authority.Script(doc.DocumentUuid, FakeBehaviour.AmbiguousAfterAccept);
        await h.Service.ClearAsync(doc, TestContext.Current.CancellationToken);
        await h.Service.ContinueClearanceAsync(doc.DocumentId, TestContext.Current.CancellationToken);
        Assert.Equal(ComplianceStatus.NeedsHumanReview, h.Record(doc.DocumentId).Status);

        var result = await h.Service.ResolveByHumanAsync(doc.DocumentId, HumanResolution.ConfirmAccepted,
            "فاطمة — المحاسبة", "تأكدنا من بوابة الجهة أن الفاتورة مقبولة",
            "verified on the authority portal that the invoice is accepted", TestContext.Current.CancellationToken);

        Assert.Equal(ComplianceStatus.Accepted, result.Status);
        Assert.Equal(1, h.Authority.AcceptancesFor(doc.DocumentUuid));   // ولم يُرسل شيء

        var transitions = h.Store.PeekTransitions(doc.DocumentId);
        Assert.Contains(transitions, t => t.Actor == "فاطمة — المحاسبة" && t.To == ComplianceStatus.Accepted);
        h.Ledger.AssertUntouched();
    }
}
