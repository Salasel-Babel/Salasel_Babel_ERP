using Microsoft.EntityFrameworkCore;

namespace Babel.Core.Persistence;

/// <summary>
/// جداول النواة: تأسيس المنشأة، ومراكز تكلفتها، وترجمات أسمائها، وملفّ قدراتها.
/// <para>
/// <c>internal</c> عمداً: لا وحدة أخرى — ولا حتى الجذر التركيبي — تستطيع حقن سياق وحدة
/// أخرى أو الاستعلام عبر جداولها (القاعدة 5). التسجيل يتم عبر
/// <see cref="CoreModuleRegistration"/> وحدها.
/// </para>
/// <para>
/// <b>وما ليس في هذا النموذج مقصود:</b> لا عمود <c>name_en</c> على أي كيان — الترجمة
/// صفٌّ في <c>core.name_translation</c> (ADR-0021 بند 2)، ولا عمود «محذوف» على مركز
/// تكلفة — الخروج من الاستعمال إيقافٌ لا حذف (ADR-0006).
/// </para>
/// </summary>
internal sealed class CoreDbContext(DbContextOptions<CoreDbContext> options) : DbContext(options)
{
    /// <summary>تأسيس المنشآت.</summary>
    public DbSet<CompanySetupRow> CompanySetups => Set<CompanySetupRow>();

    /// <summary>مراكز التكلفة.</summary>
    public DbSet<CostCenterRow> CostCenters => Set<CostCenterRow>();

    /// <summary>ترجمات الأسماء — صفوف لا أعمدة.</summary>
    public DbSet<CoreNameTranslationRow> NameTranslations => Set<CoreNameTranslationRow>();

    /// <summary>أنواع المستندات في ملفّات القدرات.</summary>
    public DbSet<CapabilityProfileDocumentRow> ProfileDocuments => Set<CapabilityProfileDocumentRow>();

    /// <summary>قرارات القدرات.</summary>
    public DbSet<CapabilityProfileCapabilityRow> ProfileCapabilities => Set<CapabilityProfileCapabilityRow>();

    /// <summary>القيم الافتراضية.</summary>
    public DbSet<CapabilityProfileDefaultRow> ProfileDefaults => Set<CapabilityProfileDefaultRow>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("core");

        modelBuilder.Entity<CompanySetupRow>(entity =>
        {
            entity.ToTable("company_setup", t =>
            {
                t.HasCheckConstraint("ck_company_setup_name_not_blank", "length(btrim(name_ar)) > 0");
                t.HasCheckConstraint("ck_company_setup_scale_range", "decimal_places between 0 and 4");
                t.HasCheckConstraint(
                    "ck_company_setup_default_shape",
                    "default_cost_center ~ '^[a-z0-9._]{1,32}$'");
            });

            entity.HasKey(row => row.CompanyId).HasName("pk_company_setup");
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.NameAr).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
            entity.Property(row => row.DecimalPlaces).HasColumnName("decimal_places");
            entity.Property(row => row.DefaultCostCenter).HasColumnName("default_cost_center").HasMaxLength(32).IsRequired();
            entity.Property(row => row.FoundedAt).HasColumnName("founded_at");
        });

        modelBuilder.Entity<CostCenterRow>(entity =>
        {
            entity.ToTable("cost_center", t =>
            {
                t.HasCheckConstraint("ck_cost_center_name_not_blank", "length(btrim(name_ar)) > 0");
                t.HasCheckConstraint("ck_cost_center_code_shape", "code ~ '^[a-z0-9._]{1,32}$'");
                t.HasCheckConstraint("ck_cost_center_state", "state in ('active','suspended')");

                // سبب الإيقاف مكتوبٌ **بالضبط** حين تكون الحالة موقوفة: إيقافٌ بلا سبب
                // يجعل من يقرأ التقرير بعد سنة لا يعرف لماذا اختفى المركز، وسببٌ على
                // مركز عامل نصٌّ لا يصفه شيء (ADR-0006).
                t.HasCheckConstraint(
                    "ck_cost_center_reason_matches_state",
                    "(state = 'suspended') = (length(btrim(suspension_reason)) > 0)");
            });

            entity.HasKey(row => new { row.CompanyId, row.Code }).HasName("pk_cost_center");
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
            entity.Property(row => row.NameAr).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
            entity.Property(row => row.State).HasColumnName("state").HasMaxLength(16).IsRequired();
            entity.Property(row => row.SuspensionReason)
                  .HasColumnName("suspension_reason").HasMaxLength(400).IsRequired().HasDefaultValue(string.Empty);
        });

        modelBuilder.Entity<CoreNameTranslationRow>(entity =>
        {
            entity.ToTable("name_translation", t =>
            {
                t.HasCheckConstraint(
                    "ck_core_name_translation_kind",
                    "entity_kind in ('company','cost_center')");

                // العربية سجلٌّ لا ترجمة (ADR-0021 بند 1). ومدخلٌ باسم «ar» يُنتج اسمين
                // عربيين لكيان واحد ولا يوجد ما يجعلهما يتطابقان — فيُرفض في المخطّط
                // نفسه لا في المستدعي وحده.
                t.HasCheckConstraint(
                    "ck_core_name_translation_not_arabic",
                    "lower(language_tag) <> 'ar' and lower(language_tag) not like 'ar-%'");
                t.HasCheckConstraint(
                    "ck_core_name_translation_tag_shape",
                    "language_tag ~ '^[A-Za-z][A-Za-z0-9]*(-[A-Za-z0-9]+)*$' and length(language_tag) <= 35");
                t.HasCheckConstraint("ck_core_name_translation_name_not_blank", "length(btrim(name)) > 0");
                t.HasCheckConstraint("ck_core_name_translation_key_not_blank", "length(btrim(entity_key)) > 0");
            });

            entity.HasKey(row => new { row.CompanyId, row.EntityKind, row.EntityKey, row.LanguageTag })
                  .HasName("pk_core_name_translation");
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.EntityKind).HasColumnName("entity_kind").HasMaxLength(32).IsRequired();
            entity.Property(row => row.EntityKey).HasColumnName("entity_key").HasMaxLength(64).IsRequired();
            entity.Property(row => row.LanguageTag).HasColumnName("language_tag").HasMaxLength(35).IsRequired();
            entity.Property(row => row.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<CapabilityProfileDocumentRow>(entity =>
        {
            entity.ToTable("capability_profile_document");
            entity.HasKey(row => new { row.CompanyId, row.DocumentType }).HasName("pk_capability_profile_document");
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.DocumentType).HasColumnName("document_type").HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<CapabilityProfileCapabilityRow>(entity =>
        {
            entity.ToTable("capability_profile_capability");
            entity.HasKey(row => new { row.CompanyId, row.DocumentType, row.Capability })
                  .HasName("pk_capability_profile_capability");
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.DocumentType).HasColumnName("document_type").HasMaxLength(64).IsRequired();
            entity.Property(row => row.Capability).HasColumnName("capability").HasMaxLength(64).IsRequired();
            entity.Property(row => row.Enabled).HasColumnName("enabled");
        });

        modelBuilder.Entity<CapabilityProfileDefaultRow>(entity =>
        {
            entity.ToTable("capability_profile_default");
            entity.HasKey(row => new { row.CompanyId, row.DocumentType, row.Field })
                  .HasName("pk_capability_profile_default");
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.DocumentType).HasColumnName("document_type").HasMaxLength(64).IsRequired();
            entity.Property(row => row.Field).HasColumnName("field").HasMaxLength(64).IsRequired();
            entity.Property(row => row.Value).HasColumnName("value").HasMaxLength(400).IsRequired();
        });
    }
}
