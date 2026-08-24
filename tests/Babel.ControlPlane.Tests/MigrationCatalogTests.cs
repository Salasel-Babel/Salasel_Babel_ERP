using Babel.ControlPlane.Migration;
using Xunit;

namespace Babel.ControlPlane.Tests;

public class MigrationCatalogTests
{
    [Fact]
    public void أرقام_الترحيلات_متتابعة_بلا_فجوة_وبلا_تكرار()
    {
        var versions = TenantSchema.All.Select(m => m.Version).ToList();
        Assert.Equal(versions.OrderBy(v => v), versions);
        Assert.Equal(versions.Distinct().Count(), versions.Count);
        Assert.Equal(Enumerable.Range(1, versions.Count), versions);
    }

    [Fact]
    public void كل_ترحيلة_تحمل_اسمين()
    {
        foreach (var m in TenantSchema.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(m.NameAr));
            Assert.False(string.IsNullOrWhiteSpace(m.NameEn));
        }
    }

    [Fact]
    public void التوسيع_يسبق_الانكماش_ويفصلهما_إصدار()
    {
        Assert.True(TenantSchema.ExpandVersion < TenantSchema.ContractVersion);
        Assert.Equal(TenantSchema.ExpandVersion + 1, TenantSchema.ContractVersion);
    }

    [Fact]
    public void التوسيع_يضيف_العمود_ولا_يحذف_شيئاً()
    {
        var expand = TenantSchema.All.First(m => m.Version == TenantSchema.ExpandVersion);
        Assert.Contains("add column", expand.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("drop column", expand.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void الانكماش_وحده_يحذف_العمود_القديم()
    {
        var contract = TenantSchema.All.First(m => m.Version == TenantSchema.ContractVersion);
        Assert.Contains("drop column", contract.Sql, StringComparison.OrdinalIgnoreCase);
        foreach (var m in TenantSchema.All.Where(x => x.Version < TenantSchema.ContractVersion))
            Assert.DoesNotContain("drop column", m.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void كل_ترحيلة_تُغيّر_البيانات_تُتبَع_بـVACUUM_ANALYZE()
    {
        // فخ-33: VACUUM ANALYZE بند إلزامي في نهاية كل استيراد أو ترحيل.
        foreach (var m in TenantSchema.All)
        {
            var touchesData = m.Sql.Contains("update ", StringComparison.OrdinalIgnoreCase)
                              || m.Sql.Contains("create index", StringComparison.OrdinalIgnoreCase)
                              || m.Version == TenantSchema.BaselineVersion;
            if (m.Version == TenantSchema.ExpandVersion) continue;   // مرحلة عبور قصيرة
            if (touchesData)
                Assert.True(m.VacuumAfter, $"الترحيلة {m.Version} تمسّ البيانات بلا VACUUM ANALYZE");
        }
    }

    [Fact]
    public void لا_عبارة_UPDATE_FROM_على_جدول_متنازَع_عليه()
    {
        // فخ-11: الصيغة لا تضمن ترتيب الأقفال.
        foreach (var m in TenantSchema.All)
            Assert.DoesNotContain("update ledger.account_balance",
                m.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void لا_SEQUENCE_ولا_SERIAL_في_مخطط_المستأجر()
    {
        // فخ-12: التسلسل يُهدر أرقاماً عند التراجع، والقيد لا يحتمل فجوة.
        foreach (var m in TenantSchema.All)
        {
            Assert.DoesNotContain("serial", m.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("nextval", m.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("create sequence", m.Sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void كل_مبلغ_في_المخطط_عشري_بمقياس_أربعة()
    {
        var all = string.Join("\n", TenantSchema.All.Select(m => m.Sql))
                  + Babel.ControlPlane.Registry.ControlSchema.Ddl;
        foreach (var forbidden in new[] { "float", "double precision", "real ", "money" })
            Assert.DoesNotContain(forbidden, all, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("numeric(19,4)", all, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void كل_جدول_بيانات_أساسية_يحمل_name_ar_و_name_en()
    {
        var ddl = Babel.ControlPlane.Registry.ControlSchema.Ddl;
        foreach (var table in new[] { "control.module", "control.plan", "control.tenant" })
            Assert.Contains(table, ddl, StringComparison.Ordinal);

        var arCount = System.Text.RegularExpressions.Regex.Count(ddl, @"name_ar\s+text\s+not null");
        var enCount = System.Text.RegularExpressions.Regex.Count(ddl, @"name_en\s+text\s+not null");
        Assert.Equal(arCount, enCount);
        Assert.True(arCount >= 6, $"عدد أزواج الاسمين في مخطط التحكّم = {arCount}");
    }
}
