using Babel.Core.Persistence;
using Babel.Core.Presets;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Core.Tests;

/// <summary>
/// <b>ثوابت الشركة وعدّاداتها تبقى بعد أن تموت العملية، والعدّاد لا يعطي رقماً مرّتين.</b>
/// <para>
/// المُثبَت هنا ليس «الصفّ يُكتب ويُقرأ» فحسب: بل أن <b>طلبين متزامنين يخرجان برقمين
/// مختلفين</b> لأن الصفّ يُقفل عند القراءة (ADR-0008)، وأن دور التطبيق <b>لا يستطيع حذف
/// عدّاد</b> ولو أراد — الصلاحية مسحوبة في القاعدة لا في الشيفرة.
/// </para>
/// </summary>
public sealed class CompanyPresetsSurviveTheProcessTests
{
    [Fact]
    public async Task الثوابت_تُقرأ_من_مخزن_ثانٍ_لم_يشهد_كتابتها()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId company = new(CoreTestEnvironment.NewCompany());

        PostgresCompanyPresetStore writer = NewStore();
        await writer.ReplaceAsync(
            company,
            CompanyPresets.Validate([new("tax.rate", "0.15"), new("warehouse.default", "WH-MAIN")]).Value,
            TestContext.Current.CancellationToken);

        PostgresCompanyPresetStore reader = NewStore();
        CompanyPresets read = await reader.ReadAsync(company, TestContext.Current.CancellationToken);

        Assert.Equal("0.15", read["tax.rate"]);
        Assert.Equal("WH-MAIN", read["warehouse.default"]);

        // الاستبدال الثاني يُزيل ما غاب عنه — المورد يُستبدل كلّه.
        await writer.ReplaceAsync(company, CompanyPresets.Validate([new("tax.rate", "0.05")]).Value, TestContext.Current.CancellationToken);
        CompanyPresets again = await reader.ReadAsync(company, TestContext.Current.CancellationToken);

        Assert.Equal("0.05", again["tax.rate"]);
        Assert.Null(again["warehouse.default"]);
    }

    [Fact]
    public async Task طلبات_متزامنة_تخرج_بأرقام_مختلفة_كلّها()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId company = new(CoreTestEnvironment.NewCompany());

        PostgresCompanyPresetStore store = NewStore();
        const int requests = 12;

        int[] allocated = await Task.WhenAll(Enumerable.Range(0, requests)
            .Select(_ => store.AllocateAsync(company, "sales_invoice", 2026, TestContext.Current.CancellationToken).AsTask()));

        Assert.Equal(Enumerable.Range(1, requests), allocated.Order());

        int next = await store.AllocateAsync(company, "sales_invoice", 2026, TestContext.Current.CancellationToken);
        Assert.Equal(requests + 1, next);

        CoreTestEnvironment.Note(requests + " طلباً متزامناً → " + string.Join(",", allocated.Order()));
    }

    [Fact]
    public async Task دور_التطبيق_لا_يستطيع_حذف_عدّاد()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId company = new(CoreTestEnvironment.NewCompany());

        PostgresCompanyPresetStore store = NewStore();
        await store.AllocateAsync(company, "payroll_run", 2026, TestContext.Current.CancellationToken);

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() =>
            CoreTestEnvironment.ApplicationAsync(
                "delete from core.document_counter where company_id = '" + company.Value + "'"));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    private static PostgresCompanyPresetStore NewStore() => new(CoreTestEnvironment.Options);
}
