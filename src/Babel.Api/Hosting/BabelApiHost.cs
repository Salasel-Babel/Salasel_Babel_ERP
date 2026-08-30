using System.Globalization;
using Babel.Api.Endpoints;
using Babel.Api.Fleet;
using Babel.Api.Errors;
using Babel.Api.Ports;
using Babel.Api.Security;
using Babel.Compliance;
using Babel.Core;
using Babel.Core.Access;
using Babel.Core.Entitlement;
using Babel.Core.Tenancy;
using Babel.ControlPlane.Support;
using Babel.Inventory;
using Babel.Ledger;
using Babel.Purchasing;
using Babel.Sales;
using Babel.SharedKernel;
using Babel.Storage;

namespace Babel.Api.Hosting;

/// <summary>
/// الجذر التركيبي لـ«سلاسل بابل» — التركيب وحده.
/// <para>
/// المشروع الوحيد الذي يعرف كل الوحدات، ولا يعرف رغم ذلك أنواعها الداخلية: كل تسجيل يمرّ
/// بدالة <c>Add&lt;Module&gt;</c> المعلنة. والقاعدة 13 تفرض ذلك على IL لا على المراجعة.
/// </para>
/// <para>
/// وملاحظتان تشغيليتان مثبَّتتان من موجة الهيكل (وثيقة المعمارية §2.2 ·
/// <c>spikes/relational-stack/VERDICT.md §5</c>): ‏Wolverine يُهيَّأ بالتوليد الساكن، و
/// <c>WolverineFx.RuntimeCompilation</c> ممنوعة في الإنتاج ومنعُها مفروض بالقاعدة 8.
/// </para>
/// </summary>
internal static class BabelApiHost
{
    /// <summary>أقصى حجم لجسم الطلب. حدٌّ معلن، لا مفاجأة عند أول حمولة كبيرة.</summary>
    public const long MaxRequestBodyBytes = 1024 * 1024;

    /// <summary>يبني التطبيق كاملاً: خدمات، وخط معالجة، ومسارات.</summary>
    /// <param name="args">وسائط سطر الأوامر.</param>
    public static WebApplication Build(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.WebHost.ConfigureKestrel(static options =>
        {
            options.Limits.MaxRequestBodySize = MaxRequestBodyBytes;
            options.AddServerHeader = false;
        });

        // النواة أولاً: الاستحقاق وقياس الاستخدام والتدقيق. إلزامية دائماً.
        //
        // ‏**بالتحميل الزائد الذي يأخذ إعدادات — أي بمخزنَين فوق PostgreSQL.** والنسخة
        // بلا وسيط تسجّل مخزنين في الذاكرة عمرهما عمر العملية، وهي حالة كانت تعني أن
        // خادماً أُعيد إقلاعه يردّ **كل** ترحيل بـ`company_setup.not_found` بينما تعمل
        // شاشات القراءة كلّها — فتُقرأ الميزانية ولا تُكتب فاتورة (ADR-0026 · ADR-0029).
        builder.Services.AddBabelCore(options => ApplyCoreConfiguration(builder.Configuration, options));

        // الدفتر: يسجّل IPostingService و LedgerAuditService. أنواعه الداخلية
        // (‏LedgerDbContext, AccountCode) لا تُرى هنا ولا يمكن أن تُرى.
        builder.Services.AddBabelLedger(options => ApplyLedgerConfiguration(builder.Configuration, options));

        // الوحدات الأفقية: خدمات تطبيق فقط. لا وصول إلى جداول بعضها ولا إلى جداول الدفتر.
        //
        // ‏**واتصالاهما يُقرآن من الإعداد** — ولم يكونا يُقرآن. كان الجذر التركيبي
        // يسجّلهما بإعداداتهما الافتراضية، فكان كل خادم يشير إلى `babel_sales` و
        // `babel_purchasing` على المضيف المحلي مهما كان النشر. ولم يظهر ذلك قطّ لأن
        // **لا باب HTTP واحداً كان يبلغ الوحدتين**: مسارٌ لا يُسلَك لا يُظهر إعداداً خاطئاً.
        builder.Services.AddBabelSales(options => ApplySalesConfiguration(builder.Configuration, options));
        builder.Services.AddBabelPurchasing(options => ApplyPurchasingConfiguration(builder.Configuration, options));
        builder.Services.AddBabelCompliance();

        // ── والمخزون كذلك يُقرأ اتصاله من الإعداد ─────────────────────────────
        // ‏**وهو نفس العطل الذي أُصلح للمبيعات والمشتريات، باقياً في وحدة ثالثة**:
        // كان الجذر يستدعي `AddBabelInventory()` بلا ضابط، فيشير كل خادم إلى
        // ‏`babel_inventory` على المضيف المحلي مهما كان النشر. وغير مرئي للسبب نفسه —
        // ‏**مسارٌ لا يُسلَك لا يُظهر إعداداً خاطئاً**: لم يكن باب HTTP واحد يبلغ منفذ
        // التقييم قبل نشر استلام البضاعة على هذا السطح.
        // (‏docs/evidence/traps.md#fakh-one-module-connection-still-read-from-a-default-after-its-siblings-were-fixed)
        builder.Services.AddBabelInventory(options => ApplyInventoryConfiguration(builder.Configuration, options));

        // ── مخزن المرفقات: مشروع مساند لا وحدة، والجذر التركيبي وحده يركّبه ──────
        //
        // ‏**وثلاثة أسطر لا سطر واحد، ولكلٍّ ثمنه المُعلَن:** المخزن، ثم مُصدِر التذاكر
        // (ويُلزم من يركّبه بضبط مفتاح توقيع)، ثم السطح المنشور الذي يناديه هذا السطح.
        // والفصل مقصود في ADR-0046: نشرٌ لا يقدّم تنزيلاً موقّعاً لا يحتاج المفتاح أصلاً.
        builder.Services.AddBabelStorage(options => ApplyStorageConfiguration(builder.Configuration, options));
        builder.Services.AddBabelStorageTickets();
        builder.Services.AddBabelAttachmentSurface();

        // سياق الطلب: يُملأ من الاعتماد وحده.
        builder.Services.AddScoped<RequestTenantContext>();
        builder.Services.AddScoped<ITenantContext>(static sp => sp.GetRequiredService<RequestTenantContext>());

        // ── دليل الاعتمادات: المُصدَر أولاً، ثم اعتماد التزويد المُهيَّأ من الإعداد ──
        //
        // ‏**وتنفيذٌ واحد مسجَّل لا اثنان**: الوسيط ينادي IApiPrincipalResolver كما كان،
        // ولا يعرف أن خلفه دليلين. وآليتا تصريح متوازيتان في خط المعالجة تعنيان أن
        // إحداهما تُصان وتُنسى الأخرى، ولا يظهر الفارق إلا يوم يتجاوزه أحد.
        //
        // ودليل الإعداد **باب إقلاع معلَن**: هو الاعتماد الوحيد الذي لا يُصدره السطح ولا
        // يدور ولا يُبطَل من HTTP، ووظيفته أن يُنشئ أوّل مالك في منشأةٍ زُوِّدت للتوّ.
        builder.Services.AddSingleton<ConfiguredPrincipalResolver>(
            _ => new ConfiguredPrincipalResolver(ReadPrincipals(builder.Configuration)));
        builder.Services.AddSingleton<IApiPrincipalResolver>(provider => new IssuedSessionResolver(
            provider.GetRequiredService<AccessResolver>(),
            provider.GetRequiredService<ConfiguredPrincipalResolver>()));

        // منفذ قراءة القيد: العقد منشور، والتنفيذ ينتظر سطح قراءة في الدفتر (ADR-0018).
        builder.Services.AddSingleton<IJournalEntryReader, UnavailableJournalEntryReader>();

        // ── منفذ الأسطول: الجذر التركيبي وحده يعرف الطرفين ──────────────────────
        //
        // ‏**والتهيئة صريحة لا مُستنتَجة**: `Babel:Fleet:Enabled` مفتاحٌ يُضبط في البيئة.
        // واستنتاجُها من وجود متغيّر اتصال كان سيجعل خادماً على آلة فيها قاعدة تحكّم
        // لغرضٍ آخر يفتح سطح الاشتراك عليها بلا أن يقرّر ذلك أحد.
        //
        // وكل سرّ من البيئة: `ControlPlaneOptions` كل قيمة فيها من متغيّر بيئة، ولا
        // كلمة مرور ولا مضيف ولا اسم قاعدة مكتوبٌ في المستودع (README لمستوى التحكّم).
        //
        // و`UseSurfaceRole` يجعل الاتصال بدورٍ **ثالث** لا بمستخدم الإدارة: خادمٌ يخدم
        // الإنترنت باتصال إدارة يستطيع أن يُسقط سجلّ الأسطول الذي يقرؤه (ADR-0003).
        builder.Services.AddSingleton<IFleetDirectory>(_ =>
            builder.Configuration.GetValue<bool>("Babel:Fleet:Enabled")
                ? new ControlPlaneFleetDirectory(new ControlPlaneOptions { UseSurfaceRole = true })
                : new UnavailableFleetDirectory());

        // ── حدّ المعدّل على الأبواب المفتوحة ─────────────────────────────────────
        // الحدّ عددٌ يُضبط في النشر لا في الشيفرة: العدد الصحيح يعتمد على شكل النشر —
        // خلف بوّابة تتقاسم عنواناً، أو مباشرةً على الإنترنت — لا على ما يفعله الباب.
        builder.Services.AddSingleton(provider => new OpenDoorRateGuard(
            provider.GetRequiredService<TimeProvider>(),
            builder.Configuration.GetValue("Babel:RateLimit:PerMinute", OpenDoorRateGuard.DefaultPerMinute)));

        WebApplication app = builder.Build();

        app.UseUnhandledFailureGuard();

        // حدّ المعدّل **قبل** المصادقة: الأبواب المحروسة به مفتوحة أصلاً، فلا اعتماد
        // يُقرأ قبله؛ ووضعُه بعدها كان سيجعل الطرق الآلي يدفع الخادم إلى قاعدة البيانات
        // في كل محاولة قبل أن يُردّ — أي أن الحارس يحرس بعد أن يقع ما يحرس منه.
        app.UseOpenDoorRateLimit();

        app.UseBabelAuthentication();
        app.MapSessionApi();
        app.MapAccessApi();
        app.MapTenantApi();
        app.MapLedgerApi();
        app.MapCapabilityProfileApi();
        app.MapCompanySetupApi();
        app.MapDocumentApi();
        app.MapAttachmentApi();
        app.MapDocsApi();

        return app;
    }

    /// <summary>
    /// يبذر الاستحقاق من الإعداد. موجة الهيكل تحمل تنفيذاً في الذاكرة، فمصدر الحقيقة
    /// اليوم هو الإعداد لا جدول اشتراكات — وهذا مُعلن لا مُخفى.
    /// </summary>
    /// <param name="app">التطبيق المبنيّ.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task SeedEntitlementsAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        IConfigurationSection root = app.Configuration.GetSection("Babel:Entitlements");
        IEntitlementService entitlements = app.Services.GetRequiredService<IEntitlementService>();

        foreach (IConfigurationSection tenantSection in root.GetChildren())
        {
            if (!Guid.TryParseExact(tenantSection.Key, "D", out Guid tenantId))
            {
                throw new InvalidOperationException(
                    $"مفتاح مستأجر غير صالح في Babel:Entitlements — «{tenantSection.Key}». / "
                    + $"Invalid tenant key in Babel:Entitlements — '{tenantSection.Key}'.");
            }

            Dictionary<BabelModule, EntitlementState> changes = [];

            foreach (IConfigurationSection moduleSection in tenantSection.GetChildren())
            {
                if (!Enum.TryParse(moduleSection.Key, ignoreCase: false, out BabelModule module)
                    || !Enum.TryParse(moduleSection.Value, ignoreCase: false, out EntitlementState state))
                {
                    throw new InvalidOperationException(
                        $"استحقاق غير صالح: «{moduleSection.Key}» = «{moduleSection.Value}». / "
                        + $"Invalid entitlement: '{moduleSection.Key}' = '{moduleSection.Value}'.");
                }

                changes[module] = state;
            }

            if (changes.Count == 0)
            {
                continue;
            }

            Result<EntitlementSet> applied = await entitlements
                .ApplyAsync(
                    new EntitlementChangeRequest(
                        new TenantId(tenantId),
                        changes,
                        UserId.SystemActor,
                        "بذر الاستحقاق من إعداد الإقلاع / entitlement seeded from startup configuration"),
                    cancellationToken)
                .ConfigureAwait(false);

            if (applied.IsFailure)
            {
                // الإقلاع يفشل بصوت عالٍ: خادم يقلع باستحقاق مرفوض هو خادم يبيع ما لم يُشترَ.
                throw new InvalidOperationException(
                    "رُفض استحقاق الإقلاع: " + string.Join(" · ", applied.Errors.Select(static e => e.ToString())));
            }
        }
    }

    /// <summary>
    /// يقرأ إعداد النواة. <b>ولا يقرأ اتصال المالك ولا يوجد له مفتاح</b>: خادمٌ يحمل
    /// اتصال المالك يستطيع أن يُسقط مشغّل ثبات المقياس ثم يكتب ما شاء (ADR-0003).
    /// </summary>
    private static void ApplyCoreConfiguration(ConfigurationManager configuration, CoreOptions options)
    {
        string? app = configuration["Babel:Core:AppConnectionString"];
        if (!string.IsNullOrWhiteSpace(app))
        {
            options.AppConnectionString = app;
        }

        string? role = configuration["Babel:Core:AppRole"];
        if (!string.IsNullOrWhiteSpace(role))
        {
            options.AppRole = role;
        }
    }

    /// <summary>
    /// يقرأ إعداد المبيعات. <b>ولا اتصال مالك هنا</b>: نشر المخطّط عملية مالك، ومسار
    /// التطبيق لا يحتاجها ولا يجوز أن يملكها — كما في النواة والدفتر بالضبط.
    /// </summary>
    private static void ApplySalesConfiguration(ConfigurationManager configuration, SalesOptions options)
    {
        string? connection = configuration["Babel:Sales:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connection))
        {
            options.ConnectionString = connection;
        }

        string? currency = configuration["Babel:Sales:CompanyCurrency"];
        if (!string.IsNullOrWhiteSpace(currency))
        {
            options.CompanyCurrency = currency;
        }
    }

    /// <summary>
    /// يقرأ إعداد المخزن. <b>ولا اتصال مالك هنا</b> — كما في النواة والدفتر والوحدتين:
    /// خادمٌ يحمله يستطيع إسقاط مشغّل «يُضاف ولا يُعدَّل» ثم الكتابة فوق سند إثبات.
    /// <para>
    /// <b>ولا مفتاح توقيع في مستودع ولا في ملفّ إعداد مُودَع.</b> يُقرأ من الإعداد —
    /// أي من البيئة عملياً — وغيابُه <b>عطلٌ يُعلَن عند التركيب</b> لا مفتاحٌ يُخترع:
    /// مُصدِرُ تذاكر يولّد لنفسه مفتاحاً عند الإقلاع يُنتج نظاماً تُقبل فيه كل تذكرة
    /// قبل إعادة التشغيل وتُرفض كلها بعدها، والفشل يُقرأ «انتهت الصلاحية» لا «لا مفتاح»
    /// (‏ADR-0046 دليل 14).
    /// </para>
    /// </summary>
    /// <param name="configuration">الإعداد.</param>
    /// <param name="options">إعدادات المخزن.</param>
    private static void ApplyStorageConfiguration(ConfigurationManager configuration, StorageOptions options)
    {
        string? app = configuration["Babel:Storage:AppConnectionString"];
        if (!string.IsNullOrWhiteSpace(app))
        {
            options.AppConnectionString = app;
        }

        string? root = configuration["Babel:Storage:RootPath"];
        if (!string.IsNullOrWhiteSpace(root))
        {
            options.RootPath = root;
        }

        string? maximum = configuration["Babel:Storage:MaximumBytes"];
        if (!string.IsNullOrWhiteSpace(maximum))
        {
            options.MaximumBytes = long.Parse(maximum, CultureInfo.InvariantCulture);
        }

        string? lifetime = configuration["Babel:Storage:TicketLifetimeSeconds"];
        if (!string.IsNullOrWhiteSpace(lifetime))
        {
            options.TicketLifetimeCap = TimeSpan.FromSeconds(int.Parse(lifetime, CultureInfo.InvariantCulture));
        }

        string? key = configuration["Babel:Storage:TicketSigningKey"];
        if (!string.IsNullOrWhiteSpace(key))
        {
            options.TicketSigningKey = Convert.FromHexString(key);
        }
    }

    /// <summary>يقرأ إعداد المشتريات، بالقيود نفسها.</summary>
    private static void ApplyPurchasingConfiguration(ConfigurationManager configuration, PurchasingOptions options)
    {
        string? connection = configuration["Babel:Purchasing:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connection))
        {
            options.ConnectionString = connection;
        }

        string? currency = configuration["Babel:Purchasing:CompanyCurrency"];
        if (!string.IsNullOrWhiteSpace(currency))
        {
            options.CompanyCurrency = currency;
        }
    }

    /// <summary>
    /// يقرأ إعداد المخزون. <b>ولا اتصال مالك هنا</b> — نشر المخطّط عملية مالك، ومسار
    /// التطبيق لا يحتاجها ولا يجوز أن يملكها، كما في النواة والدفتر والمبيعات بالضبط.
    /// </summary>
    /// <param name="configuration">الإعداد.</param>
    /// <param name="options">إعدادات الوحدة.</param>
    private static void ApplyInventoryConfiguration(ConfigurationManager configuration, InventoryOptions options)
    {
        string? connection = configuration["Babel:Inventory:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connection))
        {
            options.ConnectionString = connection;
        }

        string? currency = configuration["Babel:Inventory:CompanyCurrency"];
        if (!string.IsNullOrWhiteSpace(currency))
        {
            options.CompanyCurrency = currency;
        }
    }

    private static void ApplyLedgerConfiguration(ConfigurationManager configuration, LedgerOptions options)
    {
        string? app = configuration["Babel:Ledger:AppConnectionString"];
        if (!string.IsNullOrWhiteSpace(app))
        {
            options.AppConnectionString = app;
        }

        string? owner = configuration["Babel:Ledger:OwnerConnectionString"];
        if (!string.IsNullOrWhiteSpace(owner))
        {
            options.OwnerConnectionString = owner;
        }

        string? currency = configuration["Babel:Ledger:CompanyCurrency"];
        if (!string.IsNullOrWhiteSpace(currency))
        {
            options.CompanyCurrency = currency;
        }
    }

    /// <summary>
    /// يقرأ دليل الاعتمادات من الإعداد. القيمة المخزَّنة <b>بصمة</b> لا اعتماد.
    /// </summary>
    private static Dictionary<string, ApiPrincipal> ReadPrincipals(ConfigurationManager configuration)
    {
        Dictionary<string, ApiPrincipal> byDigest = new(StringComparer.Ordinal);

        foreach (IConfigurationSection entry in configuration.GetSection("Babel:Api:Tokens").GetChildren())
        {
            string digest = (entry["Sha256"] ?? string.Empty).Trim().ToLowerInvariant();

            if (digest.Length != 64 || !digest.All(char.IsAsciiHexDigitLower))
            {
                throw new InvalidOperationException(
                    $"بصمة اعتماد غير صالحة عند Babel:Api:Tokens:{entry.Key} — يُتوقّع SHA-256 بستّين وأربعة محرفاً سداسياً صغيراً. / "
                    + $"Invalid credential digest at Babel:Api:Tokens:{entry.Key} — a 64-character lower-case SHA-256 hex is expected.");
            }

            if (!Guid.TryParseExact(entry["Tenant"], "D", out Guid tenant)
                || !Guid.TryParseExact(entry["User"], "D", out Guid user))
            {
                throw new InvalidOperationException(
                    $"مستأجر أو مستخدم غير صالح عند Babel:Api:Tokens:{entry.Key}. / "
                    + $"Invalid tenant or user at Babel:Api:Tokens:{entry.Key}.");
            }

            HashSet<Guid> companies = [];
            foreach (IConfigurationSection company in entry.GetSection("Companies").GetChildren())
            {
                if (!Guid.TryParseExact(company.Value, "D", out Guid companyId))
                {
                    throw new InvalidOperationException(
                        $"معرّف شركة غير صالح عند Babel:Api:Tokens:{entry.Key}:Companies:{company.Key}. / "
                        + $"Invalid company id at Babel:Api:Tokens:{entry.Key}:Companies:{company.Key}.");
                }

                companies.Add(companyId);
            }

            // ── الانقضاء: اختياري، وحين يُذكر يُقرأ بصيغة واحدة لا ثقافة لها ────────
            // ‏"o" (‏ISO 8601 الدوّار) وبثقافة ثابتة: قيمةٌ تُقرأ بثقافة الخادم كانت
            // ستعني لحظتين مختلفتين على خادمين بثقافتين مختلفتين — وهو فخّ-38 نفسه
            // منقولاً من رمز الفترة إلى صلاحية الاعتماد.
            DateTimeOffset? notAfter = null;
            string? notAfterText = entry["NotAfter"];

            if (!string.IsNullOrWhiteSpace(notAfterText))
            {
                if (!DateTimeOffset.TryParseExact(
                        notAfterText,
                        "o",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out DateTimeOffset parsed))
                {
                    throw new InvalidOperationException(
                        $"لحظة انقضاء غير صالحة عند Babel:Api:Tokens:{entry.Key}:NotAfter — «{notAfterText}». "
                        + "الصيغة المقبولة ISO 8601 الدوّارة، مثل 2026-08-27T00:00:00.0000000+00:00. / "
                        + $"Invalid expiry at Babel:Api:Tokens:{entry.Key}:NotAfter — '{notAfterText}'. "
                        + "The accepted format is round-trip ISO 8601.");
                }

                notAfter = parsed;
            }

            byDigest[digest] = new ApiPrincipal(new TenantId(tenant), new UserId(user), companies, notAfter);
        }

        return byDigest;
    }
}

/// <summary>حارس العطل غير المتوقّع — آخر شيء يقف بين استثناء وبين العميل.</summary>
internal static class UnhandledFailureGuard
{
    /// <summary>
    /// يضيف الحارس. يقع <b>أولاً</b> في خط المعالجة كي يغطّي المصادقة والتوجيه معاً.
    /// <para>
    /// <b>وما يخرج منه ثابت مهما كان الداخل:</b> رمز <c>api.internal_error</c>، ومعرّف
    /// تتبّع، وجملتان. لا نوع استثناء، ولا أثر مكدّس، ولا نصّ قاعدة بيانات، ولا اسم جدول.
    /// الأثر كاملاً يذهب إلى سجلّ الخادم تحت معرّف التتبّع نفسه — فمن يملك السجلّ يربط،
    /// ومن يملك الاستجابة وحدها لا يتعلّم شيئاً عن الداخل.
    /// </para>
    /// </summary>
    /// <param name="app">التطبيق.</param>
    public static void UseUnhandledFailureGuard(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        ILogger logger = app.ApplicationServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Babel.Api.Failure");

        app.Use(async (context, next) =>
        {
            try
            {
                await next(context).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // انقطاع العميل ليس عطلاً في الخادم.
            }
#pragma warning disable CA1031 // حارس أخير: التقاط عام مقصود — البديل هو تسريب الاستثناء إلى العميل.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                string traceId = HttpProblemResults.TraceIdOf(context);

                logger.LogError(
                    exception,
                    "عطل غير متوقّع في {Method} {Path} — معرّف التتبّع {TraceId}",
                    context.Request.Method,
                    context.Request.Path.Value,
                    traceId);

                if (context.Response.HasStarted)
                {
                    // بدأت الاستجابة: لا يمكن استبدالها. الإجهاض أصدق من نصف جسم صالح.
                    context.Abort();
                    return;
                }

                context.Response.Clear();

                try
                {
                    await HttpProblemResults
                        .Code(
                            context,
                            ApiProblems.InternalErrorCode,
                            "عطل غير متوقّع في الخادم. التفصيل في سجلّ الخادم تحت معرّف التتبّع "
                            + traceId.ToString(CultureInfo.InvariantCulture) + "، ولا يعبر منه شيء إلى هنا.",
                            "An unexpected server failure. The detail is in the server log under trace id "
                            + traceId.ToString(CultureInfo.InvariantCulture) + "; none of it crosses to here.",
                            status: StatusCodes.Status500InternalServerError)
                        .ExecuteAsync(context)
                        .ConfigureAwait(false);
                }
#pragma warning disable CA1031 // آخر ملاذ: حتى كتابة المشكلة قد تفشل، والبديل اتصال يُقطع بلا رمز.
                catch (Exception writeFailure)
#pragma warning restore CA1031
                {
                    // وقع فعلاً في هذا المشروع: مُسلسِل JSON عُطِّل بإعداد خاطئ، فرمى داخل
                    // المعالج **وداخل الحارس معاً**، فوصل العميل اتصالٌ مقطوع بلا رمز حالة
                    // ولا معرّف تتبّع — أسوأ ما يمكن أن يُسلَّم لمن يشخّص عطلاً.
                    logger.LogError(writeFailure, "تعذّرت كتابة تفاصيل المشكلة — معرّف التتبّع {TraceId}", traceId);

                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "text/plain; charset=utf-8";
                    await context.Response
                        .WriteAsync(ApiProblems.InternalErrorCode + " " + traceId, cancellationToken: CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }
        });
    }
}
