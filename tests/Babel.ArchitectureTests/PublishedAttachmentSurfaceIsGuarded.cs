using System.Reflection;
using System.Text.Json;
using Babel.ArchitectureTests.Support;
using Babel.Contracts.Storage;
using Babel.Storage;
using Babel.Storage.Surface;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>ثلاثة حرّاس على سطح المرفقات المنشور.</b>
/// <para>
/// ‏<b>الأول — لا فعل تعديل ولا حذف على مرفق في العقد المنشور.</b> المرفق سندُ إثبات
/// لقيد، فيأخذ انضباط الدفتر: التصحيح إصدارٌ يشير إلى سلفه، والإزالة علامة سحب
/// (‏ADR-0046). وذلك مفروض في PostgreSQL بطبقتين — سحبُ <c>UPDATE</c> و<c>DELETE</c>
/// من دور التطبيق (‏42501)، ومشغّل رفضٍ على <b>كل</b> دور والمالك منهم (‏23001) — وهذا
/// الحارس يفرضه على <b>العقد المنشور</b>: بابٌ اسمه <c>DELETE …/attachments/{id}</c>
/// يُودَع في الوثيقة يُبنى عليه عميل قبل أن يكتشف أحدٌ أن الخادم لا يملكه.
/// </para>
/// <para>
/// ‏<b>والثاني — السطح المنشور لا يحمل إلا منفذين وإعدادات.</b> ‏<see cref="AttachmentSurface"/>
/// هو <b>الطريق الوحيد</b> من HTTP إلى المرفقات. فلو أخذ في مُنشئه سياق قاعدة بيانات
/// أو محوّلاً بعينه لأمكنه أن يقرأ ويكتب <b>بلا مرورٍ بالمنفذ</b> — أي بلا شمٍّ ولا
/// بصمة ولا مستأجرٍ في المفتاح. والحدّ هنا بنيوي لا اتفاقي: <b>منافذ العقد وإعدادات
/// المخزن، ولا شيء غيرهما</b>.
/// </para>
/// <para>
/// ‏<b>والثالث — مفتاح التوقيع وجذر التخزين لا يُودَعان.</b> مفتاحٌ مُودَع مفتاحٌ عامّ،
/// ومن يقرؤه يسكّ تذاكر لأي مرفق في أي نشرة تستعمله. وشيفرة المخزن تقرؤهما من البيئة،
/// و<b>لا نصّ ست‌عشري بطول مفتاح</b> يظهر في مصدرها ولا في إعداد الخادم المُودَع.
/// </para>
/// </summary>
public sealed class PublishedAttachmentSurfaceIsGuarded
{
    /// <summary>العقد المنشور كما أُودع.</summary>
    private static string ContractPath { get; } =
        Path.Combine(RepositoryLayout.Root, "contracts", "openapi", "v1.json");

    /// <summary>
    /// جزء المسار الذي يُميّز مورد مرفق. <b>مكتوب لا مشتقّ</b>: اشتقاقه من الوثيقة
    /// نفسها كان سيجعل الحارس يقارن الوثيقة بذاتها فيمرّ على أي حذف.
    /// </summary>
    private const string AttachmentSegment = "/attachments";

    /// <summary>الأفعال التي تُعدّل مورداً قائماً أو تُزيله.</summary>
    private static readonly string[] MutatingVerbs = ["delete", "patch", "put"];

    // ── الحارس الأول ─────────────────────────────────────────────────────────

    /// <summary>
    /// المخالفات: كل (فعل، مسار) على مورد مرفق بفعلٍ يُعدّل أو يحذف.
    /// <b>دالّة نقيّة</b> تُغذّى بالوثيقة، فتُختبر من طرفيها.
    /// </summary>
    /// <param name="paths">كائن <c>paths</c> من الوثيقة.</param>
    internal static IReadOnlyList<string> Offenders(JsonElement paths) =>
    [
        .. paths.EnumerateObject()
            .Where(static path => path.Name.Contains(AttachmentSegment, StringComparison.Ordinal))
            .SelectMany(static path => path.Value.EnumerateObject()
                .Where(static verb => MutatingVerbs.Contains(verb.Name, StringComparer.Ordinal))
                .Select(verb => verb.Name + " " + path.Name))
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>عدد عمليات موارد المرفقات التي فحصها الحارس فعلاً.</summary>
    /// <param name="paths">كائن <c>paths</c> من الوثيقة.</param>
    internal static int Examined(JsonElement paths) => paths.EnumerateObject()
        .Where(static path => path.Name.Contains(AttachmentSegment, StringComparison.Ordinal))
        .Sum(static path => path.Value.EnumerateObject()
            .Count(static verb => verb.Name is "get" or "post" or "put" or "patch" or "delete"));

    [Fact]
    public void لا_فعل_تعديل_ولا_حذف_على_أي_مورد_مرفق_في_العقد_المنشور()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ContractPath));
        JsonElement paths = document.RootElement.GetProperty("paths");

        int examined = Examined(paths);
        Assert.True(
            examined >= 7,
            FormattableString.Invariant(
                $"الحارس فحص {examined} عمليةً على موارد المرفقات — أقلّ من أن يعني «لا مخالفة» شيئاً."));

        IReadOnlyList<string> offenders = Offenders(paths);

        Assert.True(
            offenders.Count == 0,
            "فعل تعديل أو حذف على مورد مرفق في العقد المنشور. والمرفق سندُ إثبات: تصحيحه "
            + "إصدارٌ جديد على …/revisions يشير إلى سلفه، وإزالته علامةٌ على …/withdrawal، "
            + "والبايتات تبقى والبصمة تبقى (ADR-0046):\n"
            + string.Join('\n', offenders));
    }

    [Fact]
    public void الحارس_الأول_يرى_المخالفة_حين_تُزرع_فعلاً()
    {
        using JsonDocument mutated = JsonDocument.Parse(
            """
            {
              "/api/v1/companies/{companyId}/attachments/{attachmentId}": {
                "get": {}, "delete": {}, "put": {}
              },
              "/api/v1/companies/{companyId}/attachments": { "post": {}, "get": {} },
              "/api/v1/companies/{companyId}/journal-entries": { "post": {}, "delete": {} }
            }
            """);

        Assert.Equal(
            [
                "delete /api/v1/companies/{companyId}/attachments/{attachmentId}",
                "put /api/v1/companies/{companyId}/attachments/{attachmentId}",
            ],
            Offenders(mutated.RootElement));
    }

    [Fact]
    public void الحارس_الأول_يُعلن_ضموره_حين_يختفي_السطح()
    {
        using JsonDocument empty = JsonDocument.Parse(
            """{ "/api/v1/companies/{companyId}/journal-entries": { "post": {} } }""");

        Assert.Empty(Offenders(empty.RootElement));
        Assert.Equal(0, Examined(empty.RootElement));
    }

    // ── الحارس الثاني ────────────────────────────────────────────────────────

    /// <summary>ما يجوز للسطح أن يحمله: منافذ العقد، وإعدادات المخزن.</summary>
    private static readonly Type[] Allowed =
        [typeof(IAttachmentStore), typeof(IAttachmentTickets), typeof(StorageOptions)];

    /// <summary>
    /// المتعاونون غير المسموح بهم. <b>دالّة نقيّة</b> تُغذّى بقائمة أنواع.
    /// </summary>
    /// <param name="parameters">أنواع وسائط المُنشئ.</param>
    internal static IReadOnlyList<string> Unguarded(IEnumerable<Type> parameters) =>
    [
        .. parameters
            .Where(static type => !Allowed.Contains(type))
            .Select(static type => type.FullName ?? type.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    [Fact]
    public void سطح_المرفقات_لا_يحمل_إلا_منفذي_العقد_وإعدادات_المخزن()
    {
        ConstructorInfo constructor = Assert.Single(typeof(AttachmentSurface).GetConstructors());
        Type[] parameters = [.. constructor.GetParameters().Select(static p => p.ParameterType)];

        // اللافراغ: السطح يحمل المنفذين فعلاً. سطحٌ بلا منفذ يجعل «لا مخالفة» جملةً عن لا شيء.
        Assert.Contains(typeof(IAttachmentStore), parameters);
        Assert.Contains(typeof(IAttachmentTickets), parameters);

        Assert.True(
            Unguarded(parameters).Count == 0,
            "سطح المرفقات يحمل متعاوناً ليس منفذاً من العقد ولا إعدادات المخزن. ومحوّلٌ بعينه "
            + "أو سياق قاعدة بيانات في مُنشئه يفتح طريقاً إلى البايتات **لا يمرّ بالشمّ ولا "
            + "بالبصمة ولا بمستأجرٍ في المفتاح**:\n"
            + string.Join('\n', Unguarded(parameters)));
    }

    [Fact]
    public void الحارس_الثاني_يرى_المتعاون_غير_المحروس_حين_يُزرع()
    {
        // الطفرة: مُنشئٌ يأخذ المحوّل بعينه بدل المنفذ — وهو الطريق الذي يلتفّ على
        // إمكان تبديل المخزن، ويربط سطح HTTP بنظام ملفّات بالاسم.
        Assert.Equal(
            ["Babel.Storage.FileSystemAttachmentStore", "Babel.Storage.InMemoryAttachmentStore"],
            Unguarded([typeof(FileSystemAttachmentStore), typeof(InMemoryAttachmentStore)]));

        // ومن الطرف الآخر: المنفذان والإعدادات تمرّ، فالحارس ليس رافضاً لكل شيء.
        Assert.Empty(Unguarded([typeof(IAttachmentStore), typeof(IAttachmentTickets), typeof(StorageOptions)]));
    }

    // ── الحارس الثالث ────────────────────────────────────────────────────────

    [Fact]
    public void لا_مفتاح_توقيع_ولا_جذر_تخزين_مُودَع_في_شيفرة_المخزن_ولا_في_إعداد_الخادم()
    {
        string storageRoot = Path.Combine(RepositoryLayout.Root, "src", "Babel.Storage");
        string apiSettings = Path.Combine(RepositoryLayout.Root, "src", "Babel.Api", "appsettings.json");

        List<string> suspects = [];
        int scanned = 0;

        foreach (string file in Directory.EnumerateFiles(storageRoot, "*.cs", SearchOption.AllDirectories)
            .Concat([apiSettings])
            .Where(File.Exists)
            .Order(StringComparer.Ordinal))
        {
            scanned++;
            string text = File.ReadAllText(file);

            // نصٌّ ست‌عشري متّصل بطول 32 بايتاً فأكثر: هذا هو شكل المفتاح المُودَع،
            // ولا شيء آخر في هذه الشجرة يكتب أربعاً وستّين خانة متتالية.
            foreach (System.Text.RegularExpressions.Match match in
                System.Text.RegularExpressions.Regex.Matches(
                    text, "[0-9a-fA-F]{64,}", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(5)))
            {
                suspects.Add(Path.GetRelativePath(RepositoryLayout.Root, file) + ": " + match.Value[..16] + "…");
            }
        }

        Assert.True(scanned >= 8, FormattableString.Invariant($"فُحص {scanned} ملفّاً فقط — الحارس ضامر."));

        Assert.True(
            suspects.Count == 0,
            "نصٌّ يشبه مفتاحاً مُودَعاً في شيفرة المخزن أو في إعداد الخادم. والمفتاح يأتي من "
            + "البيئة وحدها، وغيابُه عطلٌ يُعلَن عند التركيب لا مفتاحٌ يُخترع (ADR-0046):\n"
            + string.Join('\n', suspects));
    }
}
