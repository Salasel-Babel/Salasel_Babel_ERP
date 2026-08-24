using Babel.Compliance.Abstractions;
using Babel.Compliance.FakeProvider;
using Babel.Compliance.Model;
using Babel.Compliance.Pipeline;
using Xunit;

namespace Babel.Compliance.Tests;

/// <summary>
/// <b>مشكلة الحصانة.</b> الصندوق الصادر يعطي «مرة على الأقل»، والإرسال ليس حصيناً،
/// ولا يوجد مفتاح حصانة موثَّق من جانب الجهة. هذه الاختبارات تقيس ما لدى <b>الجهة</b>،
/// لا ما لدى المُرسِل — لأن هذا هو الرقم الذي يهم.
/// </summary>
public class IdempotencyTests
{
    /// <summary>
    /// السيناريو القاتل: الجهة <b>سجّلت القبول</b> ثم انقطع الجواب.
    /// المطلوب إثباته: إعادة تشغيل مسار الإرسال <b>لا تُنشئ قبولاً ثانياً</b>،
    /// والعدّاد لم يتحرّك، والبايتات لم تتغيّر.
    /// </summary>
    [Fact]
    public async Task Ambiguous_after_accept_does_not_produce_a_second_acceptance()
    {
        using var h = new Harness(KeyCustody.SelfHeld, StatusProbeSupport.NotSupported);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        var doc = h.NewDocument(ComplianceFlow.Clearance, "INV-6001");
        h.Authority.Script(doc.DocumentUuid, FakeBehaviour.AmbiguousAfterAccept);

        var first = await h.Service.ClearAsync(doc, TestContext.Current.CancellationToken);

        Assert.Equal(ComplianceStatus.Ambiguous, first.Status);
        Assert.False(first.DocumentMayBeDelivered);
        // الجهة قبلت فعلاً — والمُرسِل لا يعرف ذلك.
        Assert.Equal(1, h.Authority.AcceptancesFor(doc.DocumentUuid));

        var counterAfterFirst = h.Record(doc.DocumentId).Counter;
        var fingerprintAfterFirst = h.Record(doc.DocumentId).SubmissionFingerprint;

        // الصندوق الصادر يعيد المحاولة ثلاث مرات. لا واحدة منها ترسل شيئاً.
        for (var i = 0; i < 3; i++)
            await h.Service.ContinueClearanceAsync(doc.DocumentId, TestContext.Current.CancellationToken);

        Assert.Equal(1, h.Authority.AcceptancesFor(doc.DocumentUuid));
        Assert.Equal(counterAfterFirst, h.Record(doc.DocumentId).Counter);
        Assert.Equal(fingerprintAfterFirst, h.Record(doc.DocumentId).SubmissionFingerprint);

        // ومآل المستند: طابور بشري بسبب مكتوب بلغة مفهومة، لا حلقة إعادة محاولة صامتة.
        var record = h.Record(doc.DocumentId);
        Assert.Equal(ComplianceStatus.NeedsHumanReview, record.Status);
        Assert.Contains("لا يوجد استعلام حالة", record.HumanReviewReasonAr);
        Assert.Contains("Blind resubmission is refused", record.HumanReviewReasonEn);

        h.Ledger.AssertUntouched();
    }

    /// <summary>حين يوجد استعلام حالة، يُحسم الغموض دون أي إعادة إرسال.</summary>
    [Fact]
    public async Task Ambiguity_is_resolved_by_probing_not_by_resubmitting()
    {
        using var h = new Harness(KeyCustody.SelfHeld, StatusProbeSupport.ByDocumentIdentity);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        var doc = h.NewDocument(ComplianceFlow.Clearance, "INV-6002");
        h.Authority.Script(doc.DocumentUuid, FakeBehaviour.AmbiguousAfterAccept);

        var first = await h.Service.ClearAsync(doc, TestContext.Current.CancellationToken);
        Assert.Equal(ComplianceStatus.Ambiguous, first.Status);

        var resolved = await h.Service.ContinueClearanceAsync(doc.DocumentId, TestContext.Current.CancellationToken);

        Assert.Equal(ComplianceStatus.Accepted, resolved.Status);
        Assert.Equal(1, h.Authority.AcceptancesFor(doc.DocumentUuid));   // ولا قبول ثانٍ

        var attempts = h.Store.PeekAttempts(doc.DocumentId);
        Assert.Equal(2, attempts.Count);
        Assert.False(attempts[0].IsResolution);
        Assert.True(attempts[1].IsResolution);          // الثانية حسم، لا إرسال
        Assert.Equal(AttemptOutcome.Ambiguous, attempts[0].Outcome);
        Assert.Equal(AttemptOutcome.Accepted, attempts[1].Outcome);

        h.Ledger.AssertUntouched();
    }

    /// <summary>
    /// الحالة المعاكسة: غادر الطلب ولم يُسجَّل شيء. الاستعلام يؤكد ذلك <b>إيجاباً</b>،
    /// فيعود الإرسال آمناً — ولا يزال القبول واحداً.
    /// </summary>
    [Fact]
    public async Task Probe_that_positively_reports_not_found_makes_resubmission_safe()
    {
        using var h = new Harness(KeyCustody.SelfHeld, StatusProbeSupport.ByDocumentIdentity);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        var doc = h.NewDocument(ComplianceFlow.Clearance, "INV-6003");
        h.Authority.Script(doc.DocumentUuid, FakeBehaviour.AmbiguousBeforeAccept);

        await h.Service.ClearAsync(doc, TestContext.Current.CancellationToken);
        Assert.Equal(ComplianceStatus.Ambiguous, h.Record(doc.DocumentId).Status);
        Assert.Equal(0, h.Authority.AcceptancesFor(doc.DocumentUuid));

        // الحسم: الجهة لا تعرف المستند ⇒ إعادة الإدراج في الطابور.
        await h.Service.ContinueClearanceAsync(doc.DocumentId, TestContext.Current.CancellationToken);
        Assert.Equal(ComplianceStatus.Queued, h.Record(doc.DocumentId).Status);

        // ثم إرسال عادي.
        var final = await h.Service.ContinueClearanceAsync(doc.DocumentId, TestContext.Current.CancellationToken);
        Assert.Equal(ComplianceStatus.Accepted, final.Status);
        Assert.Equal(1, h.Authority.AcceptancesFor(doc.DocumentUuid));
        Assert.Equal(1, h.Record(doc.DocumentId).Counter);   // العدّاد لم يتحرّك عبر كل ذلك

        h.Ledger.AssertUntouched();
    }

    /// <summary>
    /// سقوط العملية في منتصف النداء: يبقى صف محاولة <c>InFlight</c>.
    /// بعد انقضاء مهلة الإيجار يقرؤه الحارس <b>غموضاً</b>، لا محاولة قائمة.
    /// </summary>
    [Fact]
    public async Task A_stale_in_flight_attempt_is_read_as_ambiguity_after_a_crash()
    {
        var settings = new ComplianceSettings { AttemptLease = TimeSpan.FromMinutes(5) };
        using var h = new Harness(KeyCustody.SelfHeld, StatusProbeSupport.ByDocumentIdentity, settings: settings);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        var doc = h.NewDocument(ComplianceFlow.Clearance, "INV-6004");
        var record = await h.Factory.BuildAndQueueAsync(doc, TestContext.Current.CancellationToken);

        // محاكاة السقوط: نكتب ما يكتبه المُنسِّق قبل النداء بالضبط، ثم لا نُكمل.
        await h.Store.InTransactionAsync(async (uow, ct) =>
        {
            var live = (await uow.GetAsync(record.DocumentId, ct))!;
            live.Status = ComplianceStatus.Submitting;
            live.AttemptCount = 1;
            await uow.UpdateAsync(live, ct);
            await uow.InsertAttemptAsync(new SubmissionAttempt
            {
                AttemptId = AttemptId.New(),
                DocumentId = live.DocumentId,
                AttemptNo = 1,
                StartedAt = h.Clock.GetUtcNow(),
                PayloadFingerprint = live.SubmissionFingerprint
            }, ct);
        }, TestContext.Current.CancellationToken);

        // القبول وقع لدى الجهة أثناء النداء الضائع.
        h.Authority.RecordAcceptance(doc.DocumentUuid, Harness.Unit.Value, 1,
            record.FrozenPayload, record.SubmissionFingerprint, false, h.Clock.GetUtcNow());

        // داخل مهلة الإيجار: لا شيء يُفعل — لا إرسال موازٍ.
        var guard = new SubmissionGuard(settings, h.Clock);
        var inside = guard.Decide(h.Record(doc.DocumentId), h.Store.PeekAttempts(doc.DocumentId),
            h.Provider.Capabilities);
        Assert.Equal(SubmissionAction.Stop, inside.Action);

        // بعد انقضائها: غموض ⇒ حسم، لا إرسال.
        h.Clock.Advance(TimeSpan.FromMinutes(6));
        var after = guard.Decide(h.Record(doc.DocumentId), h.Store.PeekAttempts(doc.DocumentId),
            h.Provider.Capabilities);
        Assert.Equal(SubmissionAction.ResolveByProbe, after.Action);

        var resolved = await h.Service.ContinueClearanceAsync(doc.DocumentId, TestContext.Current.CancellationToken);
        Assert.Equal(ComplianceStatus.Accepted, resolved.Status);
        Assert.Equal(1, h.Authority.AcceptancesFor(doc.DocumentUuid));

        h.Ledger.AssertUntouched();
    }

    /// <summary>
    /// إعادة الإرسال ببايتات مطابقة كوسيلة حسم: مسموحة <b>فقط</b> حين يجتمع شرطان —
    /// وعد تعاقدي من المزوّد بكشف التكرار، وبايتات مستقرة بنيوياً.
    /// </summary>
    [Fact]
    public async Task Bounded_identical_resubmission_is_allowed_only_when_the_provider_promises_deduplication()
    {
        var settings = new ComplianceSettings { AllowIdenticalResubmitAsResolution = true };
        using var h = new Harness(KeyCustody.SelfHeld, StatusProbeSupport.NotSupported,
            deduplicates: true, settings: settings);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        var doc = h.NewDocument(ComplianceFlow.Clearance, "INV-6005");
        h.Authority.Script(doc.DocumentUuid, FakeBehaviour.AmbiguousAfterAccept);

        await h.Service.ClearAsync(doc, TestContext.Current.CancellationToken);
        Assert.Equal(ComplianceStatus.Ambiguous, h.Record(doc.DocumentId).Status);
        Assert.Equal(1, h.Authority.AcceptancesFor(doc.DocumentUuid));

        var resolved = await h.Service.ContinueClearanceAsync(doc.DocumentId, TestContext.Current.CancellationToken);

        Assert.Equal(ComplianceStatus.Accepted, resolved.Status);
        Assert.Equal(1, h.Authority.AcceptancesFor(doc.DocumentUuid));   // لا قبول ثانٍ

        var attempts = h.Store.PeekAttempts(doc.DocumentId);
        Assert.Equal(2, attempts.Count);
        Assert.True(attempts[1].ProviderReportedDuplicate);
        // البايتات مطابقة بين المحاولتين — وهي الخاصية التي جعلت الحسم ممكناً أصلاً.
        Assert.Equal(attempts[0].PayloadFingerprint, attempts[1].PayloadFingerprint);

        h.Ledger.AssertUntouched();
    }

    /// <summary>
    /// <b>القياس الحاسم في المقارنة بين الشكلين.</b>
    /// تحت «نحن نحوز»: البايتات المرسلة مطابقة في كل محاولة — خاصية بنيوية.
    /// تحت «المزوّد يحوز»: المزوّد يعيد الختم، وتوقيع ECDSA عشوائي، فالبايتات تختلف —
    /// ومعها يسقط كل كشف تكرار مبني على تطابق المحتوى.
    /// </summary>
    [Fact]
    public async Task Provider_held_custody_cannot_guarantee_byte_stable_retransmission()
    {
        // (أ) نحن نحوز المفتاح.
        using (var self = new Harness(KeyCustody.SelfHeld, StatusProbeSupport.ByDocumentIdentity))
        {
            await self.OnboardAsync(ct: TestContext.Current.CancellationToken);
            var doc = self.NewDocument(ComplianceFlow.Clearance, "INV-7001");
            self.Authority.Script(doc.DocumentUuid, FakeBehaviour.AmbiguousBeforeAccept);

            await self.Service.ClearAsync(doc, TestContext.Current.CancellationToken);
            await self.Service.ContinueClearanceAsync(doc.DocumentId, TestContext.Current.CancellationToken);
            await self.Service.ContinueClearanceAsync(doc.DocumentId, TestContext.Current.CancellationToken);

            Assert.True(self.Provider.Capabilities.ByteStableRetriesAreStructural);
            Assert.Equal(SealState.SealedLocally, self.Record(doc.DocumentId).SealState);
            Assert.Single(self.Authority.Accepted);
            // ما وصل الجهة يطابق ما جمّدناه: بصمة واحدة عبر كل شيء.
            Assert.Equal(
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    self.Record(doc.DocumentId).FrozenPayload)).ToLowerInvariant(),
                self.Authority.Accepted[0].TransmittedFingerprint);
        }

        // (ب) المزوّد يحوز المفتاح.
        using var held = new Harness(KeyCustody.ProviderHeld, StatusProbeSupport.ByDocumentIdentity);
        await held.OnboardAsync(ct: TestContext.Current.CancellationToken);
        var d2 = held.NewDocument(ComplianceFlow.Clearance, "INV-7002");

        // إرسالان ناجحان لمستندين مختلفين بنفس الجسم يُظهران أن الختم يقع عند الإرسال.
        await held.Service.ClearAsync(d2, TestContext.Current.CancellationToken);
        Assert.False(held.Provider.Capabilities.ByteStableRetriesAreStructural);
        Assert.Equal(SealState.UnsealedForProviderSeal, held.Record(d2.DocumentId).SealState);

        // ما وصل الجهة ليس ما خزّنّاه — لأن المزوّد أضاف ختمه بعد أن غادرت البايتات أيدينا.
        Assert.NotEqual(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                held.Record(d2.DocumentId).FrozenPayload)).ToLowerInvariant(),
            held.Authority.Accepted[0].TransmittedFingerprint);
    }

    /// <summary>مسار الإبلاغ يخضع للحارس نفسه: الغموض واحد مهما اختلفت الآلية.</summary>
    [Fact]
    public async Task Reporting_queue_never_blindly_resubmits_after_an_ambiguous_timeout()
    {
        using var h = new Harness(KeyCustody.SelfHeld, StatusProbeSupport.NotSupported);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        var doc = h.NewDocument(ComplianceFlow.Reporting, "SIMP-8001");
        h.Authority.Script(doc.DocumentUuid, FakeBehaviour.AmbiguousAfterAccept);

        await h.Service.QueueForReportingAsync(doc, TestContext.Current.CancellationToken);
        await h.Service.DrainReportingQueueAsync(10, TestContext.Current.CancellationToken);

        Assert.Equal(ComplianceStatus.Ambiguous, h.Record(doc.DocumentId).Status);
        Assert.Equal(1, h.Authority.AcceptancesFor(doc.DocumentUuid));

        // الطابور يحاول ثلاث مرات — ولا واحدة منها ترسل.
        for (var i = 0; i < 3; i++)
        {
            h.Clock.Advance(TimeSpan.FromMinutes(30));
            await h.Service.DrainReportingQueueAsync(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, h.Authority.AcceptancesFor(doc.DocumentUuid));
        Assert.Equal(ComplianceStatus.NeedsHumanReview, h.Record(doc.DocumentId).Status);

        h.Ledger.AssertUntouched();
    }

    /// <summary>
    /// <b>القياس المضاد.</b> لو أُطفئت الحماية وأُعيد الإرسال عمياءً بعد المهلة الغامضة،
    /// فالنتيجة قبولان لدى الجهة — أي فاتورة مكرّرة. هذا الاختبار يقيس الضرر ليُثبت
    /// أن المنع ليس احتياطاً نظرياً.
    /// </summary>
    [Fact]
    public async Task Without_the_guard_a_blind_retry_creates_a_real_duplicate_at_the_authority()
    {
        using var h = new Harness(KeyCustody.SelfHeld, StatusProbeSupport.NotSupported);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        var doc = h.NewDocument(ComplianceFlow.Clearance, "INV-9001");
        h.Authority.Script(doc.DocumentUuid, FakeBehaviour.AmbiguousAfterAccept);

        await h.Service.ClearAsync(doc, TestContext.Current.CancellationToken);
        Assert.Equal(1, h.Authority.AcceptancesFor(doc.DocumentUuid));

        // «إعادة المحاولة» كما يفعلها صندوق صادر ساذج: النداء نفسه مرة أخرى، بلا حارس.
        var record = h.Record(doc.DocumentId);
        await h.Provider.Clearance!.ClearAsync(new ClearanceRequest(
            record.DocumentId, record.DocumentUuid, record.Tenant, record.IssuingUnit,
            CredentialRef.None, record.Environment,
            new SealedPayload(record.SealState, record.FrozenPayload, null,
                Convert.FromHexString(record.SubmissionFingerprint)),
            new ChainSlot(record.Counter, record.PreviousHash),
            AttemptId.New(), 2, record.SubmissionFingerprint), TestContext.Current.CancellationToken);

        Assert.Equal(2, h.Authority.AcceptancesFor(doc.DocumentUuid));   // فاتورة مكرّرة لدى الجهة
    }
}
