using Microsoft.EntityFrameworkCore;

namespace Babel.Inventory.Persistence;

/// <summary>
/// جداول المخزون. <c>internal</c>: كل وحدة تملك جداولها (القاعدة 5)، ولا مفتاح خارجي
/// إلى وحدة أخرى ولا إلى الدفتر، ولا عمود يحمل رقم حساب.
/// <para>
/// وكل عمود قيمة <c>numeric(19,4)</c> صراحةً (‏فخ-17)، وكل عمود كمية
/// <c>numeric(19,6)</c>: الكمية ليست مبلغاً — كيلوغرامات ولترات وأمتار تُكسَر إلى ما
/// دون الهللة، ومقياسٌ مالي عليها يُنتج تقريباً صامتاً في التكلفة.
/// </para>
/// </summary>
internal sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    private const string Money = "numeric(19,4)";
    private const string Quantity = "numeric(19,6)";
    private const string UnitCost = "numeric(19,6)";

    public DbSet<StockMovementRow> Movements => Set<StockMovementRow>();

    public DbSet<ItemBalanceRow> Balances => Set<ItemBalanceRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("inventory");

        modelBuilder.Entity<StockMovementRow>(entity =>
        {
            entity.ToTable("stock_movement");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.SourceModule).HasMaxLength(32).IsRequired();
            entity.Property(row => row.DocumentType).HasMaxLength(64).IsRequired();
            entity.Property(row => row.DocumentId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.TriggerCode).HasMaxLength(32).IsRequired();
            entity.Property(row => row.EventCode).HasMaxLength(128).IsRequired();
            entity.Property(row => row.ItemId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.WarehouseId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.ItemGroup).HasMaxLength(64).IsRequired();
            entity.Property(row => row.Direction).HasMaxLength(8).IsRequired();
            entity.Property(row => row.Method).HasMaxLength(32).IsRequired();
            entity.Property(row => row.ActorId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.AgainstKey).HasMaxLength(128).IsRequired().HasDefaultValue(string.Empty);
            entity.Property(row => row.Quantity).HasColumnType(Quantity);
            entity.Property(row => row.QuantityAfter).HasColumnType(Quantity);
            entity.Property(row => row.UnitCost).HasColumnType(UnitCost);
            entity.Property(row => row.ValueAmount).HasColumnType(Money);
            entity.Property(row => row.ValueAfter).HasColumnType(Money);

            // ‏**هوية الحركة = هوية الترحيل.** ستّة حقول لا خمسة: رمز الحدث فيها
            // (‏فخ-45)، والجيل فيها كي تُعاد الحركة بعد عكسٍ مشروع.
            entity.HasIndex(row => new
            {
                row.TenantId,
                row.SourceModule,
                row.DocumentType,
                row.DocumentId,
                row.TriggerCode,
                row.Generation,
                row.EventCode,
            }).IsUnique().HasDatabaseName("uq_inventory_movement_identity");

            entity.HasIndex(row => new { row.TenantId, row.ItemId, row.WarehouseId })
                  .HasDatabaseName("ix_inventory_movement_item");

            // ‏فهرس على «ما تَرُدّ عليه» — جزئيّ لأن الغالبية العظمى من الحركات لا تَرُدّ
            // على شيء، والفراغ لا يُبحث عنه.
            entity.HasIndex(row => new { row.TenantId, row.AgainstKey })
                  .HasDatabaseName("ix_inventory_movement_against")
                  .HasFilter("\"AgainstKey\" <> ''");

            // ‏**رمز الحدث إلزامي وغير فارغ** — لا قيمة افتراضية فارغة تُلغي توسيع
            // المفتاح وهي تبدو جزءاً منه (‏فخ-47).
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_inventory_movement_event_code",
                """ length(btrim("EventCode")) > 0 """));

            // والكمية والقيمة موجبتان دائماً: الاتجاه عمودٌ مستقلّ. حركةٌ بكمية سالبة
            // في اتجاه «وارد» تعني اتجاهين لواقعة واحدة، ولا يُقرأ أيّهما هو الصحيح.
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_inventory_movement_quantity_positive",
                """ "Quantity" > 0 """));
        });

        modelBuilder.Entity<ItemBalanceRow>(entity =>
        {
            entity.ToTable("item_balance");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.ItemId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.WarehouseId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.Quantity).HasColumnType(Quantity);
            entity.Property(row => row.ValueAmount).HasColumnType(Money);
            entity.Property(row => row.UnitCost).HasColumnType(UnitCost);
            entity.HasIndex(row => new { row.TenantId, row.ItemId, row.WarehouseId })
                  .IsUnique().HasDatabaseName("uq_inventory_item_balance");
        });
    }
}
