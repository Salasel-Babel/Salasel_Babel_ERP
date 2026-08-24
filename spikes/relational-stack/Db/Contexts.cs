using BabelRelationalSpike.Support;
using Microsoft.EntityFrameworkCore;

namespace BabelRelationalSpike.Db;

/// <summary>Plain (non-Wolverine) DbContext factory for the persistence proofs and the benchmark.</summary>
public static class Contexts
{
    public static LedgerDbContext Create(string? connectionString = null,
        Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor? interceptor = null)
    {
        var b = new DbContextOptionsBuilder<LedgerDbContext>().UseNpgsql(connectionString ?? Config.App);
        if (interceptor is not null) b.AddInterceptors(interceptor);
        return new LedgerDbContext(b.Options);
    }
}
