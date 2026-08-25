using System.Text.Json;
using System.Text.Json.Nodes;
using Babel.Compliance.Abstractions;
using Babel.Compliance.Zatca.Canonicalization;
using Babel.Compliance.Zatca.Documents;
using Babel.Compliance.Zatca.Signing;

namespace Babel.Compliance.Zatca.Transport;

/// <summary>
/// من أين يأتي السرّ المرافق للشهادة. <b>مقبض إلى سرّ، لا السرّ نفسه</b> — كي لا يظهر
/// في سجل ولا في تتبّع ولا في رسالة خطأ.
/// <para/>
/// <b>ولا تنفيذ في هذا المستودع يقرأ سرّاً من ملف.</b> التنفيذ المرافق يقرأ من متغيّرات
/// البيئة، لأن أي مسار «اقرأ من ملف» يُغري بإيداع سرّ اختبار «مؤقتاً».
/// </summary>
public interface IZatcaSecretResolver
{
    ValueTask<string> ResolveAsync(SecretRef secret, CancellationToken ct);
}

/// <summary>
/// يقرأ السرّ من بيئة العملية. الاسم المفتاحي هو قيمة <see cref="SecretRef"/> نفسها.
/// <b>لا سرّ في المستودع، ولا سرّ على القرص.</b>
/// </summary>
public sealed class EnvironmentSecretResolver : IZatcaSecretResolver
{
    public ValueTask<string> ResolveAsync(SecretRef secret, CancellationToken ct)
    {
        if (secret.Value.Length == 0)
        {
            throw new ZatcaConfigurationException(
                "مقبض سرّ فارغ. الإرسال بلا مصادقة يفشل بردّ غير مفهوم، والفشل هنا أوضح. / an empty secret handle is refused.");
        }

        string? value = Environment.GetEnvironmentVariable(secret.Value);

        return value is null
            ? throw new ZatcaConfigurationException(
                $"لا قيمة للمتغيّر «{secret.Value}» في بيئة العملية. " +
                "الأسرار تُقرأ من البيئة ولا تُودَع في المستودع بحال. / " +
                $"no value for '{secret.Value}' in the process environment.")
            : ValueTask.FromResult(value);
    }
}

/// <summary>اعتماد وحدة إصدار على السلك: رمز الأمان الثنائي ومقبض السرّ.</summary>
public sealed record ZatcaCredential(CredentialRef Credential, SecretRef Secret);

/// <summary>عطل في الإعداد. يقع عند الإقلاع أو عند أول نداء، لا في منتصف إرسال.</summary>
public sealed class ZatcaConfigurationException(string message) : Exception(message);

/// <summary>
/// ما يشترك فيه المساران: بناء الجسم، والمصادقة، وقراءة الملاحظات.
/// <b>وما لا يشتركان فيه — وهو الأهم — ليس هنا:</b> المقاصة حاجزة وتُعيد نسخة مختومة،
/// والإبلاغ لاحق ولا يُعيد شيئاً. القناتان نوعان مستقلان عمداً.
/// </summary>
internal static class ZatcaSubmission
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// جسم الطلب. <b>الثلاثة كلها من <see cref="ZatcaSubmissionIdentity"/> وحده</b> —
    /// ولا يُشتقّ أيٌّ منها من مصدر ثانٍ.
    /// </summary>
    [Provisional("أسماء حقول جسم الطلب وشكله",
        DerivedFrom = "قراءة المواصفة وتنفيذات مفتوحة المصدر — لا من الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "وثيقة الواجهة المنشورة")]
    public static string Body(ZatcaSubmissionIdentity identity, ZatcaDocumentRenderer renderer)
    {
        string invoiceHash = identity.InvoiceHash(payload =>
            ZatcaDocumentRenderer.InvoiceHashBase64(renderer.RecomputeInvoiceDigest(payload.Span)));

        JsonObject body = new()
        {
            ["invoiceHash"] = invoiceHash,
            ["uuid"] = identity.BodyUuid,
            ["invoice"] = identity.BodyInvoiceBase64
        };

        return body.ToJsonString(Options);
    }

    /// <summary>
    /// <b>حدّ النداء الوحيد في هذا المزوّد.</b> كل خروج إلى السلك يمرّ من هنا، فيُصنَّف
    /// عطله حتماً — أياً كان تنفيذ السلك.
    /// <para/>
    /// ووضعُ التصنيف داخل تنفيذ HTTP كان عيباً حقيقياً وقع في هذا الفرع: سلك بديل يتجاوزه،
    /// فيتحوّل «الطلب لم يغادر» إلى «لا أدري» ويتوقّف مستند سليم في طابور بشري.
    /// </summary>
    public static async ValueTask<ZatcaWireResponse> SendAsync(
        IZatcaWire wire,
        HttpMethod method,
        Uri endpoint,
        IReadOnlyDictionary<string, string> headers,
        string body,
        TimeSpan timeout,
        CancellationToken ct)
    {
        try
        {
            return await wire.SendAsync(method, endpoint, headers, body, timeout, ct);
        }
        catch (ComplianceTransportException)
        {
            // مُصنَّف سلفاً — لا يُعاد تصنيفه ولا يُبتلع.
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            throw ZatcaFaultClassifier.Classify(exception, endpoint);
        }
    }

    public static async ValueTask<Dictionary<string, string>> HeadersAsync(
        ZatcaSubmissionIdentity identity,
        ZatcaCredential credential,
        IZatcaKeyStore keys,
        IZatcaSecretResolver secrets,
        IReadOnlyDictionary<string, string>? extra,
        CancellationToken ct)
    {
        ReadOnlyMemory<byte> certificate = keys.Certificate(credential.Credential);

        if (certificate.IsEmpty)
        {
            throw new ZatcaConfigurationException(
                $"لا شهادة مرتبطة بالمقبض «{credential.Credential}». المصادقة تحمل الشهادة نفسها، " +
                "فالإرسال بلا شهادة يفشل بردّ لا يشرح السبب. / no certificate is attached to the credential.");
        }

        string secret = await secrets.ResolveAsync(credential.Secret, ct);

        Dictionary<string, string> headers = new(ZatcaEndpoints.CommonHeaders, StringComparer.Ordinal)
        {
            ["Authorization"] = ZatcaEndpoints.BasicAuthorization(
                ZatcaDigests.BinarySecurityToken(certificate.Span), secret),

            // مفتاح الإحكام من جانبنا. **ثابت عبر المحاولات** على المستند نفسه.
            ["Idempotency-Key"] = identity.IdempotencyKey
        };

        if (extra is not null)
        {
            foreach ((string name, string value) in extra)
            {
                headers[name] = value;
            }
        }

        return headers;
    }

    /// <summary>
    /// يقرأ الملاحظات من جسم الردّ. <b>لا تُبتلع في سجل فني</b> — تُعرض للمستخدم بلغته،
    /// وغياب رسالة عربية يُملأ بنصّ الجهة كما هو لا بفراغ.
    /// </summary>
    public static IReadOnlyList<ComplianceNotice> Notices(string body)
    {
        List<ComplianceNotice> notices = [];

        if (string.IsNullOrWhiteSpace(body))
        {
            return notices;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            // ردّ ليس JSON: يُعرض كما هو بدل أن يختفي. اختفاؤه يجعل الرفض بلا سبب.
            return [ComplianceNotice.Err("non-json-response", "ردّ غير مفهوم من الجهة: " + Trim(body), "unparsable response: " + Trim(body))];
        }

        JsonNode? results = root?["validationResults"];
        Collect(results?["errorMessages"], NoticeSeverity.Error, notices);
        Collect(results?["warningMessages"], NoticeSeverity.Warning, notices);
        Collect(results?["infoMessages"], NoticeSeverity.Information, notices);

        return notices;
    }

    private static void Collect(JsonNode? array, NoticeSeverity severity, List<ComplianceNotice> into)
    {
        if (array is not JsonArray items)
        {
            return;
        }

        foreach (JsonNode? item in items)
        {
            string code = item?["code"]?.GetValue<string>() ?? "unknown";
            string message = item?["message"]?.GetValue<string>() ?? string.Empty;
            into.Add(new ComplianceNotice(code, message, message, severity));
        }
    }

    public static bool HasWarnings(IReadOnlyList<ComplianceNotice> notices) =>
        notices.Any(static n => n.Severity == NoticeSeverity.Warning);

    public static string? Status(string body, string field)
    {
        try
        {
            return JsonNode.Parse(body)?[field]?.GetValue<string>();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static ReadOnlyMemory<byte> ClearedDocument(string body)
    {
        string? encoded = Status(body, "clearedInvoice");
        return string.IsNullOrEmpty(encoded) ? ReadOnlyMemory<byte>.Empty : Convert.FromBase64String(encoded);
    }

    private static string Trim(string body) => body.Length <= 400 ? body : body[..400] + "…";
}

/// <summary>
/// <b>قناة المقاصة.</b> طلب/استجابة حاجز: المستند لا يُسلَّم للمشتري قبل عودة الرد.
/// <para/>
/// وهي <b>آلية مستقلة تماماً</b> عن قناة الإبلاغ، لا إعداد على آلية واحدة. المساران
/// متعاكسان في اتجاه الاعتماد الزمني، ودمجهما يُنتج إمّا إيقاف بيع بانتظار ردّ شبكي
/// وإمّا تسليم مستند قبل اعتماده
/// (‏<c>docs/evidence/traps.md#fakh-clearance-versus-reporting-are-opposite-paths</c>).
/// </summary>
public sealed class ZatcaClearanceChannel(
    IZatcaWire wire,
    ZatcaEndpoints endpoints,
    ZatcaDocumentRenderer renderer,
    IZatcaKeyStore keys,
    IZatcaSecretResolver secrets,
    Func<CredentialRef, ZatcaCredential> credentials,
    TimeSpan timeout,
    TimeProvider clock) : IClearanceChannel
{
    public async ValueTask<ClearanceOutcome> ClearAsync(ClearanceRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        ZatcaSubmissionIdentity identity = ZatcaSubmissionIdentity.From(request);

        Dictionary<string, string> headers = await ZatcaSubmission.HeadersAsync(
            identity, credentials(request.Credential), keys, secrets,
            // ترويسة المقاصة: تطلب تصفية لا إبلاغاً. غيابها يُحوّل الطلب إلى المسار الآخر بصمت.
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Clearance-Status"] = "1" },
            ct);

        ZatcaWireResponse response = await ZatcaSubmission.SendAsync(
            wire, HttpMethod.Post, endpoints.Clearance, headers,
            ZatcaSubmission.Body(identity, renderer), timeout, ct);

        ComplianceTransportException? fault =
            ZatcaFaultClassifier.ClassifyStatus(response.StatusCode, response.Body, response.ProviderReference);

        if (fault is not null)
        {
            throw fault;
        }

        IReadOnlyList<ComplianceNotice> notices = ZatcaSubmission.Notices(response.Body);
        string? status = ZatcaSubmission.Status(response.Body, "clearanceStatus");

        ClearanceDisposition disposition = status switch
        {
            "CLEARED" => ZatcaSubmission.HasWarnings(notices)
                ? ClearanceDisposition.ClearedWithWarnings
                : ClearanceDisposition.Cleared,
            "NOT_CLEARED" => ClearanceDisposition.Rejected,
            _ => ClearanceDisposition.Rejected
        };

        return new ClearanceOutcome(
            disposition,
            notices,
            clock.GetUtcNow(),
            ZatcaSubmission.ClearedDocument(response.Body),
            response.ProviderReference,
            // لا يُدَّعى كشف تكرار من جانب الجهة: لا مفتاح إحكام موثَّق، والادّعاء بلا وثيقة
            // هو ما يُنتج فاتورة مكرّرة يوم تُصدَّق الدعوى.
            RecognisedAsDuplicate: false);
    }
}

/// <summary>
/// <b>قناة الإبلاغ.</b> يُستدعى من عامل خلفي يقرأ الصندوق الصادر، لا من طلب مستخدم.
/// المستند سُلِّم للعميل فعلاً قبل هذا الإرسال، فالرفض هنا لا يُبطل الإصدار بل يستوجب تصحيحاً.
/// </summary>
public sealed class ZatcaReportingChannel(
    IZatcaWire wire,
    ZatcaEndpoints endpoints,
    ZatcaDocumentRenderer renderer,
    IZatcaKeyStore keys,
    IZatcaSecretResolver secrets,
    Func<CredentialRef, ZatcaCredential> credentials,
    TimeSpan timeout,
    TimeProvider clock) : IReportingChannel
{
    public async ValueTask<ReportingAcknowledgement> ReportAsync(ReportingSubmission submission, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(submission);

        ZatcaSubmissionIdentity identity = ZatcaSubmissionIdentity.From(submission);

        Dictionary<string, string> headers = await ZatcaSubmission.HeadersAsync(
            identity, credentials(submission.Credential), keys, secrets,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Clearance-Status"] = "0" },
            ct);

        ZatcaWireResponse response = await ZatcaSubmission.SendAsync(
            wire, HttpMethod.Post, endpoints.Reporting, headers,
            ZatcaSubmission.Body(identity, renderer), timeout, ct);

        ComplianceTransportException? fault =
            ZatcaFaultClassifier.ClassifyStatus(response.StatusCode, response.Body, response.ProviderReference);

        if (fault is not null)
        {
            throw fault;
        }

        IReadOnlyList<ComplianceNotice> notices = ZatcaSubmission.Notices(response.Body);
        string? status = ZatcaSubmission.Status(response.Body, "reportingStatus");

        ReportingDisposition disposition = status switch
        {
            "REPORTED" => ZatcaSubmission.HasWarnings(notices)
                ? ReportingDisposition.AcceptedWithWarnings
                : ReportingDisposition.Accepted,
            _ => ReportingDisposition.Rejected
        };

        return new ReportingAcknowledgement(
            disposition, notices, clock.GetUtcNow(), response.ProviderReference, RecognisedAsDuplicate: false);
    }
}

/// <summary>
/// وصف حالة الاستعلام لدى هذا المزوّد.
/// <para/>
/// <b>لا استعلام حالة.</b> وليس هذا نقصاً في التنفيذ بل <b>الحالة المتوقَّعة</b>: أنضج
/// عميل مفتوح المصدر لهذه المنظومة يعرض <b>صفر GET</b>. ولذلك يبقى
/// <c>StatusProbeSupport.NotSupported</c>، ومسار حسم الغموض في <c>SubmissionGuard</c>
/// <b>يجب أن يعمل بدونه</b> — وهو يعمل: ينتهي إلى طابور بشري بدل إعادة إرسال عمياء.
/// </summary>
[Provisional("هل تخدم الجهة مسار قراءة يُستعلم به عن حالة مستند بعينه",
    DerivedFrom = "لا مصدر — ثلاثة مستودعات صغيرة تنفّذ استعلاماً بـuuid، وخدمة الهيئة له غير مُتحقَّق منها",
    Risk = ProvisionalRisk.Structural,
    VerifyBy = "وثيقة الواجهة، ثم إثبات فعلي في البيئة الاختبارية")]
public static class ZatcaStatusQuery
{
    public static StatusProbeSupport Support => StatusProbeSupport.NotSupported;

    /// <summary>سبب الغياب، بنصّه، كي يُعرض في الواجهة بدل «غير متاح».</summary>
    public static string ReasonAr => FormattableString.Invariant($"لا يوجد استعلام حالة موثَّق لدى الجهة. لذلك تنتهي كل مهلة غامضة إلى مراجعة بشرية، ") +
        FormattableString.Invariant($"ولا تُعاد أي فاتورة تلقائياً بعد غموض.");
}
