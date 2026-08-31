using System.Globalization;
using Babel.Core;
using Babel.Hr;
using Babel.Ledger;
using Babel.Inventory;
using Babel.Projects;
using Babel.Purchasing;
using Babel.RealEstate;
using Babel.Sales;
using Babel.Storage;
using Npgsql;

namespace BabelDemoCompany;

/// <summary>
/// إعدادات الأداة، مقروءةً من البيئة وحدها.
/// <para>
/// <b>ولا كلمة مرور واحدة مكتوبة هنا ولا في أي ملف في هذا المستودع</b>: كل اتصال يصل
/// كاملاً من متغيّر بيئة، وللتشغيل المحلي افتراضٌ بلا كلمة مرور يعمل مع
/// <c>pg_hba: trust</c> على 127.0.0.1. القيمة على الخادم تُبنى من سرّ في مخزن الأسرار
/// عند لحظة النشر ولا تمرّ بـgit.
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

        string salesOwner = Env("BABEL_SALES_OWNER_DB")
            ?? Env("BABEL_SALES_DB")
            ?? "Host=127.0.0.1;Port=5432;Database=babel_sales;Username=postgres;Include Error Detail=true";

        string purchasingOwner = Env("BABEL_PURCHASING_OWNER_DB")
            ?? Env("BABEL_PURCHASING_DB")
            ?? "Host=127.0.0.1;Port=5432;Database=babel_purchasing;Username=postgres;Include Error Detail=true";

        string inventoryOwner = Env("BABEL_INVENTORY_OWNER_DB")
            ?? Env("BABEL_INVENTORY_DB")
            ?? "Host=127.0.0.1;Port=5432;Database=babel_inventory;Username=postgres;Include Error Detail=true";

        // ‏**اسمٌ مستقلّ للمالك، ولا ارتداد إلى `BABEL_REALESTATE_DB`**: ذلك المتغيّر
        // هو اتصال **الخادم** (يقرأه RealEstateOptions افتراضياً)، وارتدادٌ إليه هنا كان
        // يجعل حاويةً تحمل الاثنين تنشر المخطّط بدور التطبيق فتفشل — أو أسوأ: تنجح لأن
        // أحدهم منح الدور ما لا يستحقّه. الفصل يبقى بالاسم لا بالانضباط (ADR-0003).
        string realEstateOwner = Env("BABEL_REALESTATE_OWNER_DB")
            ?? "Host=127.0.0.1;Port=5432;Database=babel_realestate;Username=postgres;Include Error Detail=true";

        string projectsOwner = Env("BABEL_PROJECTS_OWNER_DB")
            ?? "Host=127.0.0.1;Port=5432;Database=babel_projects;Username=postgres;Include Error Detail=true";

        // ‏**ولا ارتداد إلى `BABEL_HR_DB` هنا** رغم أن `HrOptions` تقرؤه افتراضياً:
        // ذلك اتصال **الخادم** بدور التطبيق، وهذه الأداة تنشر بدور المالك. والارتداد
        // إليه كان يجعل حاويةً تحمل الاثنين تحاول نشر مخطّطٍ بدورٍ لا يملك DDL.
        string hrOwner = Env("BABEL_HR_OWNER_DB")
            ?? "Host=127.0.0.1;Port=5432;Database=babel_hr;Username=postgres;Include Error Detail=true";

        string storageOwner = Env("BABEL_STORAGE_OWNER_DB")
            ?? $"Host=127.0.0.1;Port=5432;Database={StorageOptions.DefaultDatabase};Username=postgres;Include Error Detail=true";

        string coreOwner = Env("BABEL_CORE_OWNER_DB")
            ?? $"Host=127.0.0.1;Port=5432;Database={CoreOptions.DefaultDatabase};Username=postgres;Include Error Detail=true";

        string coreApp = Env("BABEL_CORE_APP_DB")
            ?? FormattableString.Invariant(
                $"Host=127.0.0.1;Port=5432;Database={CoreOptions.DefaultDatabase};Username={ledger.AppRole};Include Error Detail=true");

        return new Settings(
            Env("BABEL_ADMIN_DB")
                ?? "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Include Error Detail=true",
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
