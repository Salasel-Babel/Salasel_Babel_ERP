using System.Globalization;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Babel.Compliance.Abstractions;
using Babel.Compliance.Model;
using Babel.Compliance.Pipeline;
using Babel.Compliance.Zatca.Transport;
using Xunit;

namespace Babel.Compliance.Tests.Zatca;

/// <summary>
/// السلك: تصنيف الأعطال، وهوية الإرسال، والحصانة.
/// <para/>
/// <b>القاعدة التي يقيسها هذا الملف كله:</b> ما إن يصير المستند غامضاً، تتوقّف إعادة
/// المحاولة عن كونها إرسالاً وتصير حسماً. والفرق بين «لم يصل» و«لا أدري» هو الفرق بين
/// إعادة محاولة آمنة وفاتورة ضريبية مُصفَّاة مرتين لا تُحذف.
/// </summary>
public sealed class ZatcaTransportTests(ITestOutputHelper output)
{
    private static readonly Uri Endpoint = new("https://gw-fatoora.example.invalid/e-invoicing/simulation/invoices");

    // ── تصنيف الأعطال ───────────────────────────────────────────────────────

    /// <summary>
    /// <b>الحالة الوحيدة التي يُمنَح فيها «لم يُرسل»:</b> عطل مُثبَت قبل مغادرة الطلب.
    /// </summary>
    [Theory]
    [InlineData(SocketError.ConnectionRefused)]
    [InlineData(SocketError.HostNotFound)]
    [InlineData(SocketError.NetworkUnreachable)]
    public void A_failure_proven_to_precede_the_request_leaving_is_classified_as_not_sent(SocketError code)
    {
        ComplianceTransportException fault = ZatcaFaultClassifier.Classify(
            new HttpRequestException("x", new SocketException((int)code)), Endpoint);

        output.WriteLine($"{code} ⇒ {fault.Fault.Class}: {fault.Fault.MessageAr}");
        Assert.Equal(FaultClass.TransientNotSent, fault.Fault.Class);
    }

    /// <summary>
    /// <b>المهلة غامضة دائماً.</b> تصنيفها «لم يُرسل» بلا دليل هو ما يُنتج المستند المكرّر،
    /// ولا يظهر في الاختبار بل عند العميل.
    /// </summary>
    [Fact]
    public void A_timeout_is_always_ambiguous_and_never_transient()
    {
        foreach (Exception exception in new Exception[] { new TaskCanceledException(), new TimeoutException() })
        {
            ComplianceTransportException fault = ZatcaFaultClassifier.Classify(exception, Endpoint);
            output.WriteLine($"{exception.GetType().Name} ⇒ {fault.Fault.Class}");
            Assert.Equal(FaultClass.Ambiguous, fault.Fault.Class);
        }
    }

    /// <summary>
    /// عطل مقبس غير مُدرَج في قائمة «مُثبَت أنه لم يغادر» يُصنَّف غامضاً، لا عابراً.
    /// <b>الافتراض المتحفّظ هو الحماية نفسها.</b>
    /// </summary>
    [Fact]
    public void An_unlisted_socket_error_is_ambiguous_by_default_not_transient()
    {
        ComplianceTransportException fault = ZatcaFaultClassifier.Classify(
            new HttpRequestException("x", new SocketException((int)SocketError.ConnectionReset)), Endpoint);

        output.WriteLine($"ConnectionReset ⇒ {fault.Fault.Class}: {fault.Fault.MessageAr}");
        Assert.Equal(FaultClass.Ambiguous, fault.Fault.Class);
    }

    [Theory]
    [InlineData(400, FaultClass.Permanent)]
    [InlineData(401, FaultClass.Permanent)]
    [InlineData(422, FaultClass.Permanent)]
    [InlineData(500, FaultClass.Ambiguous)]
    [InlineData(502, FaultClass.Ambiguous)]
    [InlineData(503, FaultClass.Ambiguous)]
    [InlineData(504, FaultClass.Ambiguous)]
    public void Status_codes_are_split_between_a_final_refusal_and_an_unknown_outcome(int status, FaultClass expected)
    {
        ComplianceTransportException? fault = ZatcaFaultClassifier.ClassifyStatus(status, "{}", null);
        Assert.NotNull(fault);
        output.WriteLine(FormattableString.Invariant($"{status} ⇒ {fault!.Fault.Class}"));
        Assert.Equal(expected, fault.Fault.Class);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(202)]
    public void A_success_status_produces_no_fault(int status) =>
        Assert.Null(ZatcaFaultClassifier.ClassifyStatus(status, "{}", null));

    // ── هوية الإرسال ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>سؤال الهوية يُطرح في أربعة مواضع، وتُعلَن في واحد.</b> هذا الاختبار يُثبت أن
    /// المواضع الأربعة تتفق دائماً لأنها تُشتقّ من نفس السجل.
    /// </summary>
    [Fact]
    public async Task The_submission_identity_is_the_same_in_the_body_the_header_and_the_fingerprint()
    {
        using ZatcaHarness harness = new();
        await harness.OnboardAsync(TestContext.Current.CancellationToken);

        ComplianceDocument document = harness.NewDocument(ComplianceFlow.Clearance, "INV-0001");
        ClearanceResult result = await harness.Service.ClearAsync(document, TestContext.Current.CancellationToken);

        Assert.True(result.DocumentMayBeDelivered);

        (Uri _, IReadOnlyDictionary<string, string> headers, string body) = harness.Wire.Seen.Single();
        JsonNode parsed = JsonNode.Parse(body)!;

        string bodyUuid = parsed["uuid"]!.GetValue<string>();
        string idempotencyKey = headers["Idempotency-Key"];
        ComplianceRecord record = harness.Store.Peek(document.DocumentId)!;

        output.WriteLine("uuid في الجسم       : " + bodyUuid);
        output.WriteLine("مفتاح الإحكام       : " + idempotencyKey);
        output.WriteLine("بصمة الحمولة عندنا  : " + record.SubmissionFingerprint);
        output.WriteLine("بصمة الحمولة عند الجهة: " + harness.Wire.Recorded.Single().PayloadSha256);

        Assert.Equal(document.DocumentUuid.ToString("D", CultureInfo.InvariantCulture), bodyUuid);
        Assert.Contains(bodyUuid, idempotencyKey, StringComparison.Ordinal);
        Assert.Contains(record.Counter.ToString(CultureInfo.InvariantCulture), idempotencyKey, StringComparison.Ordinal);

        // والبايتات التي وصلت الجهة هي البايتات التي بصمناها — لا نسخة مُعاد توليدها.
        Assert.Equal(record.SubmissionFingerprint, harness.Wire.Recorded.Single().PayloadSha256);
    }

    /// <summary>
    /// مفتاح الإحكام <b>لا يتغيّر بين المحاولات</b>. مفتاحٌ يتغيّر مع كل محاولة ليس مفتاح إحكام.
    /// </summary>
    [Fact]
    public async Task The_idempotency_key_does_not_change_between_attempts_on_the_same_document()
    {
        using ZatcaHarness harness = new();
        await harness.OnboardAsync(TestContext.Current.CancellationToken);

        // المحاولة الأولى: الطلب لم يغادر — إعادة المحاولة آمنة تماماً.
        harness.Wire.Script(WireBehaviour.ConnectionRefused);

        ComplianceDocument document = harness.NewDocument(ComplianceFlow.Clearance, "INV-0002");
        await harness.Service.ClearAsync(document, TestContext.Current.CancellationToken);
        await harness.Service.ContinueClearanceAsync(document.DocumentId, TestContext.Current.CancellationToken);

        List<string> keys = [.. harness.Wire.Seen.Select(s => s.Headers["Idempotency-Key"])];
        List<string> bodies = [.. harness.Wire.Seen.Select(s => s.Body)];

        output.WriteLine("عدد النداءات: " + keys.Count.ToString(CultureInfo.InvariantCulture));
        foreach (string key in keys)
        {
            output.WriteLine("  " + key);
        }

        Assert.Equal(2, keys.Count);
        Assert.Single(keys.Distinct(StringComparer.Ordinal));

        // والبايتات نفسها أيضاً: لا يُعاد توليد مصنوع مختوم.
        Assert.Single(bodies.Distinct(StringComparer.Ordinal));
    }

    // ── الحصانة ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>السيناريو الذي يُسقط الأنظمة:</b> الجهة سجّلت القبول ثم ضاع الجواب.
    /// المطلوب: <b>لا إرسال ثانٍ</b>، ومستند واحد لدى الجهة، وطابور بشري.
    /// </summary>
    [Fact]
    public async Task An_answer_lost_after_the_authority_recorded_never_produces_a_second_submission()
    {
        using ZatcaHarness harness = new();
        await harness.OnboardAsync(TestContext.Current.CancellationToken);

        harness.Wire.Script(WireBehaviour.TimeoutAfterRecording);

        ComplianceDocument document = harness.NewDocument(ComplianceFlow.Clearance, "INV-0003");
        ClearanceResult first = await harness.Service.ClearAsync(document, TestContext.Current.CancellationToken);

        output.WriteLine("النتيجة الأولى: " + first.GuidanceAr);
        Assert.False(first.DocumentMayBeDelivered);
        Assert.Equal(ComplianceStatus.Ambiguous, harness.Store.Peek(document.DocumentId)!.Status);

        // كل محاولة تالية: مسار حسم لا مسار إرسال.
        for (int attempt = 0; attempt < 4; attempt++)
        {
            await harness.Service.ContinueClearanceAsync(document.DocumentId, TestContext.Current.CancellationToken);
        }

        output.WriteLine("ما سجّلته الجهة فعلاً:\n" + harness.Wire.Describe());
        output.WriteLine("عدد النداءات على السلك: "
            + harness.Wire.Seen.Count.ToString(CultureInfo.InvariantCulture));

        Assert.Equal(1, harness.Wire.AcceptancesFor(document.DocumentUuid.ToString("D", CultureInfo.InvariantCulture)));
        Assert.Single(harness.Wire.Seen);

        ComplianceRecord record = harness.Store.Peek(document.DocumentId)!;
        output.WriteLine("الحالة النهائية: " + ComplianceStatusText.Ar(record.Status));
        Assert.Equal(ComplianceStatus.NeedsHumanReview, record.Status);
        output.WriteLine("سبب الطابور البشري: " + record.HumanReviewReasonAr);

        harness.Ledger.AssertUntouched();
    }

    /// <summary>
    /// <b>إثبات لافراغ الحماية السابقة:</b> إعادة إرسال عمياء — بتجاوز الحارس — تُنشئ
    /// مستنداً مكرّراً حقيقياً لدى الجهة. لولا هذا لما عُرف أن الحماية تحمي شيئاً.
    /// </summary>
    [Fact]
    public async Task Without_the_guard_a_blind_retry_creates_a_real_duplicate_at_the_authority()
    {
        using ZatcaHarness harness = new();
        await harness.OnboardAsync(TestContext.Current.CancellationToken);

        harness.Wire.Script(WireBehaviour.TimeoutAfterRecording);

        ComplianceDocument document = harness.NewDocument(ComplianceFlow.Clearance, "INV-0004");
        await harness.Service.ClearAsync(document, TestContext.Current.CancellationToken);

        string uuid = document.DocumentUuid.ToString("D", CultureInfo.InvariantCulture);
        Assert.Equal(1, harness.Wire.AcceptancesFor(uuid));

        // تجاوز الحارس: نداء مباشر على القناة، كما تفعل أي حلقة إعادة محاولة ساذجة.
        ComplianceRecord record = harness.Store.Peek(document.DocumentId)!;
        await harness.Provider.Clearance!.ClearAsync(new ClearanceRequest(
            record.DocumentId, record.DocumentUuid, record.Tenant, record.IssuingUnit,
            harness.Credential, record.Environment,
            new SealedPayload(record.SealState, record.FrozenPayload, null, ReadOnlyMemory<byte>.Empty),
            new ChainSlot(record.Counter, record.PreviousHash),
            AttemptId.New(), record.AttemptCount + 1, record.SubmissionFingerprint),
            TestContext.Current.CancellationToken);

        output.WriteLine("ما سجّلته الجهة بعد الإعادة العمياء:\n" + harness.Wire.Describe());
        Assert.Equal(2, harness.Wire.AcceptancesFor(uuid));
        output.WriteLine("‼ مستندان لدى الجهة بنفس المعرّف ونفس العدّاد — وهذا ما يمنعه الحارس.");
    }

    /// <summary>
    /// عطل «لم يُرسل» <b>يجوز</b> أن يُعاد فوراً: لا خطر تكرار، لأن شيئاً لم يصل.
    /// </summary>
    [Fact]
    public async Task A_request_that_never_left_is_retried_immediately_and_the_authority_sees_exactly_one()
    {
        using ZatcaHarness harness = new();
        await harness.OnboardAsync(TestContext.Current.CancellationToken);

        harness.Wire.Script(WireBehaviour.ConnectionRefused, WireBehaviour.ConnectionRefused);

        ComplianceDocument document = harness.NewDocument(ComplianceFlow.Clearance, "INV-0005");
        await harness.Service.ClearAsync(document, TestContext.Current.CancellationToken);
        await harness.Service.ContinueClearanceAsync(document.DocumentId, TestContext.Current.CancellationToken);
        ClearanceResult third = await harness.Service.ContinueClearanceAsync(
            document.DocumentId, TestContext.Current.CancellationToken);

        output.WriteLine("النتيجة الثالثة: " + third.GuidanceAr);
        output.WriteLine("ما سجّلته الجهة:\n" + harness.Wire.Describe());

        Assert.True(third.DocumentMayBeDelivered);
        Assert.Equal(1, harness.Wire.AcceptancesFor(document.DocumentUuid.ToString("D", CultureInfo.InvariantCulture)));
        harness.Ledger.AssertUntouched();
    }

    /// <summary>
    /// الرفض النهائي لا يُعاد إرساله بنفس الحمولة: إعادته عبث، والملاحظات تصل للمستخدم بلغته.
    /// </summary>
    [Fact]
    public async Task A_final_refusal_carries_its_notices_to_the_user_and_is_not_retried()
    {
        using ZatcaHarness harness = new();
        await harness.OnboardAsync(TestContext.Current.CancellationToken);

        harness.Wire.Script(WireBehaviour.BadRequest);

        ComplianceDocument document = harness.NewDocument(ComplianceFlow.Clearance, "INV-0006");
        ClearanceResult result = await harness.Service.ClearAsync(document, TestContext.Current.CancellationToken);

        output.WriteLine("النتيجة: " + result.GuidanceAr);
        Assert.False(result.DocumentMayBeDelivered);

        ComplianceRecord record = harness.Store.Peek(document.DocumentId)!;
        output.WriteLine("الحالة: " + ComplianceStatusText.Ar(record.Status));
        Assert.Equal(0, harness.Wire.AcceptancesFor(document.DocumentUuid.ToString("D", CultureInfo.InvariantCulture)));
        harness.Ledger.AssertUntouched();
    }

    // ── المسار المتعاكس ─────────────────────────────────────────────────────

    /// <summary>
    /// مسار الإبلاغ: <b>لا انتظار</b>. المستند صادر ويُسلَّم للعميل، والإرسال يقع في عامل خلفي.
    /// وهذا ما يجعل نقطة البيع دون اتصال ممكنة أصلاً.
    /// </summary>
    [Fact]
    public async Task The_reporting_path_returns_before_any_network_call_happens()
    {
        using ZatcaHarness harness = new();
        await harness.OnboardAsync(TestContext.Current.CancellationToken);

        ComplianceDocument document = harness.NewDocument(ComplianceFlow.Reporting, "SIM-0001");
        ReportingReceipt receipt = await harness.Service.QueueForReportingAsync(
            document, TestContext.Current.CancellationToken);

        output.WriteLine("الإيصال: " + receipt.MessageAr);
        output.WriteLine("نداءات السلك حتى الآن: "
            + harness.Wire.Seen.Count.ToString(CultureInfo.InvariantCulture));

        Assert.Empty(harness.Wire.Seen);

        int drained = await harness.Service.DrainReportingQueueAsync(10, TestContext.Current.CancellationToken);
        output.WriteLine("استُنزف من الطابور: " + drained.ToString(CultureInfo.InvariantCulture));

        Assert.Single(harness.Wire.Seen);
        Assert.Contains("reporting", harness.Wire.Seen.Single().Endpoint.AbsolutePath, StringComparison.Ordinal);
        harness.Ledger.AssertUntouched();
    }

    [Fact]
    public async Task Calling_the_wrong_path_for_a_document_is_refused_by_the_signature_itself()
    {
        using ZatcaHarness harness = new();
        await harness.OnboardAsync(TestContext.Current.CancellationToken);

        ComplianceDocument reporting = harness.NewDocument(ComplianceFlow.Reporting, "SIM-0002");

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await harness.Service.ClearAsync(reporting, TestContext.Current.CancellationToken));

        output.WriteLine("رُفض: " + error.Message);
        Assert.Contains("QueueForReportingAsync", error.Message, StringComparison.Ordinal);
    }
}
