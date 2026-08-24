namespace BabelRelationalSpike.Db;

// ---------------------------------------------------------------------------
// (D) One JSONB document per tenant, mapped to a real POCO graph by EF Core 10.
//     Adding a custom field, a form definition or a report template for ONE
//     customer touches no DDL and no other tenant.
//     مستند إعدادات لكل مستأجر دون ترحيل مخطط لكل عميل.
// ---------------------------------------------------------------------------

public class TenantSettings
{
    public string TenantId { get; set; } = "";
    public SettingsDoc Settings { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
}

public class SettingsDoc
{
    public string Locale { get; set; } = "ar-SA";
    public string Currency { get; set; } = "SAR";
    public int FiscalYearStartMonth { get; set; } = 1;
    public string CompanyNameAr { get; set; } = "";
    public ZatcaSettings Zatca { get; set; } = new();
    public List<CustomFieldDef> CustomFields { get; set; } = [];
    public List<ReportTemplateDef> ReportTemplates { get; set; } = [];
}

public class ZatcaSettings
{
    public string Environment { get; set; } = "sandbox";   // sandbox | simulation | production
    public string VatNumber { get; set; } = "";
    public bool PhaseTwoEnabled { get; set; }
    public int MaxRetries { get; set; } = 5;
}

public class CustomFieldDef
{
    public string Key { get; set; } = "";
    public string LabelAr { get; set; } = "";
    public string LabelEn { get; set; } = "";
    public string DataType { get; set; } = "text";
    public bool Required { get; set; }
}

public class ReportTemplateDef
{
    public string Code { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string Layout { get; set; } = "";
}
