using System.Reflection;
using System.Text.Json;
using Babel.ArchitectureTests.Support;
using Babel.Core.Application;
using Babel.Purchasing;
using Babel.Purchasing.Application;
using Babel.Purchasing.Surface;
using Babel.Sales;
using Babel.Sales.Application;
using Babel.Sales.Surface;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>حارسان على سطح المستندات المنشور.</b>
/// <para>
/// ‏<b>الأول — لا فعل تعديل ولا حذف على مستند:</b> الدفتر يُضاف إليه فقط، و<c>UPDATE</c>
/// و<c>DELETE</c> منزوعتان من دور التطبيق في PostgreSQL نفسها، والمستند المُرحَّل
/// <b>يُعكس أو يُصحَّح بإشعار</b> ولا يُعدَّل (‏ADR-0002 · ADR-0003). وذلك مفروض في
/// الطبقات السفلى؛ وهذا الحارس يفرضه على <b>العقد المنشور</b>: بابٌ اسمه
/// <c>DELETE /sales-invoices/{id}</c> يُودَع في الوثيقة يُبنى عليه عميل قبل أن يكتشف
/// أحدٌ أنّ الخادم لا يملكه أصلاً — والعقد المنشور هو ما يقرؤه فريق الواجهة، لا الشيفرة.
/// </para>
/// <para>
/// ‏<b>والثاني — السطح المنشور لا يحمل إلا خدماتٍ يحرسها الاستحقاق:</b>
/// <c>SalesSurface</c> و<c>PurchasingSurface</c> ليستا <c>IApplicationService</c>، فلا
/// تراهما القاعدة 6؛ وهما مع ذلك <b>الطريق الوحيد</b> من HTTP إلى الوحدتين. فلو أخذت
/// إحداهما <c>SalesRuntime</c> أو سياق قاعدة بيانات في مُنشئها لأمكنها أن تقرأ وتكتب
/// <b>بلا مرورٍ بالمنفِّذ</b> — وهي ثغرة تصريح كاملة، مفتوحة بسطر واحد في مُنشئ، لا
/// يلتقطها حارس قائم. وهذا الحارس يمنع ذلك بنيوياً: <b>لا تحمل السطوح إلا خدمات تطبيق
/// وإعدادات</b>، وخدمة التطبيق لا تُفلت من القاعدة 6.
/// </para>
/// </summary>
public sealed class PublishedDocumentSurfaceIsGuarded
{
    /// <summary>العقد المنشور كما أُودع.</summary>
    private static string ContractPath { get; } =
        Path.Combine(RepositoryLayout.Root, "contracts", "openapi", "v1.json");

    /// <summary>
    /// أجزاء المسار التي تُميّز مورد مستند تجاري. <b>مكتوبة لا مشتقّة</b>: اشتقاقها
    /// من الوثيقة نفسها كان سيجعل الحارس يقارن الوثيقة بذاتها، فيمرّ على أي حذف.
    /// </summary>
    private static readonly string[] DocumentSegments =
    [
        "/credit-notes",
        "/customers",
        "/payables-aging",
        "/receivables-aging",
        "/sales-invoices",
        "/supplier-bills",
        "/suppliers",
    ];

    /// <summary>الأفعال التي تُعدّل مورداً قائماً أو تُزيله.</summary>
    private static readonly string[] MutatingVerbs = ["delete", "patch", "put"];

    // ── الحارس الأول ─────────────────────────────────────────────────────────

    /// <summary>
    /// المخالفات: كل (فعل، مسار) على مورد مستند بفعلٍ يُعدّل أو يحذف.
    /// <b>دالّة نقيّة</b> تُغذّى بالوثيقة، فيمكن تغذيتها بوثيقةٍ مُشوَّهة لإثبات أنها ترى.
    /// </summary>
    /// <param name="paths">كائن <c>paths</c> من الوثيقة.</param>
    internal static IReadOnlyList<string> Offenders(JsonElement paths) =>
    [
        .. paths.EnumerateObject()
            .Where(static path => DocumentSegments.Any(segment =>
                path.Name.Contains(segment, StringComparison.Ordinal)))
            .SelectMany(static path => path.Value.EnumerateObject()
                .Where(static verb => MutatingVerbs.Contains(verb.Name, StringComparer.Ordinal))
                .Select(verb => verb.Name + " " + path.Name))
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>عدد عمليات موارد المستندات التي فحصها الحارس فعلاً.</summary>
    /// <param name="paths">كائن <c>paths</c> من الوثيقة.</param>
    internal static int Examined(JsonElement paths) => paths.EnumerateObject()
        .Where(static path => DocumentSegments.Any(segment =>
            path.Name.Contains(segment, StringComparison.Ordinal)))
        .Sum(static path => path.Value.EnumerateObject()
            .Count(static verb => verb.Name is "get" or "post" or "put" or "patch" or "delete"));

    [Fact]
    public void لا_فعل_تعديل_ولا_حذف_على_أي_مورد_مستند_في_العقد_المنشور()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ContractPath));
        JsonElement paths = document.RootElement.GetProperty("paths");

        // ‏**الطرف الأول من اللافراغ:** الحارس رأى سطحاً حقيقياً. لو أُزيل السطح كلّه،
        // أو تغيّرت مقاطع مساراته، لصار «صفر مخالفات» جملةً عن لا شيء.
        int examined = Examined(paths);
        Assert.True(
            examined >= 14,
            FormattableString.Invariant(
                $"الحارس فحص {examined} عمليةً على موارد المستندات — أقلّ من أن يعني «لا مخالفة» شيئاً."));

        IReadOnlyList<string> offenders = Offenders(paths);

        Assert.True(
            offenders.Count == 0,
            "فعل تعديل أو حذف على مورد مستند في العقد المنشور. والمستند المُرحَّل واقعة "
            + "لا تُعدَّل: تصحيحه إشعارٌ دائن أو قيد عكس يُنشئ قيداً جديداً، والمسوّدة لا "
            + "مسار تعديل لها على هذا السطح بعد (ADR-0002 · ADR-0003):\n"
            + string.Join('\n', offenders));
    }

    [Fact]
    public void الحارس_الأول_يرى_المخالفة_حين_تُزرع_فعلاً()
    {
        // ‏**الطرف الثاني من اللافراغ — اختبار طفرة.** الوثيقة أعلاه نظيفة، و«صفر
        // مخالفات» عليها لا يُثبت أن الكاشف يعمل. فتُبنى وثيقةٌ مُشوَّهة بالشكل الذي
        // يجب أن يُمسَك: مسوّدةٌ تُحذف، وفاتورةٌ مُرحَّلة تُعدَّل بـPUT.
        using JsonDocument mutated = JsonDocument.Parse(
            """
            {
              "/api/v1/companies/{companyId}/sales-invoices/{invoiceId}": {
                "get": {}, "delete": {}, "put": {}
              },
              "/api/v1/companies/{companyId}/credit-notes": { "post": {} },
              "/api/v1/companies/{companyId}/journal-entries": { "post": {}, "delete": {} }
            }
            """);

        IReadOnlyList<string> offenders = Offenders(mutated.RootElement);

        // المسار غير المستندي لا يدخل نطاق هذا الحارس — يحرسه PublishedContractTests —
        // فلا يُحسب هنا، ولا تُبتلع مخالفته: كلٌّ في موضعه.
        Assert.Equal(
            [
                "delete /api/v1/companies/{companyId}/sales-invoices/{invoiceId}",
                "put /api/v1/companies/{companyId}/sales-invoices/{invoiceId}",
            ],
            offenders);
    }

    [Fact]
    public void الحارس_الأول_يُعلن_ضموره_حين_يختفي_السطح()
    {
        // والطفرة المقابلة: وثيقةٌ بلا موارد مستندات إطلاقاً. عندئذ Offenders تُرجع
        // صفراً — **وهو صفرٌ كاذب**، وما يمسكه هو عدّاد ما فُحص لا عدّاد المخالفات.
        using JsonDocument empty = JsonDocument.Parse(
            """{ "/api/v1/companies/{companyId}/journal-entries": { "post": {}, "delete": {} } }""");

        Assert.Empty(Offenders(empty.RootElement));
        Assert.Equal(0, Examined(empty.RootElement));
    }

    // ── الحارس الثاني ────────────────────────────────────────────────────────

    /// <summary>السطوح المنشورة التي يناديها الجذر التركيبي.</summary>
    private static readonly Type[] PublishedSurfaces = [typeof(SalesSurface), typeof(PurchasingSurface)];

    /// <summary>الإعدادات المسموح للسطح أن يحملها — العملة تُقرأ منها ولا شيء غيرها.</summary>
    private static readonly Type[] AllowedOptions = [typeof(SalesOptions), typeof(PurchasingOptions)];

    /// <summary>
    /// المتعاونون غير المسموح بهم: كل نوع ليس خدمة تطبيق ولا إعدادات وحدة.
    /// <b>دالّة نقيّة</b> تُغذّى بقائمة أنواع، فتُختبر من طرفيها.
    /// </summary>
    /// <param name="parameters">أنواع وسائط المُنشئ.</param>
    internal static IReadOnlyList<string> Unguarded(IEnumerable<Type> parameters) =>
    [
        .. parameters
            .Where(static type => !typeof(IApplicationService).IsAssignableFrom(type))
            .Where(static type => !AllowedOptions.Contains(type))
            .Select(static type => type.FullName ?? type.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    [Fact]
    public void السطح_المنشور_لا_يحمل_إلا_خدمات_تطبيق_وإعدادات()
    {
        List<string> violations = [];
        int guarded = 0;

        foreach (Type surface in PublishedSurfaces)
        {
            ConstructorInfo constructor = Assert.Single(surface.GetConstructors());
            Type[] parameters = [.. constructor.GetParameters().Select(static p => p.ParameterType)];

            guarded += parameters.Count(static type => typeof(IApplicationService).IsAssignableFrom(type));

            violations.AddRange(Unguarded(parameters)
                .Select(name => surface.FullName + " ← " + name));
        }

        // اللافراغ: السطوح تحمل خدمات فعلاً. سطحٌ بلا خدمة واحدة يجعل «لا مخالفة»
        // جملةً عن لا شيء.
        Assert.True(
            guarded >= 6,
            FormattableString.Invariant($"السطوح تحمل {guarded} خدمة تطبيق — أقلّ من أن يعني الفحص شيئاً."));

        Assert.True(
            violations.Count == 0,
            "السطح المنشور يحمل متعاوناً ليس خدمة تطبيق. وخدمة التطبيق وحدها هي ما تفرض "
            + "عليه القاعدة 6 نداءَ المنفِّذ قبل أي عمل؛ فمتعاونٌ آخر — سياق قاعدة بيانات، "
            + "أو موارد وحدة، أو محرّك ترحيل — يفتح طريقاً إلى الوحدة **لا يمرّ بالاستحقاق**:\n"
            + string.Join('\n', violations));
    }

    [Fact]
    public void الحارس_الثاني_يرى_المتعاون_غير_المحروس_حين_يُزرع()
    {
        // اختبار الطفرة: مُنشئٌ يأخذ موارد الوحدة مباشرةً — وهو الطريق الذي يلتفّ
        // على المنفِّذ. ولو مرّ هذا لكان الحارس زينة.
        IReadOnlyList<string> unguarded = Unguarded([typeof(SalesRuntime), typeof(PurchasingRuntime)]);

        Assert.Equal(["Babel.Purchasing.PurchasingRuntime", "Babel.Sales.SalesRuntime"], unguarded);

        // ومن الطرف الآخر: ما هو خدمة تطبيق فعلاً يمرّ، فالحارس ليس رافضاً لكل شيء.
        Assert.Empty(Unguarded([typeof(CustomerService), typeof(SupplierService), typeof(SalesOptions)]));
    }
}
