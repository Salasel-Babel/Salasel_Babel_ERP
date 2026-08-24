using Babel.ControlPlane.Entitlement;
using Xunit;

namespace Babel.ControlPlane.Tests;

/// <summary>
/// اختبارات منطق الاستحقاق — بلا قاعدة بيانات، فتُشغَّل في كل بناء.
/// (‏traps.md §10.5: كل بند في القائمة المرجعية يجب أن يصير فحصاً آلياً.)
/// </summary>
public class EntitlementGraphTests
{
    [Fact]
    public void رسم_الاعتماديات_بلا_حلقات() =>
        Assert.Empty(ModuleCatalog.DetectCycles());

    [Fact]
    public void كل_وحدة_في_الكتالوج_تحمل_اسمين()
    {
        foreach (var m in ModuleCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(m.NameAr), $"{m.Code} بلا name_ar");
            Assert.False(string.IsNullOrWhiteSpace(m.NameEn), $"{m.Code} بلا name_en");
        }
    }

    [Fact]
    public void كل_خطة_تحمل_اسمين_وأسعارها_عشرية()
    {
        foreach (var p in PlanCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.NameAr));
            Assert.False(string.IsNullOrWhiteSpace(p.NameEn));
            Assert.IsType<decimal>(p.MonthlyPrice);
            Assert.IsType<decimal>(p.PerUserPrice);
            Assert.True(p.MonthlyPrice >= 0 && p.PerUserPrice >= 0);
        }
    }

    [Fact]
    public void الاعتماديات_المتعدّية_لنقاط_البيع_تشمل_المخزون_والذمم_والأستاذ()
    {
        var deps = ModuleCatalog.TransitiveDependencies("POS");
        Assert.Contains("INV", deps);
        Assert.Contains("AR", deps);
        Assert.Contains("AP", deps);     // عبر INV
        Assert.Contains("CORE", deps);
    }

    [Fact]
    public void المشاريع_تعتمد_على_المخزون() =>
        Assert.Contains("INV", ModuleCatalog.TransitiveDependencies("PRJ"));

    private static Dictionary<string, EntitlementState> All(EntitlementState s) =>
        ModuleCatalog.All.ToDictionary(m => m.Code, _ => s, StringComparer.Ordinal);

    [Fact]
    public void مجموعة_كلها_غير_مستحقّة_متماسكة() =>
        Assert.Empty(EntitlementValidator.Validate(All(EntitlementState.NotEntitled)));

    [Fact]
    public void مجموعة_كلها_مستحقّة_متماسكة() =>
        Assert.Empty(EntitlementValidator.Validate(All(EntitlementState.Entitled)));

    [Fact]
    public void نقاط_البيع_مستحقّة_بلا_مخزون_مرفوضة()
    {
        var set = All(EntitlementState.NotEntitled);
        set["POS"] = EntitlementState.Entitled;
        var v = EntitlementValidator.Validate(set);
        Assert.NotEmpty(v);
        Assert.Contains(v, x => x.ModuleCode == "POS");
    }

    [Fact]
    public void نقاط_البيع_مستحقّة_ومخزون_قراءة_فقط_مرفوضة()
    {
        var set = All(EntitlementState.Entitled);
        set["INV"] = EntitlementState.ReadOnly;
        var v = EntitlementValidator.Validate(set);
        Assert.Contains(v, x => x.ModuleCode == "POS");
        Assert.Contains(v, x => x.ModuleCode == "PRJ");
    }

    [Fact]
    public void وحدة_قراءة_فقط_فوق_اعتمادية_قراءة_فقط_مقبولة()
    {
        var set = All(EntitlementState.ReadOnly);
        Assert.Empty(EntitlementValidator.Validate(set));
    }

    [Fact]
    public void وحدة_غير_مستحقّة_لا_تفرض_شيئاً_على_اعتمادياتها()
    {
        var set = All(EntitlementState.NotEntitled);
        set["CORE"] = EntitlementState.Entitled;
        Assert.Empty(EntitlementValidator.Validate(set));
    }

    [Fact]
    public void وحدة_بلا_حالة_صريحة_مرفوضة()
    {
        var set = All(EntitlementState.Entitled);
        set.Remove("INV");
        Assert.Contains(EntitlementValidator.Validate(set), x => x.ModuleCode == "INV");
    }

    [Fact]
    public void التصحيحات_المقترَحة_تجعل_المجموعة_متماسكة()
    {
        var set = All(EntitlementState.NotEntitled);
        set["POS"] = EntitlementState.Entitled;

        foreach (var fix in EntitlementValidator.SuggestRepairs(set))
            set[fix.ModuleCode] = fix.NewState;

        Assert.Empty(EntitlementValidator.Validate(set));
        Assert.Equal(EntitlementState.Entitled, set["INV"]);
        Assert.Equal(EntitlementState.Entitled, set["CORE"]);
    }

    [Fact]
    public void ترتيب_الحالات_الثلاث_محفوظ()
    {
        Assert.True(EntitlementState.Entitled > EntitlementState.ReadOnly);
        Assert.True(EntitlementState.ReadOnly > EntitlementState.NotEntitled);
    }

    [Fact]
    public void وحدة_واحدة_فقط_لا_تُرحّل_قيوداً()
    {
        var noJournal = ModuleCatalog.All.Where(m => !m.PostsJournal).Select(m => m.Code).ToList();
        Assert.Equal(["REP"], noJournal);
    }

    [Fact]
    public void السند_مطلوب_في_كل_تغيير_استحقاق()
    {
        Assert.Throws<ArgumentException>(() =>
            new ChangeAuthority("a", "  ", "سبب").Validate());
        Assert.Throws<ArgumentException>(() =>
            new ChangeAuthority("", "AUTH", "سبب").Validate());
        new ChangeAuthority("a", "AUTH-1", "سبب").Validate();   // لا يرمي
    }
}
