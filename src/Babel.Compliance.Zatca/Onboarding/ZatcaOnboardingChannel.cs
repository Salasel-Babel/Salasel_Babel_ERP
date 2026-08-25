using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Babel.Compliance.Abstractions;
using Babel.Compliance.Zatca.Signing;
using Babel.Compliance.Zatca.Transport;

namespace Babel.Compliance.Zatca.Onboarding;

/// <summary>
/// حالة التسجيل بين الخطوتين: معرّف طلب شهادة الامتثال، الذي تحتاجه خطوة شهادة الإنتاج.
/// <para/>
/// <b>وهذه فجوة مُعلَنة في العقد لا في التنفيذ:</b> <c>CertificateGrant</c> لا تحمل موضعاً
/// لمرجع من جانب المزوّد، فلا سبيل لتمرير هذا المعرّف عبر الحدّ. التنفيذ المرافق يحفظه
/// في الذاكرة، و<b>هذا لا يكفي للإنتاج</b>: الخطوتان قد تفصل بينهما ساعات وإعادة تشغيل،
/// وضياع المعرّف يعني إعادة التسجيل من الصفر لتلك الوحدة. البند مُسجَّل في
/// <c>docs/evidence/verification-debt.md</c>.
/// </summary>
public interface IZatcaOnboardingState
{
    void Remember(CredentialRef credential, string complianceRequestId, OnboardingStage stage);

    string? RequestIdOf(CredentialRef credential);

    OnboardingStage StageOf(CredentialRef credential);
}

/// <summary>حالة تسجيل في الذاكرة. للاختبار وللتشغيل داخل عملية واحدة فقط.</summary>
public sealed class InMemoryOnboardingState : IZatcaOnboardingState
{
    private readonly ConcurrentDictionary<string, (string RequestId, OnboardingStage Stage)> _state =
        new(StringComparer.Ordinal);

    public void Remember(CredentialRef credential, string complianceRequestId, OnboardingStage stage) =>
        _state[credential.Value] = (complianceRequestId, stage);

    public string? RequestIdOf(CredentialRef credential) =>
        _state.TryGetValue(credential.Value, out (string RequestId, OnboardingStage Stage) entry) ? entry.RequestId : null;

    public OnboardingStage StageOf(CredentialRef credential) =>
        _state.TryGetValue(credential.Value, out (string RequestId, OnboardingStage Stage) entry)
            ? entry.Stage
            : OnboardingStage.NotStarted;
}

/// <summary>مستند تمرين واحد لفحوصات الامتثال: بايتاته المختومة وبصمته ومعرّفه.</summary>
public sealed record ComplianceExerciseDocument(Guid Uuid, string InvoiceHashBase64, ReadOnlyMemory<byte> SignedInvoice);

/// <summary>
/// <b>دورة حياة الشهادة: طلب توقيع، ثم شهادة امتثال، ثم فحوص، ثم شهادة إنتاج.</b>
/// <para/>
/// والنقطة التي لا رجعة فيها هي السطر الأول: <b>المفتاح يُولَّد في خزينتنا ولا يغادرها</b>.
/// هذا هو شكل «نحن نحوز المفتاح»، وقد اختاره المالك بقرار «نبني بأنفسنا لا نشتري مزوّداً
/// معتمداً». والانتقال عنه لاحقاً ليس تغيير إعداد: يعني إصدار شهادات جديدة لكل وحدة
/// إصدار لكل مستأجر، وإعادة بناء مسار التوحيد القياسي والتوقيع.
/// </summary>
public sealed class ZatcaOnboardingChannel(
    IZatcaWire wire,
    ZatcaEndpoints endpoints,
    IZatcaKeyStore keys,
    ILocalKeyCustodian custodian,
    IZatcaSecretResolver secrets,
    IZatcaOnboardingState state,
    Func<CredentialRef, ZatcaCredential> credentials,
    TimeSpan timeout,
    TimeProvider clock,
    Func<CredentialRef, IReadOnlyList<ComplianceExerciseDocument>>? exerciseSet = null) : IOnboardingChannel
{
    /// <summary>
    /// يولّد المفتاح <b>محلياً</b> ويبني طلب التوقيع. المزوّد ناقل لا حائز.
    /// </summary>
    public async ValueTask<CsrMaterial> CreateSigningRequestAsync(CsrRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        CredentialRef credential = await custodian.CreateKeyAsync(
            request.Tenant, request.IssuingUnit, request.Environment, ct);

        byte[] csr = keys.BuildSigningRequest(credential, request.Subject);
        state.Remember(credential, string.Empty, OnboardingStage.SigningRequestBuilt);

        return new CsrMaterial(credential, csr);
    }

    /// <summary>
    /// شهادة الامتثال. كلمة المرور لمرة واحدة تُؤخذ يدوياً من بوابة الجهة، وتعبر هذا الحدّ
    /// <b>مقبضاً لا قيمة</b> — فلا تظهر في سجل ولا في تتبّع ولا في رسالة خطأ.
    /// </summary>
    [Provisional("شكل نداء شهادة الامتثال: اسم ترويسة كلمة المرور، وحقول الجسم، وحقول الردّ",
        DerivedFrom = "قراءة المواصفة وتنفيذات مفتوحة المصدر — لا من الهيئة",
        Risk = ProvisionalRisk.Structural,
        VerifyBy = "دليل التسجيل ووثيقة الواجهة المنشوران")]
    public async ValueTask<CertificateGrant> RequestComplianceCertificateAsync(
        CredentialRef credential, OneTimePasswordRef otp, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(otp);

        string password = await secrets.ResolveAsync(otp.Secret, ct);
        byte[] csr = keys.BuildSigningRequest(credential, PendingSubject(credential));

        Dictionary<string, string> headers = new(ZatcaEndpoints.CommonHeaders, StringComparer.Ordinal)
        {
            ["OTP"] = password
        };

        JsonObject body = new() { ["csr"] = Convert.ToBase64String(csr) };

        ZatcaWireResponse response = await ZatcaSubmission.SendAsync(
            wire, HttpMethod.Post, endpoints.ComplianceCsid, headers, body.ToJsonString(), timeout, ct);

        ComplianceTransportException? fault =
            ZatcaFaultClassifier.ClassifyStatus(response.StatusCode, response.Body, response.ProviderReference);

        if (fault is not null)
        {
            throw fault;
        }

        return await AcceptGrantAsync(credential, response.Body, OnboardingStage.ComplianceCertificateIssued, ct);
    }

    /// <summary>
    /// فحوص الامتثال: تُرسَل مستندات تمرين موقَّعة بشهادة الامتثال.
    /// <para/>
    /// <b>وإن لم تُركَّب مجموعة تمرين، لا تُعلَن النتيجة نجاحاً.</b> إعلان النجاح بلا
    /// إرسال مستند واحد هو بالضبط الفحص الذي يمرّ فراغاً: المجموعة التي يفحصها
    /// <b>لا تحتوي مخالفة بنيوياً</b>، فيبدو أخضر ولا يعني شيئاً.
    /// </summary>
    public async ValueTask<ComplianceCheckResult> RunComplianceChecksAsync(CredentialRef credential, CancellationToken ct)
    {
        IReadOnlyList<ComplianceExerciseDocument> documents = exerciseSet?.Invoke(credential) ?? [];

        if (documents.Count == 0)
        {
            return new ComplianceCheckResult(
                Passed: false,
                [ComplianceNotice.Err(
                    "no-exercise-set",
                    "لم تُركَّب مجموعة مستندات تمرين، فلم يُرسَل شيء. النتيجة ليست «نجاح بلا فحص» " +
                    "بل «لم يُفحص»: عدد المستندات المطلوبة وأنواعها غير مُتحقَّق منهما أصلاً.",
                    "no exercise set was composed, so nothing was sent; this is 'not checked', not 'passed'.")],
                DocumentsExercised: 0);
        }

        List<ComplianceNotice> notices = [];
        int exercised = 0;

        foreach (ComplianceExerciseDocument document in documents)
        {
            Dictionary<string, string> headers = await ExerciseHeadersAsync(credential, ct);

            JsonObject body = new()
            {
                ["invoiceHash"] = document.InvoiceHashBase64,
                ["uuid"] = document.Uuid.ToString("D", System.Globalization.CultureInfo.InvariantCulture),
                ["invoice"] = Convert.ToBase64String(document.SignedInvoice.Span)
            };

            ZatcaWireResponse response = await ZatcaSubmission.SendAsync(
                wire, HttpMethod.Post, endpoints.ComplianceInvoices, headers, body.ToJsonString(), timeout, ct);

            notices.AddRange(ZatcaSubmission.Notices(response.Body));
            exercised++;

            ComplianceTransportException? fault =
                ZatcaFaultClassifier.ClassifyStatus(response.StatusCode, response.Body, response.ProviderReference);

            if (fault is not null)
            {
                notices.Add(ComplianceNotice.Err(fault.Fault.Code, fault.Fault.MessageAr, fault.Fault.MessageEn));
                return new ComplianceCheckResult(false, notices, exercised);
            }
        }

        bool passed = !notices.Any(static n => n.Severity == NoticeSeverity.Error);
        if (passed)
        {
            state.Remember(credential, state.RequestIdOf(credential) ?? string.Empty, OnboardingStage.ComplianceChecksPassed);
        }

        return new ComplianceCheckResult(passed, notices, exercised);
    }

    /// <summary>
    /// شهادة الإنتاج. تُطلب بمعرّف طلب شهادة الامتثال، ومصادقتها بشهادة الامتثال نفسها.
    /// </summary>
    public async ValueTask<CertificateGrant> RequestProductionCertificateAsync(CredentialRef credential, CancellationToken ct)
    {
        string requestId = state.RequestIdOf(credential) is { Length: > 0 } id
            ? id
            : throw new ZatcaConfigurationException(
                $"لا معرّف طلب امتثال محفوظ للمقبض «{credential}». " +
                "الخطوتان تفصل بينهما ساعات وإعادة تشغيل، وحالة التسجيل في الذاكرة لا تكفي للإنتاج. / " +
                "no compliance request id is remembered for this credential.");

        Dictionary<string, string> headers = await ExerciseHeadersAsync(credential, ct);
        JsonObject body = new() { ["compliance_request_id"] = requestId };

        ZatcaWireResponse response = await ZatcaSubmission.SendAsync(
            wire, HttpMethod.Post, endpoints.ProductionCsid, headers, body.ToJsonString(), timeout, ct);

        ComplianceTransportException? fault =
            ZatcaFaultClassifier.ClassifyStatus(response.StatusCode, response.Body, response.ProviderReference);

        if (fault is not null)
        {
            throw fault;
        }

        return await AcceptGrantAsync(credential, response.Body, OnboardingStage.ProductionCertificateIssued, ct);
    }

    /// <summary>
    /// التجديد. <b>ليس دورة تسجيل جديدة</b>: المفتاح نفسه، والمقبض نفسه، والسلسلة نفسها.
    /// إبدال المفتاح عند التجديد يعني سلسلة جديدة، وهو ما لا يُفعل بلا قرار صريح.
    /// </summary>
    public async ValueTask<CertificateGrant> RenewProductionCertificateAsync(CredentialRef credential, CancellationToken ct)
    {
        Dictionary<string, string> headers = await ExerciseHeadersAsync(credential, ct);
        byte[] csr = keys.BuildSigningRequest(credential, PendingSubject(credential));
        JsonObject body = new() { ["csr"] = Convert.ToBase64String(csr) };

        ZatcaWireResponse response = await ZatcaSubmission.SendAsync(
            wire, HttpMethod.Patch, endpoints.ProductionCsid, headers, body.ToJsonString(), timeout, ct);

        ComplianceTransportException? fault =
            ZatcaFaultClassifier.ClassifyStatus(response.StatusCode, response.Body, response.ProviderReference);

        if (fault is not null)
        {
            throw fault;
        }

        return await AcceptGrantAsync(credential, response.Body, OnboardingStage.ProductionCertificateIssued, ct);
    }

    /// <summary>
    /// السحب. <b>محلي فقط</b>: لا مسار سحب موثَّق لدى الجهة، وادّعاء وجوده يجعل الواجهة
    /// تعرض «مسحوبة» بينما الشهادة حيّة عند الجهة.
    /// </summary>
    [Provisional("هل توجد واجهة سحب شهادة لدى الجهة، وما أثر السحب على المستندات الصادرة",
        DerivedFrom = "لا مصدر",
        Risk = ProvisionalRisk.Structural,
        VerifyBy = "دليل التسجيل المنشور، ثم إثبات في البيئة الاختبارية")]
    public ValueTask RevokeAsync(CredentialRef credential, string reason, CancellationToken ct)
    {
        state.Remember(credential, state.RequestIdOf(credential) ?? string.Empty, OnboardingStage.Revoked);
        return ValueTask.CompletedTask;
    }

    private async ValueTask<CertificateGrant> AcceptGrantAsync(
        CredentialRef credential, string body, OnboardingStage stage, CancellationToken ct)
    {
        JsonNode? root = JsonNode.Parse(body);

        string? token = root?["binarySecurityToken"]?.GetValue<string>();
        if (string.IsNullOrEmpty(token))
        {
            throw new ZatcaConfigurationException(
                "ردّ التسجيل بلا رمز أمان ثنائي. غيابه يعني أن ما بعده سيفشل بردّ مصادقة غير مفهوم. / " +
                "the onboarding response carries no binarySecurityToken.");
        }

        // دورتا فكّ ترميز: base64 فوق base64 فوق DER. من يفكّ دورة واحدة يحصل على بايتات
        // تبدو معقولة وتفشل لاحقاً — وهو الفخّ الذي يُبنى هنا مرة واحدة كي لا يتكرّر.
        byte[] certificateDer = Convert.FromBase64String(
            System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(token)));

        await custodian.AttachCertificateAsync(credential, certificateDer, ct);

        string requestId = root?["requestID"]?.GetValue<object>()?.ToString() ?? string.Empty;
        state.Remember(credential, requestId, stage);

        DateTimeOffset now = clock.GetUtcNow();
        using System.Security.Cryptography.X509Certificates.X509Certificate2 certificate =
            System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(certificateDer);

        return new CertificateGrant(
            credential,
            certificateDer,
            credentials(credential).Secret,
            now,
            certificate.NotAfter,
            stage);
    }

    private async ValueTask<Dictionary<string, string>> ExerciseHeadersAsync(CredentialRef credential, CancellationToken ct)
    {
        ReadOnlyMemory<byte> certificate = keys.Certificate(credential);

        if (certificate.IsEmpty)
        {
            throw new ZatcaConfigurationException(
                $"لا شهادة امتثال مرتبطة بالمقبض «{credential}» — الخطوة السابقة لم تكتمل. / " +
                "no compliance certificate is attached; the previous step did not complete.");
        }

        string secret = await secrets.ResolveAsync(credentials(credential).Secret, ct);

        return new Dictionary<string, string>(ZatcaEndpoints.CommonHeaders, StringComparer.Ordinal)
        {
            ["Authorization"] = ZatcaEndpoints.BasicAuthorization(
                Canonicalization.ZatcaDigests.BinarySecurityToken(certificate.Span), secret)
        };
    }

    /// <summary>
    /// موضوع الطلب المحفوظ للمقبض. <b>يجب أن يُركَّب من إعداد وحدة الإصدار</b>؛ وغيابه
    /// عطل إعداد يقع عند الخطوة لا مستند ناقص يُرسَل.
    /// </summary>
    public Func<CredentialRef, CsrSubject>? SubjectSource { get; init; }

    private CsrSubject PendingSubject(CredentialRef credential) =>
        SubjectSource?.Invoke(credential)
        ?? throw new ZatcaConfigurationException(
            $"لا موضوع طلب توقيع مُركَّب للمقبض «{credential}». " +
            "بيانات وحدة الإصدار (الرقم الضريبي، والرقم التسلسلي، والعنوان، والنشاط) " +
            "تُقرأ من سجلّ الوحدة ولا تُخمَّن. / no CSR subject is composed for this credential.");
}
