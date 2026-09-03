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

    /// <summary>عضويات المستخدمين في المنشآت — مصدر ما يبلغه كل اعتماد.</summary>
    public DbSet<AccessMembershipRow> Memberships => Set<AccessMembershipRow>();

    /// <summary>اعتمادات الانتساب، مبصومةً لا مكتوبة.</summary>
    public DbSet<AccessEnrolmentRow> Enrolments => Set<AccessEnrolmentRow>();

    /// <summary>عائلات الجلسات، وعليها مفتاح الإبطال.</summary>
    public DbSet<AccessSessionRow> Sessions => Set<AccessSessionRow>();

    /// <summary>الاعتمادات المُصدَرة داخل العائلات، مبصومةً لا مكتوبة.</summary>
    public DbSet<AccessCredentialRow> Credentials => Set<AccessCredentialRow>();

    /// <summary>قيود التدقيق — تُلحَق ولا تُعدَّل ولا تُحذف.</summary>
    public DbSet<AuditEntryRow> AuditEntries => Set<AuditEntryRow>();

    /// <summary>أحداث الاستخدام على محور الوحدة — سجلٌّ مُلحَق لا عدّاد.</summary>
    public DbSet<ModuleUsageRow> ModuleUsage => Set<ModuleUsageRow>();

    /// <summary>أحداث النشاط على محور المستخدم.</summary>
    public DbSet<UserActivityRow> UserActivity => Set<UserActivityRow>();

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

        modelBuilder.Entity<AccessMembershipRow>(entity =>
        {
            entity.ToTable("access_membership", t =>
            {
                t.HasCheckConstraint("ck_access_membership_role", "role in ('reader','contributor','owner')");
                t.HasCheckConstraint("ck_access_membership_name_not_blank", "length(btrim(display_name_ar)) > 0");
            });

            entity.HasKey(row => new { row.CompanyId, row.UserId }).HasName("pk_access_membership");
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.UserId).HasColumnName("user_id");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.Role).HasColumnName("role").HasMaxLength(16).IsRequired();
            entity.Property(row => row.DisplayNameAr).HasColumnName("display_name_ar").HasMaxLength(200).IsRequired();
            entity.Property(row => row.GrantedAt).HasColumnName("granted_at");
            entity.Property(row => row.GrantedBy).HasColumnName("granted_by");

            // الفهرس على (المستأجر، المستخدم): هو استعلامُ **كل طلب** — «ما الذي يبلغه هذا
            // الاعتماد؟» — وبلا فهرسٍ عليه يصير مسحاً كاملاً على أكثر جدولٍ قراءةً في السطح.
            entity.HasIndex(row => new { row.TenantId, row.UserId }).HasDatabaseName("ix_access_membership_tenant_user");
        });

        modelBuilder.Entity<AccessEnrolmentRow>(entity =>
        {
            entity.ToTable("access_enrolment", t =>
                t.HasCheckConstraint("ck_access_enrolment_digest_shape", "digest ~ '^[0-9a-f]{64}$'"));

            entity.HasKey(row => row.Digest).HasName("pk_access_enrolment");
            entity.Property(row => row.Digest).HasColumnName("digest").HasMaxLength(64).IsRequired();
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.UserId).HasColumnName("user_id");
            entity.Property(row => row.IssuedAt).HasColumnName("issued_at");
            entity.Property(row => row.ExpiresAt).HasColumnName("expires_at");
            entity.Property(row => row.ConsumedAt).HasColumnName("consumed_at");
        });

        modelBuilder.Entity<AccessSessionRow>(entity =>
        {
            entity.ToTable("access_session", t =>
            {
                t.HasCheckConstraint("ck_access_session_generation", "generation >= 1");

                // السبب مكتوبٌ **بالضبط** حين تكون الجلسة مُبطَلة: إبطالٌ بلا سبب يجعل من
                // يقرأ السجلّ لا يعرف أخرج المستخدم أم سُرق اعتماده، وسببٌ على جلسة حيّة
                // نصٌّ لا يصفه شيء. وهو شكل قيد مركز التكلفة نفسه، لا نمطٌ ثانٍ.
                t.HasCheckConstraint(
                    "ck_access_session_reason_matches_state",
                    "(revoked_at is not null) = (length(btrim(revoked_reason)) > 0)");
                t.HasCheckConstraint(
                    "ck_access_session_reason_closed",
                    "revoked_reason in ('', 'signed_out', 'refresh_replayed')");
            });

            entity.HasKey(row => row.SessionId).HasName("pk_access_session");
            entity.Property(row => row.SessionId).HasColumnName("session_id");
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.UserId).HasColumnName("user_id");
            entity.Property(row => row.OpenedAt).HasColumnName("opened_at");
            entity.Property(row => row.Generation).HasColumnName("generation");
            entity.Property(row => row.RevokedAt).HasColumnName("revoked_at");
            entity.Property(row => row.RevokedReason)
                  .HasColumnName("revoked_reason").HasMaxLength(32).IsRequired().HasDefaultValue(string.Empty);
        });

        modelBuilder.Entity<AccessCredentialRow>(entity =>
        {
            entity.ToTable("access_credential", t =>
            {
                t.HasCheckConstraint("ck_access_credential_kind", "kind in ('access','refresh')");
                t.HasCheckConstraint("ck_access_credential_digest_shape", "digest ~ '^[0-9a-f]{64}$'");
                t.HasCheckConstraint("ck_access_credential_generation", "generation >= 1");
            });

            entity.HasKey(row => row.Digest).HasName("pk_access_credential");
            entity.Property(row => row.Digest).HasColumnName("digest").HasMaxLength(64).IsRequired();
            entity.Property(row => row.SessionId).HasColumnName("session_id");
            entity.Property(row => row.Kind).HasColumnName("kind").HasMaxLength(8).IsRequired();
            entity.Property(row => row.Generation).HasColumnName("generation");
            entity.Property(row => row.IssuedAt).HasColumnName("issued_at");
            entity.Property(row => row.ExpiresAt).HasColumnName("expires_at");
            entity.Property(row => row.ConsumedAt).HasColumnName("consumed_at");

            entity.HasIndex(row => row.SessionId).HasDatabaseName("ix_access_credential_session");
        });

        // ── الأثر والقياس: ثلاثة سجلّات تُلحَق ولا تُمسّ ──────────────────────
        // والحصانة **ليست** في هذا النموذج: هي في `CoreGrants.sql` (لا UPDATE ولا DELETE
        // لدور التطبيق) وفي `CoreAppendOnlyTriggers.sql` (رفضٌ ولو كان الفاعل المالك).
        // وما هنا وصفُ الأعمدة والفهارس وحدها (ADR-0003).

        modelBuilder.Entity<AuditEntryRow>(entity =>
        {
            entity.ToTable("audit_entry", t =>
                // الشكل مفروضٌ على **رمز الإجراء وحده**: قيمه ثوابتُ مصدرٍ مغلقة، فقيدٌ
                // عليها يمسك خطأً برمجياً. ولا قيد على الموضوع ولا على التفصيل — انظر
                // AuditEntryRow.Subject: رفضُ التقاطِ قيدٍ أسوأ من قبول نصٍّ فارغ.
                t.HasCheckConstraint("ck_audit_entry_action_shape", "action ~ '^[a-z][a-z0-9_.]{0,63}$'"));

            entity.HasKey(row => row.SequenceNo).HasName("pk_audit_entry");
            entity.Property(row => row.SequenceNo).HasColumnName("sequence_no").ValueGeneratedOnAdd();
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.ActorId).HasColumnName("actor_id");
            entity.Property(row => row.OccurredAt).HasColumnName("occurred_at");
            entity.Property(row => row.Action).HasColumnName("action").HasMaxLength(64).IsRequired();
            entity.Property(row => row.Subject).HasColumnName("subject").IsRequired();
            entity.Property(row => row.Details).HasColumnName("details");

            // الفهرس على (المستأجر، اللحظة، التسلسل): هو **الاستعلام الوحيد** على هذا
            // الجدول — «أثرُ هذا المستأجر بترتيب وقوعه» — والمستأجر أوّلَ العمدة لأن
            // نطاقه شرطُ مساواةٍ يسبق مدى الزمن. وبلا فهرسٍ عليه يصير كلُّ عرضِ أثرٍ
            // مسحاً كاملاً على جدولٍ لا يُحذف منه صفٌّ أبداً — أي ينمو ولا ينكمش.
            entity.HasIndex(row => new { row.TenantId, row.OccurredAt, row.SequenceNo })
                  .HasDatabaseName("ix_audit_entry_tenant_occurred");
        });

        modelBuilder.Entity<ModuleUsageRow>(entity =>
        {
            entity.ToTable("module_usage", t =>
            {
                // ‏`module >= 1` لا `between 1 and 13`: عضوٌ جديد في BabelModule يجعل
                // الحدّ الأعلى المُثبَّت يرفض قياس وحدةٍ حقيقية — أي يُسقط بيانات فوترة
                // بسبب هجرةٍ لم تُكتب. والتحقّق من كون القيمة معرَّفة يقع عند **القراءة**.
                t.HasCheckConstraint("ck_module_usage_module", "module >= 1");
                t.HasCheckConstraint("ck_module_usage_quantity", "quantity >= 0");
                t.HasCheckConstraint("ck_module_usage_operation_not_blank", "length(btrim(operation)) > 0");
            });

            entity.HasKey(row => row.SequenceNo).HasName("pk_module_usage");
            entity.Property(row => row.SequenceNo).HasColumnName("sequence_no").ValueGeneratedOnAdd();
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.Module).HasColumnName("module");
            entity.Property(row => row.Operation).HasColumnName("operation").HasMaxLength(200).IsRequired();
            entity.Property(row => row.ActorId).HasColumnName("actor_id");
            entity.Property(row => row.OccurredAt).HasColumnName("occurred_at");
            entity.Property(row => row.Quantity).HasColumnName("quantity");

            entity.HasIndex(row => new { row.TenantId, row.OccurredAt })
                  .HasDatabaseName("ix_module_usage_tenant_occurred");
        });

        modelBuilder.Entity<UserActivityRow>(entity =>
        {
            entity.ToTable("user_activity", t =>
            {
                t.HasCheckConstraint("ck_user_activity_module", "module >= 1");
                t.HasCheckConstraint("ck_user_activity_activity_not_blank", "length(btrim(activity)) > 0");
                t.HasCheckConstraint(
                    "ck_user_activity_entitlement_state",
                    "entitlement_state in ('not_entitled','read_only','entitled')");
            });

            entity.HasKey(row => row.SequenceNo).HasName("pk_user_activity");
            entity.Property(row => row.SequenceNo).HasColumnName("sequence_no").ValueGeneratedOnAdd();
            entity.Property(row => row.TenantId).HasColumnName("tenant_id");
            entity.Property(row => row.UserId).HasColumnName("user_id");
            entity.Property(row => row.Module).HasColumnName("module");
            entity.Property(row => row.Activity).HasColumnName("activity").HasMaxLength(200).IsRequired();
            entity.Property(row => row.OccurredAt).HasColumnName("occurred_at");
            entity.Property(row => row.EntitlementState)
                  .HasColumnName("entitlement_state").HasMaxLength(16).IsRequired();

            entity.HasIndex(row => new { row.TenantId, row.OccurredAt })
                  .HasDatabaseName("ix_user_activity_tenant_occurred");
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
