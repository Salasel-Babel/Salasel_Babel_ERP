using System.Collections.Immutable;
using System.Globalization;
using Babel.Core;
using Babel.Core.CompanySetup;
using Babel.Core.CapabilityProfile;
using Babel.Core.Entitlement;
using Babel.Ledger;
using Babel.Projects;
using Babel.Projects.Application;
using Babel.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BabelDemoCompany;

/// <summary>
/// بذر المقاولات — <b>مشروعٌ بعقدٍ ومقايسةٍ ومستخلصين، يُرى على سبع شاشات</b>.
/// <para>
/// وكل صفٍّ يمرّ بخدمات الوحدة المعلَنة نفسها التي يناديها سطح HTTP: مشروعٌ
/// يُسجَّل فيدخل رمزُه بُعدَ <c>project</c> في الدفتر، ثمّ عقدٌ ببنود مقايسةٍ
/// ونسبةِ احتجازٍ ومدّةِ ضمان، ثمّ مستخلصان <b>تراكميّان</b> يُرحَّلان — فتصير
/// شاشاتُ المستخلص والمحتجزات وموقفِ العقد وقائعَ لا جداولَ فارغة.
/// </para>
/// <para>
/// <b>ومستخلصان لا واحد، والثاني تراكميٌّ على الأول:</b> المستخلص في المقاولات
/// يُقاس بالكمّية المنفَّذة <b>منذ بداية العقد</b> لا بما نُفّذ في الفترة، ثمّ
/// يُخصم منه ما سبق صرفه. فمستخلصٌ واحد يُخفي هذا كلَّه ويجعل الشاشة تبدو
/// فاتورةً عادية — وهي ليست كذلك.
/// </para>
/// <para>
/// <b>ولا إدراج خام واحد</b>: مستخلصٌ يُكتب في الجدول مباشرةً ينتج محتجزاتٍ لا
/// يقابلها قيدٌ في الدفتر، وموقفَ عقدٍ لا يطابق ما رُحّل. فتُظهر الشاشاتُ أرقاماً
/// صحيحةَ الشكل كاذبةَ المصدر.
/// </para>
/// </summary>
internal sealed class ProjectsSeed : IDisposable
{
    private readonly Settings _settings;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    private readonly ProjectRegistryService _registry;
    private readonly ClientCertificateService _certificates;
    private readonly IEntitlementService _entitlements;
    private readonly ICapabilityProfileStore _profiles;
    private readonly ICompanyMoneyResolver _companyMoney;

    private CompanyMoney _money;
    private int _draftedCertificates;

    private ProjectsSeed(Settings settings)
    {
        _settings = settings;

        ServiceCollection services = new();

        services.AddBabelCore(options =>
        {
            options.AppConnectionString = settings.Core.AppConnectionString;
            options.OwnerConnectionString = settings.Core.OwnerConnectionString;
            options.AppRole = settings.Core.AppRole;
        });

        // الدفتر: منه IPostingService الذي تناديه بوّابة ترحيل المشاريع، ومنه
        // **الكاتب الوحيد** في سجلّ بُعد المشروع.
        services.AddBabelLedger(options =>
        {
            options.AppConnectionString = settings.Ledger.AppConnectionString;
            options.OwnerConnectionString = settings.Ledger.OwnerConnectionString;
            options.AppRole = settings.Ledger.AppRole;
        });

        services.AddBabelProjects(options =>
        {
            options.ConnectionString = settings.ProjectsOwner.ConnectionString;
        });

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();

        IServiceProvider scoped = _scope.ServiceProvider;
        _registry = scoped.GetRequiredService<ProjectRegistryService>();
        _certificates = scoped.GetRequiredService<ClientCertificateService>();
        _entitlements = scoped.GetRequiredService<IEntitlementService>();
        _profiles = scoped.GetRequiredService<ICapabilityProfileStore>();
        _companyMoney = scoped.GetRequiredService<ICompanyMoneyResolver>();
    }

    private TenantId Tenant => new(_settings.Company);

    private CurrencyCode Currency => _money.IsAssigned
        ? _money.Currency
        : throw new InvalidOperationException("عملة المنشأة تُحلّ في أول الدورة قبل أي مستند.");

    /// <summary>يبذر دورة المقاولات إن لم تكن مبذورة، ويُرجع عدد المستخلصات المُسوَّدة.</summary>
    /// <param name="settings">الإعدادات.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<int> RunAsync(Settings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Say.Step("بذر دورة المقاولات عبر خدمات الوحدة / seeding the contracting cycle through the module services");

        if (await AlreadySeededAsync(settings, cancellationToken).ConfigureAwait(false))
        {
            Say.Detail("المقاولات مبذورة سلفاً — لا يُعاد البذر.");
            return 0;
        }

        using ProjectsSeed seed = new(settings);
        await seed.EntitleAsync(cancellationToken).ConfigureAwait(false);
        await seed.OpenDocumentTypeAsync(cancellationToken).ConfigureAwait(false);
        await seed.CycleAsync(cancellationToken).ConfigureAwait(false);
        return seed._draftedCertificates;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }

    private static async Task<bool> AlreadySeededAsync(Settings settings, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = new(settings.ProjectsOwner.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // على أوّل ما تكتبه الدورة — المشروع — لا على المستخلص الذي هو آخرُها.
        await using NpgsqlCommand command = new(
            """select count(*) from projects.project where "TenantId" = $1""", connection);
        command.Parameters.AddWithValue(settings.Company);
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture) > 0;
    }

    private async Task EntitleAsync(CancellationToken cancellationToken)
    {
        Result<EntitlementSet> applied = await _entitlements
            .ApplyAsync(
                new EntitlementChangeRequest(
                    Tenant,
                    // ‏**والمخزون معها في الطلب نفسه، لا مصادفةً:** الوحدة تعلن
                    // تبعيّتها — مقاولاتٌ بلا مخزونٍ مستحَقّ ترفضها المنصّة بـ
                    // `entitlement.unsatisfied_requirement`، لأن المستخلص يصرف
                    // موادَّ من مستودع. وطلبان منفصلان يمرّان أو لا يمرّان بحسب
                    // ترتيب تشغيل البذور — وهو ترتيبٌ لا يجوز أن يحكم صحّةَ نشرة.
                    new Dictionary<BabelModule, EntitlementState>
                    {
                        [BabelModule.Inventory] = EntitlementState.Entitled,
                        [BabelModule.Projects] = EntitlementState.Entitled,
                    },
                    Seed.Actor,
                    "شراء وحدتَي المخزون والمقاولات للمنشأة التجريبية / inventory + projects entitled for the demo company"),
                cancellationToken)
            .ConfigureAwait(false);

        Ok(applied, "شراء وحدتَي المخزون والمقاولات");
    }

    /// <summary>
    /// يفتح نوع «مستخلص العميل» في ملفّ قدرات المنشأة — <b>ولا يُفتح ضمناً</b>.
    /// <para>
    /// ملفُّ القدرات يقرّر أيَّ أنواع المستندات تُقبل لهذا المستأجر، و<b>الاستحقاق
    /// وحده لا يكفي</b>: وحدةٌ مشتراةٌ تعني أن المنشأة تملك المقاولات، وملفُّ
    /// القدرات يعني أنها فعّلت مستخلصَ العميل بعينه — والفصلُ مقصود، فمنشأةٌ قد
    /// تملك الوحدة وتعمل بعقودٍ مقطوعة بلا مستخلصات.
    /// </para>
    /// <para>
    /// <b>ويُقرأ الملفُّ القائم ويُضاف إليه، لا يُستبدل:</b> كتابةُ ملفٍّ جديد هنا
    /// تمحو <c>sales.invoice</c> الذي كتبته بذرةُ المبيعات، فتسقط فواتيرُ لم
    /// يمسّها أحد. و<c>retention</c> مفعَّلةٌ لأن العقد يحتجز ٥٪ — وقدرةٌ مطفأة مع
    /// عقدٍ محتجِز تجعل المحتجزَ يُرفض عند أول مستخلص.
    /// </para>
    /// </summary>
    private async Task OpenDocumentTypeAsync(CancellationToken cancellationToken)
    {
        ValidatedCapabilityProfile? current = await _profiles.FindAsync(Tenant, cancellationToken).ConfigureAwait(false);

        Dictionary<string, DocumentProfileDraft> documents = new(StringComparer.Ordinal);

        if (current is not null)
        {
            foreach (DocumentShape shape in current.Shapes)
            {
                // كلُّ قدرةٍ متاحةٍ تُنقل بحالتها الفعلية — المفعَّلةُ مفعَّلةً
                // والمطفأةُ مطفأةً. وقراءةُ المفعَّلة وحدها تطفئ الباقيَ ضمناً،
                // وذلك سحبُ قدرةٍ لم يطلبه أحد.
                documents[shape.DocumentType.Value] = new DocumentProfileDraft(
                    shape.AvailableCapabilities.ToDictionary(
                        static capability => capability.Value,
                        shape.EnabledCapabilities.Contains,
                        StringComparer.Ordinal),
                    shape.Defaults);
            }
        }

        documents["projects.client_certificate"] = new DocumentProfileDraft(
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["advance"] = false,
                ["retention"] = true,
            },
            ImmutableSortedDictionary<string, string>.Empty);

        Result<ValidatedCapabilityProfile> profile =
            ValidatedCapabilityProfile.Create(new CapabilityProfileDraft(documents), EmbeddedPostingEventDirectory.Default);

        Ok(profile, "ملفّ القدرات بعد فتح مستخلص العميل");
        await _profiles.SaveAsync(Tenant, profile.Value, cancellationToken).ConfigureAwait(false);
        Say.Detail("ملفُّ القدرات: فُتح «مستخلص العميل» بقدرة المحتجز — والأنواعُ السابقة كما هي.");
    }

    private async Task CycleAsync(CancellationToken cancellationToken)
    {
        Result<CompanyMoney> money = await _companyMoney.ResolveAsync(Tenant, cancellationToken).ConfigureAwait(false);
        Say.Require(money.IsSuccess, "عملة المنشأة من صفّ التأسيس", string.Join(" | ", money.Errors.Select(static e => e.ToString())));
        _money = money.Value;

        // ── ١ · مشروعان — أحدهما بعقدٍ والآخر بلا عقدٍ بعد ─────────────────────
        //
        // والثاني مقصود: شاشةُ سجلّ المشاريع تعرض الحالتين كما تقعان عند العميل —
        // مشروعٌ تعاقَد وآخرُ ما زال تسجيلاً. وسجلٌّ كلُّ صفٍّ فيه متعاقَد يُخفي
        // الحالةَ الأكثر شيوعاً في أول الأمر.
        Guid tower = await ProjectAsync(
            "PRJ-001", "برج نخيل التجاري — الدمام", "Nakheel Commercial Tower — Dammam", 2, 1, cancellationToken).ConfigureAwait(false);
        _ = await ProjectAsync(
            "PRJ-002", "توسعة مستودعات الخبر", "Khobar warehouse expansion", 7, 20, cancellationToken).ConfigureAwait(false);
        Say.Detail("مشاريع: اثنان — أحدهما متعاقَدٌ والآخر تسجيلٌ بلا عقدٍ بعد.");

        // ── ٢ · العقد ببنود مقايسته ────────────────────────────────────────────
        //
        // والاحتجازُ ٥٪ ومدّةُ الضمان ١٢ شهراً: رقمان يحكمان شاشتَي المحتجزات
        // وخطابات الضمان، فلو كانا صفرين لظهرت الشاشتان فارغتين على عقدٍ قائم.
        Guid contract = await ContractAsync(
            "CON-2026-001",
            tower,
            "CUST-001",
            0.05m,
            12,
            [
                new BoqItemDraft("BOQ-010", "أعمال الحفر والردم", new ProjectQuantity(4_500m, "M3"), Money.Of(38m, Currency)),
                new BoqItemDraft("BOQ-020", "خرسانة مسلّحة للأساسات", new ProjectQuantity(1_200m, "M3"), Money.Of(520m, Currency)),
                new BoqItemDraft("BOQ-030", "حديد تسليح مورَّد ومركَّب", new ProjectQuantity(180_000m, "KG"), Money.Of(4.2m, Currency)),
                new BoqItemDraft("BOQ-040", "أعمال البناء بالبلوك", new ProjectQuantity(8_600m, "M2"), Money.Of(95m, Currency)),
                new BoqItemDraft("BOQ-050", "التشطيبات الداخلية", new ProjectQuantity(8_600m, "M2"), Money.Of(310m, Currency)),
            ],
            cancellationToken).ConfigureAwait(false);

        Say.Detail("عقدٌ واحد: خمسةُ بنودِ مقايسةٍ بكمّياتٍ وأسعارِ وحدة، احتجازٌ ٥٪ وضمانٌ ١٢ شهراً.");

        // ── ٣ · مستخلصان تراكميّان — **مسوّدتان لا مُرحَّلَين، وذلك مقصود** ────
        //
        // ‏`ClientCertificateService.PostAsync` يرفض بـ`projects.contract_policy.pending`
        // لأن أربعةَ بنودِ سياسةٍ على العقد لم يعتمدها محاسب: وعاءُ نسبة المحتجز
        // وقاعدةُ استرداد الدفعة المقدمة · مستوى التصنيف الضريبي ومن يحسب الضريبة ·
        // موضعُ التقريب · ظهورُ المحتجز المدين في مطابقة العميل.
        //
        // **ولا يُخترع لها جواب هنا.** الجدول `projects.contract_policy` يُبنى فارغاً
        // ولا بابَ على السطح يكتب فيه بقصد — «هذه إجاباتُ محاسبٍ لا إعداداتُ
        // مستخدم». وبذرةٌ تكتب فيها تختار عن المالك أربعةَ قراراتٍ محاسبية وتُسكِت
        // رفضاً كُتب ليُرى، فتُظهر مستخلصاً مُرحَّلاً بأرقامٍ تقوم على تخمين —
        // و«قيدٌ يقوم على تخمينٍ قيدٌ متوازن يقنع كل حارس ولا يقنع مدقّقاً».
        //
        // فالمسوّدتان تُبذران، والرفضُ يبقى ظاهراً على شاشة المستخلص باسم رمزه.
        //
        // الكمّيةُ في المستخلص الثاني **تشمل** كمّية الأول — وهذا هو معنى
        // «تراكمي»: الوحدةُ تخصم ما سبق صرفه ولا يخصمه من يكتب. ولو أرسلتُ
        // الفرقَ لظهر المستخلصُ الثاني بنصف قيمته الصحيحة.
        await CertificateAsync(
            "IPC-001", contract, 1, 2, 28,
            [
                Line("BOQ-010", "أعمال الحفر والردم", 4_500m, "M3", 171_000m),
                Line("BOQ-020", "خرسانة مسلّحة للأساسات", 400m, "M3", 208_000m),
            ],
            cancellationToken).ConfigureAwait(false);

        await CertificateAsync(
            "IPC-002", contract, 2, 5, 31,
            [
                Line("BOQ-010", "أعمال الحفر والردم", 4_500m, "M3", 171_000m),
                Line("BOQ-020", "خرسانة مسلّحة للأساسات", 1_200m, "M3", 624_000m),
                Line("BOQ-030", "حديد تسليح مورَّد ومركَّب", 96_000m, "KG", 403_200m),
            ],
            cancellationToken).ConfigureAwait(false);

        Say.Detail("مستخلصات: " + Say.Count(_draftedCertificates) + " مسوّدةً تراكمياً — والثاني يشمل الأول.");
        Say.Detail("ولا تُرحَّل: أربعةُ بنودِ سياسةٍ على العقد تنتظر اعتمادَ محاسب — وذلك ما يُرى على الشاشة.");
    }

    private async Task<Guid> ProjectAsync(
        string code, string arabic, string english, int month, int day, CancellationToken cancellationToken)
    {
        Result<ProjectView> created = await _registry
            .CreateProjectAsync(
                Tenant,
                Seed.Actor,
                new ProjectDraft(code, Named(arabic, english), new DateOnly(_settings.FiscalYear, month, day)),
                cancellationToken)
            .ConfigureAwait(false);

        Ok(created, "تسجيل المشروع " + code);
        return created.Value.Id;
    }

    private async Task<Guid> ContractAsync(
        string number, Guid project, string customer, decimal retention, int guaranteeMonths,
        IReadOnlyList<BoqItemDraft> items, CancellationToken cancellationToken)
    {
        Result<ContractView> created = await _registry
            .CreateContractAsync(
                Tenant,
                Seed.Actor,
                new ContractDraft(
                    number, project, customer, new DateOnly(_settings.FiscalYear, 2, 10), retention, guaranteeMonths, items),
                cancellationToken)
            .ConfigureAwait(false);

        Ok(created, "إنشاء العقد " + number);
        return created.Value.Id;
    }

    private async Task CertificateAsync(
        string number, Guid contract, int sequence, int month, int day,
        IReadOnlyList<CertificateLineDraft> lines, CancellationToken cancellationToken)
    {
        Result<CertificateView> drafted = await _certificates
            .DraftAsync(
                Tenant,
                Seed.Actor,
                new CertificateDraft(
                    number,
                    contract,
                    sequence,
                    new DateOnly(_settings.FiscalYear, month, 1),
                    new DateOnly(_settings.FiscalYear, month, day),
                    lines),
                cancellationToken)
            .ConfigureAwait(false);

        Ok(drafted, "تسويد المستخلص " + number);
        _draftedCertificates++;
    }

    /// <summary>سطرُ مستخلصٍ على بندِ مقايسة — والكمّيةُ <b>تراكمية</b> لا كمّيةَ فترة.</summary>
    private CertificateLineDraft Line(string code, string description, decimal cumulative, string unit, decimal amount)
        => new(null, code, description, new ProjectQuantity(cumulative, unit), Money.Of(amount, Currency));

    /// <summary>اسمٌ بسجلّه العربي وترجمته الإنجليزية — لا حقلٌ إنجليزيّ ثابت (ADR-0021).</summary>
    private static TranslatedName Named(string arabic, string english)
        => new(arabic, new Dictionary<string, string> { ["en"] = english });

    private static void Ok<T>(Result<T> result, string what)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                what + " — رُفض: " + string.Join(" | ", result.Errors.Select(static error => error.ToString())));
        }
    }
}
