using Babel.Compliance.Abstractions;
using Babel.Compliance.FakeProvider;
using Babel.Compliance.Model;
using Babel.Compliance.Pipeline;
using Xunit;

namespace Babel.Compliance.Tests;

/// <summary>
/// الدورتان الأساسيتان: مقبولة ومرفوضة.
/// <b>المقياس الحاسم في كليهما واحد: القيد المحاسبي لا يتغيّر.</b>
/// </summary>
public class LifecycleTests
{
    [Fact]
    public async Task Clearance_posted_queued_submitted_accepted()
    {
        using var h = new Harness(KeyCustody.SelfHeld);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        var doc = h.NewDocument(ComplianceFlow.Clearance, "INV-1001");
        var result = await h.Service.ClearAsync(doc, TestContext.Current.CancellationToken);

        Assert.Equal(ComplianceStatus.Accepted, result.Status);
        Assert.True(result.DocumentMayBeDelivered);

        // مسار الحالات بالضبط: مُنشأ ← الطابور ← قيد الإرسال ← مقبولة
        Assert.Equal(
            [ComplianceStatus.Built, ComplianceStatus.Queued, ComplianceStatus.Submitting, ComplianceStatus.Accepted],
            h.StatusPath(doc.DocumentId));

        var record = h.Record(doc.DocumentId);
        Assert.Equal(1, record.Counter);
        Assert.NotNull(record.StampedDocument);
        Assert.NotEmpty(record.StampedDocument!);
        Assert.Equal(1, h.Authority.AcceptancesFor(doc.DocumentUuid));

        // العزل المالي: مقبولة ⇒ تدخل الإقرار وأعمار الذمم.
        var view = await h.Service.ViewAsync(doc.DocumentId, TestContext.Current.CancellationToken);
        Assert.True(view!.Fiscal.JournalEntryPosted);
        Assert.True(view.Fiscal.IncludeInVatReturn);
        Assert.True(view.Fiscal.IncludeInReceivablesAging);
        Assert.False(view.Fiscal.IsQuarantined);

        h.Ledger.AssertUntouched();
    }

    [Fact]
    public async Task Clearance_rejected_leaves_the_journal_entry_untouched_and_quarantines_only_compliance()
    {
        using var h = new Harness(KeyCustody.SelfHeld);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        var doc = h.NewDocument(ComplianceFlow.Clearance, "INV-1002", net: 2500.0000m);
        h.Authority.Script(doc.DocumentUuid, FakeBehaviour.Reject);

        var result = await h.Service.ClearAsync(doc, TestContext.Current.CancellationToken);

        Assert.Equal(ComplianceStatus.Rejected, result.Status);
        Assert.False(result.DocumentMayBeDelivered);
        Assert.Contains(result.Notices, n => n.Severity == NoticeSeverity.Error);

        Assert.Equal(
            [ComplianceStatus.Built, ComplianceStatus.Queued, ComplianceStatus.Submitting, ComplianceStatus.Rejected],
            h.StatusPath(doc.DocumentId));

        var view = await h.Service.ViewAsync(doc.DocumentId, TestContext.Current.CancellationToken);

        // ما يتغيّر: حالة الالتزام والعزل. ما لا يتغيّر: القيد.
        Assert.True(view!.Fiscal.JournalEntryPosted);
        Assert.False(view.Fiscal.IncludeInVatReturn);
        Assert.False(view.Fiscal.IncludeInReceivablesAging);
        Assert.True(view.Fiscal.IsQuarantined);

        h.Ledger.AssertUntouched();

        // المستند المرفوض استهلك عدّاده — «كل قيمة عدّاد محسوبة، بما فيها الملغى».
        Assert.Equal(1, h.Record(doc.DocumentId).Counter);
        var next = h.NewDocument(ComplianceFlow.Clearance, "INV-1003");
        await h.Service.ClearAsync(next, TestContext.Current.CancellationToken);
        Assert.Equal(2, h.Record(next.DocumentId).Counter);
    }

    [Fact]
    public async Task Reporting_is_fire_and_forget_and_does_not_block_on_the_authority()
    {
        using var h = new Harness(KeyCustody.SelfHeld);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        var doc = h.NewDocument(ComplianceFlow.Reporting, "SIMP-2001", net: 87.0000m);
        var receipt = await h.Service.QueueForReportingAsync(doc, TestContext.Current.CancellationToken);

        // لا نداء وقع بعد: البيع اكتمل والمستند سُلِّم.
        Assert.Empty(h.Authority.Accepted);
        Assert.Equal(ComplianceStatus.Queued, h.Record(doc.DocumentId).Status);
        Assert.Equal(1, receipt.Counter);
        Assert.Equal(h.Clock.GetUtcNow() + h.Settings.ReportingWindow, receipt.ReportingDeadline);

        // ثم يعمل العامل الخلفي.
        var drained = await h.Service.DrainReportingQueueAsync(10, TestContext.Current.CancellationToken);
        Assert.Equal(1, drained);
        Assert.Equal(ComplianceStatus.Accepted, h.Record(doc.DocumentId).Status);
        Assert.Equal(1, h.Authority.AcceptancesFor(doc.DocumentUuid));

        h.Ledger.AssertUntouched();
    }

    [Fact]
    public async Task The_two_flows_do_not_share_one_mechanism()
    {
        using var h = new Harness(KeyCustody.SelfHeld);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        var clearanceDoc = h.NewDocument(ComplianceFlow.Clearance, "INV-3001");
        var reportingDoc = h.NewDocument(ComplianceFlow.Reporting, "SIMP-3001");

        // استدعاء الآلية الخطأ يرمي — الفرق البنيوي منفَّذ في التوقيعات، لا موصوفاً في تعليق.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.Service.QueueForReportingAsync(clearanceDoc, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.Service.ClearAsync(reportingDoc, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Clearance_never_enters_the_outbox_queue()
    {
        using var h = new Harness(KeyCustody.SelfHeld);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        await h.Service.ClearAsync(h.NewDocument(ComplianceFlow.Clearance, "INV-4001"),
            TestContext.Current.CancellationToken);
        Assert.Empty(h.Store.PeekWork());

        await h.Service.QueueForReportingAsync(h.NewDocument(ComplianceFlow.Reporting, "SIMP-4001"),
            TestContext.Current.CancellationToken);
        Assert.Single(h.Store.PeekWork());
    }

    [Fact]
    public async Task Counter_and_chain_are_gapless_and_verifiable_over_ten_documents()
    {
        using var h = new Harness(KeyCustody.SelfHeld);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        var ids = new List<ComplianceDocumentId>();
        for (var i = 1; i <= 10; i++)
        {
            var doc = h.NewDocument(ComplianceFlow.Clearance, $"INV-50{i:D2}", net: 100.0000m * i);
            if (i == 4) h.Authority.Script(doc.DocumentUuid, FakeBehaviour.Reject);   // مرفوضة تستهلك عدّادها
            await h.Service.ClearAsync(doc, TestContext.Current.CancellationToken);
            ids.Add(doc.DocumentId);
        }

        var records = ids.Select(h.Record).OrderBy(r => r.Counter).ToList();
        Assert.Equal(Enumerable.Range(1, 10).Select(i => (long)i), records.Select(r => r.Counter));

        var expectedPrev = Babel.Compliance.Canonical.ComplianceCanonical.Genesis(Harness.Tenant, Harness.Unit);
        foreach (var r in records)
        {
            Assert.Equal(expectedPrev, r.PreviousHash);
            expectedPrev = r.DocumentHash;
        }

        Assert.Equal(ComplianceStatus.Rejected, records[3].Status);
        h.Ledger.AssertUntouched();
    }
}
