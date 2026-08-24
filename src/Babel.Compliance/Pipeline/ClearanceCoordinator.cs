using Babel.Compliance.Abstractions;
using Babel.Compliance.Canonical;
using Babel.Compliance.Model;
using Babel.Compliance.Store;

namespace Babel.Compliance.Pipeline;

/// <summary>نتيجة نداء مقاصة حاجز، بالشكل الذي تعرضه الواجهة.</summary>
public sealed record ClearanceResult(
    ComplianceDocumentId DocumentId,
    ComplianceStatus Status,
    IReadOnlyList<ComplianceNotice> Notices,
    string StatusAr,
    string StatusEn,
    bool DocumentMayBeDelivered,
    string GuidanceAr,
    string GuidanceEn);

/// <summary>
/// <b>مسار المقاصة: saga طلب/استجابة حاجزة.</b> مستقلة تماماً عن مسار الإبلاغ —
/// لا تشترك معه في طابور ولا في عامل خلفي ولا في سياسة إعادة محاولة.
/// دمج المسارين يعطي إما مقاصة غير آمنة أو إبلاغاً بطيئاً بلا داعٍ (02-architecture §11.3).
/// <para/>
/// <b>حالة الانتظار مرئية لأنها مكتوبة قبل النداء:</b> الانتقال إلى
/// <see cref="ComplianceStatus.Submitting"/> وصف المحاولة يُثبَّتان ويُتمّان قبل أن يُفتح
/// أي اتصال. أي قارئ للوحة يرى «قيد الإرسال» طوال النداء، وسقوط العملية في منتصفه
/// يترك أثراً بدل أن يختفي.
/// </summary>
public sealed class ClearanceCoordinator(
    IComplianceStore store,
    IComplianceProvider provider,
    IIssuingUnitRegistry registry,
    ComplianceSettings settings,
    TimeProvider clock)
{
    private readonly SubmissionGuard _guard = new(settings, clock);

    public async Task<ClearanceResult> ClearAsync(ComplianceDocumentId id, CancellationToken ct)
    {
        var channel = provider.Clearance
            ?? throw new NotSupportedException("المزوّد لا يعرض قناة مقاصة / provider exposes no clearance channel");

        var (record, attempts) = await LoadAsync(id, ct);
        if (record.Flow != ComplianceFlow.Clearance)
            throw new InvalidOperationException(
                $"المستند {id} في مسار الإبلاغ ولا يُقاصّ / document is on the reporting flow");

        if (await ReapStaleAttemptsAsync(record, attempts, ct))
            (record, attempts) = await LoadAsync(id, ct);

        var decision = _guard.Decide(record, attempts, provider.Capabilities);

        switch (decision.Action)
        {
            case SubmissionAction.Stop:
                return Describe(record, decision);

            case SubmissionAction.HumanReview:
                await MoveToHumanReviewAsync(record, decision, ct);
                return Describe(await ReloadAsync(id, ct), decision);

            case SubmissionAction.ResolveByProbe:
                await ResolveByProbeAsync(record, ct);
                return Describe(await ReloadAsync(id, ct), decision);

            case SubmissionAction.Submit:
            case SubmissionAction.ResolveByIdenticalResubmit:
            default:
                await SubmitAsync(record, channel, decision, ct);
                return Describe(await ReloadAsync(id, ct), decision);
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

    // ------------------------------------------------------------------ الإرسال

    private async Task SubmitAsync(
        ComplianceRecord record, IClearanceChannel channel, SubmissionDecision decision, CancellationToken ct)
    {
        var isResolution = decision.Action == SubmissionAction.ResolveByIdenticalResubmit;
        var attempt = await OpenAttemptAsync(record, isResolution, ct);

        // مقبض الاعتماد يُقرأ وقت الإرسال لا وقت البناء: تجديد الشهادة يغيّره،
        // ولا يجوز أن تُوقف محاولةُ إعادة إرسال على مقبض قديم.
        var registration = await registry.GetAsync(record.Tenant, record.IssuingUnit, ct);
        var credential = registration?.Credential ?? CredentialRef.None;

        // البايتات المرسلة هي البايتات المجمَّدة — نفسها في كل محاولة، بلا استثناء.
        var request = new ClearanceRequest(
            record.DocumentId,
            record.DocumentUuid,
            record.Tenant,
            record.IssuingUnit,
            credential,
            record.Environment,
            new SealedPayload(
                record.SealState,
                record.FrozenPayload,
                null,
                Convert.FromHexString(record.SubmissionFingerprint)),
            new ChainSlot(record.Counter, record.PreviousHash),
            attempt.AttemptId,
            attempt.AttemptNo,
            record.SubmissionFingerprint);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(settings.ClearanceTimeout);

        try
        {
            var outcome = await channel.ClearAsync(request, timeout.Token);
            await RecordOutcomeAsync(record, attempt, outcome, ct);
        }
        catch (Exception ex)
        {
            await RecordFaultAsync(record, attempt, Classify(ex, ct), ex, ct);
        }
    }

    // ------------------------------------------------------------------ الحسم

    private async Task ResolveByProbeAsync(ComplianceRecord record, CancellationToken ct)
    {
        var query = provider.StatusQuery!;
        var attempt = await OpenAttemptAsync(record, isResolution: true, ct);

        var probe = new StatusProbe(
            record.DocumentId, record.DocumentUuid, record.Tenant, record.IssuingUnit,
            new ChainSlot(record.Counter, record.PreviousHash),
            record.SubmissionFingerprint, record.ProviderReference);

        try
        {
            var result = await query.ProbeAsync(probe, ct);
            await ApplyProbeAsync(record, attempt, result, ct);
        }
        catch (Exception ex)
        {
            await RecordFaultAsync(record, attempt, Classify(ex, ct), ex, ct);
        }
    }

    private async Task ApplyProbeAsync(
        ComplianceRecord record, SubmissionAttempt attempt, StatusProbeResult result, CancellationToken ct)
    {
        var now = ComplianceCanonical.PgInstant(clock.GetUtcNow());

        await store.InTransactionAsync(async (uow, token) =>
        {
            var live = (await uow.GetAsync(record.DocumentId, token))!;
            attempt.CompletedAt = now;
            attempt.ProviderReference = result.ProviderReference ?? live.ProviderReference;

            switch (result.State)
            {
                case ProbedState.Accepted:
                case ProbedState.AcceptedWithWarnings:
                {
                    var to = result.State == ProbedState.Accepted
                        ? ComplianceStatus.Accepted : ComplianceStatus.AcceptedWithWarnings;
                    attempt.Outcome = to == ComplianceStatus.Accepted
                        ? AttemptOutcome.Accepted : AttemptOutcome.AcceptedWithWarnings;
                    live.ProviderReference = attempt.ProviderReference;
                    live.Notices = [.. result.Notices];
                    if (!result.StampedDocument.IsEmpty) live.StampedDocument = result.StampedDocument.ToArray();
                    await ComplianceJournal.TransitionAsync(uow, live, to, "compliance.resolve.probe",
                        "حُسم الغموض باستعلام الحالة: المستند مقبول لدى الجهة — لم يُعَد إرساله",
                        "ambiguity resolved by status probe: the document is accepted; it was never resubmitted",
                        now, attempt.AttemptId, token);
                    break;
                }
                case ProbedState.Rejected:
                    attempt.Outcome = AttemptOutcome.Rejected;
                    live.Notices = [.. result.Notices];
                    await ComplianceJournal.TransitionAsync(uow, live, ComplianceStatus.Rejected,
                        "compliance.resolve.probe",
                        "حُسم الغموض باستعلام الحالة: المستند مرفوض — القيد المحاسبي باقٍ كما هو",
                        "ambiguity resolved by status probe: rejected; the journal entry stands untouched",
                        now, attempt.AttemptId, token);
                    break;

                case ProbedState.NotFound:
                    // تأكيد إيجابي بأن الطلب لم يصل — الحالة الوحيدة التي يعود فيها الإرسال العادي آمناً.
                    // «الغياب» وحده لا يكفي: يجب أن يكون الاستعلام قد أجاب صراحةً «لا أعرف هذا المستند».
                    attempt.Outcome = AttemptOutcome.NotSent;
                    live.ResolutionAttemptCount = 0;
                    await ComplianceJournal.TransitionAsync(uow, live, ComplianceStatus.Queued,
                        "compliance.resolve.probe",
                        "استعلام الحالة يؤكد أن الجهة لا تعرف هذا المستند: الطلب لم يصل، وإعادة الإرسال آمنة",
                        "status probe positively confirms the authority has no such document: the request never arrived, so resubmission is safe",
                        now, attempt.AttemptId, token);
                    break;

                case ProbedState.Pending:
                case ProbedState.Unknown:
                default:
                    attempt.Outcome = AttemptOutcome.Ambiguous;
                    live.ResolutionAttemptCount++;
                    await uow.UpdateAsync(live, token);
                    break;
            }

            await uow.UpdateAttemptAsync(attempt, token);
        }, ct);
    }

    // ------------------------------------------------------------------ التسجيل

    private async Task<SubmissionAttempt> OpenAttemptAsync(
        ComplianceRecord record, bool isResolution, CancellationToken ct)
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
                // بصمة الحمولة وقت هذه المحاولة: مقارنتها بالأولى تكشف إعادة الختم.
                PayloadFingerprint = ComplianceDocumentFactory.Fingerprint(live.FrozenPayload),
                IsResolution = isResolution
            };

            live.AttemptCount++;
            if (isResolution) live.ResolutionAttemptCount++;

            // الانتقال والصف يُكتبان معاً وقبل النداء. هذه هي حالة الانتظار المرئية.
            await ComplianceJournal.TransitionAsync(uow, live, ComplianceStatus.Submitting,
                actor: isResolution ? "compliance.resolve" : "compliance.clearance",
                reasonAr: isResolution
                    ? $"محاولة حسم رقم {live.ResolutionAttemptCount} ببايتات مطابقة (بصمة {attempt.PayloadFingerprint[..12]}…)"
                    : $"محاولة إرسال رقم {attempt.AttemptNo} (بصمة {attempt.PayloadFingerprint[..12]}…)",
                reasonEn: isResolution
                    ? $"resolution attempt {live.ResolutionAttemptCount} with identical bytes"
                    : $"submission attempt {attempt.AttemptNo}",
                at: now, attempt: attempt.AttemptId, ct: token);

            await uow.InsertAttemptAsync(attempt, token);
            return attempt;
        }, ct);
    }

    private async Task RecordOutcomeAsync(
        ComplianceRecord record, SubmissionAttempt attempt, ClearanceOutcome outcome, CancellationToken ct)
    {
        var now = ComplianceCanonical.PgInstant(clock.GetUtcNow());

        await store.InTransactionAsync(async (uow, token) =>
        {
            var live = (await uow.GetAsync(record.DocumentId, token))!;

            attempt.CompletedAt = now;
            attempt.ProviderReference = outcome.ProviderReference;
            attempt.ProviderReportedDuplicate = outcome.RecognisedAsDuplicate;

            var (status, attemptOutcome, ar, en) = outcome.Disposition switch
            {
                ClearanceDisposition.Cleared =>
                    (ComplianceStatus.Accepted, AttemptOutcome.Accepted,
                     "قُبل المستند من الجهة", "cleared by the authority"),
                ClearanceDisposition.ClearedWithWarnings =>
                    (ComplianceStatus.AcceptedWithWarnings, AttemptOutcome.AcceptedWithWarnings,
                     "قُبل المستند بملاحظات", "cleared with warnings"),
                _ =>
                    (ComplianceStatus.Rejected, AttemptOutcome.Rejected,
                     "رُفض المستند — القيد المحاسبي باقٍ كما هو ولا يُمسّ",
                     "rejected — the journal entry stands untouched")
            };

            if (outcome.RecognisedAsDuplicate)
            {
                ar += " (تعرّف المزوّد عليه كإرسال مكرّر لإرسال سابق)";
                en += " (the provider recognised this as a duplicate of an earlier submission)";
            }

            attempt.Outcome = attemptOutcome;
            live.ProviderReference = outcome.ProviderReference;
            live.Notices = [.. outcome.Notices];
            if (!outcome.StampedDocument.IsEmpty) live.StampedDocument = outcome.StampedDocument.ToArray();

            await ComplianceJournal.TransitionAsync(uow, live, status,
                "compliance.clearance", ar, en, now, attempt.AttemptId, token);
            await uow.UpdateAttemptAsync(attempt, token);
        }, ct);
    }

    private async Task RecordFaultAsync(
        ComplianceRecord record, SubmissionAttempt attempt, ComplianceFault fault, Exception ex, CancellationToken ct)
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
            attempt.ProviderReference = fault.ProviderReference;

            switch (fault.Class)
            {
                case FaultClass.TransientNotSent:
                    attempt.Outcome = AttemptOutcome.NotSent;
                    var exhausted = live.AttemptCount >= settings.Retry.MaxAttempts;
                    await ComplianceJournal.TransitionAsync(uow, live,
                        exhausted ? ComplianceStatus.TransportFailed : ComplianceStatus.Queued,
                        "compliance.clearance",
                        exhausted
                            ? $"استُنفدت محاولات الإرسال ({live.AttemptCount}) والطلب لم يغادر: {fault.MessageAr}"
                            : $"الطلب لم يغادر ({fault.Code}) — إعادة المحاولة آمنة: {fault.MessageAr}",
                        exhausted
                            ? $"delivery attempts exhausted ({live.AttemptCount}); request never left: {fault.MessageEn}"
                            : $"request never left ({fault.Code}); retry is safe: {fault.MessageEn}",
                        now, attempt.AttemptId, token);
                    break;

                case FaultClass.Permanent:
                    attempt.Outcome = AttemptOutcome.Rejected;
                    live.Notices = [.. live.Notices, ComplianceNotice.Err(fault.Code, fault.MessageAr, fault.MessageEn)];
                    await ComplianceJournal.TransitionAsync(uow, live, ComplianceStatus.Rejected,
                        "compliance.clearance",
                        $"رفض نهائي: {fault.MessageAr} — القيد المحاسبي باقٍ كما هو",
                        $"permanent rejection: {fault.MessageEn} — the journal entry stands untouched",
                        now, attempt.AttemptId, token);
                    break;

                case FaultClass.Ambiguous:
                default:
                    attempt.Outcome = AttemptOutcome.Ambiguous;
                    live.ProviderReference ??= fault.ProviderReference;
                    await ComplianceJournal.TransitionAsync(uow, live, ComplianceStatus.Ambiguous,
                        "compliance.clearance",
                        $"مهلة غامضة: {fault.MessageAr}. غادر الطلب ولم يصل الجواب، فلا يُعرف هل تمّت المقاصة. " +
                        "إعادة الإرسال العمياء ممنوعة من هنا.",
                        $"ambiguous timeout: {fault.MessageEn}. The request left and no answer came back. " +
                        "Blind resubmission is refused from here.",
                        now, attempt.AttemptId, token);
                    break;
            }

            await uow.UpdateAttemptAsync(attempt, token);
            _ = ex;
        }, ct);
    }

    private async Task MoveToHumanReviewAsync(
        ComplianceRecord record, SubmissionDecision decision, CancellationToken ct)
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

    // ------------------------------------------------------------------ أدوات

    /// <summary>
    /// <b>التصنيف الافتراضي للمجهول هو «غامض»، لا «فشل».</b> استثناء غير معروف خرج من
    /// نداء شبكي قد يكون خرج بعد أن غادر الطلب؛ ومعاملته كفشل يفتح الباب لإرسال مكرّر.
    /// </summary>
    private static ComplianceFault Classify(Exception ex, CancellationToken outer) => ex switch
    {
        ComplianceTransportException cte => cte.Fault,

        OperationCanceledException when outer.IsCancellationRequested =>
            ComplianceFault.Ambiguous("cancelled",
                "أُلغيت العملية بعد أن غادر الطلب — الحالة لدى الجهة غير معروفة",
                "operation cancelled after the request left — the state at the authority is unknown"),

        OperationCanceledException or TimeoutException =>
            ComplianceFault.Ambiguous("timeout",
                "انتهت المهلة دون جواب",
                "the call timed out with no answer"),

        _ => ComplianceFault.Ambiguous("unknown",
                $"استثناء غير مصنَّف أثناء النداء: {ex.GetType().Name}",
                $"unclassified exception during the call: {ex.GetType().Name}")
    };

    private async Task<(ComplianceRecord Record, IReadOnlyList<SubmissionAttempt> Attempts)> LoadAsync(
        ComplianceDocumentId id, CancellationToken ct) =>
        await store.InTransactionAsync(async (uow, token) =>
        {
            var r = await uow.GetAsync(id, token)
                ?? throw new KeyNotFoundException($"لا سجل التزام للمستند {id}");
            var a = await uow.AttemptsAsync(id, token);
            return (r, a);
        }, ct);

    private async Task<ComplianceRecord> ReloadAsync(ComplianceDocumentId id, CancellationToken ct) =>
        (await LoadAsync(id, ct)).Record;

    private static ClearanceResult Describe(ComplianceRecord r, SubmissionDecision d)
    {
        var deliverable = r.IsAccepted;
        var (ar, en) = r.Status switch
        {
            ComplianceStatus.Accepted or ComplianceStatus.AcceptedWithWarnings =>
                ("تمّت المقاصة — يجوز تسليم المستند للمشتري",
                 "cleared — the document may be delivered to the buyer"),
            ComplianceStatus.Rejected =>
                ("رُفض المستند فلا يُسلَّم للمشتري. القيد المحاسبي مُرحَّل ولم يُمسّ، والمستند معزول عن الإقرار وعن أعمار الذمم.",
                 "rejected: do not deliver. The journal entry is posted and untouched; the document is quarantined from the VAT return and AR aging."),
            ComplianceStatus.Ambiguous =>
                ("لم يصل جواب من الجهة. لا يُسلَّم المستند، ولا يُعاد إرساله يدوياً — الحسم آلي أو بشري عبر لوحة المطابقة.",
                 "no answer from the authority. Do not deliver and do not resubmit by hand; resolution is automatic or human via the reconciliation board."),
            ComplianceStatus.NeedsHumanReview =>
                ($"يحتاج قراراً بشرياً: {r.HumanReviewReasonAr}", $"needs a human decision: {r.HumanReviewReasonEn}"),
            _ => (d.ReasonAr, d.ReasonEn)
        };

        return new ClearanceResult(
            r.DocumentId, r.Status, r.Notices,
            ComplianceStatusText.Ar(r.Status), ComplianceStatusText.En(r.Status),
            deliverable, ar, en);
    }
}
