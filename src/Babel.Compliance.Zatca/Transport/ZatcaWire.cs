using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Zatca.Transport;

/// <summary>ردّ خام من السلك: الرمز، والجسم، وترويسة المرجع إن وُجدت.</summary>
public sealed record ZatcaWireResponse(int StatusCode, string Body, string? ProviderReference);

/// <summary>
/// السلك نفسه. <b>واجهة كي يكون الاختبار قادراً على إنتاج كل صنف عطل بلا شبكة</b> —
/// وأهمّها الصنف الذي لا يمكن إنتاجه بشبكة حقيقية عند الطلب: <b>الجواب المفقود بعد
/// أن سجّلت الجهة القبول</b>.
/// </summary>
public interface IZatcaWire
{
    ValueTask<ZatcaWireResponse> SendAsync(
        HttpMethod method,
        Uri endpoint,
        IReadOnlyDictionary<string, string> headers,
        string jsonBody,
        TimeSpan timeout,
        CancellationToken ct);
}

/// <summary>
/// تنفيذ السلك فوق <see cref="HttpClient"/>.
/// <para/>
/// <b>ولا تصنيف هنا.</b> هذا الملف ينقل بايتات ويرمي ما يقع كما هو؛ والتصنيف يقع عند
/// <b>حدّ القناة</b> في <see cref="ZatcaSubmission"/>، لأنه السؤال الذي يُطرح في كل نداء
/// مهما كان تنفيذ السلك. وضعُه هنا كان عيباً حقيقياً وقع في هذا الفرع وكُشف باختبار:
/// سلكٌ بديل — سلك اختبار مثلاً — كان <b>يتجاوز التصنيف كله</b>، فيصير عطل «لم يغادر
/// الطلب» غموضاً يوقف المستند في طابور بشري بلا سبب. القاعدة المستفادة هي نفسها التي
/// دفع هذا المشروع ثمنها في محرّك الترحيل: <b>السؤال يُطرح في مواضع أكثر مما يُجاب فيه،
/// فيوضع الجواب عند الحدّ الذي يمرّ منه الجميع.</b>
/// </summary>
public sealed class ZatcaHttpWire(HttpClient client) : IZatcaWire
{
    public async ValueTask<ZatcaWireResponse> SendAsync(
        HttpMethod method,
        Uri endpoint,
        IReadOnlyDictionary<string, string> headers,
        string jsonBody,
        TimeSpan timeout,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(headers);

        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);

        using HttpRequestMessage request = new(method, endpoint)
        {
            Content = new StringContent(jsonBody, new UTF8Encoding(false), "application/json")
        };

        foreach ((string name, string value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        using HttpResponseMessage response = await client.SendAsync(request, deadline.Token);
        string body = await response.Content.ReadAsStringAsync(deadline.Token);
        string? reference = response.Headers.TryGetValues("X-Correlation-Id", out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;

        return new ZatcaWireResponse((int)response.StatusCode, body, reference);
    }
}

/// <summary>
/// <b>تصنيف الأعطال — الموضع الوحيد الذي يقرّر «لم يصل» من «لا أدري».</b>
/// <para/>
/// والفرق بين الاثنين هو الفرق بين إعادة محاولة آمنة تماماً وإرسال مكرّر لا يمكن
/// التراجع عنه: المستند الضريبي المُصفَّى مرتين لا يُحذف، بل يُصحَّح بإشعار.
/// <para/>
/// <b>القاعدة الحاكمة هنا، ومخالفتها تُبطل الحماية كلها:</b>
/// <c>TransientNotSent</c> لا تُمنَح إلا حين يكون <b>مُثبَتاً</b> أن الطلب لم يغادر —
/// أي عطل يقع <b>قبل</b> فتح الاتصال أو أثناء المصافحة. وكل مهلة، وكل انقطاع بعد
/// الإرسال، وكل ردّ بلا جسم: <c>Ambiguous</c>. التصنيف المتساهل هنا لا يظهر في
/// الاختبار، ويظهر عند العميل فاتورةً مكرّرة.
/// </summary>
public static class ZatcaFaultClassifier
{
    public static ComplianceTransportException Classify(Exception exception, Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(endpoint);

        string host = endpoint.Host;

        // ── مُثبَت أن الطلب لم يغادر: عطل قبل الاتصال أو في المصافحة ───────────────
        if (exception is HttpRequestException http && NeverLeft(http))
        {
            return new ComplianceTransportException(
                ComplianceFault.NotSent(
                    "connect-failed",
                    $"تعذّر فتح الاتصال بـ{host} — الطلب لم يغادر، وإعادة المحاولة آمنة تماماً",
                    $"could not open a connection to {host}; the request never left, retrying is entirely safe"),
                exception);
        }

        // ── مهلة: الطلب قد يكون غادر ووصل وسُجِّل. لا يمكن معرفة ذلك ──────────────
        if (exception is OperationCanceledException or TimeoutException)
        {
            return new ComplianceTransportException(
                ComplianceFault.Ambiguous(
                    "read-timeout",
                    $"انتهت المهلة قبل وصول جواب من {host}. لا يمكن معرفة هل سُجِّل المستند لدى الجهة أم لا، " +
                    "وإعادة الإرسال العمياء هنا قد تُنشئ مستنداً مكرّراً لا يُحذف",
                    $"the deadline passed before {host} answered; whether the document was recorded cannot be known, " +
                    "and blind resubmission may create a duplicate that cannot be deleted"),
                exception);
        }

        // ── أي شيء آخر على السلك: غامض بالافتراض، لا عابر ──────────────────────────
        return new ComplianceTransportException(
            ComplianceFault.Ambiguous(
                "wire-fault",
                $"عطل غير مصنَّف على السلك مع {host}: {exception.GetType().Name}. " +
                "يُعامَل غامضاً بالافتراض لأن تصنيفه «لم يُرسل» بلا دليل هو ما يُنتج المستند المكرّر",
                $"unclassified wire fault against {host}: {exception.GetType().Name}; treated as ambiguous by default"),
            exception);
    }

    /// <summary>
    /// أعطال يُستدلّ منها <b>يقيناً</b> على أن شيئاً لم يغادر: رفض اتصال، أو تعذّر تحليل
    /// اسم المضيف، أو مضيف غير قابل للوصول. أي رمز مقبس آخر لا يُصنَّف هنا.
    /// </summary>
    private static bool NeverLeft(HttpRequestException exception) =>
        exception.InnerException is SocketException socket
        && socket.SocketErrorCode is SocketError.ConnectionRefused
            or SocketError.HostNotFound
            or SocketError.HostUnreachable
            or SocketError.NetworkUnreachable
            or SocketError.AddressNotAvailable;

    /// <summary>
    /// تصنيف الرمز. <b>الرموز التي تعني «وسيط لم يستطع إيصال الجواب» غامضة لا نهائية</b>:
    /// ‏502 و503 و504 كلها تقع بعد أن غادر الطلب.
    /// </summary>
    [Provisional("رموز الاستجابة الفعلية ودلالة كل رمز، وأيّها يعني رفضاً نهائياً",
        DerivedFrom = "قراءة المواصفة — لا من الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "جدول رموز الاستجابة في مواصفة الواجهة، ثم إثبات في البيئة الاختبارية")]
    public static ComplianceTransportException? ClassifyStatus(int status, string body, string? providerReference)
    {
        if (status is >= 200 and < 300) return null;

        // مقبول بملاحظات: الجهة تستعمل 202 لهذا، ويُعامَل نجاحاً في القنوات.
        if (status is 400 or 401 or 403 or 404 or 409 or 413 or 415 or 422)
        {
            return new ComplianceTransportException(ComplianceFault.Permanent(
                string.Create(CultureInfo.InvariantCulture, $"http-{status}"),
                $"رفض نهائي بالرمز {status}. إعادة الإرسال بنفس الحمولة عبث: {Trim(body)}",
                $"permanent rejection with status {status}; resubmitting the same payload is futile: {Trim(body)}"));
        }

        return new ComplianceTransportException(ComplianceFault.Ambiguous(
            string.Create(CultureInfo.InvariantCulture, $"http-{status}"),
            $"الرمز {status} يأتي من وسيط بعد أن غادر الطلب: لا يُعرف هل بلغ الجهة وسُجِّل. {Trim(body)}",
            $"status {status} comes from an intermediary after the request left; whether it reached and was recorded is unknown. {Trim(body)}",
            providerReference));
    }

    private static string Trim(string body) =>
        body.Length <= 400 ? body : body[..400] + "…";
}

/// <summary>
/// نقاط النهاية. <b>بيئتان منفصلتان تماماً</b> — لا يُشتقّ مسار الإنتاج من مسار المحاكاة
/// باستبدال جزء من النصّ، لأن استبدالاً واحداً فائتاً يُرسل فاتورة اختبار إلى الإنتاج.
/// </summary>
[Provisional("عناوين نقاط النهاية ومساراتها وأسماء الترويسات وإصدار الواجهة",
    DerivedFrom = "قراءة المواصفة وتنفيذات مفتوحة المصدر — لا من الهيئة. النطاقات محجوبة عن بيئة البناء (403)",
    Risk = ProvisionalRisk.Structural,
    VerifyBy = "وثيقة الواجهة المنشورة على بوابة الهيئة")]
public sealed record ZatcaEndpoints(Uri Base)
{
    public Uri ComplianceCsid => new(Base, "compliance");

    public Uri ComplianceInvoices => new(Base, "compliance/invoices");

    public Uri ProductionCsid => new(Base, "production/csids");

    public Uri Clearance => new(Base, "invoices/clearance/single");

    public Uri Reporting => new(Base, "invoices/reporting/single");

    /// <summary>الترويسات الثابتة لكل نداء.</summary>
    public static IReadOnlyDictionary<string, string> CommonHeaders { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Accept"] = "application/json",
            ["Accept-Version"] = "V2",
            ["Accept-Language"] = "ar"
        };

    /// <summary>ترويسة المصادقة: <c>Basic</c> فوق (رمز الأمان الثنائي : السرّ).</summary>
    [Provisional("شكل المصادقة على واجهات الإرسال",
        DerivedFrom = "تنفيذات مفتوحة المصدر — لا من الهيئة",
        Risk = ProvisionalRisk.Structural,
        VerifyBy = "مواصفة المصادقة في وثيقة الواجهة")]
    public static string BasicAuthorization(string binarySecurityToken, string secret) =>
        "Basic " + Convert.ToBase64String(
            Encoding.ASCII.GetBytes(binarySecurityToken + ":" + secret));
}
