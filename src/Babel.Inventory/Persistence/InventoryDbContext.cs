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

    public DbSet<ItemRow> Items => Set<ItemRow>();

    public DbSet<ItemTranslationRow> ItemNames => Set<ItemTranslationRow>();

    public DbSet<ItemUnitRow> ItemUnits => Set<ItemUnitRow>();

    public DbSet<StockDocumentRow> Documents => Set<StockDocumentRow>();

    public DbSet<InventoryPostingRow> Postings => Set<InventoryPostingRow>();

    public DbSet<WarehouseRow> Warehouses => Set<WarehouseRow>();

    public DbSet<LocationRow> Locations => Set<LocationRow>();

    public DbSet<WarehouseTranslationRow> WarehouseNames => Set<WarehouseTranslationRow>();

    public DbSet<LocationTranslationRow> LocationNames => Set<LocationTranslationRow>();

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
            entity.Property(row => row.LocationId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.BaseUnit).HasMaxLength(32).IsRequired();
            entity.Property(row => row.EnteredUnit).HasMaxLength(32).IsRequired();
            entity.Property(row => row.EnteredMagnitude).HasColumnType(Quantity);
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

            entity.HasIndex(row => new { row.TenantId, row.ItemId, row.WarehouseId, row.LocationId })
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
            entity.Property(row => row.LocationId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.BaseUnit).HasMaxLength(32).IsRequired();
            entity.Property(row => row.Quantity).HasColumnType(Quantity);
            entity.Property(row => row.ValueAmount).HasColumnType(Money);
            entity.Property(row => row.UnitCost).HasColumnType(UnitCost);

            // ‏**المفتاح أربعة أبعاد لا ثلاثة**: المنشأة والصنف والمستودع والموقع.
            entity.HasIndex(row => new { row.TenantId, row.ItemId, row.WarehouseId, row.LocationId })
                  .IsUnique().HasDatabaseName("uq_inventory_item_balance");
        });

        modelBuilder.Entity<ItemRow>(entity =>
        {
            entity.ToTable("item");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Code).HasMaxLength(64).IsRequired();
            entity.Property(row => row.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(row => row.ItemGroup).HasMaxLength(64).IsRequired();
            entity.Property(row => row.BaseUnit).HasMaxLength(32).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.Code })
                  .IsUnique().HasDatabaseName("uq_inventory_item_code");
        });

        modelBuilder.Entity<ItemTranslationRow>(entity =>
        {
            entity.ToTable("item_name_translation");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.ItemCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.Locale).HasMaxLength(16).IsRequired();
            entity.Property(row => row.Text).HasMaxLength(256).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.ItemCode, row.Locale })
                  .IsUnique().HasDatabaseName("uq_inventory_item_name_translation");
        });

        modelBuilder.Entity<ItemUnitRow>(entity =>
        {
            entity.ToTable("item_unit");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.ItemCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.UnitCode).HasMaxLength(32).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.ItemCode, row.UnitCode })
                  .IsUnique().HasDatabaseName("uq_inventory_item_unit");

            // معاملٌ بمقامٍ صفر ليس معاملاً، وبسطٌ غير موجب يقلب اتجاه الكمية بصمت.
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_inventory_item_unit_ratio_positive",
                """ "Numerator" > 0 and "Denominator" > 0 """));
        });

        modelBuilder.Entity<StockDocumentRow>(entity =>
        {
            entity.ToTable("stock_document");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.Direction).HasMaxLength(8).IsRequired();
            entity.Property(row => row.ItemCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.WarehouseId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.LocationId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.ItemGroup).HasMaxLength(64).IsRequired();
            entity.Property(row => row.UnitCode).HasMaxLength(32).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.Magnitude).HasColumnType(Quantity);
            entity.Property(row => row.CostAmount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number })
                  .IsUnique().HasDatabaseName("uq_inventory_stock_document_number");

            entity.ToTable(table => table.HasCheckConstraint(
                "ck_inventory_stock_document_magnitude_positive",
                """ "Magnitude" > 0 """));
        });

        // ── كتالوج المستودعات والمواقع ───────────────────────────────────────
        // ‏**ولا مفتاح خارجي منهما ولا إليهما.** عمودا `WarehouseId` و`LocationId` في
        // الحركة والرصيد نصّان حرّان مُلئا قبل وجود هذا الكتالوج، ومفتاحٌ خارجي
        // يُصادق الجدول كلّه لحظة إنشائه فيُحوّل خطأً إملائياً تاريخياً إلى هجرةٍ
        // ساقطة لا شيء يُصلحها على دفترٍ يُضاف إليه فقط. الوجود يُفرَض عند إنشاء
        // المسوّدة، لا في القاعدة.
        modelBuilder.Entity<WarehouseRow>(entity =>
        {
            entity.ToTable("warehouse");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Code).HasMaxLength(64).IsRequired();
            entity.Property(row => row.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(row => row.Qualifier).HasMaxLength(64).IsRequired().HasDefaultValue(string.Empty);
            entity.Property(row => row.Origin).HasMaxLength(16).IsRequired();
            entity.Property(row => row.IsActive).HasDefaultValue(true);
            entity.HasIndex(row => new { row.TenantId, row.Code })
                  .IsUnique().HasDatabaseName("uq_inventory_warehouse_code");
        });

        modelBuilder.Entity<LocationRow>(entity =>
        {
            entity.ToTable("location");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.WarehouseCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.Code).HasMaxLength(64).IsRequired();
            entity.Property(row => row.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(row => row.Origin).HasMaxLength(16).IsRequired();
            entity.Property(row => row.IsActive).HasDefaultValue(true);

            // **الزوج هو المفتاح** — رمز موقعٍ واحد في مستودعين موقعان لا موقع.
            entity.HasIndex(row => new { row.TenantId, row.WarehouseCode, row.Code })
                  .IsUnique().HasDatabaseName("uq_inventory_location");
        });

        modelBuilder.Entity<WarehouseTranslationRow>(entity =>
        {
            entity.ToTable("warehouse_name_translation");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.WarehouseCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.LanguageTag).HasMaxLength(35).IsRequired();
            entity.Property(row => row.Text).HasMaxLength(256).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.WarehouseCode, row.LanguageTag })
                  .IsUnique().HasDatabaseName("uq_inventory_warehouse_name_translation");
        });

        modelBuilder.Entity<LocationTranslationRow>(entity =>
        {
            entity.ToTable("location_name_translation");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.WarehouseCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.LocationCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.LanguageTag).HasMaxLength(35).IsRequired();
            entity.Property(row => row.Text).HasMaxLength(256).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.WarehouseCode, row.LocationCode, row.LanguageTag })
                  .IsUnique().HasDatabaseName("uq_inventory_location_name_translation");
        });

        modelBuilder.Entity<InventoryPostingRow>(entity =>
        {
            entity.ToTable("document_posting");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.DocumentType).HasMaxLength(64).IsRequired();
            entity.Property(row => row.DocumentId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.TriggerCode).HasMaxLength(32).IsRequired();
            entity.Property(row => row.EventCode).HasMaxLength(128).IsRequired();
            entity.Property(row => row.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.Property(row => row.PartyId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.FailureCode).HasMaxLength(128).IsRequired();
            entity.Property(row => row.FailureMessageAr).HasMaxLength(1000).IsRequired();
            entity.Property(row => row.FailureMessageEn).HasMaxLength(1000).IsRequired();

            // هوية الإحكام السداسية — ورمز الحدث فيها (‏ADR-0016 · فخ-45).
            entity.HasIndex(row => new
            {
                row.TenantId,
                row.DocumentType,
                row.DocumentId,
                row.TriggerCode,
                row.Generation,
                row.EventCode,
            }).IsUnique().HasDatabaseName("uq_inventory_document_posting_identity");
        });
    }
}
