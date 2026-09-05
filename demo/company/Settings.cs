using System.Globalization;
using Babel.Core;
using Babel.Hr;
using Babel.Ledger;
using Babel.Inventory;
using Babel.Projects;
using Babel.Purchasing;
using Babel.RealEstate;
using Babel.Sales;
using Babel.SharedKernel;
using Babel.Storage;
using Npgsql;

namespace BabelDemoCompany;

/// <summary>
/// إعدادات الأداة، مقروءةً من البيئة وحدها.
/// <para>
/// <b>ولا كلمة مرور ولا نصّ اتصال مكتوبٌ هنا ولا في أي ملف في هذا المستودع</b>: كل
/// اتصال يصل كاملاً من متغيّر بيئة، و<b>غيابُه يوقف الأداة برسالةٍ تسمّي المتغيّر</b>.
/// القيمة على الخادم تُبنى من سرّ في مخزن الأسرار عند لحظة النشر ولا تمرّ بـgit.
/// <b>وللتطوير على جهازٍ محلّي متغيّرٌ واحد يقول ذلك:</b> <c>BABEL_LOCAL_DEV=1</c>،
/// وحينها تُبنى اتصالاتٌ محلّية بلا كلمة مرور تعمل مع <c>pg_hba: trust</c> على المِعوَد
/// (‏ADR-جديد).
/// </para>
/// </summary>
internal sealed class Settings
{
    /// <summary>الدفتر الافتراضي — نفس الاسم الذي تفترضه الواجهة.</summary>
    public const string Book = "MAIN";

    private Settings(
        string maintenance,
        LedgerOptions ledger,
        CoreOptions core,
        SalesOptions salesOwner,
        PurchasingOptions purchasingOwner,
        InventoryOptions inventoryOwner,
        RealEstateOptions realEstateOwner,
        ProjectsOptions projectsOwner,
        HrOptions hrOwner,
        StorageOptions storageOwner,
        Guid company,
        int fiscalYear)
    {
        Maintenance = maintenance;
        Ledger = ledger;
        Core = core;
        SalesOwner = salesOwner;
        PurchasingOwner = purchasingOwner;
        InventoryOwner = inventoryOwner;
        RealEstateOwner = realEstateOwner;
        ProjectsOwner = projectsOwner;
        HrOwner = hrOwner;
        StorageOwner = storageOwner;
        Company = company;
        FiscalYear = fiscalYear;
    }

    /// <summary>اتصال الصيانة: إنشاء قواعد البيانات والأدوار. دور خارق، ولا يُستعمل بعد ذلك.</summary>
    public string Maintenance { get; }

    /// <summary>إعدادات الدفتر — اتصال المالك واتصال التطبيق واسم الدور.</summary>
    public LedgerOptions Ledger { get; }

    /// <summary>
    /// إعدادات النواة — اتصال المالك واتصال التطبيق واسم الدور.
    /// <para>
    /// ودورُ التطبيق <b>هو دور الدفتر نفسه</b>: منشأةٌ واحدة، وخادمٌ واحد، ودورٌ واحد
    /// أقلّ امتيازاً يعبر المخطّطات كلّها. والفصل الذي يهمّ هو مالك/تطبيق لا
    /// دورٌ لكل مخطّط.
    /// </para>
    /// </summary>
    public CoreOptions Core { get; }

    /// <summary>اتصال <b>مالك</b> مخطّط المبيعات — للنشر وحده.</summary>
    public SalesOptions SalesOwner { get; }

    /// <summary>اتصال <b>مالك</b> مخطّط المشتريات — للنشر وحده.</summary>
    public PurchasingOptions PurchasingOwner { get; }

    /// <summary>إعدادات المخزون — التقييم وتكلفة المبيعات (‏ADR-0039).</summary>
    public InventoryOptions InventoryOwner { get; }

    /// <summary>
    /// اتصال <b>مالك</b> قاعدة العقارات — للنشر والبذر، ولا يصل الخادمَ أبداً.
    /// <para>
    /// وهو الاتصال الوحيد في هذه الأداة الذي يستلزم أكثر من <c>create table</c>:
    /// مخطّط العقارات يركّب امتداد <c>btree_gist</c> ويبني عليه قيد استبعاد زمنياً،
    /// وذلك فعلُ مالك بامتياز — ولذلك موضعه هنا لا في مسار التطبيق (‏ADR-0003 ·
    /// <see cref="RealEstateExtension"/>).
    /// </para>
    /// </summary>
    public RealEstateOptions RealEstateOwner { get; }

    /// <summary>اتصال <b>مالك</b> قاعدة المقاولات — للنشر وحده.</summary>
    public ProjectsOptions ProjectsOwner { get; }

    /// <summary>اتصال <b>مالك</b> قاعدة الموارد البشرية — للنشر وحده.</summary>
    public HrOptions HrOwner { get; }

    /// <summary>
    /// إعدادات مخزن المرفقات بدور المالك — ومعها <b>اسم دور التطبيق</b>، لأن
    /// <c>StorageGrants.sql</c> يقرؤه من إعداد الجلسة ولا يُثبَّت اسم بيئة في نصّ نشر.
    /// </summary>
    public StorageOptions StorageOwner { get; }

    /// <summary>معرّف الشركة/المستأجر التجريبي.</summary>
    public Guid Company { get; }

    /// <summary>السنة المالية التي تُبذر فتراتها وتُرحَّل فيها المستندات.</summary>
    public int FiscalYear { get; }

    /// <summary>اسم قاعدة الدفتر كما تُقرأ من اتصال المالك.</summary>
    public string LedgerDatabase => DatabaseOf(Ledger.OwnerConnectionString);

    /// <summary>اسم قاعدة المبيعات.</summary>
    public string SalesDatabase => DatabaseOf(SalesOwner.ConnectionString);

    /// <summary>اسم قاعدة المشتريات.</summary>
    public string PurchasingDatabase => DatabaseOf(PurchasingOwner.ConnectionString);

    /// <summary>اسم قاعدة المخزون.</summary>
    public string InventoryDatabase => DatabaseOf(InventoryOwner.ConnectionString);

    /// <summary>اسم قاعدة النواة.</summary>
    public string CoreDatabase => DatabaseOf(Core.OwnerConnectionString);

    /// <summary>اسم قاعدة العقارات.</summary>
    public string RealEstateDatabase => DatabaseOf(RealEstateOwner.ConnectionString);

    /// <summary>اسم قاعدة المقاولات.</summary>
    public string ProjectsDatabase => DatabaseOf(ProjectsOwner.ConnectionString);

    /// <summary>اسم قاعدة الموارد البشرية.</summary>
    public string HrDatabase => DatabaseOf(HrOwner.ConnectionString);

    /// <summary>اسم قاعدة المرفقات.</summary>
    public string StorageDatabase => DatabaseOf(StorageOwner.OwnerConnectionString);

    /// <summary>يقرأ الإعدادات من البيئة.</summary>
    public static Settings FromEnvironment()
    {
        LedgerOptions ledger = new();

        // ‏**والدفتر يُرفض غيابُ اتصاليه هنا** — لا عند أول `DatabaseOf` فيُقرأ العطل
        // «اتصالٌ بلا اسم قاعدة». الاثنان مطلوبان: المالك ينشر المخطّط، ودور التطبيق
        // يبذر ويُثبت — وهو ما يجعل البذر عاجزاً بنيوياً عن الكتابة خارج المسار المُعلَن.
        ledger.EnsureOwnerConfigured();
        ledger.EnsureAppConfigured();

        string salesOwner = Owner("BABEL_SALES_OWNER_DB", Env("BABEL_SALES_DB"), SalesOptions.DefaultDatabase);

        string purchasingOwner = Owner(
            "BABEL_PURCHASING_OWNER_DB", Env("BABEL_PURCHASING_DB"), PurchasingOptions.DefaultDatabase);

        string inventoryOwner = Owner(
            "BABEL_INVENTORY_OWNER_DB", Env("BABEL_INVENTORY_DB"), InventoryOptions.DefaultDatabase);

        // ‏**اسمٌ مستقلّ للمالك، ولا ارتداد إلى `BABEL_REALESTATE_DB`**: ذلك المتغيّر
        // هو اتصال **الخادم** (يقرأه RealEstateOptions افتراضياً)، وارتدادٌ إليه هنا كان
        // يجعل حاويةً تحمل الاثنين تنشر المخطّط بدور التطبيق فتفشل — أو أسوأ: تنجح لأن
        // أحدهم منح الدور ما لا يستحقّه. الفصل يبقى بالاسم لا بالانضباط (ADR-0003).
        string realEstateOwner = Owner("BABEL_REALESTATE_OWNER_DB", null, RealEstateOptions.DefaultDatabase);

        string projectsOwner = Owner("BABEL_PROJECTS_OWNER_DB", null, ProjectsOptions.DefaultDatabase);

        // ‏**ولا ارتداد إلى `BABEL_HR_DB` هنا** رغم أن `HrOptions` تقرؤه افتراضياً:
        // ذلك اتصال **الخادم** بدور التطبيق، وهذه الأداة تنشر بدور المالك. والارتداد
        // إليه كان يجعل حاويةً تحمل الاثنين تحاول نشر مخطّطٍ بدورٍ لا يملك DDL.
        string hrOwner = Owner("BABEL_HR_OWNER_DB", null, HrDefaultDatabase);

        string storageOwner = Owner("BABEL_STORAGE_OWNER_DB", null, StorageOptions.DefaultDatabase);

        string coreOwner = Owner("BABEL_CORE_OWNER_DB", null, CoreOptions.DefaultDatabase);

        string coreApp = Required(
            "BABEL_CORE_APP_DB",
            DeploymentSetting.Resolve(
                Env("BABEL_CORE_APP_DB"),
                DeploymentSetting.LocalDevelopmentDeclared(),
                CoreOptions.DefaultDatabase,
                ledger.AppRole),
            "اتصال دور التطبيق على قاعدة النواة",
            "the Core application-role connection");

        return new Settings(
            Owner("BABEL_ADMIN_DB", null, MaintenanceDatabase),
            ledger,
            new CoreOptions
            {
                OwnerConnectionString = coreOwner,
                AppConnectionString = coreApp,
                AppRole = Env("BABEL_CORE_APP_ROLE") ?? ledger.AppRole,
            },
            new SalesOptions { ConnectionString = salesOwner, CompanyCurrency = ledger.CompanyCurrency },
            new PurchasingOptions { ConnectionString = purchasingOwner, CompanyCurrency = ledger.CompanyCurrency },
            new InventoryOptions { ConnectionString = inventoryOwner, CompanyCurrency = ledger.CompanyCurrency },
            new RealEstateOptions { ConnectionString = realEstateOwner, CompanyCurrency = ledger.CompanyCurrency },
            new ProjectsOptions { ConnectionString = projectsOwner, CompanyCurrency = ledger.CompanyCurrency },
            new HrOptions { ConnectionString = hrOwner, CompanyCurrency = ledger.CompanyCurrency },
            new StorageOptions { OwnerConnectionString = storageOwner, AppRole = ledger.AppRole },
            Guid.TryParseExact(Env("BABEL_DEMO_COMPANY_ID"), "D", out Guid company)
                ? company
                : new Guid("d3305e1e-0000-4000-8000-000000000001"),
            int.TryParse(Env("BABEL_DEMO_FISCAL_YEAR"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int year)
                ? year
                : 2026);
    }

    /// <summary>اسم قاعدة الصيانة — القاعدة التي يُتّصل بها كي تُنشأ بقيّة القواعد.</summary>
    private const string MaintenanceDatabase = "postgres";

    /// <summary>اسم قاعدة الموارد البشرية للتطوير المحلّي.</summary>
    private const string HrDefaultDatabase = "babel_hr";

    /// <summary>
    /// اتصالُ مالكٍ من البيئة. <b>الغياب يُرفض ولا يُخمَّن</b> — إلا في وضع تطويرٍ
    /// مُعلَن باسمه (<c>BABEL_LOCAL_DEV</c>)، وحينها يُبنى اتصالٌ محلّي على المِعوَد.
    /// <para>
    /// <b>وهذه الأداة هي حاوية الترحيل في النشر</b> (<c>deploy/Dockerfile.migrator</c>)،
    /// فارتدادُها الصامت لم يكن ارتداداً على جهاز مطوّر بل <b>على خادم</b>: متغيّرٌ ناقص
    /// كان يجعلها تنشر مخطّطاً — أو تحاول — على <c>127.0.0.1</c> <b>داخل الحاوية نفسها</b>
    /// بالمستخدم الفائق، فيُقرأ العطلُ «‏Connection refused» أي عطلَ شبكةٍ لا إعداداً ناقصاً.
    /// وهو مكتوب بنصّه في تعليق <c>deploy/compose.yml</c> فوق سطر المخزون.
    /// </para>
    /// </summary>
    /// <param name="variable">اسم متغيّر اتصال المالك.</param>
    /// <param name="alternative">بديلٌ مقبول من البيئة، أو <c>null</c> إن لم يكن له بديل.</param>
    /// <param name="database">اسم القاعدة في وضع التطوير المُعلَن.</param>
    private static string Owner(string variable, string? alternative, string database)
    {
        string resolved = DeploymentSetting.Resolve(
            Env(variable) ?? alternative,
            DeploymentSetting.LocalDevelopmentDeclared(),
            database,
            DeploymentSetting.LocalDevelopmentOwnerRole);

        return Required(variable, resolved, "اتصال المالك على قاعدة " + database, "the owner connection for " + database);
    }

    /// <summary>يرفع عطلاً يسمّي المتغيّر إن كانت القيمة المحسومة فارغة.</summary>
    private static string Required(string variable, string resolved, string subjectAr, string subjectEn) =>
        string.IsNullOrWhiteSpace(resolved)
            ? throw DeploymentSetting.Missing(
                "demo.connection_not_configured", variable, variable, subjectAr, subjectEn)
            : resolved;

    private static string? Env(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string DatabaseOf(string connectionString)
    {
        string? database = new NpgsqlConnectionStringBuilder(connectionString).Database;
        return string.IsNullOrWhiteSpace(database)
            ? throw new InvalidOperationException("اتصال بلا اسم قاعدة بيانات: " + Redact(connectionString))
            : database;
    }

    /// <summary>يحجب كلمة المرور قبل أي طباعة. سجلٌّ يحمل اعتماداً هو تسريب مؤجَّل.</summary>
    public static string Redact(string connectionString)
    {
        NpgsqlConnectionStringBuilder builder = new(connectionString);
        if (!string.IsNullOrEmpty(builder.Password))
        {
            builder.Password = "***";
        }

        return builder.ConnectionString;
    }
}
