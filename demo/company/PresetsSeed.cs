using Babel.Core;
using Babel.Core.Presets;
using Babel.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace BabelDemoCompany;

/// <summary>
/// بذرُ ثوابت الشركة التجريبية (ADR-0095): ما يُحسم مرّةً فيختفي من شاشات الإدخال.
/// <para>
/// القيم هي ما تستعمله بقيّة البذور نفسها — الفرع <c>BR-01</c>، والصندوق والبنك،
/// والمستودع الرئيسي ورفّه الأول — فلا تناقض بين ما يُرحَّل وما تفترضه الشاشة.
/// </para>
/// </summary>
internal static class PresetsSeed
{
    public static async Task RunAsync(Settings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Say.Step("بذر ثوابت الشركة: الفرع والضريبة والخزينة والمستودع / seeding the company presets");

        ServiceCollection services = new();
        services.AddBabelCore(options =>
        {
            options.AppConnectionString = settings.Core.AppConnectionString;
            options.OwnerConnectionString = settings.Core.OwnerConnectionString;
            options.AppRole = settings.Core.AppRole;
        });

        await using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        CompanyPresetService presets = scope.ServiceProvider.GetRequiredService<CompanyPresetService>();

        KeyValuePair<string, string>[] values =
        [
            new("branch.default", Company.Branch),
            new("tax.classification", "standard"),
            new("tax.rate", "0.15"),
            new("tax.recoverable", "true"),
            new("expense.category", "office"),
            new("settlement.method", "cash"),
            new("treasury.cash", Company.Cash),
            new("treasury.bank", Company.Bank),
            new("warehouse.default", "WH-MAIN"),
            new("location.default", "LOC-A1"),
            new("hr.class", "SA"),
            new("hr.settlement.method", "bank"),
            new("hr.treasury", Company.Bank),
        ];

        Result<CompanyPresets> replaced = await presets
            .ReplaceAsync(new CompanyPresetReplacement(new TenantId(settings.Company), Seed.Actor, values), cancellationToken)
            .ConfigureAwait(false);

        if (replaced.IsFailure)
        {
            throw new InvalidOperationException(
                "رُفض بذر الثوابت: " + string.Join(" · ", replaced.Errors.Select(static e => e.Code + " — " + e.MessageAr)));
        }

        Say.Detail(replaced.Value.Count + " ثابتاً مضبوطاً / presets set");
    }
}
