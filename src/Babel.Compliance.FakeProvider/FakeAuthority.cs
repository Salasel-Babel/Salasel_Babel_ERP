using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using Babel.Compliance.Abstractions;

namespace Babel.Compliance.FakeProvider;

/// <summary>ما تفعله «الجهة» الوهمية في المحاولة التالية على مستند بعينه.</summary>
public enum FakeBehaviour
{
    Accept,
    AcceptWithWarnings,
    Reject,

    /// <summary>
    /// <b>السيناريو الذي يُسقط الأنظمة:</b> الجهة <b>تُسجّل القبول</b> ثم ينقطع الجواب.
    /// المستند صُفِّي فعلاً، والمُرسِل لا يعرف. إعادة الإرسال هنا تُنشئ مستنداً مكرّراً حقيقياً.
    /// </summary>
    AmbiguousAfterAccept,

    /// <summary>وصل الطلب ولم يُسجَّل شيء، ثم انقطع الجواب. إعادة الإرسال هنا صحيحة — لكن لا يمكن معرفة ذلك.</summary>
    AmbiguousBeforeAccept,

    /// <summary>الطلب لم يغادر أصلاً. إعادة المحاولة آمنة تماماً.</summary>
    TransientNotSent,

    /// <summary>رفض نهائي مفهوم.</summary>
    PermanentFault
}

/// <summary>ما سجّلته «الجهة» فعلاً. هذا هو مصدر الحقيقة الذي تقيس عليه الاختبارات.</summary>
public sealed record AuthorityLedgerEntry(
    Guid DocumentUuid,
    string IssuingUnit,
    long Counter,
    string TransmittedFingerprint,
    string SubmissionFingerprint,
    string Reference,
    bool Warnings,
    DateTimeOffset At);

/// <summary>
/// «جهة» وهمية بحالة داخلية. تُسجّل ما قبلته فعلاً، بغضّ النظر عمّا رآه المُرسِل —
/// وهذا هو بيت القصيد: <b>اختبارات الحصانة تقيس ما لدى الجهة، لا ما لدى المُرسِل.</b>
/// </summary>
public sealed class FakeAuthority
{
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<FakeBehaviour>> _script = new();
    private readonly ConcurrentBag<AuthorityLedgerEntry> _accepted = [];
    private readonly Lock _gate = new();

    /// <summary>
    /// هل تكشف الجهة تكرار بايتات متطابقة؟ <b>الافتراضي: لا.</b>
    /// افتراض العكس بلا وثيقة رسمية هو بالضبط نوع الافتراض الذي يُنتج فواتير مكرّرة في الإنتاج.
    /// </summary>
    [Provisional("هل تملك الجهة أي كشف تكرار من جانبها، وعلى أي مفتاح",
        DerivedFrom = "لا مصدر — لا يوجد مفتاح حصانة موثَّق من جانب الجهة",
        Risk = ProvisionalRisk.Structural,
        VerifyBy = "مواصفة الواجهة: هل توجد ترويسة idempotency، وهل تُرفض إعادة الإرسال المطابق")]
    public bool DeduplicatesIdenticalBytes { get; set; }

    public IReadOnlyList<AuthorityLedgerEntry> Accepted => [.. _accepted];

    public int AcceptancesFor(Guid documentUuid) => _accepted.Count(e => e.DocumentUuid == documentUuid);

    /// <summary>يبرمج سلوك المحاولات المتتالية على مستند. ما بعد آخر عنصر: قبول.</summary>
    public void Script(Guid documentUuid, params FakeBehaviour[] behaviours)
    {
        var q = _script.GetOrAdd(documentUuid, _ => new ConcurrentQueue<FakeBehaviour>());
        foreach (var b in behaviours) q.Enqueue(b);
    }

    public FakeBehaviour Next(Guid documentUuid) =>
        _script.TryGetValue(documentUuid, out var q) && q.TryDequeue(out var b) ? b : FakeBehaviour.Accept;

    /// <summary>
    /// يسجّل قبولاً. يعيد <c>(reference, duplicate)</c>؛ و<c>duplicate</c> صحيح فقط
    /// حين يكون كشف التكرار مُفعَّلاً <b>و</b> البايتات مطابقة لإرسال سابق.
    /// </summary>
    public (string Reference, bool Duplicate) RecordAcceptance(
        Guid documentUuid, string unit, long counter,
        ReadOnlySpan<byte> transmitted, string submissionFingerprint, bool warnings, DateTimeOffset at)
    {
        var fingerprint = Convert.ToHexString(SHA256.HashData(transmitted)).ToLowerInvariant();

        lock (_gate)
        {
            if (DeduplicatesIdenticalBytes)
            {
                var prior = _accepted.FirstOrDefault(e => e.TransmittedFingerprint == fingerprint);
                if (prior is not null) return (prior.Reference, true);
            }

            var reference = string.Create(CultureInfo.InvariantCulture,
                $"FAKE-{unit}-{counter}-{_accepted.Count + 1:D4}");
            _accepted.Add(new AuthorityLedgerEntry(
                documentUuid, unit, counter, fingerprint, submissionFingerprint, reference, warnings, at));
            return (reference, false);
        }
    }

    public AuthorityLedgerEntry? Find(Guid documentUuid) =>
        _accepted.OrderBy(e => e.At).FirstOrDefault(e => e.DocumentUuid == documentUuid);
}
