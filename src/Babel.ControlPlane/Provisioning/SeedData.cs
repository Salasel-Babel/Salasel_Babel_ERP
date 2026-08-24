using Babel.ControlPlane.Support;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Provisioning;

/// <summary>حساب في دليل الحسابات الابتدائي المبذور لكل مستأجر جديد.</summary>
/// <param name="Code">رقم الحساب.</param>
/// <param name="NameAr">اسم الحساب بالعربية — إلزامي.</param>
/// <param name="NameEn">اسم الحساب بالإنجليزية — إلزامي.</param>
/// <param name="Type">نوع الحساب (‏أصل، التزام، حقوق ملكية، إيراد، مصروف).</param>
/// <param name="Subledger">الوحدة صاحبة الأستاذ المساعد؛ <c>null</c> لحساب تجميعي لا يخصّ وحدة.</param>
/// <param name="Postable">هل يُرحَّل عليه مباشرةً؟ حسابات العناوين لا تقبل الترحيل.</param>
public sealed record SeedAccount(
    string Code, string NameAr, string NameEn, string Type, string? Subledger, bool Postable = true);

/// <summary>دور افتراضي يُبذَر مع المستأجر الجديد.</summary>
/// <param name="Code">رمز الدور.</param>
/// <param name="NameAr">اسم الدور بالعربية — إلزامي.</param>
/// <param name="NameEn">اسم الدور بالإنجليزية — إلزامي.</param>
/// <param name="IsAdmin">هل هو دور المدير الذي يُسنَد إلى أول مستخدم؟</param>
/// <param name="Sort">ترتيب العرض — وهو أيضاً ترتيب الإدراج الكلّي الثابت.</param>
public sealed record SeedRole(string Code, string NameAr, string NameEn, bool IsAdmin, int Sort);

/// <summary>
/// بذور المستأجر الجديد: دليل حسابات ابتدائي، وأدوار افتراضية.
///
/// <para><b>⚠️ غير مُتحقَّق منه:</b> هذا الدليل <b>ليس</b> دليلاً محاسبياً
/// سعودياً رسمياً ولا مُصادَقاً عليه من أي جهة. هو هيكل عملي للبدء، يُستبدل
/// بدليل العميل. لا مصدر تنظيمي سعودي كان متاحاً وقت كتابته
/// (‏<c>docs/evidence/verification-debt.md</c> §1).</para>
///
/// <para>كل الإدراجات هنا <b>مرتَّبة ترتيباً كلّياً ثابتاً</b> قبل الإصدار —
/// فخ-10: نفس العبارة بنفس البيانات وبترتيب مختلف قِيست عند فارق ~11,000×
/// و22–35 حالة جمود.</para>
/// </summary>
public static class SeedData
{
    /// <summary>دليل الحسابات الابتدائي، مرتّباً ترتيباً كلّياً ثابتاً برقم الحساب.</summary>
    public static readonly IReadOnlyList<SeedAccount> ChartOfAccounts =
    [
        new("1000", "الأصول", "Assets", "Asset", null, Postable: false),
        new("1100", "النقد وما في حكمه", "Cash and cash equivalents", "Asset", "CORE"),
        new("1110", "الصندوق", "Cash on hand", "Asset", "POS"),
        new("1200", "الذمم المدينة", "Accounts receivable", "Asset", "AR"),
        new("1300", "المخزون", "Inventory", "Asset", "INV"),
        new("1400", "أعمال تحت التنفيذ", "Work in progress", "Asset", "PRJ"),
        new("1500", "الأصول الثابتة", "Fixed assets", "Asset", "FA"),
        new("1590", "مجمّع الإهلاك", "Accumulated depreciation", "Asset", "FA"),
        new("1600", "ضريبة القيمة المضافة — مدخلات", "VAT input", "Asset", "CORE"),
        new("2000", "الالتزامات", "Liabilities", "Liability", null, Postable: false),
        new("2100", "الذمم الدائنة", "Accounts payable", "Liability", "AP"),
        new("2200", "رواتب مستحقة", "Accrued payroll", "Liability", "PAY"),
        new("2300", "ضريبة القيمة المضافة — مخرجات", "VAT output", "Liability", "CORE"),
        new("3000", "حقوق الملكية", "Equity", "Equity", null, Postable: false),
        new("3100", "رأس المال", "Share capital", "Equity", "CORE"),
        new("3900", "الأرباح المُبقاة", "Retained earnings", "Equity", "CORE"),
        new("4000", "الإيرادات", "Revenue", "Revenue", null, Postable: false),
        new("4100", "إيرادات المبيعات", "Sales revenue", "Revenue", "AR"),
        new("4200", "إيرادات نقاط البيع", "Point-of-sale revenue", "Revenue", "POS"),
        new("4300", "إيرادات المشاريع", "Project revenue", "Revenue", "PRJ"),
        new("5000", "المصروفات", "Expenses", "Expense", null, Postable: false),
        new("5100", "تكلفة البضاعة المباعة", "Cost of goods sold", "Expense", "INV"),
        new("5200", "مصروف الرواتب", "Payroll expense", "Expense", "PAY"),
        new("5300", "مصروف الإهلاك", "Depreciation expense", "Expense", "FA"),
        new("5900", "مصروفات عمومية وإدارية", "General and administrative", "Expense", "CORE"),
    ];

    /// <summary>الأدوار الافتراضية، مرتّبةً ترتيباً كلّياً ثابتاً.</summary>
    public static readonly IReadOnlyList<SeedRole> Roles =
    [
        new("ACCOUNTANT", "محاسب", "Accountant", false, 20),
        new("AUDITOR", "مدقّق", "Auditor", false, 40),
        new("CASHIER", "أمين صندوق", "Cashier", false, 30),
        new("OWNER", "مالك الحساب", "Account owner", true, 10),
        new("VIEWER", "مطّلع", "Viewer", false, 50),
    ];

    /// <summary>
    /// يزرع دليل الحسابات في <b>عبارة واحدة</b> بصفوف مرتّبة بـ<c>account_code</c>
    /// تصاعدياً. مُحكَم: <c>ON CONFLICT DO UPDATE</c> بقيم متطابقة.
    /// </summary>
    /// <summary>
    /// يبذر دليل الحسابات. مُحكَم بـ<c>ON CONFLICT DO UPDATE</c> بقيم متطابقة لا
    /// <c>DO NOTHING</c>: الثاني يجعل إعادة التزويد تُصيب صفر صفوف فيرمي تأكيد
    /// عدد الصفوف — أي يُفشِل بالضبط المسار الذي وُجد الإحكام من أجله.
    /// </summary>
    /// <param name="c">اتصال مفتوح بقاعدة المستأجر.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>عدد الصفوف المتأثرة.</returns>
    public static async Task<int> SeedChartOfAccountsAsync(NpgsqlConnection c,
        CancellationToken ct = default)
    {
        var rows = ChartOfAccounts.OrderBy(a => a.Code, StringComparer.Ordinal).ToList();
        var values = string.Join(",\n            ",
            rows.Select((_, i) => $"(@c{i}, @ar{i}, @en{i}, @t{i}, @s{i}, @p{i})"));

        return await Db.WriteAsync(c, $"""
            insert into ledger.account
                (account_code, name_ar, name_en, account_type, subledger, is_postable)
            values {values}
            on conflict (account_code) do update
               set name_ar = excluded.name_ar,
                   name_en = excluded.name_en,
                   account_type = excluded.account_type,
                   subledger = excluded.subledger,
                   is_postable = excluded.is_postable
            """, rows.Count, p =>
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    p.AddWithValue($"c{i}", rows[i].Code);
                    p.AddWithValue($"ar{i}", rows[i].NameAr);
                    p.AddWithValue($"en{i}", rows[i].NameEn);
                    p.AddWithValue($"t{i}", rows[i].Type);
                    p.Add(Db.P($"s{i}", rows[i].Subledger, NpgsqlDbType.Text));
                    p.AddWithValue($"p{i}", rows[i].Postable);
                }
            }, null, ct);
    }

    /// <summary>يبذر الأدوار الافتراضية. مُحكَم وقابل لإعادة التشغيل بلا أثر إضافي.</summary>
    /// <param name="c">اتصال مفتوح بقاعدة المستأجر.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>عدد الصفوف المتأثرة.</returns>
    public static async Task<int> SeedRolesAsync(NpgsqlConnection c, CancellationToken ct = default)
    {
        var rows = Roles.OrderBy(r => r.Code, StringComparer.Ordinal).ToList();
        var values = string.Join(",\n            ",
            rows.Select((_, i) => $"(@c{i}, @ar{i}, @en{i}, @a{i}, @o{i})"));

        return await Db.WriteAsync(c, $"""
            insert into app.role (role_code, name_ar, name_en, is_admin, sort_order)
            values {values}
            on conflict (role_code) do update
               set name_ar = excluded.name_ar,
                   name_en = excluded.name_en,
                   is_admin = excluded.is_admin,
                   sort_order = excluded.sort_order
            """, rows.Count, p =>
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    p.AddWithValue($"c{i}", rows[i].Code);
                    p.AddWithValue($"ar{i}", rows[i].NameAr);
                    p.AddWithValue($"en{i}", rows[i].NameEn);
                    p.AddWithValue($"a{i}", rows[i].IsAdmin);
                    p.AddWithValue($"o{i}", rows[i].Sort);
                }
            }, null, ct);
    }

    /// <summary>
    /// يفتح الفترات المالية للسنة الجارية. وجودها شرط مسبق: صفوف الأرصدة
    /// تُكتب بـ<c>ON CONFLICT</c>، لكن القيد نفسه يُحيل إلى الفترة بمفتاح أجنبي
    /// — والفترة الغائبة يجب أن تُفشِل الترحيل بصوت عالٍ لا أن تُبتلع (فخ-09).
    /// </summary>
    /// <summary>
    /// يبذر فترات السنة المالية الاثنتي عشرة. <b>لا تُدهَس حالة الفترة ولا تاريخ
    /// إقفالها عند إعادة التزويد</b> — إعادة فتح فترة مُقفلة بإعادة تشغيل بذرة
    /// عطل مالي صامت.
    /// </summary>
    /// <param name="c">اتصال مفتوح بقاعدة المستأجر.</param>
    /// <param name="year">السنة المالية.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>عدد الصفوف المتأثرة.</returns>
    public static async Task<int> SeedPeriodsAsync(NpgsqlConnection c, int year,
        CancellationToken ct = default)
    {
        var rows = Enumerable.Range(1, 12)
            .Select(m => (Code: $"{year:D4}-{m:D2}",
                          Start: new DateOnly(year, m, 1),
                          End: new DateOnly(year, m, DateTime.DaysInMonth(year, m))))
            .OrderBy(x => x.Code, StringComparer.Ordinal).ToList();

        var values = string.Join(",\n            ",
            rows.Select((_, i) => $"(@c{i}, @s{i}, @e{i}, 'Open', null)"));

        return await Db.WriteAsync(c, $"""
            insert into ledger.period (period_code, starts_on, ends_on, state, closed_at)
            values {values}
            -- DO UPDATE لا DO NOTHING: عدد الصفوف يجب أن يكون ثابتاً في كل
            -- إعادة تشغيل حتى يبقى تأكيد العدد ذا معنى. والحالة وتاريخ الإقفال
            -- لا يُلمسان — إعادة التزويد لا تُعيد فتح فترة مُقفَلة.
            on conflict (period_code) do update
               set starts_on = excluded.starts_on,
                   ends_on   = excluded.ends_on
            """, rows.Count, p =>
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    p.AddWithValue($"c{i}", rows[i].Code);
                    p.Add(Db.P($"s{i}", rows[i].Start, NpgsqlDbType.Date));
                    p.Add(Db.P($"e{i}", rows[i].End, NpgsqlDbType.Date));
                }
            }, null, ct);
    }
}
