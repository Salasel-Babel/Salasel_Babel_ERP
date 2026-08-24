using Babel.Compliance.Abstractions;
using Babel.Compliance.Canonical;
using Babel.Compliance.Model;
using Babel.Compliance.Store;

namespace Babel.Compliance.Pipeline;

/// <summary>
/// <b>مسار الإبلاغ: أطلق وانسَ داخل الصندوق الصادر.</b>
/// <para/>
/// الفروق البنيوية عن المقاصة — وهي فروق في الآلية لا في الإعداد:
/// <list type="bullet">
///   <item>لا نداء من طلب المستخدم إطلاقاً. البيع يكتمل، وهذا العامل يعمل بعده.</item>
///   <item>لا حالة انتظار تحجز الواجهة. المستند سُلِّم للعميل فعلاً.</item>
///   <item>إعادة المحاولة مجدولة في الطابور بتباعد أُسّي، لا حلقة داخل الطلب.</item>
///   <item>لا نسخة مختومة عائدة تُنتظر.</item>
/// </list>
/// وما يشترك فيه المساران شيء واحد فقط: <b>حارس الحصانة</b> — لأن الغموض واحد.
/// </summary>
public sealed class ReportingWorker(
    IComplianceStore store,
    IComplianceProvider provider,
    IIssuingUnitRegistry registry,
    ComplianceSettings settings,
    TimeProvider clock)
{
    private readonly SubmissionGuard _guard = new(settings, clock);

    /// <summary>
    /// يستنزف الطابور مرة واحدة. في الإنتاج يستدعيه معالج Wolverine الذي يقرأ من
    /// الصندوق الصادر الدائم؛ وفي الاختبار يُستدعى مباشرة. <b>الجسم واحد في الحالتين.</b>
    /// </summary>
    public async Task<int> DrainAsync(int max, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var due = await store.InTransactionAsync((uow, token) => uow.DueWorkAsync(now, max, token), ct);

        var handled = 0;
        foreach (var item in due)
        {
            await ProcessAsync(item, ct);
            handled++;
        }
        return handled;
    }

    public async Task ProcessAsync(ComplianceWorkItem item, CancellationToken ct)
    {
        var channel = provider.Reporting
            ?? throw new NotSupportedException("المزوّد لا يعرض قناة إبلاغ / provider exposes no reporting channel");

        var (record, attempts) = await LoadAsync(item.DocumentId, ct);
        if (record.Flow != ComplianceFlow.Reporting)
            throw new InvalidOperationException($"المستند {item.DocumentId} ليس في مسار الإبلاغ");

        if (await ReapStaleAttemptsAsync(record, attempts, ct))
            (record, attempts) = await LoadAsync(item.DocumentId, ct);

        var decision = _guard.Decide(record, attempts, provider.Capabilities);

        switch (decision.Action)
        {
            case SubmissionAction.Stop:
                await CloseWorkAsync(item, ct);
                return;

            case SubmissionAction.HumanReview:
                await MoveToHumanReviewAsync(record, decision, ct);
                await CloseWorkAsync(item, ct);
                return;

            case SubmissionAction.ResolveByProbe:
                await ResolveByProbeAsync(record, item, ct);
                return;

            case SubmissionAction.Submit:
            case SubmissionAction.ResolveByIdenticalResubmit:
            default:
                await SendAsync(record, channel, item,
                    isResolution: decision.Action == SubmissionAction.ResolveByIdenticalResubmit, ct);
                return;
        }
    }


    /// <summary>
    /// <b>الإقلاع بعد سقوط العملية.</b> صف <c>InFlight</c> تجاوز مهلة الإيجار يعني أن النداء
    /// انقطع دون تسجيل نتيجته. يُغلق الصف <b>غموضاً</b> — لا فشلاً — ويُنقل المستند إلى
    /// حالة الغموض قبل أن يتخذ الحارس قراره، وإلا حاول المُنسِّق فتح محاولة جديدة فوق محاولة قائمة.
    /// </summary>
    private async Task<bool> ReapStaleAttemptsAsync(
        ComplianceRecord record, IReadOnlyList<SubmissionAttempt> attempts, CancellationToken ct)
    {
        var now = ComplianceCanonical.PgInstant(clock.GetUtcNow());
        var stale = attempts.Where(a => a.IsStale(now, settings.AttemptLease)).ToList();
        if (stale.Count == 0) return false;

        await store.InTransactionAsync(async (uow, token) =>
        {
            var live = (await uow.GetAsync(record.DocumentId, token))!;
            foreach (var a in stale)
            {
                a.Outcome = AttemptOutcome.Ambiguous;
                a.CompletedAt = now;
                a.FaultClass = FaultClass.Ambiguous;
                a.FaultCode = "process-crash";
                a.FaultMessageAr = "سقطت العملية أثناء النداء: بدأت المحاولة ولم تُسجَّل نتيجتها";
                a.FaultMessageEn = "the process died mid-call: the attempt started and no outcome was recorded";
                await uow.UpdateAttemptAsync(a, token);
            }

            if (live.Status == ComplianceStatus.Submitting)
                await ComplianceJournal.TransitionAsync(uow, live, ComplianceStatus.Ambiguous,
                    "compliance.recovery",
                    $"استُرجعت {stale.Count} محاولة معلّقة تجاوزت مهلة الإيجار — تُعامَل غموضاً لا فشلاً",
                    $"reaped {stale.Count} in-flight attempt(s) past their lease — treated as ambiguity, not failure",
                    now, stale[0].AttemptId, token);
        }, ct);

        return true;
    }

    private async Task SendAsync(
        ComplianceRecord record, IReportingChannel channel, ComplianceWorkItem item, bool isResolution, CancellationToken ct)
    {
        var attempt = await OpenAttemptAsync(record, isResolution, ct);
        var registration = await registry.GetAsync(record.Tenant, record.IssuingUnit, ct);

        var submission = new ReportingSubmission(
            record.DocumentId,
            record.DocumentUuid,
            record.Tenant,
            record.IssuingUnit,
            registration?.Credential ?? CredentialRef.None,
            record.Environment,
            new SealedPayload(record.SealState, record.FrozenPayload, null,
                Convert.FromHexString(record.SubmissionFingerprint)),
            new ChainSlot(record.Counter, record.PreviousHash),
            attempt.AttemptId,
            attempt.AttemptNo,
            record.SubmissionFingerprint);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(settings.ReportingTimeout);

        try
        {
            var ack = await channel.ReportAsync(submission, timeout.Token);
            await RecordAckAsync(record, attempt, ack, item, ct);
        }
        catch (Exception ex)
        {
            await RecordFaultAsync(record, attempt, Classify(ex, ct), item, ct);
        }
    }

    private async Task ResolveByProbeAsync(ComplianceRecord record, ComplianceWorkItem item, CancellationToken ct)
    {
        var query = provider.StatusQuery!;
        var attempt = await OpenAttemptAsync(record, isResolution: true, ct);
        var probe = new StatusProbe(record.DocumentId, record.DocumentUuid, record.Tenant, record.IssuingUnit,
            new ChainSlot(record.Counter, record.PreviousHash), record.SubmissionFingerprint, record.ProviderReference);

        try
        {
            var result = await query.ProbeAsync(probe, ct);
            var mapped = result.State switch
            {
                ProbedState.Accepted => (ReportingDisposition?)ReportingDisposition.Accepted,
                ProbedState.AcceptedWithWarnings => ReportingDisposition.AcceptedWithWarnings,
                ProbedState.Rejected => ReportingDisposition.Rejected,
                _ => null
            };

            if (mapped is { } disposition)
            {
                await RecordAckAsync(record, attempt,
                    new ReportingAcknowledgement(disposition, result.Notices, clock.GetUtcNow(), result.ProviderReference),
                    item, ct, resolvedByProbe: true);
            }
            else if (result.State == ProbedState.NotFound)
            {
                await RequeueAfterNotFoundAsync(record, attempt, item, ct);
            }
            else
            {
                await RecordFaultAsync(record, attempt,
                    ComplianceFault.Ambiguous("probe-inconclusive",
                        "استعلام الحالة لم يحسم", "status probe was inconclusive"), item, ct);
            }
        }
        catch (Exception ex)
        {
            await RecordFaultAsync(record, attempt, Classify(ex, ct), item, ct);
        }
    }

    private async Task<SubmissionAttempt> OpenAttemptAsync(ComplianceRecord record, bool isResolution, CancellationToken ct)
    {
        var now = ComplianceCanonical.PgInstant(clock.GetUtcNow());
        return await store.InTransactionAsync(async (uow, token) =>
        {
            var live = (await uow.GetAsync(record.DocumentId, token))!;
            var attempts = await uow.AttemptsAsync(record.DocumentId, token);

            var attempt = new SubmissionAttempt
            {
                AttemptId = AttemptId.New(),
                DocumentId = live.DocumentId,
                AttemptNo = attempts.Count + 1,
                StartedAt = now,
                PayloadFingerprint = ComplianceDocumentFactory.Fingerprint(live.FrozenPayload),
                IsResolution = isResolution
            };

            live.AttemptCount++;
            if (isResolution) live.ResolutionAttemptCount++;

            await ComplianceJournal.TransitionAsync(uow, live, ComplianceStatus.Submitting,
                isResolution ? "compliance.reporting.resolve" : "compliance.reporting",
                isResolution
                    ? $"محاولة حسم رقم {live.ResolutionAttemptCount} ببايتات مطابقة"
                    : $"محاولة إبلاغ رقم {attempt.AttemptNo}",
                isResolution
                    ? $"resolution attempt {live.ResolutionAttemptCount} with identical bytes"
                    : $"reporting attempt {attempt.AttemptNo}",
                now, attempt.AttemptId, token);

            await uow.InsertAttemptAsync(attempt, token);
            return attempt;
        }, ct);
    }

    private async Task RecordAckAsync(
        ComplianceRecord record, SubmissionAttempt attempt, ReportingAcknowledgement ack,
        ComplianceWorkItem item, CancellationToken ct, bool resolvedByProbe = false)
    {
        var now = ComplianceCanonical.PgInstant(clock.GetUtcNow());
        await store.InTransactionAsync(async (uow, token) =>
        {
            var live = (await uow.GetAsync(record.DocumentId, token))!;
            attempt.CompletedAt = now;
            attempt.ProviderReference = ack.ProviderReference;
            attempt.ProviderReportedDuplicate = ack.RecognisedAsDuplicate;

            var (status, outcome, ar, en) = ack.Disposition switch
            {
                ReportingDisposition.Accepted =>
                    (ComplianceStatus.Accepted, AttemptOutcome.Accepted, "قُبل الإبلاغ", "reporting accepted"),
                ReportingDisposition.AcceptedWithWarnings =>
                    (ComplianceStatus.AcceptedWithWarnings, AttemptOutcome.AcceptedWithWarnings,
                     "قُبل الإبلاغ بملاحظات", "reporting accepted with warnings"),
                _ => (ComplianceStatus.Rejected, AttemptOutcome.Rejected,
                      "رُفض الإبلاغ — المستند صدر فعلاً وسُلِّم، والرفض يستوجب معالجة وتصحيحاً، والقيد باقٍ كما هو",
                      "reporting rejected — the document was already issued and delivered; correction is required and the journal entry stands untouched")
            };

            if (resolvedByProbe) { ar += " (حُسم باستعلام الحالة دون إعادة إرسال)"; en += " (resolved by status probe, not resubmitted)"; }
            if (ack.RecognisedAsDuplicate) { ar += " (تعرّف المزوّد عليه كإرسال مكرّر)"; en += " (recognised by the provider as a duplicate)"; }

            attempt.Outcome = outcome;
            live.ProviderReference = ack.ProviderReference;
            live.Notices = [.. ack.Notices];

            await ComplianceJournal.TransitionAsync(uow, live, status, "compliance.reporting", ar, en, now, attempt.AttemptId, token);
            await uow.UpdateAttemptAsync(attempt, token);

            item.Done = true;
            await uow.UpdateWorkAsync(item, token);
        }, ct);
    }

    private async Task RecordFaultAsync(
        ComplianceRecord record, SubmissionAttempt attempt, ComplianceFault fault,
        ComplianceWorkItem item, CancellationToken ct)
    {
        var now = ComplianceCanonical.PgInstant(clock.GetUtcNow());
        await store.InTransactionAsync(async (uow, token) =>
        {
            var live = (await uow.GetAsync(record.DocumentId, token))!;
            attempt.CompletedAt = now;
            attempt.FaultClass = fault.Class;
            attempt.FaultCode = fault.Code;
            attempt.FaultMessageAr = fault.MessageAr;
            attempt.FaultMessageEn = fault.MessageEn;

            switch (fault.Class)
            {
                case FaultClass.TransientNotSent:
                {
                    attempt.Outcome = AttemptOutcome.NotSent;
                    var exhausted = live.AttemptCount >= settings.Retry.MaxAttempts;
                    await ComplianceJournal.TransitionAsync(uow, live,
                        exhausted ? ComplianceStatus.TransportFailed : ComplianceStatus.Queued,
                        "compliance.reporting",
                        exhausted
                            ? $"استُنفدت محاولات الإبلاغ ({live.AttemptCount}): {fault.MessageAr}"
                            : $"الطلب لم يغادر — إعادة جدولة: {fault.MessageAr}",
                        exhausted
                            ? $"reporting attempts exhausted ({live.AttemptCount}): {fault.MessageEn}"
                            : $"request never left — rescheduled: {fault.MessageEn}",
                        now, attempt.AttemptId, token);

                    // إعادة الجدولة بتباعد أُسّي مع اهتزاز — لا حلقة محاولات داخل نداء واحد.
                    item.Attempts++;
                    item.LastErrorAr = fault.MessageAr;
                    item.LastErrorEn = fault.MessageEn;
                    item.Done = exhausted;
                    item.NotBefore = now + settings.Retry.DelayFor(item.Attempts);
                    await uow.UpdateWorkAsync(item, token);
                    break;
                }

                case FaultClass.Permanent:
                    attempt.Outcome = AttemptOutcome.Rejected;
                    live.Notices = [.. live.Notices, ComplianceNotice.Err(fault.Code, fault.MessageAr, fault.MessageEn)];
                    await ComplianceJournal.TransitionAsync(uow, live, ComplianceStatus.Rejected,
                        "compliance.reporting",
                        $"رفض نهائي: {fault.MessageAr} — القيد المحاسبي باقٍ كما هو",
                        $"permanent rejection: {fault.MessageEn} — the journal entry stands untouched",
                        now, attempt.AttemptId, token);
                    item.Done = true;
                    await uow.UpdateWorkAsync(item, token);
                    break;

                case FaultClass.Ambiguous:
                default:
                    attempt.Outcome = AttemptOutcome.Ambiguous;
                    await ComplianceJournal.TransitionAsync(uow, live, ComplianceStatus.Ambiguous,
                        "compliance.reporting",
                        $"مهلة غامضة: {fault.MessageAr}. الصندوق الصادر لن يعيد الإرسال — العنصر يتحوّل إلى مهمة حسم.",
                        $"ambiguous timeout: {fault.MessageEn}. The outbox will NOT resubmit; the item becomes a resolution task.",
                        now, attempt.AttemptId, token);

                    // العنصر نفسه يبقى في الطابور لكن بنوع مختلف: حسم، لا إرسال.
                    // تغيير النوع ليس تجميلاً: لوحة المتابعة تعرض «قيد الحسم» لا «قيد الإرسال».
                    item.Kind = ComplianceWorkKind.ResolveAmbiguity;
                    item.Attempts++;
                    item.LastErrorAr = fault.MessageAr;
                    item.LastErrorEn = fault.MessageEn;
                    item.NotBefore = now + settings.Retry.DelayFor(item.Attempts);
                    await uow.UpdateWorkAsync(item, token);
                    break;
            }

            await uow.UpdateAttemptAsync(attempt, token);
        }, ct);
    }

    private async Task RequeueAfterNotFoundAsync(
        ComplianceRecord record, SubmissionAttempt attempt, ComplianceWorkItem item, CancellationToken ct)
    {
        var now = ComplianceCanonical.PgInstant(clock.GetUtcNow());
        await store.InTransactionAsync(async (uow, token) =>
        {
            var live = (await uow.GetAsync(record.DocumentId, token))!;
            attempt.Outcome = AttemptOutcome.NotSent;
            attempt.CompletedAt = now;
            live.ResolutionAttemptCount = 0;
            await ComplianceJournal.TransitionAsync(uow, live, ComplianceStatus.Queued, "compliance.reporting.resolve",
                "استعلام الحالة يؤكد أن الجهة لا تعرف هذا المستند: إعادة الإبلاغ آمنة",
                "status probe positively confirms the authority has no such document: re-reporting is safe",
                now, attempt.AttemptId, token);
            await uow.UpdateAttemptAsync(attempt, token);
            item.NotBefore = now;
            await uow.UpdateWorkAsync(item, token);
        }, ct);
    }

    private async Task MoveToHumanReviewAsync(ComplianceRecord record, SubmissionDecision decision, CancellationToken ct)
    {
        var now = ComplianceCanonical.PgInstant(clock.GetUtcNow());
        await store.InTransactionAsync(async (uow, token) =>
        {
            var live = (await uow.GetAsync(record.DocumentId, token))!;
            if (live.Status == ComplianceStatus.NeedsHumanReview) return;
            live.HumanReviewReasonAr = decision.ReasonAr;
            live.HumanReviewReasonEn = decision.ReasonEn;
            await ComplianceJournal.TransitionAsync(uow, live, ComplianceStatus.NeedsHumanReview,
                "compliance.guard", decision.ReasonAr, decision.ReasonEn, now, null, token);
        }, ct);
    }

    private async Task CloseWorkAsync(ComplianceWorkItem item, CancellationToken ct) =>
        await store.InTransactionAsync(async (uow, token) =>
        {
            item.Done = true;
            await uow.UpdateWorkAsync(item, token);
        }, ct);

    private static ComplianceFault Classify(Exception ex, CancellationToken outer) => ex switch
    {
        ComplianceTransportException cte => cte.Fault,
        OperationCanceledException when outer.IsCancellationRequested =>
            ComplianceFault.Ambiguous("cancelled",
                "أُلغيت العملية بعد أن غادر الطلب", "cancelled after the request left"),
        OperationCanceledException or TimeoutException =>
            ComplianceFault.Ambiguous("timeout", "انتهت المهلة دون جواب", "the call timed out with no answer"),
        _ => ComplianceFault.Ambiguous("unknown",
                $"استثناء غير مصنَّف أثناء النداء: {ex.GetType().Name}",
                $"unclassified exception during the call: {ex.GetType().Name}")
    };

    private async Task<(ComplianceRecord Record, IReadOnlyList<SubmissionAttempt> Attempts)> LoadAsync(
        ComplianceDocumentId id, CancellationToken ct) =>
        await store.InTransactionAsync(async (uow, token) =>
        {
            var r = await uow.GetAsync(id, token) ?? throw new KeyNotFoundException($"لا سجل التزام للمستند {id}");
            var a = await uow.AttemptsAsync(id, token);
            return (r, a);
        }, ct);
}
