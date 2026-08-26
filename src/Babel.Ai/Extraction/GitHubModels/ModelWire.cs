using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Babel.SharedKernel;

namespace Babel.Ai.Extraction.GitHubModels;

/// <summary>ردّ خام من السلك: الرمز، والجسم، ومدّة الانتظار المُعلنة إن وُجدت.</summary>
/// <param name="StatusCode">رمز الحالة.</param>
/// <param name="Body">الجسم كما ورد.</param>
/// <param name="RetryAfterSeconds">مدّة الانتظار المُعلنة عند تجاوز الحدّ.</param>
public sealed record ModelWireResponse(int StatusCode, string Body, int? RetryAfterSeconds);

/// <summary>
/// السلك. <b>واجهة كي يُنتَج كل صنف عطل في اختبار بلا شبكة</b> — وهو نفس شكل
/// <c>IZatcaWire</c> في حدّ الالتزام وللسبب نفسه: أصناف العطل التي يموت عليها عرضٌ حيّ
/// هي بالضبط التي لا تُطلَب من خدمة حقيقية عند الحاجة (تجاوز الحدّ، والامتناع، والقطع).
/// </summary>
public interface IModelWire
{
    /// <summary>يرسل جسماً ويعيد الردّ خاماً. الأعطال تُرمى ويصنّفها الحدّ لا هذا الملف.</summary>
    /// <param name="endpoint">العنوان.</param>
    /// <param name="token">الرمز — يُمرَّر ولا يُخزَّن ولا يُسجَّل.</param>
    /// <param name="jsonBody">جسم الطلب.</param>
    /// <param name="timeout">المهلة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<ModelWireResponse> SendAsync(
        Uri endpoint,
        string token,
        string jsonBody,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

/// <summary>
/// تنفيذ السلك فوق <see cref="HttpClient"/>. <b>ولا تصنيف هنا</b> — ينقل بايتات ويرمي
/// ما يقع كما هو، ويقع التصنيف عند حدّ المزوّد كي يمرّ به كل تنفيذ للسلك.
/// <para>
/// وهذا الدرس مدفوع الثمن في هذا المستودع: وضعُ التصنيف داخل تنفيذ HTTP جعل سلكاً
/// بديلاً <b>يتجاوز التصنيف كله</b>.
/// </para>
/// </summary>
public sealed class HttpModelWire(HttpClient client) : IModelWire
{
    private readonly HttpClient _client = client ?? throw new ArgumentNullException(nameof(client));

    /// <inheritdoc />
    public async ValueTask<ModelWireResponse> SendAsync(
        Uri endpoint,
        string token,
        string jsonBody,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(jsonBody);

        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        using HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(jsonBody, new UTF8Encoding(false), "application/json"),
        };

        // الرمز يوضع على الطلب وحده. ولا يُكتب في سجل ولا في رسالة عطل في هذا الملف كله.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using HttpResponseMessage response = await _client.SendAsync(request, deadline.Token).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(deadline.Token).ConfigureAwait(false);

        return new ModelWireResponse((int)response.StatusCode, body, RetryAfter(response));
    }

    private static int? RetryAfter(HttpResponseMessage response)
    {
        RetryConditionHeaderValue? header = response.Headers.RetryAfter;

        if (header?.Delta is { } delta)
        {
            return (int)Math.Ceiling(delta.TotalSeconds);
        }

        return response.Headers.TryGetValues("x-ratelimit-timeremaining", out IEnumerable<string>? values)
            && int.TryParse(values.FirstOrDefault(), out int seconds)
            ? seconds
            : null;
    }
}

/// <summary>
/// <b>تصنيف الأعطال — الموضع الوحيد الذي يقرّر ما تعنيه كل حالة.</b>
/// <para>
/// وأصنافه ستّة لا خامس لها في الشيفرة: لا رمز · رُفض الرمز · تجاوز الحدّ · مهلة ·
/// نموذج غير متاح · تعذّر الوصول. وما لا يقع في واحد منها يخرج <b>مُسمّى برمزه ورقم
/// حالته ومقتطف من جسمه</b> — لا «حدث خطأ غير متوقّع».
/// </para>
/// </summary>
public static class ModelFaultClassifier
{
    /// <summary>أقصى ما يُقتطَف من جسم الردّ في رسالة عطل.</summary>
    public const int ExcerptLength = 200;

    /// <summary>يصنّف رمز حالة إلى عطل مُسمّى، أو <c>null</c> إن كان الردّ ناجحاً.</summary>
    /// <param name="response">الردّ.</param>
    /// <param name="options">الإعدادات.</param>
    public static Error? Classify(ModelWireResponse response, GitHubModelsOptions options)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(options);

        return response.StatusCode switch
        {
            >= 200 and < 300 => null,
            401 or 403 => GitHubModelsErrors.TokenRejected(response.StatusCode, options.TokenVariable),
            429 => GitHubModelsErrors.RateLimited(response.RetryAfterSeconds),
            404 or 400 when NamesTheModel(response.Body, options.ModelId) =>
                GitHubModelsErrors.ModelUnavailable(options.ModelId, response.StatusCode),
            408 => GitHubModelsErrors.TimedOut(options.Timeout),
            >= 500 => GitHubModelsErrors.ModelUnavailable(options.ModelId, response.StatusCode),
            _ => GitHubModelsErrors.UnexpectedStatus(response.StatusCode, Excerpt(response.Body)),
        };
    }

    /// <summary>يصنّف استثناءً وقع على السلك.</summary>
    /// <param name="exception">الاستثناء.</param>
    /// <param name="endpoint">العنوان.</param>
    /// <param name="options">الإعدادات.</param>
    public static Error ClassifyException(Exception exception, Uri endpoint, GitHubModelsOptions options)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(options);

        return exception switch
        {
            OperationCanceledException or TimeoutException => GitHubModelsErrors.TimedOut(options.Timeout),
            HttpRequestException http => GitHubModelsErrors.Unreachable(endpoint.Host, Describe(http)),
            _ => GitHubModelsErrors.Unreachable(endpoint.Host, exception.GetType().Name),
        };
    }

    /// <summary>يقتطف من الجسم بحدٍّ، كي لا تنقل رسالةُ عطلٍ صفحةَ HTML كاملة إلى سجل.</summary>
    /// <param name="body">الجسم.</param>
    public static string Excerpt(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        string single = body.ReplaceLineEndings(" ").Trim();
        return single.Length <= ExcerptLength ? single : single[..ExcerptLength] + "…";
    }

    private static bool NamesTheModel(string body, string modelId) =>
        body.Contains(modelId, StringComparison.OrdinalIgnoreCase)
        || body.Contains("model_not_found", StringComparison.OrdinalIgnoreCase)
        || body.Contains("unknown_model", StringComparison.OrdinalIgnoreCase);

    private static string Describe(HttpRequestException exception) =>
        exception.InnerException is SocketException socket
            ? socket.SocketErrorCode.ToString()
            : exception.HttpRequestError.ToString();
}
