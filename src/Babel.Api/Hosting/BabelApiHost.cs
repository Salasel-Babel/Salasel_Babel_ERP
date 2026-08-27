using System.Globalization;
using Babel.Api.Endpoints;
using Babel.Api.Errors;
using Babel.Api.Ports;
using Babel.Api.Security;
using Babel.Compliance;
using Babel.Core;
using Babel.Core.Entitlement;
using Babel.Core.Tenancy;
using Babel.Inventory;
using Babel.Ledger;
using Babel.Purchasing;
using Babel.Sales;
using Babel.SharedKernel;

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
        builder.Services.AddBabelSales();
        builder.Services.AddBabelPurchasing();
        builder.Services.AddBabelCompliance();
        builder.Services.AddBabelInventory();

        // سياق الطلب: يُملأ من الاعتماد وحده.
        builder.Services.AddScoped<RequestTenantContext>();
        builder.Services.AddScoped<ITenantContext>(static sp => sp.GetRequiredService<RequestTenantContext>());

        builder.Services.AddSingleton<IApiPrincipalResolver>(
            _ => new ConfiguredPrincipalResolver(ReadPrincipals(builder.Configuration)));

        // منفذ قراءة القيد: العقد منشور، والتنفيذ ينتظر سطح قراءة في الدفتر (ADR-0018).
        builder.Services.AddSingleton<IJournalEntryReader, UnavailableJournalEntryReader>();

        WebApplication app = builder.Build();

        app.UseUnhandledFailureGuard();
        app.UseBabelAuthentication();
        app.MapLedgerApi();
        app.MapCapabilityProfileApi();
        app.MapCompanySetupApi();

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

            byDigest[digest] = new ApiPrincipal(new TenantId(tenant), new UserId(user), companies);
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
