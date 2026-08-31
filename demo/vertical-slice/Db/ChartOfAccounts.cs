using BabelDemo.Support;
using Npgsql;

namespace BabelDemo.Db;

/// <summary>
/// دليل حسابات مبذور من docs/reference/chart-of-accounts.md.
/// كل صف يحمل الاسم العربي والاسم الإنجليزي — لا استثناء.
/// Every seeded row carries both name_ar and name_en.
/// </summary>
internal static class ChartOfAccounts
{
    public sealed record Row(string Code, string? Parent, string NameAr, string NameEn,
                             string Type, string NormalSide, bool Postable);

    public static readonly Row[] Accounts =
    [
        new("1",    null, "الأصول",                              "Assets",                              "asset",     "debit",  false),
        new("11",   "1",  "الأصول المتداولة",                    "Current assets",                      "asset",     "debit",  false),
        new("1101", "11", "النقد بالصندوق",                      "Cash on hand",                        "asset",     "debit",  true),
        new("1201", "11", "النقد لدى البنوك",                    "Cash at banks",                       "asset",     "debit",  true),
        new("1301", "11", "العملاء — ذمم مدينة",                 "Trade receivables",                   "asset",     "debit",  true),
        new("1305", "11", "ضريبة القيمة المضافة المستردة",       "Recoverable VAT (input)",             "asset",     "debit",  true),
        new("1306", "11", "مصروفات مدفوعة مقدماً",               "Prepaid expenses",                    "asset",     "debit",  true),
        new("1401", "11", "مخزون البضاعة",                       "Merchandise inventory",               "asset",     "debit",  true),
        new("15",   "1",  "الأصول غير المتداولة",                "Non-current assets",                  "asset",     "debit",  false),
        new("1501", "15", "الأصول الثابتة — التكلفة",            "Fixed assets — cost",                 "asset",     "debit",  true),
        new("1502", "15", "مجمع إهلاك الأصول الثابتة",           "Accumulated depreciation",            "asset",     "credit", true),

        new("2",    null, "الخصوم",                              "Liabilities",                         "liability", "credit", false),
        new("21",   "2",  "الخصوم المتداولة",                    "Current liabilities",                 "liability", "credit", false),
        new("2101", "21", "الموردون — ذمم دائنة",                "Trade payables",                      "liability", "credit", true),
        new("2131", "21", "ضريبة القيمة المضافة — مخرجات",       "VAT payable (output)",                "liability", "credit", true),
        new("2141", "21", "دفعات مقدمة من عملاء",                "Customer advances",                   "liability", "credit", true),
        new("2201", "21", "رواتب مستحقة الدفع",                  "Accrued payroll",                     "liability", "credit", true),
        new("2206", "21", "مصروفات مستحقة",                      "Accrued expenses",                    "liability", "credit", true),
        new("25",   "2",  "الخصوم غير المتداولة",                "Non-current liabilities",             "liability", "credit", false),
        new("2251", "25", "قروض طويلة الأجل",                    "Long-term loans",                     "liability", "credit", true),

        new("3",    null, "حقوق الملكية",                        "Equity",                              "equity",    "credit", false),
        new("3101", "3",  "رأس المال",                           "Share capital",                       "equity",    "credit", true),
        new("3151", "3",  "الاحتياطي النظامي",                   "Statutory reserve",                   "equity",    "credit", true),
        new("3201", "3",  "الأرباح المبقاة",                     "Retained earnings",                   "equity",    "credit", true),

        new("4",    null, "الإيرادات",                           "Revenue",                             "revenue",   "credit", false),
        new("4101", "4",  "إيرادات المبيعات",                    "Sales revenue",                       "revenue",   "credit", true),
        new("4102", "4",  "مردودات ومسموحات المبيعات",           "Sales returns and allowances",        "revenue",   "debit",  true),
        new("4201", "4",  "إيرادات عقود المقاولات",              "Construction contract revenue",       "revenue",   "credit", true),
        new("4301", "4",  "إيرادات الإيجار",                     "Rental income",                       "revenue",   "credit", true),

        new("5",    null, "المصروفات",                           "Expenses",                            "expense",   "debit",  false),
        new("51",   "5",  "تكلفة الإيرادات",                     "Cost of revenue",                     "expense",   "debit",  false),
        new("5101", "51", "تكلفة البضاعة المباعة",               "Cost of goods sold",                  "expense",   "debit",  true),
        new("5201", "51", "تكلفة مواد المشاريع",                 "Project materials cost",              "expense",   "debit",  true),
        new("55",   "5",  "المصروفات التشغيلية والإدارية",       "Operating and administrative expenses","expense",   "debit",  false),
        new("5501", "55", "رواتب وأجور",                         "Salaries and wages",                  "expense",   "debit",  true),
        new("5510", "55", "إيجارات",                             "Rent",                                "expense",   "debit",  true),
        new("5511", "55", "مرافق (كهرباء ومياه واتصالات)",       "Utilities",                           "expense",   "debit",  true),
        new("5516", "55", "أتعاب مهنية واستشارية",               "Professional fees",                   "expense",   "debit",  true),
        new("5520", "55", "تقنية معلومات واشتراكات برمجية",      "IT and software subscriptions",       "expense",   "debit",  true),
        new("5601", "55", "مصروف الإهلاك",                       "Depreciation expense",                "expense",   "debit",  true),
        new("5801", "55", "مصروفات بنكية وفوائد",                "Bank charges and interest",           "expense",   "debit",  true),
    ];

    /// <summary>يُنفَّذ بحساب المالك: دور التطبيق لا يملك INSERT على دليل الحسابات.</summary>
    public static async Task SeedAsync()
    {
        await using var c = await Sql.OpenAsync(Config.Owner);
        var order = 0;
        foreach (var a in Accounts)
        {
            await using var cmd = new NpgsqlCommand("""
                insert into ledger.account
                    (account_code, parent_code, name_ar, name_en, account_type, normal_side, is_postable, sort_order)
                values (@code, @parent, @ar, @en, @type, @side, @postable, @sort)
                on conflict (account_code) do update set
                    parent_code = excluded.parent_code, name_ar = excluded.name_ar,
                    name_en = excluded.name_en, account_type = excluded.account_type,
                    normal_side = excluded.normal_side, is_postable = excluded.is_postable,
                    sort_order = excluded.sort_order
                """, c);
            cmd.Parameters.AddWithValue("code", a.Code);
            cmd.Parameters.AddWithValue("parent", (object?)a.Parent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("ar", a.NameAr);
            cmd.Parameters.AddWithValue("en", a.NameEn);
            cmd.Parameters.AddWithValue("type", a.Type);
            cmd.Parameters.AddWithValue("side", a.NormalSide);
            cmd.Parameters.AddWithValue("postable", a.Postable);
            cmd.Parameters.AddWithValue("sort", order++);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
