using Microsoft.EntityFrameworkCore;

namespace Babel.Purchasing.Persistence;

/// <summary>
/// جداول المشتريات. <c>internal</c>: كل وحدة تملك جداولها (Rule05)، ولا مفتاح خارجي
/// إلى وحدة أخرى ولا إلى الدفتر، ولا عمود يحمل رقم حساب.
/// <para>وكل عمود مالي <c>numeric(19,4)</c> صراحةً (فخ-17).</para>
/// </summary>
internal sealed class PurchasingDbContext(DbContextOptions<PurchasingDbContext> options) : DbContext(options)
{
    private const string Money = "numeric(19,4)";

    public DbSet<SupplierRow> Suppliers => Set<SupplierRow>();

    public DbSet<PurchaseRequestRow> Requests => Set<PurchaseRequestRow>();

    public DbSet<PurchaseOrderRow> Orders => Set<PurchaseOrderRow>();

    public DbSet<GoodsReceiptRow> Receipts => Set<GoodsReceiptRow>();

    public DbSet<SupplierBillRow> Bills => Set<SupplierBillRow>();

    public DbSet<DebitNoteRow> DebitNotes => Set<DebitNoteRow>();

    public DbSet<SupplierPaymentRow> Payments => Set<SupplierPaymentRow>();

    public DbSet<LandedCostRow> LandedCosts => Set<LandedCostRow>();

    public DbSet<PurchaseLineRow> Lines => Set<PurchaseLineRow>();

    public DbSet<PayableAllocationRow> Allocations => Set<PayableAllocationRow>();

    public DbSet<DocumentPostingRow> Postings => Set<DocumentPostingRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("purchasing");

        modelBuilder.Entity<SupplierRow>(entity =>
        {
            entity.ToTable("supplier");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Code).HasMaxLength(64).IsRequired();
            entity.Property(row => row.NameAr).HasMaxLength(200).IsRequired();
            entity.Property(row => row.NameEn).HasMaxLength(200).IsRequired();
            entity.Property(row => row.CreditLimit).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Code }).IsUnique().HasDatabaseName("uq_purchasing_supplier_code");
        });

        modelBuilder.Entity<PurchaseRequestRow>(entity =>
        {
            entity.ToTable("purchase_request");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CostCenterId).HasMaxLength(64);
            entity.Property(row => row.EstimatedTotal).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number }).IsUnique().HasDatabaseName("uq_purchasing_request_number");
        });

        modelBuilder.Entity<PurchaseOrderRow>(entity =>
        {
            entity.ToTable("purchase_order");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.WarehouseId).HasMaxLength(64);
            entity.Property(row => row.CostCenterId).HasMaxLength(64);
            entity.Property(row => row.NetTotal).HasColumnType(Money);
            entity.Property(row => row.TaxTotal).HasColumnType(Money);
            entity.Property(row => row.GrossTotal).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number }).IsUnique().HasDatabaseName("uq_purchasing_order_number");
        });

        modelBuilder.Entity<GoodsReceiptRow>(entity =>
        {
            entity.ToTable("goods_receipt");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.WarehouseId).HasMaxLength(64);
            entity.Property(row => row.ReceiptCost).HasColumnType(Money);
            entity.Property(row => row.BilledValue).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number }).IsUnique().HasDatabaseName("uq_purchasing_receipt_number");
        });

        modelBuilder.Entity<SupplierBillRow>(entity =>
        {
            entity.ToTable("supplier_bill");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.WarehouseId).HasMaxLength(64);
            entity.Property(row => row.CostCenterId).HasMaxLength(64);
            entity.Property(row => row.ItemGroup).HasMaxLength(64);
            entity.Property(row => row.ExpenseCategory).HasMaxLength(64);
            entity.Property(row => row.BillKind).HasMaxLength(16).IsRequired();
            entity.Property(row => row.ReceiptValue).HasColumnType(Money);
            entity.Property(row => row.PriceVariance).HasColumnType(Money);
            entity.Property(row => row.NetTotal).HasColumnType(Money);
            entity.Property(row => row.TaxTotal).HasColumnType(Money);
            entity.Property(row => row.RecoverableTax).HasColumnType(Money);
            entity.Property(row => row.NonRecoverableTax).HasColumnType(Money);
            entity.Property(row => row.GrossTotal).HasColumnType(Money);
            entity.Property(row => row.AllocatedAmount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number }).IsUnique().HasDatabaseName("uq_purchasing_bill_number");
        });

        modelBuilder.Entity<DebitNoteRow>(entity =>
        {
            entity.ToTable("debit_note");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.WarehouseId).HasMaxLength(64);
            entity.Property(row => row.ItemGroup).HasMaxLength(64);
            entity.Property(row => row.ItemId).HasMaxLength(64);
            entity.Property(row => row.NetTotal).HasColumnType(Money);
            entity.Property(row => row.TaxTotal).HasColumnType(Money);
            entity.Property(row => row.GrossTotal).HasColumnType(Money);
            entity.Property(row => row.AllocatedAmount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number }).IsUnique().HasDatabaseName("uq_purchasing_debit_note_number");
        });

        modelBuilder.Entity<SupplierPaymentRow>(entity =>
        {
            entity.ToTable("supplier_payment");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.SettlementMethod).HasMaxLength(32).IsRequired();
            entity.Property(row => row.TreasuryPartyId).HasMaxLength(64);
            entity.Property(row => row.PaidAmount).HasColumnType(Money);
            entity.Property(row => row.BankFee).HasColumnType(Money);
            entity.Property(row => row.AllocatedAmount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number }).IsUnique().HasDatabaseName("uq_purchasing_payment_number");
        });

        modelBuilder.Entity<LandedCostRow>(entity =>
        {
            entity.ToTable("landed_cost");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.WarehouseId).HasMaxLength(64);
            entity.Property(row => row.ItemGroup).HasMaxLength(64);
            entity.Property(row => row.ItemId).HasMaxLength(64);
            entity.Property(row => row.Source).HasMaxLength(32).IsRequired();
            entity.Property(row => row.SettlementMethod).HasMaxLength(32).IsRequired();
            entity.Property(row => row.TreasuryPartyId).HasMaxLength(64);
            entity.Property(row => row.CostAmount).HasColumnType(Money);
            entity.Property(row => row.AllocatedAmount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number }).IsUnique().HasDatabaseName("uq_purchasing_landed_cost_number");
        });

        modelBuilder.Entity<PurchaseLineRow>(entity =>
        {
            entity.ToTable("purchase_line");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.OwnerType).HasMaxLength(16).IsRequired();
            entity.Property(row => row.ItemId).HasMaxLength(64);
            entity.Property(row => row.ItemGroup).HasMaxLength(64);
            entity.Property(row => row.DescriptionAr).HasMaxLength(400);
            entity.Property(row => row.DescriptionEn).HasMaxLength(400);
            entity.Property(row => row.TaxClassification).HasMaxLength(16).IsRequired();
            entity.Property(row => row.Quantity).HasColumnType(Money);
            entity.Property(row => row.ReceivedQuantity).HasColumnType(Money);
            entity.Property(row => row.BilledQuantity).HasColumnType(Money);
            entity.Property(row => row.UnitPrice).HasColumnType(Money);
            entity.Property(row => row.TaxRate).HasColumnType("numeric(9,6)");
            entity.Property(row => row.LineNet).HasColumnType(Money);
            entity.Property(row => row.LineTax).HasColumnType(Money);
            entity.HasIndex(row => new { row.OwnerType, row.OwnerId, row.LineNo })
                  .IsUnique().HasDatabaseName("uq_purchasing_line_owner");
        });

        modelBuilder.Entity<PayableAllocationRow>(entity =>
        {
            entity.ToTable("payable_allocation");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.SourceType).HasMaxLength(16).IsRequired();
            entity.Property(row => row.AllocatedAmount).HasColumnType(Money);
            entity.HasIndex(row => new { row.SourceType, row.SourceId, row.LineNo })
                  .IsUnique().HasDatabaseName("uq_purchasing_allocation_line");
            entity.HasIndex(row => row.BillId).HasDatabaseName("ix_purchasing_allocation_bill");
        });

        modelBuilder.Entity<DocumentPostingRow>(entity =>
        {
            entity.ToTable("document_posting");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.DocumentType).HasMaxLength(64).IsRequired();
            entity.Property(row => row.DocumentId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.TriggerCode).HasMaxLength(32).IsRequired();
            entity.Property(row => row.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.Property(row => row.EventCode).HasMaxLength(128).IsRequired();
            entity.Property(row => row.PartyId).HasMaxLength(64);
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.FailureCode).HasMaxLength(128);
            entity.Property(row => row.FailureMessageAr).HasMaxLength(1000);
            entity.Property(row => row.FailureMessageEn).HasMaxLength(1000);
            entity.Property(row => row.ControlEffect).HasColumnType(Money);

            // هوية الإحكام كما يعرّفها المحرك: خمسة حقول **ومنها رمز الحدث**، ولا حارس
            // تصاعدي (فخ-13). ورمز الحدث هنا لأن المستند الواحد يُنتج حدثين مختلفين
            // عند الإطلاق نفسه — فاتورة مورد بشقّ بضاعة وشقّ مصروف — وبدونه يُبتلع
            // الثاني بصمت (ADR-0017).
            entity.HasIndex(row => new
            {
                row.TenantId, row.DocumentType, row.DocumentId, row.TriggerCode, row.Generation, row.EventCode,
            }).IsUnique().HasDatabaseName("uq_purchasing_posting_identity");
            entity.HasIndex(row => new { row.TenantId, row.State }).HasDatabaseName("ix_purchasing_posting_state");

            // ورمزٌ فارغ يُعيد تركيب العطب داخل مفتاح موسَّع: القيمة الفارغة تجعل
            // كل حدث بلا رمز مساوياً لكل حدث آخر بلا رمز. الفراغ ممنوع في القاعدة
            // نفسها لا في الشيفرة وحدها.
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_purchasing_document_posting_event_code", """length(btrim("EventCode")) > 0"""));
        });
    }
}
