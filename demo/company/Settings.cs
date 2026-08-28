using System.Globalization;
using Babel.Core;
using Babel.Ledger;
using Babel.Inventory;
using Babel.Purchasing;
using Babel.Sales;
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
        Guid company,
        int fiscalYear)
    {
        Maintenance = maintenance;
        Ledger = ledger;
        Core = core;
        SalesOwner = salesOwner;
        PurchasingOwner = purchasingOwner;
        InventoryOwner = inventoryOwner;
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
