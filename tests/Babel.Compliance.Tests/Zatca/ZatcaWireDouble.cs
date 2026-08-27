using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Babel.Compliance.Zatca.Transport;

namespace Babel.Compliance.Tests.Zatca;

/// <summary>ما تفعله «الجهة» في النداء التالي.</summary>
public enum WireBehaviour
{
    Accept,
    AcceptWithWarnings,
    Reject,

    /// <summary>
    /// <b>السيناريو الذي يُسقط الأنظمة:</b> الجهة <b>تُسجّل القبول</b> ثم ينقطع الجواب.
    /// المستند صُفِّي فعلاً، والمُرسِل لا يعرف. إعادة الإرسال هنا تُنشئ مستنداً مكرّراً.
    /// </summary>
    TimeoutAfterRecording,

    /// <summary>وصل الطلب ولم يُسجَّل شيء، ثم انقطع الجواب. إعادة الإرسال صحيحة — ولا يمكن معرفة ذلك.</summary>
    TimeoutBeforeRecording,

    /// <summary>الطلب لم يغادر أصلاً: رفض اتصال. إعادة المحاولة آمنة تماماً.</summary>
    ConnectionRefused,

    /// <summary>رفض نهائي مفهوم.</summary>
    BadRequest,

    /// <summary>وسيط لم يستطع إيصال الجواب. غامض لا نهائي.</summary>
    GatewayTimeout
}

/// <summary>ما سجّلته «الجهة» فعلاً — لا ما رآه المُرسِل.</summary>
public sealed record WireRecord(
    string Uuid,
    string InvoiceHash,
    string IdempotencyKey,
    string PayloadSha256,
    DateTimeOffset At);

/// <summary>
/// سلك وهمي بحالة داخلية. <b>يسجّل ما قبلته الجهة فعلاً بغضّ النظر عمّا رآه المُرسِل</b> —
/// وهذا هو بيت القصيد: اختبارات الحصانة تقيس ما لدى الجهة لا ما لدى المُرسِل.
/// <para/>
/// وهو الطريق الوحيد لإنتاج الحالة التي لا تُنتَج عند الطلب على شبكة حقيقية:
/// <b>سُجِّل القبول ثم ضاع الجواب</b>.
/// </summary>
public sealed class FakeZatcaWire : IZatcaWire
{
    private readonly ConcurrentQueue<WireBehaviour> _script = new();
    private readonly List<WireRecord> _recorded = [];
    private readonly List<(Uri Endpoint, IReadOnlyDictionary<string, string> Headers, string Body)> _seen = [];
    private readonly Lock _gate = new();

    public FakeZatcaWire(TimeProvider clock) => Clock = clock;

    public TimeProvider Clock { get; }

    public IReadOnlyList<WireRecord> Recorded
    {
        get { lock (_gate) return [.. _recorded]; }
    }

    public IReadOnlyList<(Uri Endpoint, IReadOnlyDictionary<string, string> Headers, string Body)> Seen
    {
        get { lock (_gate) return [.. _seen]; }
    }

    /// <summary>ما بعد آخر عنصر: قبول.</summary>
    public void Script(params WireBehaviour[] behaviours)
    {
        foreach (WireBehaviour behaviour in behaviours)
        {
            _script.Enqueue(behaviour);
        }
    }

    public int AcceptancesFor(string uuid)
    {
        lock (_gate) return _recorded.Count(r => string.Equals(r.Uuid, uuid, StringComparison.Ordinal));
    }

    public ValueTask<ZatcaWireResponse> SendAsync(
        HttpMethod method,
        Uri endpoint,
        IReadOnlyDictionary<string, string> headers,
        string jsonBody,
        TimeSpan timeout,
        CancellationToken ct)
    {
        lock (_gate)
        {
            _seen.Add((endpoint, headers, jsonBody));
        }

        WireBehaviour behaviour = _script.TryDequeue(out WireBehaviour next) ? next : WireBehaviour.Accept;

        switch (behaviour)
        {
            case WireBehaviour.ConnectionRefused:
                throw new HttpRequestException(
                    "connection refused", new SocketException((int)SocketError.ConnectionRefused));

            case WireBehaviour.TimeoutBeforeRecording:
                throw new TaskCanceledException("انتهت المهلة قبل التسجيل");

            case WireBehaviour.BadRequest:
                return ValueTask.FromResult(new ZatcaWireResponse(400, Rejection(), null));

            case WireBehaviour.GatewayTimeout:
                return ValueTask.FromResult(new ZatcaWireResponse(504, "gateway timeout", null));
        }

        Record(jsonBody, headers);

        if (behaviour == WireBehaviour.TimeoutAfterRecording)
        {
            // القبول سُجِّل. الجواب لم يصل. هذا هو ما يجب أن يصمد أمامه التصميم.
            throw new TaskCanceledException("سُجِّل القبول ثم انقطع الجواب");
        }

        bool warnings = behaviour == WireBehaviour.AcceptWithWarnings;
        bool cleared = behaviour != WireBehaviour.Reject;

        return ValueTask.FromResult(new ZatcaWireResponse(200, Success(cleared, warnings, endpoint), "REF-0001"));
    }

    private void Record(string jsonBody, IReadOnlyDictionary<string, string> headers)
    {
        JsonNode body = JsonNode.Parse(jsonBody)!;
        string uuid = body["uuid"]!.GetValue<string>();
        string invoice = body["invoice"]!.GetValue<string>();

        lock (_gate)
        {
            _recorded.Add(new WireRecord(
                uuid,
                body["invoiceHash"]!.GetValue<string>(),
                headers.TryGetValue("Idempotency-Key", out string? key) ? key : "(none)",
                Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(invoice))).ToLowerInvariant(),
                Clock.GetUtcNow()));
        }
    }

    private static string Success(bool cleared, bool warnings, Uri endpoint)
    {
        bool isClearance = endpoint.AbsolutePath.Contains("clearance", StringComparison.Ordinal);

        JsonObject results = new()
        {
            ["errorMessages"] = new JsonArray(),
            ["warningMessages"] = warnings
                ? new JsonArray(new JsonObject { ["code"] = "BR-KSA-W-01", ["message"] = "قبول بملاحظات" })
                : new JsonArray(),
            ["infoMessages"] = new JsonArray()
        };

        JsonObject root = new() { ["validationResults"] = results };

        if (isClearance)
        {
            root["clearanceStatus"] = cleared ? "CLEARED" : "NOT_CLEARED";
            root["clearedInvoice"] = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes("<ClearedInvoice reference=\"REF-0001\" />"));
        }
        else
        {
            root["reportingStatus"] = cleared ? "REPORTED" : "NOT_REPORTED";
        }

        return root.ToJsonString();
    }

    private static string Rejection() => new JsonObject
    {
        ["validationResults"] = new JsonObject
        {
            ["errorMessages"] = new JsonArray(
                new JsonObject { ["code"] = "BR-KSA-E-01", ["message"] = "حقل إلزامي مفقود" }),
            ["warningMessages"] = new JsonArray(),
            ["infoMessages"] = new JsonArray()
        }
    }.ToJsonString();

    public string Describe()
    {
        lock (_gate)
        {
            return string.Join("\n", _recorded.Select((r, i) => string.Create(
                CultureInfo.InvariantCulture,
                $"  [{i + 1}] uuid={r.Uuid} hash={r.InvoiceHash[..12]}… key={r.IdempotencyKey} payload={r.PayloadSha256[..12]}…")));
        }
    }
}
