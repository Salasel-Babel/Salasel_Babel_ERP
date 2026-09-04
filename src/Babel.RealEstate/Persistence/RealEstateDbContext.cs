using Microsoft.EntityFrameworkCore;

namespace Babel.RealEstate.Persistence;

/// <summary>
/// جداول العقارات. <c>internal</c>: كل وحدة تملك جداولها (القاعدة 5)، ولا مفتاح خارجي
/// إلى وحدة أخرى ولا إلى الدفتر، ولا عمود يحمل رقم حساب (القاعدة 2).
/// <para>
/// وكل عمود قيمة <c>numeric(19,4)</c> صراحةً (‏فخ-17). <b>ولا عمود نسبة واحد في هذا
/// المخطّط</b>: الحصّة كسرٌ ببسطٍ ومقام صحيحين، لأن لا مقياس عشري معلَن لأي نسبة في
/// هذا المستودع، واختيارُ مقياسٍ هنا كتابةُ رقمٍ نظامي في مخطّط.
/// </para>
/// <para>
/// <b>ولا عمود <c>name_en</c>:</b> السجلّ عربي عمودٌ، والترجمات صفوفٌ في جدولها
/// (ADR-0021 · القاعدة 14).
/// </para>
/// </summary>
internal sealed class RealEstateDbContext(DbContextOptions<RealEstateDbContext> options) : DbContext(options)
{
    private const string Money = "numeric(19,4)";

    public DbSet<PropertyRow> Properties => Set<PropertyRow>();

    public DbSet<PropertyTranslationRow> PropertyNames => Set<PropertyTranslationRow>();

    public DbSet<PropertyOwnerShareRow> OwnerShares => Set<PropertyOwnerShareRow>();

    public DbSet<UnitRow> Units => Set<UnitRow>();

    public DbSet<PartyRow> Parties => Set<PartyRow>();

    public DbSet<PartyTranslationRow> PartyNames => Set<PartyTranslationRow>();

    public DbSet<LeaseContractRow> Leases => Set<LeaseContractRow>();

    public DbSet<PaymentScheduleLineRow> ScheduleLines => Set<PaymentScheduleLineRow>();

    public DbSet<RentInvoiceRow> RentInvoices => Set<RentInvoiceRow>();

    public DbSet<RentInvoiceLineRow> RentInvoiceLines => Set<RentInvoiceLineRow>();

    public DbSet<TenantReceiptRow> TenantReceipts => Set<TenantReceiptRow>();

    public DbSet<DocumentPostingRow> Postings => Set<DocumentPostingRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("realestate");

        modelBuilder.Entity<PropertyRow>(entity =>
        {
            entity.ToTable("property");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Code).HasMaxLength(64).IsRequired();
            entity.Property(row => row.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(row => row.OwnershipModel).HasMaxLength(32).IsRequired();

            // ‏**المفتاح ثلاثي**: صفّ سجلّ أبعاد الدفتر مفتاحه (company_id, property_id).
            entity.HasIndex(row => new { row.TenantId, row.CompanyId, row.Code })
                  .IsUnique().HasDatabaseName("uq_realestate_property_code");

            entity.ToTable(table => table.HasCheckConstraint(
                "ck_realestate_property_ownership_model",
                """ "OwnershipModel" in ('own_property','managed_for_others') """));
        });

        modelBuilder.Entity<PropertyTranslationRow>(entity =>
        {
            entity.ToTable("property_name_translation");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.PropertyCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.LanguageTag).HasMaxLength(35).IsRequired();
            entity.Property(row => row.Text).HasMaxLength(256).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.CompanyId, row.PropertyCode, row.LanguageTag })
                  .IsUnique().HasDatabaseName("uq_realestate_property_name_translation");
        });

        modelBuilder.Entity<PropertyOwnerShareRow>(entity =>
        {
            entity.ToTable("property_owner_share");
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => new { row.TenantId, row.CompanyId, row.PropertyId, row.OwnerId })
                  .IsUnique().HasDatabaseName("uq_realestate_property_owner_share");

            // كسرٌ بمقامٍ صفر ليس كسراً، وبسطٌ يتجاوز مقامه حصّةٌ فوق العقار كلّه.
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_realestate_owner_share_is_a_fraction",
                """ "ShareNumerator" > 0 and "ShareDenominator" > 0 and "ShareNumerator" <= "ShareDenominator" """));
        });

        modelBuilder.Entity<UnitRow>(entity =>
        {
            entity.ToTable("unit");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Code).HasMaxLength(64).IsRequired();
            entity.Property(row => row.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(row => row.Usage).HasMaxLength(32).IsRequired();
            entity.Property(row => row.VatTreatment).HasMaxLength(32).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.CompanyId, row.Code })
                  .IsUnique().HasDatabaseName("uq_realestate_unit_code");
            entity.HasIndex(row => new { row.TenantId, row.CompanyId, row.PropertyId })
                  .HasDatabaseName("ix_realestate_unit_property");

            // ‏**لا قيمة افتراضية ولا اشتقاق**: القيمتان مُدخَلتان ومُراجَعتان (م-3).
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_realestate_unit_usage",
                """ "Usage" in ('residential','commercial') """));

            entity.ToTable(table => table.HasCheckConstraint(
                "ck_realestate_unit_vat_treatment",
                """ "VatTreatment" in ('standard','exempt') """));
        });

        modelBuilder.Entity<PartyRow>(entity =>
        {
            entity.ToTable("party");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.PartyRole).HasMaxLength(16).IsRequired();
            entity.Property(row => row.Code).HasMaxLength(64).IsRequired();
            entity.Property(row => row.NameAr).HasMaxLength(256).IsRequired();
            entity.Property(row => row.VatNumber).HasMaxLength(64).IsRequired();
            entity.Property(row => row.TaxResidency).HasMaxLength(16).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.CompanyId, row.PartyRole, row.Code })
                  .IsUnique().HasDatabaseName("uq_realestate_party_code");

            entity.ToTable(table => table.HasCheckConstraint(
                "ck_realestate_party_role",
                """ "PartyRole" in ('lessee','owner','broker') """));

            entity.ToTable(table => table.HasCheckConstraint(
                "ck_realestate_party_tax_residency",
                """ "TaxResidency" in ('resident','non_resident') """));
        });

        modelBuilder.Entity<PartyTranslationRow>(entity =>
        {
            entity.ToTable("party_name_translation");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.PartyRole).HasMaxLength(16).IsRequired();
            entity.Property(row => row.PartyCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.LanguageTag).HasMaxLength(35).IsRequired();
            entity.Property(row => row.Text).HasMaxLength(256).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.CompanyId, row.PartyRole, row.PartyCode, row.LanguageTag })
                  .IsUnique().HasDatabaseName("uq_realestate_party_name_translation");
        });

        modelBuilder.Entity<LeaseContractRow>(entity =>
        {
            entity.ToTable("lease_contract");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.EjarContractNumber).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.TotalRent).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.CompanyId, row.EjarContractNumber })
                  .IsUnique().HasDatabaseName("uq_realestate_lease_contract_no");
            entity.HasIndex(row => new { row.TenantId, row.CompanyId, row.UnitId })
                  .HasDatabaseName("ix_realestate_lease_unit");

            // مدّةٌ تنتهي قبل أن تبدأ ليست مدّة، وقيد الاستبعاد الزمني يقرأ المدى نفسه.
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_realestate_lease_term_is_ordered",
                """ "EndsOn" >= "StartsOn" """));
        });

        modelBuilder.Entity<PaymentScheduleLineRow>(entity =>
        {
            entity.ToTable("payment_schedule_line");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Amount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.LeaseId, row.Seq })
                  .IsUnique().HasDatabaseName("uq_realestate_payment_schedule_line");

            entity.ToTable(table => table.HasCheckConstraint(
                "ck_realestate_schedule_period_is_ordered",
                """ "PeriodTo" >= "PeriodFrom" """));

            entity.ToTable(table => table.HasCheckConstraint(
                "ck_realestate_schedule_amount_positive",
                """ "Amount" > 0 """));
        });

        modelBuilder.Entity<RentInvoiceRow>(entity =>
        {
            entity.ToTable("rent_invoice");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.VatTreatment).HasMaxLength(32).IsRequired();
            entity.Property(row => row.ExemptionReasonCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.EventCode).HasMaxLength(128).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.NetTotal).HasColumnType(Money);
            entity.Property(row => row.TaxTotal).HasColumnType(Money);
            entity.Property(row => row.GrossTotal).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.CompanyId, row.Number })
                  .IsUnique().HasDatabaseName("uq_realestate_rent_invoice_number");
            entity.HasIndex(row => new { row.TenantId, row.LesseeId })
                  .HasDatabaseName("ix_realestate_rent_invoice_lessee");
        });

        modelBuilder.Entity<RentInvoiceLineRow>(entity =>
        {
            entity.ToTable("rent_invoice_line");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Net).HasColumnType(Money);
            entity.Property(row => row.Tax).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.InvoiceId })
                  .HasDatabaseName("ix_realestate_rent_invoice_line_invoice");

            // ‏**قسطٌ واحد لا يُفوتَر مرّتين** — والحارس فهرسٌ لا فحصٌ في الخدمة:
            // نداءان متزامنان يجتازان فحص «هل فُوتر؟» معاً ويلتقيان هنا.
            entity.HasIndex(row => new { row.TenantId, row.ScheduleLineId })
                  .IsUnique().HasDatabaseName("uq_realestate_rent_invoice_line_schedule");
        });

        modelBuilder.Entity<TenantReceiptRow>(entity =>
        {
            entity.ToTable("tenant_receipt");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.SettlementMethod).HasMaxLength(32).IsRequired();
            entity.Property(row => row.TreasuryPartyId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.EventCode).HasMaxLength(128).IsRequired();
            entity.Property(row => row.Received).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.CompanyId, row.Number })
                  .IsUnique().HasDatabaseName("uq_realestate_tenant_receipt_number");

            entity.ToTable(table => table.HasCheckConstraint(
                "ck_realestate_tenant_receipt_positive",
                """ "Received" > 0 """));
        });

        modelBuilder.Entity<DocumentPostingRow>(entity =>
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
            entity.Property(row => row.ControlEffect).HasColumnType(Money);

            // ‏**هوية الإحكام السداسية منسوخةً حرفاً** — ورمز الحدث فيها (ADR-0016 · فخ-45):
            // المستند الواحد يُنتج أكثر من حدث عند الإطلاق نفسه، ورمزٌ خارج الهوية
            // يجعلهما هويةً واحدة فيُبتلع الثاني بصمت.
            entity.HasIndex(row => new
            {
                row.TenantId,
                row.DocumentType,
                row.DocumentId,
                row.TriggerCode,
                row.Generation,
                row.EventCode,
            }).IsUnique().HasDatabaseName("uq_realestate_posting_identity");

            entity.ToTable(table => table.HasCheckConstraint(
                "ck_realestate_posting_event_code",
                """ length(btrim("EventCode")) > 0 """));
        });
    }
}
