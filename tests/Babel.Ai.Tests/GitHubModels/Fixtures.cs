using System.Net.Sockets;
using System.Text.Json;
using Babel.Ai.Extraction;
using Babel.Ai.Extraction.GitHubModels;
using Babel.Ai.Tests.Support;
using Babel.SharedKernel;

namespace Babel.Ai.Tests.GitHubModels;

/// <summary>سلك مُبرمَج: يعيد ردّاً مُودَعاً أو يرمي عطلاً — ويعدّ كم مرّة نُودي.</summary>
internal sealed class ScriptedWire : IModelWire
{
    private readonly Func<ModelWireResponse> _answer;
    private readonly Exception? _throws;

    private ScriptedWire(Func<ModelWireResponse> answer, Exception? throws)
    {
        _answer = answer;
        _throws = throws;
    }

    /// <summary>كم مرّة خرج طلب إلى السلك. <b>يُفحص:</b> «لا إعادة تلقائية» ادّعاء يُقاس.</summary>
    public int Calls { get; private set; }

    /// <summary>آخر جسم طلب خرج — يُفحص عليه ألا يحمل رمز حساب.</summary>
    public string LastBody { get; private set; } = string.Empty;

    /// <summary>آخر رمز مُرِّر — يُفحص أنه هو ما قُرئ من البيئة لا شيء آخر.</summary>
    public string LastToken { get; private set; } = string.Empty;

    public static ScriptedWire Answering(int status, string body, int? retryAfter = null) =>
        new(() => new ModelWireResponse(status, body, retryAfter), null);

    public static ScriptedWire Throwing(Exception exception) => new(() => throw exception, exception);

    public ValueTask<ModelWireResponse> SendAsync(
        Uri endpoint,
        string token,
        string jsonBody,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Calls++;
        LastBody = jsonBody;
        LastToken = token;

        return _throws is not null ? throw _throws : ValueTask.FromResult(_answer());
    }
}

/// <summary>قارئ أسرار في الذاكرة — لا يلمس بيئة العملية، فالاختبار يمرّ متوازياً.</summary>
internal sealed class InMemorySecrets(string? value) : ISecretReader
{
    public string? Read(string variable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variable);
        return value;
    }
}

/// <summary>بيئة اختبار المزوّد البعيد.</summary>
internal static class ModelFixtures
{
    /// <summary>الرمز المستعمَل في الاختبار. <b>ليس سرّاً</b>: نصّ لا يعمل في أي مكان.</summary>
    public const string TestToken = "not-a-real-token-value";

    /// <summary>اسم متغيّر البيئة كما يُقرأ من الإعداد.</summary>
    public const string TokenVariable = "BABEL_AI_TEST_TOKEN";

    /// <summary>معرّف نموذج للاختبار. يُقرأ من الإعداد كما في التشغيل.</summary>
    public const string ModelId = "openai/gpt-4o-mini";

    public static GitHubModelsOptions Options() => new()
    {
        TokenVariable = TokenVariable,
        Endpoint = new Uri("https://models.example.invalid/inference/chat/completions"),
        ModelId = ModelId,
        Timeout = TimeSpan.FromSeconds(7),
        MaxOutputTokens = 2_000,
    };

    /// <summary>يقرأ متجه ردّ مُودَعاً، ويحقن فيه محتوى إن طُلب.</summary>
    public static string Envelope(string name, string? content = null)
    {
        string text = File.ReadAllText(RepositoryRoot.At("tests/Babel.Ai.Tests/fixtures/github-models/" + name));

        return content is null
            ? text
            : text.Replace("\"__CONTENT__\"", JsonSerializer.Serialize(content), StringComparison.Ordinal)
                  .Replace("__CONTENT__", JsonEscape(content), StringComparison.Ordinal);
    }

    /// <summary>مُخرَج صالح تماماً — نفس ما يجب أن يعيده مزوّد حقيقي.</summary>
    public static string ValidExtraction() =>
        DeterministicExtractionProvider.Compose(CaptureHarness.ConsistentInvoice());

    public static ExtractionRequest Request() => new()
    {
        Tenant = new TenantId(Guid.CreateVersion7()),
        DocumentId = CaptureHarness.DocumentId,
        Channel = Babel.Ai.Capture.CaptureChannel.Chat,
        MediaType = "image/jpeg",
        Content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
    };

    /// <summary>عطل مقبس يُثبت أن الطلب لم يغادر.</summary>
    public static Exception ConnectionRefused() =>
        new HttpRequestException("connect", new SocketException((int)SocketError.ConnectionRefused));

    private static string JsonEscape(string value)
    {
        string serialised = JsonSerializer.Serialize(value);
        return serialised[1..^1];
    }
}
