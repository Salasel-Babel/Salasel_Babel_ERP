using Microsoft.EntityFrameworkCore;

namespace Babel.Sales.Persistence;

/// <summary>
/// جداول المبيعات. <c>internal</c>: كل وحدة تملك جداولها، والقراءة العابرة عبر واجهات
/// معلنة لا عبر <c>JOIN</c> مباشر (وثيقة المعمارية §13 · Rule05).
/// <para>
/// ولاحظ ما ليس هنا: لا مفتاح خارجي إلى <c>ledger.account</c>، ولا كيان من وحدة أخرى،
/// ولا عمود يحمل رقم حساب. الفاتورة تحمل معرّف عميل، والربط المحاسبي يقرّره الدفتر.
/// </para>
/// <para>
/// وكل عمود مالي <c>numeric(19,4)</c> صراحةً: المقياس الضمني يعيد <c>100.0000m</c> حيث
/// كُتب <c>100.00m</c> فتختلف البصمة (فخ-17).
/// </para>
/// </summary>
internal sealed class SalesDbContext(DbContextOptions<SalesDbContext> options) : DbContext(options)
{
    private const string Money = "numeric(19,4)";

    public DbSet<CustomerRow> Customers => Set<CustomerRow>();

    public DbSet<QuotationRow> Quotations => Set<QuotationRow>();

    public DbSet<SalesOrderRow> Orders => Set<SalesOrderRow>();

    public DbSet<SalesInvoiceRow> Invoices => Set<SalesInvoiceRow>();

    public DbSet<SalesLineRow> Lines => Set<SalesLineRow>();

    public DbSet<CreditNoteRow> CreditNotes => Set<CreditNoteRow>();

    public DbSet<CustomerReceiptRow> Receipts => Set<CustomerReceiptRow>();

    public DbSet<CustomerAdvanceRow> Advances => Set<CustomerAdvanceRow>();

    public DbSet<ReceivableAllocationRow> Allocations => Set<ReceivableAllocationRow>();

    public DbSet<DocumentPostingRow> Postings => Set<DocumentPostingRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("sales");

        modelBuilder.Entity<CustomerRow>(entity =>
        {
            entity.ToTable("customer");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Code).HasMaxLength(64).IsRequired();
            entity.Property(row => row.NameAr).HasMaxLength(200).IsRequired();
            entity.Property(row => row.NameEn).HasMaxLength(200).IsRequired();
            entity.Property(row => row.CreditLimit).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Code }).IsUnique().HasDatabaseName("uq_sales_customer_code");
        });

        modelBuilder.Entity<QuotationRow>(entity =>
        {
            entity.ToTable("quotation");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.NetTotal).HasColumnType(Money);
            entity.Property(row => row.TaxTotal).HasColumnType(Money);
            entity.Property(row => row.GrossTotal).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number }).IsUnique().HasDatabaseName("uq_sales_quotation_number");
        });

        modelBuilder.Entity<SalesOrderRow>(entity =>
        {
            entity.ToTable("sales_order");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.BranchId).HasMaxLength(64);
            entity.Property(row => row.NetTotal).HasColumnType(Money);
            entity.Property(row => row.TaxTotal).HasColumnType(Money);
            entity.Property(row => row.GrossTotal).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number }).IsUnique().HasDatabaseName("uq_sales_order_number");
        });

        modelBuilder.Entity<SalesInvoiceRow>(entity =>
        {
            entity.ToTable("sales_invoice");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.BranchId).HasMaxLength(64);
            entity.Property(row => row.ItemGroup).HasMaxLength(64);
            entity.Property(row => row.NetTotal).HasColumnType(Money);
            entity.Property(row => row.TaxTotal).HasColumnType(Money);
            entity.Property(row => row.GrossTotal).HasColumnType(Money);
            entity.Property(row => row.AdvanceApplied).HasColumnType(Money);
            entity.Property(row => row.AllocatedAmount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number }).IsUnique().HasDatabaseName("uq_sales_invoice_number");
            entity.HasIndex(row => new { row.TenantId, row.CustomerId, row.State }).HasDatabaseName("ix_sales_invoice_party");
        });

        modelBuilder.Entity<SalesLineRow>(entity =>
        {
            entity.ToTable("sales_line");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.OwnerType).HasMaxLength(16).IsRequired();
            entity.Property(row => row.DescriptionAr).HasMaxLength(400);
            entity.Property(row => row.DescriptionEn).HasMaxLength(400);
            entity.Property(row => row.ItemGroup).HasMaxLength(64);
            entity.Property(row => row.TaxClassification).HasMaxLength(16).IsRequired();
            entity.Property(row => row.Quantity).HasColumnType(Money);
            entity.Property(row => row.UnitPrice).HasColumnType(Money);
            entity.Property(row => row.DiscountAmount).HasColumnType(Money);
            entity.Property(row => row.TaxRate).HasColumnType("numeric(9,6)");
            entity.Property(row => row.LineNet).HasColumnType(Money);
            entity.Property(row => row.LineTax).HasColumnType(Money);
            entity.HasIndex(row => new { row.OwnerType, row.OwnerId, row.LineNo }).IsUnique().HasDatabaseName("uq_sales_line_owner");
        });

        modelBuilder.Entity<CreditNoteRow>(entity =>
        {
            entity.ToTable("credit_note");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.BranchId).HasMaxLength(64);
            entity.Property(row => row.NetTotal).HasColumnType(Money);
            entity.Property(row => row.TaxTotal).HasColumnType(Money);
            entity.Property(row => row.GrossTotal).HasColumnType(Money);
            entity.Property(row => row.AllocatedAmount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number }).IsUnique().HasDatabaseName("uq_sales_credit_note_number");
        });

        modelBuilder.Entity<CustomerReceiptRow>(entity =>
        {
            entity.ToTable("customer_receipt");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.SettlementMethod).HasMaxLength(32).IsRequired();
            entity.Property(row => row.TreasuryPartyId).HasMaxLength(64);
            entity.Property(row => row.ReceivedAmount).HasColumnType(Money);
            entity.Property(row => row.DiscountAmount).HasColumnType(Money);
            entity.Property(row => row.AllocatedAmount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number }).IsUnique().HasDatabaseName("uq_sales_receipt_number");
        });

        modelBuilder.Entity<CustomerAdvanceRow>(entity =>
        {
            entity.ToTable("customer_advance");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.SettlementMethod).HasMaxLength(32).IsRequired();
            entity.Property(row => row.TreasuryPartyId).HasMaxLength(64);
            entity.Property(row => row.NetAmount).HasColumnType(Money);
            entity.Property(row => row.TaxAmount).HasColumnType(Money);
            entity.Property(row => row.AppliedAmount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number }).IsUnique().HasDatabaseName("uq_sales_advance_number");
        });

        modelBuilder.Entity<ReceivableAllocationRow>(entity =>
        {
            entity.ToTable("receivable_allocation");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.SourceType).HasMaxLength(16).IsRequired();
            entity.Property(row => row.AllocatedAmount).HasColumnType(Money);
            entity.HasIndex(row => new { row.SourceType, row.SourceId, row.LineNo })
                  .IsUnique().HasDatabaseName("uq_sales_allocation_line");
            entity.HasIndex(row => row.InvoiceId).HasDatabaseName("ix_sales_allocation_invoice");
        });

        modelBuilder.Entity<DocumentPostingRow>(entity =>
        {
            entity.ToTable("document_posting");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.DocumentType).HasMaxLength(64).IsRequired();
            entity.Property(row => row.DocumentId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.TriggerCode).HasMaxLength(32).IsRequired();
            entity.Property(row => row.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.Property(row => row.EventCode).HasMaxLength(128);
            entity.Property(row => row.PartyId).HasMaxLength(64);
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.FailureCode).HasMaxLength(128);
            entity.Property(row => row.FailureMessageAr).HasMaxLength(1000);
            entity.Property(row => row.FailureMessageEn).HasMaxLength(1000);
            entity.Property(row => row.ControlEffect).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.State }).HasDatabaseName("ix_sales_posting_state");

            // هوية الإحكام كما يعرّفها المحرك بالضبط: أربعة حقول، بلا أي عدّاد تصاعدي.
            // الحارس التصاعدي لكل طرف ممنوع — قيس وهو يُسقط 500 من 1500 ريال (فخ-13).
            entity.HasIndex(row => new { row.TenantId, row.DocumentType, row.DocumentId, row.TriggerCode, row.Generation })
                  .IsUnique().HasDatabaseName("uq_sales_posting_identity");
        });
    }
}
