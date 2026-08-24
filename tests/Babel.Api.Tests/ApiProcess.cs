using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Babel.Api.Tests;

/// <summary>
/// خادم «سلاسل بابل» مُقلَعاً <b>عمليةً مستقلّة</b>، ومخاطَباً عبر HTTP وحده.
/// <para>
/// وهذا هو جوهر ما طُلب: «العزل التام بين فرونت اند وباك اند». اختبارٌ يستضيف الخادم داخل
/// عمليته يستطيع أن يصل إلى حاويته وإلى أنواعه الداخلية وإلى ساعته، فيُثبت أشياء لن تكون
/// صحيحة للواجهة الحقيقية. أما هنا فلا يملك الاختبار إلا ما تملكه الواجهة: عنوان، واعتماد،
/// وعقد منشور.
/// </para>
/// <para>
/// ولذلك أيضاً <b>تُضبط الثقافة بمتغيّر بيئة النظام</b> (<c>LANG</c>/<c>LC_ALL</c>) لا بسطر
/// شيفرة داخل العملية: هذه هي الطريقة التي تُضبط بها ثقافة خادم إنتاج فعلاً، وهي التي
/// تُنتج المفاجأة — خادم عربي تقويمه الافتراضي أم القرى.
/// </para>
/// </summary>
internal sealed class ApiProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly StringBuilder _output = new();

    private ApiProcess(Process process, HttpClient client, int port)
    {
        _process = process;
        Client = client;
        Port = port;
    }

    /// <summary>عميل HTTP موجَّه إلى هذه العملية. لا شيء غيره يصل إليها.</summary>
    public HttpClient Client { get; }

    /// <summary>المنفذ الذي أقلعت عليه.</summary>
    public int Port { get; }

    /// <summary>كل ما كتبته العملية على المُخرَج القياسي وعلى الخطأ القياسي.</summary>
    public string Output
    {
        get
        {
            lock (_output)
            {
                return _output.ToString();
            }
        }
    }

    /// <summary>يُقلع الخادم وينتظر حتى يستجيب لنقطة الصحّة.</summary>
    /// <param name="environment">متغيّرات البيئة الإضافية — الإعداد كله يصل بها.</param>
    /// <param name="culture">اسم الموضع النظامي، مثل <c>ar_SA.UTF-8</c>.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<ApiProcess> StartAsync(
        IReadOnlyDictionary<string, string> environment,
        string culture,
        CancellationToken cancellationToken = default)
    {
        string executable = RepositoryPaths.ApiExecutable;

        if (!File.Exists(executable))
        {
            throw new InvalidOperationException(
                $"ثنائي الخادم غير موجود عند «{executable}». هذه المجموعة لا تشير إلى Babel.Api بمرجع مشروع عمداً "
                + "(القاعدة 3)، فهي تحتاج أن يكون الحل قد بُني كاملاً قبل تشغيلها: dotnet build Babel.slnx. / "
                + $"The server binary is missing at '{executable}'. This suite deliberately holds no project reference to "
                + "Babel.Api (Rule 03), so it requires the whole solution to have been built first.");
        }

        int port = FreePort();

        ProcessStartInfo start = new(executable)
        {
            WorkingDirectory = RepositoryPaths.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        start.Environment["ASPNETCORE_URLS"] = string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}");
        start.Environment["DOTNET_ENVIRONMENT"] = "Production";
        start.Environment["Logging__LogLevel__Default"] = "Warning";
        start.Environment["LANG"] = culture;
        start.Environment["LC_ALL"] = culture;

        foreach ((string key, string value) in environment)
        {
            start.Environment[key] = value;
        }

        Process process = new() { StartInfo = start, EnableRaisingEvents = true };

        ApiProcess api = new(
            process,
            new HttpClient
            {
                BaseAddress = new Uri(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/")),
                Timeout = TimeSpan.FromSeconds(30),
            },
            port);

        process.OutputDataReceived += api.Capture;
        process.ErrorDataReceived += api.Capture;

        if (!process.Start())
        {
            throw new InvalidOperationException("تعذّر إقلاع الخادم. / Could not start the server process.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await api.WaitUntilHealthyAsync(cancellationToken).ConfigureAwait(false);
        return api;
    }

    /// <summary>يوقف العملية ويحرّر العميل.</summary>
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        catch (InvalidOperationException)
        {
            // العملية انتهت بين الفحص والقتل — لا شيء يُفعل.
        }

        _process.Dispose();
    }

    private void Capture(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is null)
        {
            return;
        }

        lock (_output)
        {
            _output.AppendLine(e.Data);
        }
    }

    private async Task WaitUntilHealthyAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(60);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant($"انتهت عملية الخادم قبل أن تستجيب (رمز الخروج {_process.ExitCode}).\n")
                    + Output);
            }

            try
            {
                using HttpResponseMessage response = await Client
                    .GetAsync(new Uri("/health", UriKind.Relative), cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // لم يبدأ الاستماع بعد.
            }
            catch (TaskCanceledException)
            {
                // مهلة قصيرة أثناء الإقلاع.
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("لم يستجب الخادم خلال المهلة.\n" + Output);
    }

    private static int FreePort()
    {
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        return ((System.Net.IPEndPoint)socket.LocalEndPoint!).Port;
    }
}

/// <summary>
/// اعتماد اختباري: نصّه يبقى في الذاكرة، و<b>بصمته وحدها</b> هي ما يصل إلى إعداد الخادم.
/// <para>
/// وهذا ليس تجميلاً: الشكل نفسه في الإنتاج. لا يوجد في أي ملف إعداد ولا في أي متغيّر بيئة
/// قيمةٌ تصلح للانتحال — من يقرأ الإعداد يقرأ <c>SHA-256</c> ولا يستطيع أن يعكسه.
/// </para>
/// </summary>
/// <param name="Value">النصّ المقدَّم في ترويسة <c>Authorization</c>.</param>
/// <param name="Tenant">المستأجر.</param>
/// <param name="User">المستخدم.</param>
/// <param name="Companies">الشركات التي يبلغها.</param>
internal sealed record TestCredential(string Value, Guid Tenant, Guid User, IReadOnlyList<Guid> Companies)
{
    /// <summary>ينشئ اعتماداً عشوائياً — لا نصّ ثابت يُودَع في المستودع.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="user">المستخدم.</param>
    /// <param name="companies">الشركات.</param>
    public static TestCredential Create(Guid tenant, Guid user, params Guid[] companies) =>
        new(Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24)), tenant, user, companies);

    /// <summary>بصمة الاعتماد كما تُكتب في الإعداد.</summary>
    public string Digest => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Value)));

    /// <summary>قيمة ترويسة التصريح.</summary>
    public string Header => "Bearer " + Value;
}
