using Babel.Compliance.Abstractions;
using Babel.Compliance.Canonical;
using Babel.Compliance.Model;
using Babel.Compliance.Store;

namespace Babel.Compliance.Pipeline;

/// <summary>إيصال إبلاغ فوري: لا انتظار، لا شبكة، لا حالة معلّقة تحجز المستخدم.</summary>
public sealed record ReportingReceipt(
    ComplianceDocumentId DocumentId,
    long Counter,
    string DocumentHashHex,
    DateTimeOffset QueuedAt,
    DateTimeOffset ReportingDeadline,
    string MessageAr,
    string MessageEn);

/// <summary>
/// واجهة الوحدة. <b>مساران، دالتان، ولا دالة ثالثة تجمعهما.</b>
/// استدعاء الدالة الخطأ لمسار مستند يرمي — الفرق البنيوي مُنفَّذ في التوقيعات نفسها،
/// لا موصوفاً في تعليق.
/// </summary>
public sealed class ComplianceService(
    ComplianceDocumentFactory factory,
    ClearanceCoordinator clearance,
    ReportingWorker reporting,
    IComplianceStore store,
    ComplianceSettings settings,
    TimeProvider clock)
{
    /// <summary>
    /// <b>مسار الإبلاغ.</b> يبني ويضع في الصندوق الصادر ويعود فوراً.
    /// البيع اكتمل، والمستند سُلِّم؛ الإبلاغ يجري في عامل خلفي.
    /// <b>وهذا ما يجعل نقطة البيع دون إنترنت ممكنة أصلاً.</b>
    /// </summary>
    public async Task<ReportingReceipt> QueueForReportingAsync(ComplianceDocument document, CancellationToken ct)
    {
        if (document.Flow != ComplianceFlow.Reporting)
            throw new InvalidOperationException(
                "هذا المستند في مسار المقاصة: يُستدعى ClearAsync وهي حاجزة. " +
                "المساران لا يتقاسمان آلية واحدة. / this document is on the clearance flow; call ClearAsync (blocking).");

        var record = await factory.BuildAndQueueAsync(document, ct);
        var now = ComplianceCanonical.PgInstant(clock.GetUtcNow());

        return new ReportingReceipt(
            record.DocumentId,
            record.Counter,
            Convert.ToHexString(record.DocumentHash).ToLowerInvariant(),
            now,
            now + settings.ReportingWindow,
            "أُدرج المستند في طابور الإبلاغ. لا حاجة لانتظار الجهة — المستند صادر ويُسلَّم للعميل الآن.",
            "queued for reporting. No need to wait for the authority — the document is issued and may be handed to the customer now.");
    }

    /// <summary>
    /// <b>مسار المقاصة.</b> يبني ثم <b>يحجز</b> حتى يعود رد الجهة أو تنتهي المهلة.
    /// لا يُطبع المستند ولا يُسلَّم للمشتري قبل أن تعود <see cref="ClearanceResult.DocumentMayBeDelivered"/> صحيحة.
    /// </summary>
    public async Task<ClearanceResult> ClearAsync(ComplianceDocument document, CancellationToken ct)
    {
        if (document.Flow != ComplianceFlow.Clearance)
            throw new InvalidOperationException(
                "هذا المستند في مسار الإبلاغ: يُستدعى QueueForReportingAsync وهي غير حاجزة. " +
                "/ this document is on the reporting flow; call QueueForReportingAsync (non-blocking).");

        var record = await factory.BuildAndQueueAsync(document, ct);
        return await clearance.ClearAsync(record.DocumentId, ct);
    }

    /// <summary>إعادة محاولة مقاصة قائمة (بعد عطل «لم يُرسل»، أو حسم غموض). لا تبني مستنداً جديداً.</summary>
    public Task<ClearanceResult> ContinueClearanceAsync(ComplianceDocumentId id, CancellationToken ct) =>
        clearance.ClearAsync(id, ct);

    /// <summary>يستنزف طابور الإبلاغ. في الإنتاج يقوده الصندوق الصادر الدائم.</summary>
    public Task<int> DrainReportingQueueAsync(int max, CancellationToken ct) => reporting.DrainAsync(max, ct);

    /// <summary>حالة المستند كما تُعرض للمستخدم، مع أثر العزل المالي.</summary>
    public async Task<ComplianceView?> ViewAsync(ComplianceDocumentId id, CancellationToken ct)
    {
        var data = await store.InTransactionAsync(async (uow, token) =>
        {
            var r = await uow.GetAsync(id, token);
            if (r is null) return null;
            var a = await uow.AttemptsAsync(id, token);
            var t = await uow.TransitionsAsync(id, token);
            return new { Record = r, Attempts = a, Transitions = t };
        }, ct);

        if (data is null) return null;

        var inclusion = FiscalInclusionEvaluator.Evaluate(data.Record, settings.Quarantine);
        return new ComplianceView(data.Record, inclusion, data.Attempts, data.Transitions);
    }

    /// <summary>
    /// <b>القرار البشري.</b> حين يعجز الآلي، يحسم إنسان — ويُوثَّق قراره كأي انتقال آخر،
    /// باسمه وسببه. هذا هو مخرج الطابور البشري، ولا يوجد مخرج آخر.
    /// </summary>
    public async Task<ClearanceResult> ResolveByHumanAsync(
        ComplianceDocumentId id, HumanResolution resolution, string actor, string noteAr, string noteEn, CancellationToken ct)
    {
        var now = ComplianceCanonical.PgInstant(clock.GetUtcNow());

        await store.InTransactionAsync(async (uow, token) =>
        {
            var live = await uow.GetAsync(id, token) ?? throw new KeyNotFoundException($"لا سجل التزام للمستند {id}");
            if (live.Status != ComplianceStatus.NeedsHumanReview)
                throw new InvalidOperationException(
                    $"المستند ليس في الطابور البشري (حالته {ComplianceStatusText.Ar(live.Status)}) " +
                    "/ document is not in the human queue");

            var to = resolution switch
            {
                HumanResolution.ConfirmAccepted => ComplianceStatus.Accepted,
                HumanResolution.ConfirmRejected => ComplianceStatus.Rejected,
                HumanResolution.AbandonDelivery => ComplianceStatus.TransportFailed,
                _ => ComplianceStatus.Submitting
            };

            await ComplianceJournal.TransitionAsync(uow, live, to, actor,
                $"قرار بشري ({resolution}): {noteAr}", $"human decision ({resolution}): {noteEn}", now, null, token);

            if (to == ComplianceStatus.Submitting)
            {
                // إعادة إرسال بقرار بشري: تُعاد الحالة إلى الطابور بعلم تام بخطر التكرار.
                live.ResolutionAttemptCount = 0;
                await ComplianceJournal.TransitionAsync(uow, live, ComplianceStatus.Queued, actor,
                    "أُعيد إدراجه بقرار بشري صريح، مع تحمّل خطر إنشاء مستند مكرّر لدى الجهة",
                    "requeued by explicit human decision, accepting the risk of creating a duplicate at the authority",
                    now, null, token);
            }
        }, ct);

        return await clearance.ClearAsync(id, ct);
    }
}

public enum HumanResolution
{
    /// <summary>الإنسان تحقّق (ببوابة الجهة أو بنسخة ورقية) أن المستند مقبول.</summary>
    ConfirmAccepted,

    /// <summary>الإنسان تحقّق أن المستند مرفوض أو غير موجود لدى الجهة، وسيصدر مستنداً تصحيحياً.</summary>
    ConfirmRejected,

    /// <summary>يُترك دون إرسال ويُعالَج خارج النظام.</summary>
    AbandonDelivery,

    /// <summary>إعادة الإرسال بقرار صريح، مع تحمّل خطر التكرار.</summary>
    ResubmitAcceptingDuplicateRisk
}

public sealed record ComplianceView(
    ComplianceRecord Record,
    FiscalInclusion Fiscal,
    IReadOnlyList<SubmissionAttempt> Attempts,
    IReadOnlyList<StatusTransition> Transitions)
{
    public string StatusAr => ComplianceStatusText.Ar(Record.Status);
    public string StatusEn => ComplianceStatusText.En(Record.Status);

    /// <summary>هل تغيّرت البايتات بين المحاولات؟ يكشف إعادة ختم المزوّد.</summary>
    public bool PayloadStableAcrossAttempts =>
        Attempts.Select(a => a.PayloadFingerprint).Distinct().Count() <= 1;
}
