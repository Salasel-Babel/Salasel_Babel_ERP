using Microsoft.EntityFrameworkCore;

namespace Babel.Sales.Persistence;

/// <summary>
/// جداول المبيعات. <c>internal</c>: كل وحدة تملك جداولها، والقراءة العابرة عبر واجهات
/// معلنة لا عبر <c>JOIN</c> مباشر (وثيقة المعمارية §13 · Rule05).
/// <para>
/// ولاحظ ما ليس هنا: لا مفتاح خارجي إلى <c>ledger.account</c>، ولا كيان من وحدة أخرى.
/// الفاتورة تحمل معرّف عميل نصياً وتنشر حدثاً؛ الربط المحاسبي يقرّره الدفتر.
/// </para>
/// </summary>
internal sealed class SalesDbContext(DbContextOptions<SalesDbContext> options) : DbContext(options)
{
    public DbSet<CustomerRow> Customers => Set<CustomerRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("sales");
        modelBuilder.Entity<CustomerRow>(entity =>
        {
            entity.ToTable("customer");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.NameAr).HasMaxLength(200).IsRequired();
            entity.Property(row => row.NameEn).HasMaxLength(200).IsRequired();
            entity.Property(row => row.CreditLimit).HasColumnType("numeric(19,4)");
        });
    }
}
