using System.Security.Cryptography;
using Babel.Compliance.Abstractions;

namespace Babel.Compliance.FakeProvider;

/// <summary>
/// ما يفعله المزوّد الوهمي بالحمولة قبل تسليمها «للجهة» — وهو الفرق العملي
/// الوحيد بين شكلَي الحيازة على السلك.
/// </summary>
internal static class FakeWire
{
    /// <summary>
    /// يحوّل الحمولة إلى البايتات التي تصل الجهة فعلاً.
    /// <list type="bullet">
    ///   <item><b>مختومة عندنا</b> ⇒ تُمرَّر كما هي. مطابقة بايتياً في كل محاولة.</item>
    ///   <item><b>غير مختومة</b> ⇒ <b>المزوّد يختمها الآن</b>، بتوقيع ECDSA جديد في كل محاولة.
    ///         النتيجة: بايتات مختلفة في كل مرة، حتى لو كان المستند نفسه حرفياً.</item>
    /// </list>
    /// </summary>
    public static byte[] Transmit(SealedPayload payload, EphemeralKeyVault vault, CredentialRef credential)
    {
        if (payload.State == SealState.SealedLocally) return payload.Bytes.ToArray();

        var key = vault.Key(credential);
        var signature = key.SignData(payload.Bytes.Span, HashAlgorithmName.SHA256);
        var body = System.Text.Encoding.UTF8.GetString(payload.Bytes.Span);
        var stamped = body + "<ProviderSeal>" + Convert.ToBase64String(signature) + "</ProviderSeal>";
        return new System.Text.UTF8Encoding(false).GetBytes(stamped);
    }
}

/// <summary>قناة مقاصة وهمية. حاجزة، وتعيد نسخة «مختومة».</summary>
public sealed class FakeClearanceChannel(
    FakeAuthority authority,
    EphemeralKeyVault vault,
    Func<ProviderCapabilities> capabilities,
    TimeProvider clock) : IClearanceChannel
{
    public async ValueTask<ClearanceOutcome> ClearAsync(ClearanceRequest request, CancellationToken cancellationToken)
    {
        var caps = capabilities();
        var now = clock.GetUtcNow();
        var behaviour = authority.Next(request.DocumentUuid);

        if (behaviour == FakeBehaviour.TransientNotSent)
            throw new ComplianceTransportException(ComplianceFault.NotSent(
                "connect-refused", "تعذّر فتح الاتصال — الطلب لم يغادر", "connection refused — the request never left"));

        if (behaviour == FakeBehaviour.PermanentFault)
            throw new ComplianceTransportException(ComplianceFault.Permanent(
                "invalid-document", "رفض نهائي: المستند غير مقبول شكلاً", "permanent: the document is structurally invalid"));

        // كشف تكرار على مستوى المزوّد: وعد تعاقدي، لا خاصية بنيوية. يُقرأ من القدرات.
        if (caps.DeduplicatesBySubmissionFingerprint)
        {
            var prior = authority.Accepted.FirstOrDefault(e => e.SubmissionFingerprint == request.SubmissionFingerprint);
            if (prior is not null)
                return new ClearanceOutcome(
                    prior.Warnings ? ClearanceDisposition.ClearedWithWarnings : ClearanceDisposition.Cleared,
                    [ComplianceNotice.Info("duplicate", "إرسال مكرّر لمستند سبق قبوله", "duplicate of an already accepted submission")],
                    now, StampFor(prior), prior.Reference, RecognisedAsDuplicate: true);
        }

        var transmitted = FakeWire.Transmit(request.Payload, vault, request.Credential);

        if (behaviour == FakeBehaviour.AmbiguousBeforeAccept)
            throw new ComplianceTransportException(ComplianceFault.Ambiguous(
                "read-timeout",
                "غادر الطلب ولم يصل جواب (لم تُسجّل الجهة شيئاً — لكن هذا غير معروف للمُرسِل)",
                "the request left and no answer came back (nothing was recorded — but the sender cannot know that)"));

        if (behaviour == FakeBehaviour.Reject)
            return new ClearanceOutcome(ClearanceDisposition.Rejected,
                [ComplianceNotice.Err("BABEL-FAKE-REJECT",
                    "رفض من الجهة الوهمية لغرض الاختبار", "rejected by the fake authority for test purposes")],
                now, ReadOnlyMemory<byte>.Empty, ProviderReference: null);

        var warnings = behaviour == FakeBehaviour.AcceptWithWarnings;
        var (reference, duplicate) = authority.RecordAcceptance(
            request.DocumentUuid, request.IssuingUnit.Value, request.Chain.Counter,
            transmitted, request.SubmissionFingerprint, warnings, now);

        if (behaviour == FakeBehaviour.AmbiguousAfterAccept)
        {
            // القبول سُجِّل. الجواب لم يصل. هذا هو السيناريو الذي يجب أن يصمد أمامه التصميم.
            await Task.Yield();
            throw new ComplianceTransportException(ComplianceFault.Ambiguous(
                "read-timeout-after-write",
                "غادر الطلب وسُجِّل لدى الجهة، ثم انقطع الجواب — إعادة الإرسال هنا تُنشئ مستنداً مكرّراً",
                "the request left and WAS recorded, then the answer was lost — resubmitting here creates a duplicate",
                providerRef: null));
        }

        return new ClearanceOutcome(
            warnings ? ClearanceDisposition.ClearedWithWarnings : ClearanceDisposition.Cleared,
            warnings
                ? [ComplianceNotice.Warn("BABEL-FAKE-WARN", "قبول بملاحظات", "accepted with warnings")]
                : [],
            now,
            StampFor(authority.Find(request.DocumentUuid)!),
            reference,
            duplicate);
    }

    private static ReadOnlyMemory<byte> StampFor(AuthorityLedgerEntry entry) =>
        new System.Text.UTF8Encoding(false).GetBytes(
            $"<ClearedInvoice reference=\"{entry.Reference}\" counter=\"{entry.Counter}\" />");
}

/// <summary>قناة إبلاغ وهمية. لا نسخة مختومة عائدة — الفرق البنيوي محفوظ في النوع نفسه.</summary>
public sealed class FakeReportingChannel(
    FakeAuthority authority,
    EphemeralKeyVault vault,
    Func<ProviderCapabilities> capabilities,
    TimeProvider clock) : IReportingChannel
{
    public async ValueTask<ReportingAcknowledgement> ReportAsync(
        ReportingSubmission submission, CancellationToken cancellationToken)
    {
        var caps = capabilities();
        var now = clock.GetUtcNow();
        var behaviour = authority.Next(submission.DocumentUuid);

        if (behaviour == FakeBehaviour.TransientNotSent)
            throw new ComplianceTransportException(ComplianceFault.NotSent(
                "connect-refused", "تعذّر فتح الاتصال — الطلب لم يغادر", "connection refused — the request never left"));

        if (behaviour == FakeBehaviour.PermanentFault)
            throw new ComplianceTransportException(ComplianceFault.Permanent(
                "invalid-document", "رفض نهائي", "permanent rejection"));

        if (caps.DeduplicatesBySubmissionFingerprint)
        {
            var prior = authority.Accepted.FirstOrDefault(e => e.SubmissionFingerprint == submission.SubmissionFingerprint);
            if (prior is not null)
                return new ReportingAcknowledgement(
                    prior.Warnings ? ReportingDisposition.AcceptedWithWarnings : ReportingDisposition.Accepted,
                    [ComplianceNotice.Info("duplicate", "إرسال مكرّر لمستند سبق قبوله", "duplicate of an already accepted submission")],
                    now, prior.Reference, RecognisedAsDuplicate: true);
        }

        var transmitted = FakeWire.Transmit(submission.Payload, vault, submission.Credential);

        if (behaviour == FakeBehaviour.AmbiguousBeforeAccept)
            throw new ComplianceTransportException(ComplianceFault.Ambiguous(
                "read-timeout", "غادر الطلب ولم يصل جواب", "the request left and no answer came back"));

        if (behaviour == FakeBehaviour.Reject)
            return new ReportingAcknowledgement(ReportingDisposition.Rejected,
                [ComplianceNotice.Err("BABEL-FAKE-REJECT",
                    "رفض من الجهة الوهمية — المستند صادر فعلاً ويستوجب تصحيحاً",
                    "rejected by the fake authority — the document was already issued and needs correction")],
                now, null);

        var warnings = behaviour == FakeBehaviour.AcceptWithWarnings;
        var (reference, duplicate) = authority.RecordAcceptance(
            submission.DocumentUuid, submission.IssuingUnit.Value, submission.Chain.Counter,
            transmitted, submission.SubmissionFingerprint, warnings, now);

        if (behaviour == FakeBehaviour.AmbiguousAfterAccept)
        {
            await Task.Yield();
            throw new ComplianceTransportException(ComplianceFault.Ambiguous(
                "read-timeout-after-write",
                "سُجِّل الإبلاغ لدى الجهة ثم انقطع الجواب",
                "the report WAS recorded and then the answer was lost"));
        }

        return new ReportingAcknowledgement(
            warnings ? ReportingDisposition.AcceptedWithWarnings : ReportingDisposition.Accepted,
            warnings ? [ComplianceNotice.Warn("BABEL-FAKE-WARN", "قبول بملاحظات", "accepted with warnings")] : [],
            now, reference, duplicate);
    }
}

/// <summary>
/// استعلام حالة وهمي. <b>يُركَّب أو لا يُركَّب</b> — وغيابه هو الحالة المتوقَّعة،
/// لأن العميل الأنضج المفتوح المصدر لهذه المنظومة يعرض صفر GET.
/// </summary>
public sealed class FakeStatusQuery(FakeAuthority authority, StatusProbeSupport support, TimeProvider clock)
    : IComplianceStatusQuery
{
    public StatusProbeSupport Support => support;

    public ValueTask<StatusProbeResult> ProbeAsync(StatusProbe probe, CancellationToken cancellationToken)
    {
        if (support == StatusProbeSupport.NotSupported)
            throw new NotSupportedException("لا يوجد استعلام حالة لدى هذا المزوّد");

        var entry = authority.Find(probe.DocumentUuid);
        var now = clock.GetUtcNow();

        if (entry is null)
            return ValueTask.FromResult(new StatusProbeResult(
                ProbedState.NotFound, [], now, ReadOnlyMemory<byte>.Empty));

        return ValueTask.FromResult(new StatusProbeResult(
            entry.Warnings ? ProbedState.AcceptedWithWarnings : ProbedState.Accepted,
            [ComplianceNotice.Info("probe", "الحالة لدى الجهة: مقبول", "state at the authority: accepted")],
            now,
            new System.Text.UTF8Encoding(false).GetBytes(
                $"<ClearedInvoice reference=\"{entry.Reference}\" counter=\"{entry.Counter}\" />"),
            entry.Reference));
    }
}
