using Microsoft.EntityFrameworkCore;

namespace Babel.Core.Persistence;

/// <summary>
/// جداول النواة. <c>internal</c> عمداً: لا وحدة أخرى — ولا حتى الجذر التركيبي —
/// تستطيع حقن سياق وحدة أخرى أو الاستعلام عبر جداولها (القاعدة 5).
/// التسجيل يتم عبر <see cref="CoreModuleRegistration"/> وحدها.
/// </summary>
internal sealed class CoreDbContext(DbContextOptions<CoreDbContext> options) : DbContext(options)
{
    public DbSet<TenantRow> Tenants => Set<TenantRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("core");
        modelBuilder.Entity<TenantRow>(entity =>
        {
            entity.ToTable("tenant");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.NameAr).HasMaxLength(200).IsRequired();
            entity.Property(row => row.NameEn).HasMaxLength(200).IsRequired();
        });
    }
}
