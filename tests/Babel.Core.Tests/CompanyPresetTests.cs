using Babel.Core.Audit;
using Babel.Core.Entitlement;
using Babel.Core.Metering;
using Babel.Core.Presets;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Core.Tests;

/// <summary>
/// <b>ثوابت الشركة — ما يُحسم مرّةً في الإعداد ثم يختفي من شاشات الإدخال.</b>
/// <para>
/// الكتالوج مغلق: مفتاحٌ خارجه يُرفض لأن الشاشة التي تقرؤه لن تكون موجودة. وكل قيمة
/// تُصدَّق بنوعها، وتُعاد الأخطاء كلّها دفعةً. والترقيم تخصيصٌ من عدّادٍ لا اختراعٌ باليد.
/// </para>
/// </summary>
public sealed class CompanyPresetTests
{
    private static readonly TenantId Company = new(Guid.Parse("7e5e7000-1111-4111-8111-7e5e70000001"));
    private static readonly UserId Actor = new(Guid.Parse("7e5e7000-2222-4222-8222-7e5e70000002"));

    [Fact]
    public void مفتاح_خارج_الكتالوج_يُرفض()
    {
        Result<CompanyPresets> refused = CompanyPresets.Validate([new("coffee.default", "latte")]);

        Assert.True(refused.IsFailure);
        Assert.Contains(refused.Errors, error => error.Code == "company_presets.unknown_key");
    }

    [Fact]
    public void كل_أسباب_الرفض_تُرجع_مجتمعة_لا_أوّلها()
    {
        Result<CompanyPresets> refused = CompanyPresets.Validate(
        [
            new("tax.rate", "15"),
            new("tax.classification", "luxury"),
            new("tax.recoverable", "yes"),
            new("number.sales_invoice", "inv-"),
        ]);

        Assert.True(refused.IsFailure);
        Assert.Equal(4, refused.Errors.Count);
        Assert.Contains(refused.Errors, error => error.Code == "company_presets.value_not_a_choice");
        Assert.Equal(3, refused.Errors.Count(error => error.Code == "company_presets.value_malformed"));
    }

    [Fact]
    public void القيمة_الفارغة_تزيل_الثابت_ولا_تضبطه_فارغاً()
    {
        CompanyPresets presets = CompanyPresets.Validate([new("branch.default", "  "), new("tax.rate", "0.15")]).Value;

        Assert.Equal(1, presets.Count);
        Assert.Null(presets["branch.default"]);
        Assert.Equal("0.15", presets["tax.rate"]);
    }

    [Fact]
    public void الرقم_يُركَّب_من_البادئة_والسنة_والتسلسل_بأربع_خانات()
    {
        PresetCatalogue.TryFindSeries("sales_invoice", out DocumentSeries series);

        Assert.Equal("INV-2026-0007", CompanyPresets.Empty.Compose(series, 2026, 7).Number);

        CompanyPresets custom = CompanyPresets.Validate([new("number.sales_invoice", "SB")]).Value;
        Assert.Equal("SB-2026-12345", custom.Compose(series, 2026, 12345).Number);
    }

    [Fact]
    public async Task الاستبدال_يُقرأ_بعده_ويُسجَّل_في_التدقيق()
    {
        (CompanyPresetService service, InMemoryAuditLog audit) = NewService();

        Result<CompanyPresets> replaced = await service.ReplaceAsync(
            new CompanyPresetReplacement(Company, Actor, [new("tax.rate", "0.15"), new("branch.default", "BR-01")]),
            TestContext.Current.CancellationToken);

        Assert.True(replaced.IsSuccess);

        Result<CompanyPresets> read = await service.GetAsync(Company, Actor, TestContext.Current.CancellationToken);
        Assert.Equal("BR-01", read.Value["branch.default"]);
        Assert.Equal("0.15", read.Value["tax.rate"]);

        IReadOnlyList<AuditEntry> entries = await audit.ReadAsync(Company, TestContext.Current.CancellationToken);
        Assert.Contains(entries, entry => entry.Action == "company_presets.replaced");
    }

    [Fact]
    public async Task الاستبدال_الثاني_يُزيل_ما_غاب_عنه()
    {
        (CompanyPresetService service, _) = NewService();

        await service.ReplaceAsync(
            new CompanyPresetReplacement(Company, Actor, [new("tax.rate", "0.15"), new("branch.default", "BR-01")]),
            TestContext.Current.CancellationToken);
        await service.ReplaceAsync(
            new CompanyPresetReplacement(Company, Actor, [new("tax.rate", "0.05")]),
            TestContext.Current.CancellationToken);

        Result<CompanyPresets> read = await service.GetAsync(Company, Actor, TestContext.Current.CancellationToken);
        Assert.Null(read.Value["branch.default"]);
        Assert.Equal("0.05", read.Value["tax.rate"]);
    }

    [Fact]
    public async Task التخصيص_يتقدّم_داخل_السنة_ويبدأ_من_جديد_في_سنة_أخرى()
    {
        (CompanyPresetService service, _) = NewService();

        AllocatedNumber first = (await service.AllocateNumberAsync(Company, Actor, "sales_invoice", new DateOnly(2026, 3, 1), TestContext.Current.CancellationToken)).Value;
        AllocatedNumber second = (await service.AllocateNumberAsync(Company, Actor, "sales_invoice", new DateOnly(2026, 9, 1), TestContext.Current.CancellationToken)).Value;
        AllocatedNumber other = (await service.AllocateNumberAsync(Company, Actor, "supplier_bill", new DateOnly(2026, 9, 1), TestContext.Current.CancellationToken)).Value;
        AllocatedNumber nextYear = (await service.AllocateNumberAsync(Company, Actor, "sales_invoice", new DateOnly(2027, 1, 1), TestContext.Current.CancellationToken)).Value;

        Assert.Equal("INV-2026-0001", first.Number);
        Assert.Equal("INV-2026-0002", second.Number);
        Assert.Equal("BILL-2026-0001", other.Number);
        Assert.Equal("INV-2027-0001", nextYear.Number);
    }

    [Fact]
    public async Task سلسلة_غير_معروفة_تُرفض_ولا_يتقدّم_عدّاد()
    {
        (CompanyPresetService service, _) = NewService();

        Result<AllocatedNumber> refused = await service.AllocateNumberAsync(Company, Actor, "lottery", new DateOnly(2026, 1, 1), TestContext.Current.CancellationToken);

        Assert.True(refused.IsFailure);
        Assert.Contains(refused.Errors, error => error.Code == "company_presets.unknown_series");
    }

    private static (CompanyPresetService Service, InMemoryAuditLog Audit) NewService()
    {
        InMemoryUsageStore usage = new();
        InMemoryAuditLog audit = new();
        TimeProvider clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero));
        InMemoryEntitlementService entitlements = new(audit, clock);
        EntitlementEnforcer enforcer = new(entitlements, usage, clock);

        return (new CompanyPresetService(new InMemoryCompanyPresetStore(), enforcer, audit, clock), audit);
    }
}
