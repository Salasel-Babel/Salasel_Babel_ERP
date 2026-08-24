using Babel.Contracts.Posting;
using Babel.Ledger.Posting;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Ledger;

/// <summary>
/// نقطة تركيب الدفتر. الجذر التركيبي يسجّل <see cref="IPostingService"/> ولا يرى
/// <c>LedgerDbContext</c> ولا <c>AccountCode</c> — كلاهما <c>internal</c>.
/// </summary>
public static class LedgerModuleRegistration
{
    /// <summary>يسجّل الدفتر.</summary>
    public static IServiceCollection AddBabelLedger(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IPostingService, PostingService>();
        return services;
    }
}
