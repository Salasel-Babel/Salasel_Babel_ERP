using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Babel.Contracts.Posting;
using Babel.Core.Audit;
using Babel.Core.CapabilityProfile;
using Babel.Core.Entitlement;
using Babel.Core.Metering;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Core.Tests;

/// <summary>
/// <b>ملفّ القدرات — القدرة واقعةٌ في الخلفية، والشاشة مُشتقّة منها.</b>
/// <para>
/// كل اختبار هنا يبذر حالته بنفسه: لا مخزن مشترك بين اختبارين، ولا ترتيب تنفيذ يُعتمد عليه.
/// </para>
/// </summary>
public sealed class CapabilityProfileTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa"));
    private static readonly UserId Actor = new(Guid.Parse("bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb"));

    private static string RepositoryRoot { get; } = FindRepositoryRoot();

    // ── الفهرس والكتالوج ────────────────────────────────────────────────────

    [Fact]
    public void الفهرس_يقرأ_أحداث_المصفوفة_ولا_يمرّ_فارغاً()
    {
        EmbeddedPostingEventDirectory directory = EmbeddedPostingEventDirectory.Default;

        int onDisk = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "data", "posting-matrix", "events"), "*.json")
            .Sum(CountEvents);

        Assert.True(onDisk > 20, $"قُرئ من القرص {onDisk} حدثاً — المقارنة ستمرّ فراغاً.");
        Assert.Equal(onDisk, directory.Count);
        Assert.True(directory.Contains(new PostingEventCode("sales.advance.received")));
        Assert.False(directory.Contains(new PostingEventCode("sales.advance.invented")));
        Assert.False(directory.Contains(PostingEventCode.None));
    }

    [Fact]
    public void كل_حدث_يذكره_الكتالوج_موجود_في_المصفوفة()
    {
        // شرط دخول أي قدرة إلى الكتالوج: أن تفتح حدثاً موجوداً. وهذا الاختبار هو ما يجعل
        // الشرط مفروضاً لا متَّفقاً عليه.
        ImmutableArray<PostingEventCode> referenced = CapabilityCatalogue.ReferencedEvents;

        Assert.True(referenced.Length >= 5, $"الكتالوج يذكر {referenced.Length} حدثاً فقط — الفحص ضامر.");
        Assert.True(CapabilityCatalogue.DocumentTypes.Length >= 2);
        Assert.True(CapabilityCatalogue.DocumentTypes.All(static type => type.Capabilities.Length > 0));

        ImmutableArray<PostingEventCode> unserved =
            CapabilityCatalogue.UnservedEvents(EmbeddedPostingEventDirectory.Default);

        Assert.True(
            unserved.Length == 0,
            "الكتالوج يذكر أحداثاً لا تقابلها المصفوفة:\n"
            + string.Join('\n', unserved.Select(static code => code.Value)));

        // ولا اسم فارغ ولا مفتاح ترجمة مكرَّر: العربية هي الارتداد المضمون، والمفتاح معرّف.
        Assert.All(
            CapabilityCatalogue.DocumentTypes,
            static type =>
            {
                Assert.False(string.IsNullOrWhiteSpace(type.NameAr));
                Assert.False(string.IsNullOrWhiteSpace(type.NameKey));
                Assert.All(type.Capabilities, static capability =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(capability.NameAr));
                    Assert.False(string.IsNullOrWhiteSpace(capability.NameKey));
                });
            });

        // ولا قدرة بلا حدث: قدرةٌ لا تفتح حدثاً تفضيلُ شاشة لا قدرة محاسبية.
        Assert.All(
            CapabilityCatalogue.DocumentTypes.SelectMany(static type => type.Capabilities),
            static capability => Assert.True(
                capability.RequiredEvents.Length > 0,
                $"القدرة {capability.Code.Value} لا تفتح حدثاً — مكانها الواجهة لا الكتالوج."));
    }

    // ── البوفيه في مقابل المقاول ────────────────────────────────────────────

    [Fact]
    public void ملفّان_من_البيانات_يُنتجان_شكلين_مختلفين_بالكود_نفسه_والمصفوفة_نفسها()
    {
        ValidatedCapabilityProfile buffet = Load("buffet.json");
        ValidatedCapabilityProfile contractor = Load("contractor.json");

        DocumentShape buffetInvoice = buffet.ShapeOf(new DocumentTypeCode("sales.invoice"))!;
        DocumentShape contractorInvoice = contractor.ShapeOf(new DocumentTypeCode("sales.invoice"))!;

        // البوفيه: العميل والأصناف وطريقة الدفع. ثلاثة أشياء، ولا قدرة واحدة.
        Assert.Equal(["customer", "lines", "paymentMethod"], buffetInvoice.Fields);
        Assert.Empty(buffetInvoice.EnabledCapabilities);
        Assert.Equal("cash", buffetInvoice.Defaults["paymentMethod"]);

        // المقاول: الحقول نفسها ومعها حقلا القدرتين المُشغَّلتين.
        Assert.Equal(["advanceApplied", "customer", "lines", "paymentMethod", "warehouse"], contractorInvoice.Fields);
        Assert.Equal(
            ["advance", "cost_of_sales"],
            contractorInvoice.EnabledCapabilities.Select(static code => code.Value));
        Assert.Equal("W1", contractorInvoice.Defaults["warehouse"]);

        // ونوع مستند لا وجود له عند البوفيه أصلاً.
        Assert.Null(buffet.ShapeOf(new DocumentTypeCode("projects.client_certificate")));
        DocumentShape certificate = contractor.ShapeOf(new DocumentTypeCode("projects.client_certificate"))!;
        Assert.Equal(["advanceRecovery", "contract", "retention", "workValue"], certificate.Fields);

        // والكتالوج واحد: ما اختلف هو صفوف البيانات لا الشيفرة.
        Assert.Equal(buffetInvoice.AvailableCapabilities, contractorInvoice.AvailableCapabilities);

        // والاسم عربيٌّ إلزامي ومفتاح ترجمة — لا ثنائية لغتين.
        Assert.Equal("فاتورة مبيعات", buffetInvoice.NameAr);
        Assert.Equal("document_type.sales.invoice", buffetInvoice.NameKey);
    }

    [Fact]
    public void البوفيه_يرفض_مستنداً_يحمل_دفعة_مقدمة_لم_يُشغّلها_والمقاول_يقبله()
    {
        DocumentSubmission withDeposit = new(
            new DocumentTypeCode("sales.invoice"),
            ["customer", "lines", "paymentMethod", "advanceApplied"]);

        Result<AdmittedDocument> refused = Load("buffet.json").Admit(withDeposit);

        Assert.True(refused.IsFailure);
        Error error = Assert.Single(refused.Errors);
        Assert.Equal("document_admission.capability_not_enabled", error.Code);
        Assert.Contains("advanceApplied", error.MessageAr, StringComparison.Ordinal);
        Assert.Contains("دفعة مقدمة من العميل", error.MessageAr, StringComparison.Ordinal);

        Result<AdmittedDocument> admitted = Load("contractor.json").Admit(withDeposit);

        Assert.True(admitted.IsSuccess);
        Assert.Equal(["advanceApplied", "customer", "lines", "paymentMethod"], admitted.Value.Fields);
    }

    [Fact]
    public void حقل_لا_يعرفه_الكتالوج_يُرفض_ولا_يُتجاهل_بصمت()
    {
        Result<AdmittedDocument> refused = Load("buffet.json").Admit(new DocumentSubmission(
            new DocumentTypeCode("sales.invoice"),
            ["customer", "marketerCommission"]));

        Assert.True(refused.IsFailure);
        Assert.Equal("document_admission.field_unknown", Assert.Single(refused.Errors).Code);
    }

    // ── الرفض عند الحفظ ─────────────────────────────────────────────────────

    [Fact]
    public void قدرة_لا_تخدمها_المصفوفة_تُرفض_باسمها_وبالحدث_الناقص()
    {
        // مصفوفة أُسقط منها حدثا الدفعة المقدمة — تمثيلٌ حرفي لحالة «القدرة تسمّي ما لا
        // تستطيع المصفوفة خدمته»، وهي الحالة التي يوجد هذا التحقّق من أجلها.
        IPostingEventDirectory without = DirectoryWithout("sales.advance.received", "sales.advance.applied");

        Result<ValidatedCapabilityProfile> result = ValidatedCapabilityProfile.Create(
            Draft(("sales.invoice", new Dictionary<string, bool> { ["advance"] = true }, [])),
            without);

        Assert.True(result.IsFailure);
        Error error = Assert.Single(result.Errors);
        Assert.Equal("capability_profile.capability_not_served_by_matrix", error.Code);
        Assert.Contains("advance", error.MessageAr, StringComparison.Ordinal);
        Assert.Contains("sales.advance.received", error.MessageAr, StringComparison.Ordinal);
        Assert.Contains("sales.advance.applied", error.MessageAr, StringComparison.Ordinal);

        // والقدرة المُطفأة لا تُرفض بغياب حدثها: الملفّ لا يعِد بما لا يفعله.
        Assert.True(ValidatedCapabilityProfile
            .Create(Draft(("sales.invoice", new Dictionary<string, bool> { ["advance"] = false }, [])), without)
            .IsSuccess);
    }

    [Fact]
    public void نوع_مستند_غير_معروف_وقدرة_غير_معروفة_وملفّ_فارغ_كلها_تُرفض()
    {
        Assert.Equal(
            "capability_profile.document_type_unknown",
            Assert.Single(ValidatedCapabilityProfile.Create(
                Draft(("sales.quotation", new Dictionary<string, bool>(), [])),
                EmbeddedPostingEventDirectory.Default).Errors).Code);

        Assert.Equal(
            "capability_profile.capability_unknown",
            Assert.Single(ValidatedCapabilityProfile.Create(
                Draft(("sales.invoice", new Dictionary<string, bool> { ["installments"] = true }, [])),
                EmbeddedPostingEventDirectory.Default).Errors).Code);

        Assert.Equal(
            "capability_profile.empty",
            Assert.Single(ValidatedCapabilityProfile.Create(
                new CapabilityProfileDraft(new Dictionary<string, DocumentProfileDraft>()),
                EmbeddedPostingEventDirectory.Default).Errors).Code);
    }

    [Fact]
    public void قيمة_افتراضية_لحقل_خارج_الشكل_تُرفض_ولا_تبقى_في_البيانات()
    {
        Result<ValidatedCapabilityProfile> result = ValidatedCapabilityProfile.Create(
            Draft(("sales.invoice",
                new Dictionary<string, bool> { ["cost_of_sales"] = false },
                new Dictionary<string, string> { ["warehouse"] = "W1" })),
            EmbeddedPostingEventDirectory.Default);

        Assert.True(result.IsFailure);
        Assert.Equal("capability_profile.default_field_not_in_shape", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void كل_أسباب_الرفض_تُرجَع_مجتمعة_لا_أوّلها()
    {
        Result<ValidatedCapabilityProfile> result = ValidatedCapabilityProfile.Create(
            new CapabilityProfileDraft(new Dictionary<string, DocumentProfileDraft>
            {
                ["sales.quotation"] = DocumentProfileDraft.Bare,
                ["sales.invoice"] = new(
                    new Dictionary<string, bool> { ["installments"] = true, ["delivery"] = true },
                    new Dictionary<string, string>()),
            }),
            EmbeddedPostingEventDirectory.Default);

        Assert.True(result.IsFailure);
        Assert.Equal(3, result.Errors.Count);
    }

    // ── تغيير الملفّ ومستنداتٌ مفتوحة ───────────────────────────────────────

    [Fact]
    public async Task تشغيل_قدرة_يمرّ_وسحبها_يُرفض_بلا_إقرار_ثم_يُقبل_به_ويُسجَّل()
    {
        (CapabilityProfileService service, InMemoryAuditLog audit) = NewService();

        // ١ — ملفّ البوفيه أولاً.
        Assert.True((await service.SaveAsync(Request(BuffetDraft(), null), TestContext.Current.CancellationToken)).IsSuccess);

        // ٢ — التوسيع: تشغيل قدرة لا يحتاج شيئاً — لا يُبطل مستنداً قائماً.
        Assert.True((await service.SaveAsync(Request(WithAdvanceDraft(), null), TestContext.Current.CancellationToken)).IsSuccess);

        // ٣ — التضييق: سحب القدرة نفسها يُرفض بلا إقرار مسبَّب.
        Result<ValidatedCapabilityProfile> refused =
            await service.SaveAsync(Request(BuffetDraft(), null), TestContext.Current.CancellationToken);

        Assert.True(refused.IsFailure);
        Error error = Assert.Single(refused.Errors);
        Assert.Equal("capability_profile.capability_withdrawal_requires_acknowledgement", error.Code);
        Assert.Contains("advance", error.MessageAr, StringComparison.Ordinal);

        // والمخزن لم يتغيّر: الرفض رفضٌ لا نصف حفظ.
        Assert.True((await service.GetAsync(Tenant, Actor, TestContext.Current.CancellationToken))
            .Value.IsEnabled(new DocumentTypeCode("sales.invoice"), new CapabilityCode("advance")));

        // ٤ — ومع الإقرار المسبَّب يمرّ، ويُكتب السبب في سجل التدقيق.
        Assert.True((await service.SaveAsync(
            Request(BuffetDraft(), "أُقفلت كل الدفعات المقدمة المفتوحة ورصيد حساب الدفعات صفر"),
            TestContext.Current.CancellationToken)).IsSuccess);

        IReadOnlyList<AuditEntry> entries = await audit.ReadAsync(Tenant, TestContext.Current.CancellationToken);
        AuditEntry last = entries[^1];
        Assert.Equal("capability_profile.saved", last.Action);
        Assert.Contains("سحب قدرات", last.Details!, StringComparison.Ordinal);
        Assert.Contains("رصيد حساب الدفعات صفر", last.Details!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task مستأجر_بلا_ملفّ_لا_يُفترض_له_ملفّ_ضمني()
    {
        (CapabilityProfileService service, _) = NewService();

        Result<ValidatedCapabilityProfile> result = await service.GetAsync(Tenant, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("capability_profile.not_found", Assert.Single(result.Errors).Code);
    }

    // ── الحارس البنيوي ──────────────────────────────────────────────────────

    [Fact]
    public void لا_طريق_في_النواة_إلى_ملفّ_محفوظ_يتجاوز_التحقّق()
    {
        Assembly core = typeof(ValidatedCapabilityProfile).Assembly;

        // ١ — لا مُنشئ عام: القيمة لا تُبنى من خارج المصنع.
        Assert.Empty(typeof(ValidatedCapabilityProfile).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(AdmittedDocument).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

        // ٢ — ولا مصنع ثانٍ: كل عضو في النواة يُرجع ملفّاً صالحاً إمّا هو المصنع، وإمّا
        //     يمرّ به حتماً (قراءة من مخزن لا يقبل غير الصالح، أو خدمة تنادي المصنع).
        string[] producers =
        [
            .. core.GetTypes()
                .Where(static type => !type.Name.StartsWith('<'))
                .SelectMany(static type => type
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(static method => !method.IsSpecialName)
                    .Where(static method => Produces(method.ReturnType))
                    .Select(method => type.Name + "." + method.Name))
                .Order(StringComparer.Ordinal),
        ];

        Assert.True(producers.Length >= 4, $"المسح وجد {producers.Length} منتِجاً — الحارس ضامر.");
        Assert.Equal(
            [
                "CapabilityProfileService.GetAsync",
                "CapabilityProfileService.SaveAsync",
                "ICapabilityProfileStore.FindAsync",
                "InMemoryCapabilityProfileStore.FindAsync",

                // المخزن فوق PostgreSQL منتِجٌ رابع — و**يمرّ بالمصنع حتماً**: يقرأ
                // المسودّة المخزَّنة ثم ينادي Create فيُطابقها بمصفوفة الترحيل من جديد،
                // ويرمي إن رُفضت. أي أنه لا يُنتج ملفّاً لم يُطابَق، وذلك شرط دخوله هذه
                // القائمة لا استثناءً منها.
                "PostgresCapabilityProfileStore.FindAsync",
                "ValidatedCapabilityProfile.Create",
            ],
            producers);

        // ٣ — والمخزن لا يقبل مسودّة: التوقيع نفسه يمنع حفظ ما لم يُطابَق.
        foreach (Type type in new[]
                 {
                     typeof(ICapabilityProfileStore),
                     typeof(InMemoryCapabilityProfileStore),
                     core.GetType("Babel.Core.Persistence.PostgresCapabilityProfileStore", throwOnError: true)!,
                 })
        {
            MethodInfo save = type.GetMethod("SaveAsync")!;
            Assert.Contains(save.GetParameters(), parameter => parameter.ParameterType == typeof(ValidatedCapabilityProfile));
            Assert.DoesNotContain(save.GetParameters(), static parameter => parameter.ParameterType == typeof(CapabilityProfileDraft));
        }
    }

    // ── الأدوات ─────────────────────────────────────────────────────────────

    private static bool Produces(Type returnType)
    {
        if (returnType == typeof(ValidatedCapabilityProfile))
        {
            return true;
        }

        if (!returnType.IsGenericType)
        {
            return false;
        }

        Type[] arguments = returnType.GetGenericArguments();
        return arguments.Length == 1
            && (arguments[0] == typeof(ValidatedCapabilityProfile)
                || (arguments[0].IsGenericType && arguments[0].GetGenericArguments() is [Type inner] && inner == typeof(ValidatedCapabilityProfile)));
    }

    private static CapabilityProfileSaveRequest Request(CapabilityProfileDraft draft, string? reason)
        => new(Tenant, Actor, draft, reason);

    private static CapabilityProfileDraft BuffetDraft()
        => Draft(("sales.invoice", new Dictionary<string, bool> { ["advance"] = false }, []));

    private static CapabilityProfileDraft WithAdvanceDraft()
        => Draft(("sales.invoice", new Dictionary<string, bool> { ["advance"] = true }, []));

    private static CapabilityProfileDraft Draft(
        params (string DocumentType, Dictionary<string, bool> Capabilities, Dictionary<string, string> Defaults)[] documents)
        => new(documents.ToDictionary(
            static document => document.DocumentType,
            static document => new DocumentProfileDraft(document.Capabilities, document.Defaults),
            StringComparer.Ordinal));

    private static (CapabilityProfileService Service, InMemoryAuditLog Audit) NewService()
    {
        InMemoryUsageStore usage = new();
        InMemoryAuditLog audit = new();
        TimeProvider clock = new FixedTimeProvider(new DateTimeOffset(2026, 8, 25, 9, 0, 0, TimeSpan.Zero));
        InMemoryEntitlementService entitlements = new(audit, clock);
        EntitlementEnforcer enforcer = new(entitlements, usage, clock);

        return (
            new CapabilityProfileService(
                new InMemoryCapabilityProfileStore(),
                EmbeddedPostingEventDirectory.Default,
                enforcer,
                audit,
                clock),
            audit);
    }

    private static ValidatedCapabilityProfile Load(string fileName)
    {
        string path = Path.Combine(RepositoryRoot, "data", "capability-profiles", fileName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

        Dictionary<string, DocumentProfileDraft> documents = new(StringComparer.Ordinal);

        foreach (JsonProperty documentType in document.RootElement.GetProperty("documents").EnumerateObject())
        {
            Dictionary<string, bool> capabilities = new(StringComparer.Ordinal);
            foreach (JsonProperty capability in documentType.Value.GetProperty("capabilities").EnumerateObject())
            {
                capabilities[capability.Name] = capability.Value.GetBoolean();
            }

            Dictionary<string, string> defaults = new(StringComparer.Ordinal);
            foreach (JsonProperty entry in documentType.Value.GetProperty("defaults").EnumerateObject())
            {
                defaults[entry.Name] = entry.Value.GetString()!;
            }

            documents[documentType.Name] = new DocumentProfileDraft(capabilities, defaults);
        }

        Result<ValidatedCapabilityProfile> result = ValidatedCapabilityProfile.Create(
            new CapabilityProfileDraft(documents),
            EmbeddedPostingEventDirectory.Default);

        Assert.True(
            result.IsSuccess,
            $"ملفّ مرجعي مرفوض — {fileName}:\n" + string.Join('\n', result.Errors.Select(static e => e.ToString())));

        return result.Value;
    }

    private static PartialDirectory DirectoryWithout(params string[] removed)
        => new PartialDirectory(EmbeddedPostingEventDirectory.Default, removed);

    private static int CountEvents(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("events").GetArrayLength();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Babel.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("جذر المستودع غير موجود.");
    }

    /// <summary>مصفوفة أُسقطت منها أحداث — لإثبات أن الفحص يرى الغياب فعلاً.</summary>
    private sealed class PartialDirectory(IPostingEventDirectory inner, IReadOnlyCollection<string> removed)
        : IPostingEventDirectory
    {
        public int Count => Codes.Count;

        public IReadOnlyList<PostingEventCode> Codes =>
            [.. inner.Codes.Where(code => !removed.Contains(code.Value, StringComparer.Ordinal))];

        public bool Contains(PostingEventCode code)
            => inner.Contains(code) && !removed.Contains(code.Value, StringComparer.Ordinal);
    }
}
