using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Babel.Ai.Suggestions;
using Babel.Ai.Voice;
using Babel.ArchitectureTests.Support;
using Babel.Contracts.Voice;
using Babel.SharedKernel;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>لا مسارَ منطوق يبلغ ترحيلاً — مقيساً على العقد المنشور نفسه، لا على قائمة نيّات.</b>
/// <para>
/// <b>القاعدة:</b> يبلغ الصوت <b>كل عملية إنشاء مسوّدة</b>، ولا يبلغ <b>عملية ترحيلٍ واحدة</b>،
/// ولا توقيعاً ولا اعتماداً. <b>وهي آمنة في هذا النظام بعينه</b> لأن المسوّدة لا تمسّ الدفتر،
/// والدفترُ يُضاف إليه فقط (‏<c>REVOKE UPDATE, DELETE</c> · سلسلة بصمات SHA‑256 · عدّاد بلا
/// فجوات): فمسوّدةٌ خاطئة تُلقى بلا ثمن، وقيدٌ خاطئ يُكلّف <b>قيداً عاكساً وجيلاً ثانياً
/// يبقيان في السجلّ</b>. وبوابةُ التأكيد لم تُرفَع بل <b>انتقلت</b>: كانت تحرس الجملة، وصارت
/// تحرس الالتزام — تظهر المسوّدة على الشاشة ويكون الترحيل فعلاً بصرياً يدوياً.
/// </para>
/// <para>
/// <b>ولماذا حارسٌ بنيوي لا اختبارٌ لكل نيّة:</b> حارسٌ يعدّ نيّات اليوم لا يمنع خطأ الغد.
/// وهذا الحارس يقرأ <b>ثلاثة مصادر</b> ويقيس بينها:
/// <list type="number">
///   <item>سجلّ النيّات كما بنته الوحدات السبع — كل نيّةٍ منشورة تسمّي عمليةً واحدة.</item>
///   <item><c>contracts/openapi/v1.json</c> — العملية <b>موجودة فيه</b>، ومسارُها ليس باب ترحيل.</item>
///   <item>مرآة المتصفّح وكلّ ملفّات <c>web/src/voice/</c> — <b>لا اسمَ عملية ترحيلٍ فيها</b>،
///         لأن المتصفّح هو ما ينادي الباب فعلاً.</item>
/// </list>
/// </para>
/// <para>
/// <b>ومعه القرينة البنيوية الثانية:</b> النيّة تُعلن <c>Posts</c> <b>إذا وفقط إذا</b> كان
/// لمورد مسوّدتها بابُ ترحيلٍ منشور (‏<c>…/posting</c>). فادّعاءُ أثرٍ محاسبيّ لمستندٍ لا
/// يُرحَّل، أو إنكارُه لمستندٍ يُرحَّل، كلاهما يُحمِّر هنا.
/// </para>
/// </summary>
public sealed partial class NoVoiceIntentReachesAPostingOperation
{
    /// <summary>مقطعُ بابِ الترحيل في مسارٍ منشور — لا «data/posting-matrix» في تعليق.</summary>
    [GeneratedRegex("/posting(?![-\\w])", RegexOptions.CultureInvariant)]
    private static partial Regex PostingSegment();

    private static string ContractPath { get; } =
        Path.Combine(RepositoryLayout.Root, "contracts", "openapi", "v1.json");

    /// <summary>السجلّ كما يبنيه الجذر التركيبي — من الوحدات السبع لا من نيّاتٍ مُصطنَعة.</summary>
    private static VoiceIntentRegistry Registry { get; } = Build();

    private static VoiceIntentRegistry Build()
    {
        IVoiceIntentCatalogue[] catalogues =
        [
            new Babel.Ledger.Voice.LedgerVoiceIntents(),
            new Babel.Purchasing.Voice.PurchasingVoiceIntents(),
            new Babel.Sales.Voice.SalesVoiceIntents(),
            new Babel.Projects.Voice.ProjectsVoiceIntents(),
            new Babel.Hr.Voice.HrVoiceIntents(),
            new Babel.Inventory.Voice.InventoryVoiceIntents(),
            new Babel.RealEstate.Voice.RealEstateVoiceIntents(),
        ];

        Result<VoiceIntentRegistry> built = VoiceIntentRegistry.Build(catalogues, MatrixPostingVocabulary.Default);

        return built.IsSuccess
            ? built.Value
            : throw new InvalidOperationException(
                "سجلّ النيّات لم يُبنَ: " + string.Join(" · ", built.Errors.Select(static error => error.MessageAr)));
    }

    /// <summary>العمليات المنشورة: المعرّف ← المسار والفعل.</summary>
    private static Dictionary<string, (string Path, string Method)> Operations()
    {
        Dictionary<string, (string Path, string Method)> found = new(StringComparer.Ordinal);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ContractPath));

        foreach (JsonProperty path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (JsonProperty operation in path.Value.EnumerateObject())
            {
                if (operation.Value.ValueKind == JsonValueKind.Object
                    && operation.Value.TryGetProperty("operationId", out JsonElement id)
                    && id.GetString() is string name)
                {
                    found[name] = (path.Name, operation.Name);
                }
            }
        }

        return found;
    }

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

    [Fact]
    public void العقد_المنشور_ليس_ضامراً_ولا_السجل()
    {
        // حارس لا فراغ (‏فخ-43): عقدٌ لا يُقرأ، أو سجلٌّ فارغ، يجعل كل ما تحته يمرّ على لا شيء.
        Assert.True(Operations().Count >= 150, "العمليات المقروءة: " + Count(Operations().Count));
        Assert.True(Registry.Count >= 40, "النيّات المقروءة: " + Count(Registry.Count));
    }

    [Fact]
    public void كل_نية_منشورة_تسمي_عمليةً_موجودة_في_العقد()
    {
        Dictionary<string, (string Path, string Method)> operations = Operations();
        int measured = 0;

        foreach (VoiceIntent intent in Registry.Intents)
        {
            if (intent.Status == VoiceIntentStatus.AwaitingOwnerDecision)
            {
                Assert.Null(intent.OperationId);
                continue;
            }

            measured++;
            Assert.NotNull(intent.OperationId);
            Assert.True(
                operations.ContainsKey(intent.OperationId!),
                "النيّة «" + intent.Id + "» تسمّي العملية «" + intent.OperationId
                + "» وهي ليست في العقد المنشور. وبابٌ لا وجود له يُنتج مسوّدةً لا تُحفَظ.");
        }

        Assert.True(measured >= 40, "النيّات المنشورة: " + Count(measured));
    }

    [Fact]
    public void لا_نية_تبلغ_ترحيلاً_ولا_توقيعاً_ولا_اعتماداً()
    {
        Dictionary<string, (string Path, string Method)> operations = Operations();

        // ‏**مسارات الأفعال التي لا تُعكَس** — تُقرأ من المسار لا من الاسم وحده، فعمليةٌ
        // تُسمّى غداً «commitClientCertificate» ومسارُها «…/posting» تبقى ممنوعة.
        string[] irreversibleSegments =
            ["/posting", "/activation", "/approval", "/termination", "/revocation", "/reversal", "/lapse"];

        int measured = 0;

        foreach (VoiceIntent intent in Registry.Intents)
        {
            if (intent.OperationId is null)
            {
                continue;
            }

            measured++;

            Assert.Null(VoiceOperationGuard.Refuse(intent.OperationId));

            (string Path, string Method) operation = operations[intent.OperationId];

            foreach (string segment in irreversibleSegments)
            {
                Assert.False(
                    operation.Path.EndsWith(segment, StringComparison.Ordinal),
                    "النيّة «" + intent.Id + "» تبلغ «" + operation.Path
                    + "» — وهو بابُ أثرٍ لا يُعكَس. والصوت يبلغ المسوّدة وحدها.");
            }
        }

        Assert.True(measured >= 40, "العمليات المقيسة: " + Count(measured));
    }

    [Fact]
    public void لا_عملية_ترحيلٍ_واحدة_يسمّيها_مسار_الصوت_في_المتصفح()
    {
        // ‏**والمتصفّح هو ما ينادي الباب فعلاً**: سطرٌ يُكتب هناك بيدٍ لا يراه أي حارسٍ
        // في الخادم إلا هذا. فتُمسح ملفّات المكوّن كلّها بحثاً عن اسم عملية ترحيلٍ منشورة.
        Dictionary<string, (string Path, string Method)> operations = Operations();

        string[] posting =
        [
            .. operations.Keys.Where(static name => name.StartsWith("post", StringComparison.Ordinal)),
            "approveLeaseRegistrationForBilling",
            "terminateEmployee",
            "reverseJournalEntry",
            "revokeMembership",
        ];

        Assert.True(posting.Length >= 20, "أسماء الترحيل المقروءة: " + Count(posting.Length));

        string folder = Path.Combine(RepositoryLayout.Root, "web", "src", "voice");
        string[] files = [.. Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(static file => file.EndsWith(".ts", StringComparison.Ordinal)
                               || file.EndsWith(".tsx", StringComparison.Ordinal))];

        Assert.True(files.Length >= 8, "ملفّات المكوّن المقروءة: " + Count(files.Length));

        foreach (string file in files)
        {
            string text = File.ReadAllText(file);

            foreach (string name in posting)
            {
                Assert.False(
                    text.Contains(name, StringComparison.Ordinal),
                    Path.GetFileName(file) + " يسمّي «" + name + "» — والمسار المنطوق لا يبلغ ترحيلاً ولا توقيعاً.");
            }

            // ‏«data/posting-matrix» في تعليقٍ ليست مساراً منشوراً: يُمنع المقطع
            // وحده — «/posting» لا يتبعه حرفٌ ولا شرطة — لا كلُّ ذكرٍ للكلمة.
            Assert.False(
                PostingSegment().IsMatch(text),
                Path.GetFileName(file) + " يحمل مقطع «/posting» — وهو بابُ الترحيل بعينه.");
        }
    }

    [Fact]
    public void الأثر_المحاسبي_مُعلَنٌ_إذا_وفقط_إذا_كان_للمستند_باب_ترحيل()
    {
        Dictionary<string, (string Path, string Method)> operations = Operations();
        HashSet<string> postingPaths = [.. operations.Values
            .Where(static operation => operation.Path.EndsWith("/posting", StringComparison.Ordinal))
            .Select(static operation => operation.Path)];

        Assert.True(postingPaths.Count >= 20, "أبواب الترحيل المقروءة: " + Count(postingPaths.Count));

        int drafts = 0;

        foreach (VoiceIntent intent in Registry.Intents)
        {
            if (intent.OperationId is null || !intent.OperationId.StartsWith("draft", StringComparison.Ordinal))
            {
                continue;
            }

            drafts++;

            string resource = operations[intent.OperationId].Path;
            bool hasPostingDoor = postingPaths.Any(path =>
                path.StartsWith(resource + "/", StringComparison.Ordinal));

            Assert.Equal(hasPostingDoor, intent.LedgerEffect == VoiceLedgerEffect.Posts);
        }

        Assert.True(drafts >= 20, "نيّات المسوّدات المقيسة: " + Count(drafts));
    }
}
