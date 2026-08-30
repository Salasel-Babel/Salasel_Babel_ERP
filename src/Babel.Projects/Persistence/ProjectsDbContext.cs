using Microsoft.EntityFrameworkCore;

namespace Babel.Projects.Persistence;

/// <summary>
/// جداول المقاولات. <c>internal</c>: كل وحدة تملك جداولها، والقراءة العابرة عبر واجهات
/// معلنة لا عبر <c>JOIN</c> مباشر (القاعدة 5).
/// <para>
/// ولاحظ ما ليس هنا: لا مفتاح خارجي إلى <c>ledger.account</c>، ولا كيان من وحدة أخرى،
/// ولا عمود يحمل رقم حساب، ولا عمود <c>name_en</c> — الترجمة صفٌّ في
/// <c>projects.name_translation</c> (ADR-0021 · القاعدة 14).
/// </para>
/// <para>
/// وكل عمود مالي <c>numeric(19,4)</c> صراحةً، وكل نسبة <c>numeric(9,6)</c>: المقياس
/// الضمني يعيد <c>100.0000m</c> حيث كُتب <c>100.00m</c> فتختلف البصمة (فخ-17).
/// </para>
/// </summary>
internal sealed class ProjectsDbContext(DbContextOptions<ProjectsDbContext> options) : DbContext(options)
{
    private const string Money = "numeric(19,4)";
    private const string Rate = "numeric(9,6)";

    public DbSet<ProjectRow> Projects => Set<ProjectRow>();

    public DbSet<NameTranslationRow> NameTranslations => Set<NameTranslationRow>();

    public DbSet<ProjectContractRow> Contracts => Set<ProjectContractRow>();

    public DbSet<ContractPolicyRow> ContractPolicies => Set<ContractPolicyRow>();

    public DbSet<BoqItemRow> BoqItems => Set<BoqItemRow>();

    public DbSet<ChangeOrderRow> ChangeOrders => Set<ChangeOrderRow>();

    public DbSet<SubcontractorRow> Subcontractors => Set<SubcontractorRow>();

    public DbSet<SubcontractRow> Subcontracts => Set<SubcontractRow>();

    public DbSet<SubcontractLineRow> SubcontractLines => Set<SubcontractLineRow>();

    public DbSet<ClientCertificateRow> ClientCertificates => Set<ClientCertificateRow>();

    public DbSet<SubcontractorCertificateRow> SubcontractorCertificates => Set<SubcontractorCertificateRow>();

    public DbSet<CertificateLineRow> CertificateLines => Set<CertificateLineRow>();

    public DbSet<SubcontractorAdvanceRow> SubcontractorAdvances => Set<SubcontractorAdvanceRow>();

    public DbSet<RetentionMovementRow> RetentionMovements => Set<RetentionMovementRow>();

    public DbSet<AdvanceMovementRow> AdvanceMovements => Set<AdvanceMovementRow>();

    public DbSet<RetentionReleaseRow> RetentionReleases => Set<RetentionReleaseRow>();

    public DbSet<RetentionCollectionRow> RetentionCollections => Set<RetentionCollectionRow>();

    public DbSet<GuaranteeRow> Guarantees => Set<GuaranteeRow>();

    public DbSet<DocumentPostingRow> Postings => Set<DocumentPostingRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("projects");

        modelBuilder.Entity<ProjectRow>(entity =>
        {
            entity.ToTable("project");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Code).HasMaxLength(64).IsRequired();
            entity.Property(row => row.NameAr).HasMaxLength(200).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.Code })
                  .IsUnique().HasDatabaseName("uq_projects_project_code");
        });

        modelBuilder.Entity<NameTranslationRow>(entity =>
        {
            entity.ToTable("name_translation");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.EntityKind).HasMaxLength(32).IsRequired();
            entity.Property(row => row.LanguageTag).HasMaxLength(16).IsRequired();
            entity.Property(row => row.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.EntityKind, row.EntityId, row.LanguageTag })
                  .IsUnique().HasDatabaseName("uq_projects_name_translation");
        });

        modelBuilder.Entity<ProjectContractRow>(entity =>
        {
            entity.ToTable("project_contract");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.CustomerPartyId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.RetentionRate).HasColumnType(Rate);
            entity.HasIndex(row => new { row.TenantId, row.Number })
                  .IsUnique().HasDatabaseName("uq_projects_contract_number");
            entity.HasIndex(row => new { row.TenantId, row.ProjectId })
                  .HasDatabaseName("ix_projects_contract_project");
        });

        modelBuilder.Entity<ContractPolicyRow>(entity =>
        {
            entity.ToTable("contract_policy");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.ItemCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.Resolution).HasMaxLength(400).IsRequired();
            entity.Property(row => row.ApprovedBy).HasMaxLength(128).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.ContractId, row.ItemCode })
                  .IsUnique().HasDatabaseName("uq_projects_contract_policy_item");

            // ‏**لا صفّ بقرارٍ فارغ ولا بمعتمِدٍ فارغ.** الفراغ في أيّهما يجعل الصفّ
            // يرفع الحجب بلا أن يقول أحدٌ شيئاً — وهو بالضبط القيمة الافتراضية
            // المتخفّية التي يمنعها هذا التصميم.
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_projects_contract_policy_answered",
                """length(btrim("Resolution")) > 0 and length(btrim("ApprovedBy")) > 0"""));
        });

        modelBuilder.Entity<BoqItemRow>(entity =>
        {
            entity.ToTable("boq_item");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Code).HasMaxLength(64).IsRequired();
            entity.Property(row => row.DescriptionAr).HasMaxLength(400).IsRequired();
            entity.Property(row => row.Unit).HasMaxLength(16).IsRequired();
            entity.Property(row => row.ContractQuantity).HasColumnType(Money);
            entity.Property(row => row.UnitRate).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.ContractId, row.Code })
                  .IsUnique().HasDatabaseName("uq_projects_boq_item_code");
        });

        modelBuilder.Entity<ChangeOrderRow>(entity =>
        {
            entity.ToTable("change_order");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.ReasonAr).HasMaxLength(400).IsRequired();
            entity.Property(row => row.ApprovedBy).HasMaxLength(128).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.Number })
                  .IsUnique().HasDatabaseName("uq_projects_change_order_number");
            entity.HasIndex(row => new { row.TenantId, row.ContractId })
                  .HasDatabaseName("ix_projects_change_order_contract");
        });

        modelBuilder.Entity<SubcontractorRow>(entity =>
        {
            entity.ToTable("subcontractor");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Code).HasMaxLength(64).IsRequired();
            entity.Property(row => row.NameAr).HasMaxLength(200).IsRequired();
            entity.Property(row => row.VatNumber).HasMaxLength(32).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.Code })
                  .IsUnique().HasDatabaseName("uq_projects_subcontractor_code");
        });

        modelBuilder.Entity<SubcontractRow>(entity =>
        {
            entity.ToTable("subcontract");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.RetentionRate).HasColumnType(Rate);
            entity.HasIndex(row => new { row.TenantId, row.Number })
                  .IsUnique().HasDatabaseName("uq_projects_subcontract_number");
            entity.HasIndex(row => new { row.TenantId, row.ProjectId })
                  .HasDatabaseName("ix_projects_subcontract_project");
        });

        modelBuilder.Entity<SubcontractLineRow>(entity =>
        {
            entity.ToTable("subcontract_line");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Code).HasMaxLength(64).IsRequired();
            entity.Property(row => row.DescriptionAr).HasMaxLength(400).IsRequired();
            entity.Property(row => row.Unit).HasMaxLength(16).IsRequired();
            entity.Property(row => row.ContractQuantity).HasColumnType(Money);
            entity.Property(row => row.UnitRate).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.SubcontractId, row.Code })
                  .IsUnique().HasDatabaseName("uq_projects_subcontract_line_code");
        });

        modelBuilder.Entity<ClientCertificateRow>(entity =>
        {
            entity.ToTable("client_certificate");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.FrozenRetentionRate).HasColumnType(Rate);
            entity.HasIndex(row => new { row.TenantId, row.Number })
                  .IsUnique().HasDatabaseName("uq_projects_client_certificate_number");

            // التفرّد الوظيفي المفروض بنيوياً: عليه وحده يقوم الاشتقاق التراكمي.
            entity.HasIndex(row => new { row.TenantId, row.ContractId, row.SequenceNo })
                  .IsUnique().HasDatabaseName("uq_projects_client_certificate_sequence");
        });

        modelBuilder.Entity<SubcontractorCertificateRow>(entity =>
        {
            entity.ToTable("subcontractor_certificate");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.FrozenRetentionRate).HasColumnType(Rate);
            entity.HasIndex(row => new { row.TenantId, row.Number })
                  .IsUnique().HasDatabaseName("uq_projects_subcontractor_certificate_number");
            entity.HasIndex(row => new { row.TenantId, row.SubcontractId, row.SequenceNo })
                  .IsUnique().HasDatabaseName("uq_projects_subcontractor_certificate_sequence");
        });

        modelBuilder.Entity<CertificateLineRow>(entity =>
        {
            entity.ToTable("certificate_line");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.OwnerType).HasMaxLength(16).IsRequired();
            entity.Property(row => row.LineKind).HasMaxLength(16).IsRequired();
            entity.Property(row => row.DescriptionAr).HasMaxLength(400).IsRequired();
            entity.Property(row => row.Unit).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CumulativeQuantity).HasColumnType(Money);
            entity.Property(row => row.PreviousQuantity).HasColumnType(Money);
            entity.Property(row => row.Amount).HasColumnType(Money);
            entity.HasIndex(row => new { row.OwnerType, row.OwnerId, row.LineNo })
                  .IsUnique().HasDatabaseName("uq_projects_certificate_line_owner");
        });

        modelBuilder.Entity<SubcontractorAdvanceRow>(entity =>
        {
            entity.ToTable("subcontractor_advance");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.SettlementMethod).HasMaxLength(32).IsRequired();
            entity.Property(row => row.TreasuryPartyId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.Amount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number })
                  .IsUnique().HasDatabaseName("uq_projects_subcontractor_advance_number");
        });

        modelBuilder.Entity<RetentionMovementRow>(entity =>
        {
            entity.ToTable("retention_movement");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Side).HasMaxLength(16).IsRequired();
            entity.Property(row => row.PartyKind).HasMaxLength(32).IsRequired();
            entity.Property(row => row.PartyId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.ProjectCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.DocumentType).HasMaxLength(64).IsRequired();
            entity.Property(row => row.DocumentId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.EventCode).HasMaxLength(128).IsRequired();
            entity.Property(row => row.Amount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.ContractId })
                  .HasDatabaseName("ix_projects_retention_movement_contract");

            // هوية الحركة هي هوية الترحيل نفسها: صفٌّ واحد لكل (مستند × حدث).
            entity.HasIndex(row => new { row.TenantId, row.DocumentType, row.DocumentId, row.EventCode })
                  .IsUnique().HasDatabaseName("uq_projects_retention_movement_identity");
        });

        modelBuilder.Entity<AdvanceMovementRow>(entity =>
        {
            entity.ToTable("advance_movement");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.PartyKind).HasMaxLength(32).IsRequired();
            entity.Property(row => row.PartyId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.DocumentType).HasMaxLength(64).IsRequired();
            entity.Property(row => row.DocumentId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.EventCode).HasMaxLength(128).IsRequired();
            entity.Property(row => row.Amount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.DocumentType, row.DocumentId, row.EventCode })
                  .IsUnique().HasDatabaseName("uq_projects_advance_movement_identity");
        });

        modelBuilder.Entity<RetentionReleaseRow>(entity =>
        {
            entity.ToTable("retention_release");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.ApprovedBy).HasMaxLength(128).IsRequired();
            entity.Property(row => row.Amount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number })
                  .IsUnique().HasDatabaseName("uq_projects_retention_release_number");

            // الاعتماد الصريح شرطُ الإطلاق بنصّه، فالفراغ مرفوض في القاعدة لا في الشيفرة وحدها.
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_projects_retention_release_approved", """length(btrim("ApprovedBy")) > 0"""));
        });

        modelBuilder.Entity<RetentionCollectionRow>(entity =>
        {
            entity.ToTable("retention_collection");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.SettlementMethod).HasMaxLength(32).IsRequired();
            entity.Property(row => row.TreasuryPartyId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.Amount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number })
                  .IsUnique().HasDatabaseName("uq_projects_retention_collection_number");
        });

        modelBuilder.Entity<GuaranteeRow>(entity =>
        {
            entity.ToTable("guarantee");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Kind).HasMaxLength(32).IsRequired();
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.IssuerNameAr).HasMaxLength(200).IsRequired();
            entity.Property(row => row.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(row => row.AttachmentId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.Amount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number })
                  .IsUnique().HasDatabaseName("uq_projects_guarantee_number");
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
            entity.Property(row => row.SubledgerKind).HasMaxLength(32).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.FailureCode).HasMaxLength(128);
            entity.Property(row => row.FailureMessageAr).HasMaxLength(1000);
            entity.Property(row => row.FailureMessageEn).HasMaxLength(1000);
            entity.Property(row => row.ControlEffect).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.State })
                  .HasDatabaseName("ix_projects_posting_state");

            // هوية الإحكام كما يعرّفها المحرك: خمسة مكوّنات **ومنها رمز الحدث**، وستّة
            // أعمدة في الفهرس بإضافة المستأجر. ولا حارس تصاعدي لكل طرف (فخ-13).
            entity.HasIndex(row => new
            {
                row.TenantId, row.DocumentType, row.DocumentId, row.TriggerCode, row.Generation, row.EventCode,
            }).IsUnique().HasDatabaseName("uq_projects_posting_identity");

            // ورمزٌ فارغ يُعيد تركيب العطب داخل مفتاح موسَّع: كل حدث بلا رمز يساوي
            // كل حدث آخر بلا رمز. الفراغ ممنوع في القاعدة لا في الشيفرة وحدها (فخ-45).
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_projects_document_posting_event_code", """length(btrim("EventCode")) > 0"""));
        });
    }
}
