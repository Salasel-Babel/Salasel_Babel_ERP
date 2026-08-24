using Babel.Compliance.Abstractions;
using Babel.Compliance.Model;

namespace Babel.Compliance.Pipeline;

/// <summary>ما الذي يُفعل بمستند وصل إلى آلية الإرسال — إرسال، أم حسم، أم توقّف، أم إنسان.</summary>
public enum SubmissionAction
{
    /// <summary>إرسال عادي. لا غموض سابقاً.</summary>
    Submit,

    /// <summary>حسم غموض عبر استعلام حالة. لا يُنشئ إرسالاً جديداً.</summary>
    ResolveByProbe,

    /// <summary>
    /// حسم غموض بإعادة إرسال <b>ببايتات مطابقة تماماً</b>، معتمداً على كشف تكرار
    /// من جانب المزوّد. مسموح فقط حين تُصرَّح القدرة وتكون البايتات مستقرة.
    /// </summary>
    ResolveByIdenticalResubmit,

    /// <summary>لا شيء يُفعل: المستند محسوم.</summary>
    Stop,

    /// <summary>تعذّر الحسم آلياً. طابور بشري.</summary>
    HumanReview
}

public sealed record SubmissionDecision(
    SubmissionAction Action,
    string ReasonAr,
    string ReasonEn);

/// <summary>
/// <b>حارس الحصانة.</b> هذا النوع هو الجواب على السؤال المركزي:
/// «الصندوق الصادر يعطي مرة على الأقل، والإرسال ليس حصيناً — فماذا تفعل إعادة المحاولة؟»
/// <para/>
/// القاعدة الحاكمة في سطر واحد:
/// <b>ما إن يصير المستند غامضاً، تتوقف إعادة المحاولة عن كونها إرسالاً وتصير حسماً.</b>
/// <para/>
/// وثلاث خصائص تحمي هذا كله، وكلها مثبتة في البناء لا هنا:
/// <list type="number">
///   <item><b>العدّاد يُخصَّص مرة واحدة عند البناء.</b> لا محاولة تحرق قيمة عدّاد،
///         فلا تُنتج المهلة الغامضة فجوة في السلسلة أبداً.</item>
///   <item><b>البايتات تُجمَّد عند أول ختم وتُخزَّن.</b> لا يُعاد توليد مصنوع مختوم.</item>
///   <item><b>صف المحاولة يُكتب قبل النداء.</b> فسقوط العملية في منتصف النداء
///         يترك أثراً يقرأه الحارس عند الإقلاع، بدل أن يختفي بلا أثر.</item>
/// </list>
/// </summary>
public sealed class SubmissionGuard(ComplianceSettings settings, TimeProvider clock)
{
    public SubmissionDecision Decide(
        ComplianceRecord record,
        IReadOnlyList<SubmissionAttempt> attempts,
        ProviderCapabilities capabilities)
    {
        var now = clock.GetUtcNow();

        if (ComplianceStatusMachine.IsSettled(record.Status))
            return new SubmissionDecision(SubmissionAction.Stop,
                $"المستند محسوم بالفعل: {ComplianceStatusText.Ar(record.Status)}",
                $"already settled: {ComplianceStatusText.En(record.Status)}");

        if (record.Status == ComplianceStatus.NeedsHumanReview)
            return new SubmissionDecision(SubmissionAction.HumanReview,
                "المستند في الطابور البشري بالفعل — لا إرسال آلي",
                "already in the human queue — no automatic submission");

        // ---- صف InFlight قديم = سقوط في منتصف النداء = غموض، لا محاولة قائمة -------
        var stale = attempts.FirstOrDefault(a => a.IsStale(now, settings.AttemptLease));

        // الغموض يُقرأ من **آخر** محاولة، لا من التاريخ كله: غموض حُسم لاحقاً بتأكيد
        // إيجابي («الجهة لا تعرف هذا المستند») لم يعد غموضاً، وإبقاؤه يجمّد المستند إلى الأبد.
        var last = attempts.Count == 0 ? null : attempts[^1];
        var hasAmbiguity = record.Status == ComplianceStatus.Ambiguous
                           || stale is not null
                           || last?.Outcome == AttemptOutcome.Ambiguous;

        if (!hasAmbiguity)
        {
            var live = attempts.FirstOrDefault(a => a.Outcome == AttemptOutcome.InFlight);
            if (live is not null)
                return new SubmissionDecision(SubmissionAction.Stop,
                    $"محاولة قائمة الآن (رقم {live.AttemptNo}) لم يتجاوز عمرها مهلة الإيجار — لا إرسال موازٍ",
                    $"attempt {live.AttemptNo} is in flight within its lease — no parallel submission");

            return new SubmissionDecision(SubmissionAction.Submit,
                "لا غموض سابق — إرسال عادي",
                "no prior ambiguity — ordinary submission");
        }

        // ---- من هنا فصاعداً: مسار حسم، لا مسار إرسال -------------------------------

        if (record.ResolutionAttemptCount >= settings.MaxResolutionAttempts)
            return new SubmissionDecision(SubmissionAction.HumanReview,
                $"استُنفدت محاولات الحسم ({record.ResolutionAttemptCount}/{settings.MaxResolutionAttempts}) " +
                "دون جواب قاطع من الجهة",
                $"resolution attempts exhausted ({record.ResolutionAttemptCount}/{settings.MaxResolutionAttempts})");

        if (capabilities.StatusQuery != StatusProbeSupport.NotSupported)
            return new SubmissionDecision(SubmissionAction.ResolveByProbe,
                "مهلة غامضة: يُستعلم عن الحالة بدل إعادة الإرسال",
                "ambiguous timeout: probe the status instead of resubmitting");

        // لا استعلام حالة. هذه هي الحالة المتوقَّعة، لا الاستثناء:
        // العميل الأنضج المفتوح المصدر لهذه المنظومة يعرض صفر GET.
        var byteStable = record.SealState == SealState.SealedLocally
                         || capabilities.GuaranteesByteStableRetransmission;

        if (settings.AllowIdenticalResubmitAsResolution &&
            capabilities.DeduplicatesBySubmissionFingerprint &&
            byteStable)
            return new SubmissionDecision(SubmissionAction.ResolveByIdenticalResubmit,
                "لا استعلام حالة، والمزوّد يكشف التكرار، والبايتات مطابقة — إعادة إرسال محدودة للحسم",
                "no status query, provider deduplicates, bytes are identical — one bounded resubmission to resolve");

        return new SubmissionDecision(SubmissionAction.HumanReview,
            BuildHumanReasonAr(capabilities, byteStable),
            BuildHumanReasonEn(capabilities, byteStable));
    }

    private static string BuildHumanReasonAr(ProviderCapabilities c, bool byteStable)
    {
        var why = new List<string>();
        if (c.StatusQuery == StatusProbeSupport.NotSupported) why.Add("لا يوجد استعلام حالة");
        if (!c.DeduplicatesBySubmissionFingerprint) why.Add("المزوّد لا يضمن كشف التكرار");
        if (!byteStable) why.Add("البايتات غير مطابقة بين المحاولات (المزوّد يعيد الختم في كل مرة)");
        return "تعذّر حسم الغموض آلياً: " + string.Join("، ", why) +
               ". لا يجوز إعادة الإرسال عمياء لأنها قد تُنشئ مستنداً مكرّراً لدى الجهة.";
    }

    private static string BuildHumanReasonEn(ProviderCapabilities c, bool byteStable)
    {
        var why = new List<string>();
        if (c.StatusQuery == StatusProbeSupport.NotSupported) why.Add("no status query");
        if (!c.DeduplicatesBySubmissionFingerprint) why.Add("provider gives no duplicate detection");
        if (!byteStable) why.Add("bytes differ between attempts (the provider re-seals each time)");
        return "ambiguity cannot be resolved automatically: " + string.Join("; ", why) +
               ". Blind resubmission is refused because it may create a duplicate at the authority.";
    }
}
