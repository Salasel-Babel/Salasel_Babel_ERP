using Microsoft.EntityFrameworkCore;

namespace Babel.Ledger.Persistence;

/// <summary>
/// جداول الدفتر. <c>internal</c>: <b>القاعدة 1</b> مفروضة بثلاث طبقات، وهذه الثانية.
/// <list type="number">
///   <item>لا مرجع مشروع من أي وحدة أفقية إلى Babel.Ledger — لا يوجد ما يُستدعى أصلاً.</item>
///   <item>أنواع الاستمرارية <c>internal</c> — لا يراها حتى الجذر التركيبي.</item>
///   <item>صلاحيات PostgreSQL: الدور التطبيقي <c>INSERT</c> و<c>SELECT</c> فقط،
///         مع <c>REVOKE UPDATE, DELETE, TRUNCATE</c> والهجرات بدور مالك منفصل
///         (ADR-0003 — مقيس، رمز الرفض 42501).</item>
/// </list>
/// أي طبقة وحدها قابلة للالتفاف؛ الثلاث معاً ليست كذلك.
/// <para>
/// <b>ولاحظ ما لا يفعله هذا السياق:</b> لا يكتب قيداً. الكتابة كلها عبر
/// <c>ledger.post_entry</c> — مكالمة خادم واحدة، صفر ذهاب وإياب بين أخذ القفل
/// والـCOMMIT (فخ-14). السياق هنا للقراءة وللهجرات فقط، وهذا هو سبب بقاء
/// <c>SaveChanges</c> بلا استعمال في مسار الترحيل.
/// </para>
/// </summary>
internal sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<AccountRow> Accounts => Set<AccountRow>();

    public DbSet<PostingRoleRow> PostingRoles => Set<PostingRoleRow>();

    public DbSet<RoleAccountMapRow> RoleAccountMap => Set<RoleAccountMapRow>();

    public DbSet<PropertyDimensionRow> PropertyDimensions => Set<PropertyDimensionRow>();

    public DbSet<FiscalPeriodRow> FiscalPeriods => Set<FiscalPeriodRow>();

    public DbSet<PostingCounterRow> PostingCounters => Set<PostingCounterRow>();

    public DbSet<JournalEntryRow> JournalEntries => Set<JournalEntryRow>();

    public DbSet<JournalLineRow> JournalLines => Set<JournalLineRow>();

    public DbSet<ChainLinkRow> ChainLinks => Set<ChainLinkRow>();

    public DbSet<AccountBalanceRow> AccountBalances => Set<AccountBalanceRow>();

    public DbSet<ProcessEventRow> ProcessEvents => Set<ProcessEventRow>();

    public DbSet<NameTranslationRow> NameTranslations => Set<NameTranslationRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("ledger");

        // ── دليل الحسابات ─────────────────────────────────────────────────
        // عمودان للاسم العربي عمداً (فخ-26): name_ar هو الحقل المُوقَّع كما أدخله
        // المستخدم، و name_ar_search مشتقّ للبحث ولا يدخل التجزئة أبداً.
        modelBuilder.Entity<AccountRow>(entity =>
        {
            entity.ToTable("account", t =>
            {
                t.HasCheckConstraint("ck_account_type",
                    "account_type in ('asset','liability','equity','revenue','expense')");
                t.HasCheckConstraint("ck_account_natural_side", "natural_side in ('debit','credit')");
                t.HasCheckConstraint("ck_account_level", "account_level between 1 and 4");
                // الشجرة تُشتق من الرمز نفسه: مستوى الحساب طول رمزه، وأبوه بادئة رمزه.
                t.HasCheckConstraint("ck_account_parent_matches_code",
                    "(parent_code is null and account_level = 1) or (parent_code is not null "
                    + "and account_code like parent_code || '%' and length(account_code) > length(parent_code))");
                t.HasCheckConstraint("ck_account_level_matches_code",
                    "(account_level = 1 and length(account_code) = 1) or (account_level = 2 and length(account_code) = 2) "
                    + "or (account_level = 3 and length(account_code) = 3) or (account_level = 4 and length(account_code) >= 4)");
                // GR-COA-001 من باب البيانات: التجميعي لا يُرحَّل عليه.
                t.HasCheckConstraint("ck_account_postable_is_leaf", "not is_postable or account_level = 4");
                t.HasCheckConstraint("ck_account_currency_mode", "currency_mode in ('any','company_only','fixed')");
                t.HasCheckConstraint("ck_account_fixed_currency",
                    "currency_mode <> 'fixed' or currency_code is not null");
                // الاسم العربي هو **السجلّ** ومرجع الارتداد (ADR-0021): فارغاً يترك
                // رقم حساب بلا معنى في تقرير مدقّق، ولا يوجد ما يرتدّ إليه العرض.
                t.HasCheckConstraint("ck_account_name_ar_not_blank", "length(btrim(name_ar)) > 0");
            });

            entity.HasKey(row => new { row.CompanyId, row.Code }).HasName("pk_account");
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.Code).HasColumnName("account_code");
            entity.Property(row => row.NameAr).HasColumnName("name_ar");
            entity.Property(row => row.NameArSearch).HasColumnName("name_ar_search").HasDefaultValue(string.Empty);
            entity.Property(row => row.ParentCode).HasColumnName("parent_code");
            entity.Property(row => row.Level).HasColumnName("account_level");
            entity.Property(row => row.AccountType).HasColumnName("account_type");
            entity.Property(row => row.NaturalSide).HasColumnName("natural_side");
            entity.Property(row => row.IsPostable).HasColumnName("is_postable");
            entity.Property(row => row.IsContra).HasColumnName("is_contra").HasDefaultValue(false);
            entity.Property(row => row.StatementSection).HasColumnName("statement_section");
            entity.Property(row => row.SubledgerType).HasColumnName("subledger_type").HasDefaultValue("none");
            entity.Property(row => row.RequiredDimensions).HasColumnName("required_dimensions")
                  .HasColumnType("text[]").HasDefaultValueSql("'{}'");
            entity.Property(row => row.CurrencyMode).HasColumnName("currency_mode").HasDefaultValue("any");
            entity.Property(row => row.CurrencyCode).HasColumnName("currency_code");
            entity.Property(row => row.IsProtected).HasColumnName("is_protected").HasDefaultValue(false);
            entity.Property(row => row.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(row => row.Status).HasColumnName("status").HasDefaultValue("drafted");
            entity.Property(row => row.SourceRef).HasColumnName("source_ref");
            entity.Property(row => row.CaveatAr).HasColumnName("caveat_ar");
            entity.Property(row => row.CaveatEn).HasColumnName("caveat_en");
            entity.HasIndex(row => new { row.CompanyId, row.ParentCode }).HasDatabaseName("ix_account_parent");
            entity.HasIndex(row => new { row.CompanyId, row.NameArSearch }).HasDatabaseName("ix_account_search");
        });

        // ── كتالوج الأدوار وخريطة المستأجر ────────────────────────────────
        // سطر المصفوفة يذكر دوراً لا رمز حساب. الخريطة هي ما يجعل مستأجرَين
        // يُنتجان حسابين مختلفين من الحدث نفسه دون سطر كود واحد.
        modelBuilder.Entity<PostingRoleRow>(entity =>
        {
            entity.ToTable("posting_role", t =>
            {
                t.HasCheckConstraint(
                    "ck_posting_role_side", "expected_side is null or expected_side in ('debit','credit')");
                t.HasCheckConstraint("ck_posting_role_name_ar_not_blank", "length(btrim(name_ar)) > 0");
            });
            entity.HasKey(row => row.RoleCode).HasName("pk_posting_role");
            entity.Property(row => row.RoleCode).HasColumnName("role_code");
            entity.Property(row => row.NameAr).HasColumnName("name_ar");
            entity.Property(row => row.ExpectedAccountType).HasColumnName("expected_account_type");
            entity.Property(row => row.ExpectedSide).HasColumnName("expected_side");
            entity.Property(row => row.Status).HasColumnName("status").HasDefaultValue("drafted");
            entity.Property(row => row.NoteAr).HasColumnName("note_ar");
            entity.Property(row => row.NoteEn).HasColumnName("note_en");
        });

        modelBuilder.Entity<RoleAccountMapRow>(entity =>
        {
            entity.ToTable("role_account_map");
            entity.HasKey(row => new { row.CompanyId, row.RoleCode, row.Qualifier }).HasName("pk_role_account_map");
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.RoleCode).HasColumnName("role_code");
            // '*' هو المؤهّل الافتراضي. لكل دور صفّ بـ'*' إلزاماً، وإلا وقف المحرك (V01).
            entity.Property(row => row.Qualifier).HasColumnName("qualifier");
            entity.Property(row => row.Code).HasColumnName("account_code");
            entity.Property(row => row.Status).HasColumnName("status").HasDefaultValue("drafted");
            entity.Property(row => row.NoteAr).HasColumnName("note_ar");
            entity.Property(row => row.NoteEn).HasColumnName("note_en");
            entity.HasOne<PostingRoleRow>().WithMany().HasForeignKey(row => row.RoleCode)
                  .HasConstraintName("fk_role_account_map_role");
            entity.HasOne<AccountRow>().WithMany().HasForeignKey(row => new { row.CompanyId, row.Code })
                  .HasConstraintName("fk_role_account_map_account");
        });

        // ── سجلّ أبعاد العقار — الطبقة الثالثة لقاعدة الحجب GR-RE-001 ──────
        modelBuilder.Entity<PropertyDimensionRow>(entity =>
        {
            entity.ToTable("property_dimension", t =>
            {
                t.HasCheckConstraint(
                    "ck_property_ownership_model", "ownership_model in ('own_property','managed_for_others')");
                t.HasCheckConstraint("ck_property_name_ar_not_blank", "length(btrim(name_ar)) > 0");
            });
            entity.HasKey(row => new { row.CompanyId, row.PropertyId }).HasName("pk_property_dimension");
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.PropertyId).HasColumnName("property_id");
            entity.Property(row => row.OwnershipModel).HasColumnName("ownership_model");
            entity.Property(row => row.NameAr).HasColumnName("name_ar");
        });

        // ── الفترات المالية ───────────────────────────────────────────────
        modelBuilder.Entity<FiscalPeriodRow>(entity =>
        {
            entity.ToTable("fiscal_period", t =>
            {
                t.HasCheckConstraint("ck_fiscal_period_state", "state in ('open','closed','permanently_closed')");
                t.HasCheckConstraint("ck_fiscal_period_range", "ends_on >= starts_on");
                t.HasCheckConstraint("ck_fiscal_period_name_ar_not_blank", "length(btrim(name_ar)) > 0");
            });
            entity.HasKey(row => new { row.CompanyId, row.FiscalYear, row.PeriodNo }).HasName("pk_fiscal_period");
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.FiscalYear).HasColumnName("fiscal_year");
            entity.Property(row => row.PeriodNo).HasColumnName("period_no");
            entity.Property(row => row.PeriodCode).HasColumnName("period_code");
            entity.Property(row => row.StartsOn).HasColumnName("starts_on");
            entity.Property(row => row.EndsOn).HasColumnName("ends_on");
            entity.Property(row => row.State).HasColumnName("state").HasDefaultValue("open");
            entity.Property(row => row.NameAr).HasColumnName("name_ar");
            entity.Property(row => row.ClosedAt).HasColumnName("closed_at");
            entity.Property(row => row.ClosedBy).HasColumnName("closed_by");
            entity.HasIndex(row => new { row.CompanyId, row.PeriodCode }).IsUnique().HasDatabaseName("uq_fiscal_period_code");
        });

        // ── الترجمات: صفوف لا أعمدة (ADR-0021 بند 2) ──────────────────────
        // العربي عمودٌ على الكيان لأنه السجلّ؛ وكل لغة أخرى صفٌّ هنا. واللغة الخامسة
        // إدخالُ صفوف لا هجرةُ مخطّط — وهو الفرق العملي بين «متعدّد» و«ثنائي».
        modelBuilder.Entity<NameTranslationRow>(entity =>
        {
            entity.ToTable("name_translation", t =>
            {
                t.HasCheckConstraint(
                    "ck_name_translation_kind",
                    "entity_kind in ('account','fiscal_period','posting_role','property')");

                // النطاق مكتوبٌ في المخطّط لا في اتفاق: الأدوار عامّة على مستوى المنتج
                // ولا شركة لها، وما سواها مملوك لشركة بعينها. والقيد ثنائي الاتجاه
                // فيمنع الخلط في الجهتين معاً.
                t.HasCheckConstraint(
                    "ck_name_translation_scope",
                    "(entity_kind = 'posting_role') = (company_id = '00000000-0000-0000-0000-000000000000'::uuid)");

                // العربية سجلٌّ لا ترجمة: صفٌّ بوسم عربي يُنتج اسمين عربيين لكيان واحد
                // لا شيء يجعلهما يتطابقان — والحارس هنا لا في الشيفرة وحدها.
                t.HasCheckConstraint(
                    "ck_name_translation_not_arabic",
                    "lower(language_tag) <> 'ar' and lower(language_tag) not like 'ar-%'");

                // الوسم معرّف BCP-47 لاتيني: يعبر مسار HTTP ومفاتيح الجداول.
                t.HasCheckConstraint(
                    "ck_name_translation_tag_shape",
                    "language_tag ~ '^[A-Za-z][A-Za-z0-9]*(-[A-Za-z0-9]+)*$' and length(language_tag) <= 35");

                // ترجمة فارغة أسوأ من غيابها: الغياب يرتدّ إلى العربية، والفراغ يُعرض فراغاً.
                t.HasCheckConstraint("ck_name_translation_name_not_blank", "length(btrim(name)) > 0");
                t.HasCheckConstraint("ck_name_translation_key_not_blank", "length(btrim(entity_key)) > 0");
            });

            entity.HasKey(row => new { row.CompanyId, row.EntityKind, row.EntityKey, row.LanguageTag })
                  .HasName("pk_name_translation");
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.EntityKind).HasColumnName("entity_kind");
            entity.Property(row => row.EntityKey).HasColumnName("entity_key");
            entity.Property(row => row.LanguageTag).HasColumnName("language_tag");
            entity.Property(row => row.Name).HasColumnName("name");

            // قراءة الشاشة تسأل «كل ترجمات كيانات هذا النوع لهذه الشركة» — وهو ما يخدمه
            // المفتاح الأساسي نفسه ببادئته، فلا فهرس ثانٍ يُصان بلا داعٍ.
            entity.HasIndex(row => new { row.CompanyId, row.EntityKind, row.LanguageTag })
                  .HasDatabaseName("ix_name_translation_lookup");
        });

        // ── العدّاد بلا فجوات — صفّ لكل (شركة × دفتر × سنة مالية) ──────────
        // ليس SEQUENCE: التسلسل غير معاملاتي ويُهدر أرقاماً عند التراجع (فخ-12 · ADR-0008).
        // والنطاق ليس عاماً: صفّ عدّاد واحد للمنشأة كلها صفٌّ ساخن يقتل الإنتاجية (فخ-15).
        modelBuilder.Entity<PostingCounterRow>(entity =>
        {
            entity.ToTable("posting_counter", t => t.HasCheckConstraint(
                "ck_posting_counter_positive", "next_entry_no >= 1 and next_chain_seq >= 1"));
            entity.HasKey(row => new { row.CompanyId, row.BookId, row.FiscalYear }).HasName("pk_posting_counter");
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.BookId).HasColumnName("book_id");
            entity.Property(row => row.FiscalYear).HasColumnName("fiscal_year");
            entity.Property(row => row.NextEntryNo).HasColumnName("next_entry_no").HasDefaultValue(1L);
            entity.Property(row => row.NextChainSeq).HasColumnName("next_chain_seq").HasDefaultValue(1L);
        });

        // ── الدفتر: يُضاف إليه فقط ────────────────────────────────────────
        modelBuilder.Entity<JournalEntryRow>(entity =>
        {
            entity.ToTable("journal_entry", t =>
            {
                t.HasCheckConstraint("ck_journal_entry_status", "status in ('POSTED','REVERSAL')");
                t.HasCheckConstraint("ck_journal_entry_generation", "posting_generation >= 1");

                // رمز الحدث جزء من هوية الترحيل، ورمزٌ فارغ يُعيد حدثين حدثاً واحداً
                // فيبتلع المفتاح الثاني منهما بصمت (D-3). العمود NOT NULL بحكم
                // النوع، وهذا القيد يمنع الفراغ والمسافات وحدها.
                t.HasCheckConstraint("ck_journal_entry_event_code", "length(btrim(event_code)) > 0");
                t.HasCheckConstraint("ck_journal_entry_reversal_has_reason",
                    "status <> 'REVERSAL' or (reverses_entry_id is not null and reversal_reason_ar is not null)");
            });
            entity.HasKey(row => row.EntryId).HasName("pk_journal_entry");
            entity.Property(row => row.EntryId).HasColumnName("entry_id");
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.BookId).HasColumnName("book_id");
            entity.Property(row => row.FiscalYear).HasColumnName("fiscal_year");
            entity.Property(row => row.EntryNo).HasColumnName("entry_no");
            entity.Property(row => row.EntryDate).HasColumnName("entry_date");
            entity.Property(row => row.PeriodCode).HasColumnName("period_code");
            entity.Property(row => row.PostedAt).HasColumnName("posted_at");
            entity.Property(row => row.Status).HasColumnName("status");
            entity.Property(row => row.Actor).HasColumnName("actor");
            entity.Property(row => row.ActorSearch).HasColumnName("actor_search").HasDefaultValue(string.Empty);
            entity.Property(row => row.Memo).HasColumnName("memo").HasDefaultValue(string.Empty);
            entity.Property(row => row.MemoAr).HasColumnName("memo_ar").HasDefaultValue(string.Empty);
            entity.Property(row => row.MemoArSearch).HasColumnName("memo_ar_search").HasDefaultValue(string.Empty);
            entity.Property(row => row.SourceModule).HasColumnName("source_module");
            entity.Property(row => row.SourceDocType).HasColumnName("source_doc_type");
            entity.Property(row => row.SourceDocId).HasColumnName("source_doc_id");
            entity.Property(row => row.PostingTriggerCode).HasColumnName("posting_trigger_code");
            entity.Property(row => row.PostingGeneration).HasColumnName("posting_generation").HasDefaultValue(1);
            // ولا قيمة افتراضية: الافتراضي الفارغ هو بالضبط ما يُسقط الحدث الثاني.
            entity.Property(row => row.EventCode).HasColumnName("event_code");
            entity.Property(row => row.IdempotencyKey).HasColumnName("idempotency_key");
            entity.Property(row => row.Currency).HasColumnName("currency");
            entity.Property(row => row.ReversesEntryId).HasColumnName("reverses_entry_id");
            entity.Property(row => row.ReversalReasonAr).HasColumnName("reversal_reason_ar");
            entity.Property(row => row.ReversalReasonEn).HasColumnName("reversal_reason_en");
            entity.Property(row => row.ClosedPeriodPermission).HasColumnName("closed_period_permission");
            entity.Property(row => row.ClosedPeriodAuthoriser).HasColumnName("closed_period_authoriser");

            entity.HasOne<JournalEntryRow>().WithMany().HasForeignKey(row => row.ReversesEntryId)
                  .HasConstraintName("fk_journal_entry_reverses");

            entity.HasIndex(row => new { row.CompanyId, row.BookId, row.FiscalYear, row.EntryNo })
                  .IsUnique().HasDatabaseName("uq_journal_entry_no");

            // ═══ الإحكام: لكل قيد، ومستقلٌّ عن الترتيب ═══
            // ممنوع منعاً باتاً أي حارس «تسلسل مُطبَّق تصاعدي لكل حساب»
            // (WHERE applied_seq < @seq): قيس وهو يُسقط بصمت قيداً وصل بعد أحدث
            // منه فضاعت 500 من 1500 ريال — بخس 33٪ — ومزامنة نقاط البيع دون
            // اتصال تُسلّم خارج الترتيب بطبيعتها (فخ-13).
            //
            // ورمز الحدث **داخل** المفتاح (D-3): المستند الواحد يُنتج حدثين عند
            // الإطلاق نفسه في حالات يومية لا استثنائية — فاتورة مبيعات تعترف
            // بالإيراد وتُنزل المخزون بالتكلفة؛ وفاتورة مورد تُثبت الالتزام
            // وتعترف بفرق سعر مقابل استلام سابق؛ ودفعةٌ تُسدّد التزاماً وتسجّل
            // رسماً بنكياً؛ ومسيرُ رواتب يُثبت الأجر وحصة المنشأة في التأمينات.
            // وبلا رمز الحدث في المفتاح كان الثاني منهما يُعدّ «مُرحَّلاً سلفاً»
            // فلا يُكتب ولا يُبلَّغ عنه خطأ: الدفتر يبقى متوازناً، والسلسلة تبقى
            // صحيحة، والعَرَض الوحيد دفتر مساعد لا يطابق حسابه الضابط.
            // ‏D-3 · ADR-0016.
            entity.HasIndex(row => new
            {
                row.CompanyId, row.SourceDocType, row.SourceDocId, row.PostingTriggerCode, row.PostingGeneration,
                row.EventCode,
            }).IsUnique().HasDatabaseName("uq_posting_identity");

            entity.HasIndex(row => new { row.CompanyId, row.BookId, row.PeriodCode })
                  .HasDatabaseName("ix_journal_entry_period");
            entity.HasIndex(row => new { row.CompanyId, row.SourceDocType, row.SourceDocId })
                  .HasDatabaseName("ix_journal_entry_source");
        });

        modelBuilder.Entity<JournalLineRow>(entity =>
        {
            entity.ToTable("journal_line", t =>
            {
                t.HasCheckConstraint("ck_journal_line_sign",
                    "debit >= 0 and credit >= 0 and debit_company >= 0 and credit_company >= 0");
                t.HasCheckConstraint("ck_journal_line_one_side", "debit = 0 or credit = 0");
                t.HasCheckConstraint("ck_journal_line_company_side", "debit_company = 0 or credit_company = 0");
                t.HasCheckConstraint("ck_journal_line_fx_positive", "fx_rate > 0");

                // ‏**ADR-0026 مفروضاً على المخطّط لا على المستدعي.** لكل منشأة مركز
                // تكلفة واحد على الأقل، ولا سطر بلا مركز. والقيد هنا — لا في C# وحدها —
                // لأن الثابتة يجب أن تصمد أمام **أي** كاتب: نصّ SQL يدوي، أو أداة
                // استيراد، أو هجرة مستقبلية سهت. والخواء يُمنع مع الفراغ: نصٌّ من
                // مسافات هو غياب في ثوب حضور.
                t.HasCheckConstraint(
                    "ck_journal_line_cost_center_present",
                    "cost_center_id is not null and length(btrim(cost_center_id)) > 0");
            });
            entity.HasKey(row => row.LineId).HasName("pk_journal_line");
            entity.Property(row => row.LineId).HasColumnName("line_id");
            entity.Property(row => row.EntryId).HasColumnName("entry_id");
            entity.Property(row => row.LineNo).HasColumnName("line_no");
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.Code).HasColumnName("account_code");
            entity.Property(row => row.RoleCode).HasColumnName("role_code").HasDefaultValue(string.Empty);
            entity.Property(row => row.Qualifier).HasColumnName("qualifier").HasDefaultValue("*");
            // المقياس القانوني للمبالغ: numeric(19,4) في كل النطاق (فخ-17).
            // ولا float ولا double في أي مكان، بما في ذلك JSON (Rule04).
            entity.Property(row => row.Debit).HasColumnName("debit").HasColumnType("numeric(19,4)").HasDefaultValue(0m);
            entity.Property(row => row.Credit).HasColumnName("credit").HasColumnType("numeric(19,4)").HasDefaultValue(0m);
            entity.Property(row => row.Currency).HasColumnName("currency");
            entity.Property(row => row.FxRate).HasColumnName("fx_rate").HasColumnType("numeric(19,8)").HasDefaultValue(1m);
            entity.Property(row => row.DebitCompany).HasColumnName("debit_company").HasColumnType("numeric(19,4)").HasDefaultValue(0m);
            entity.Property(row => row.CreditCompany).HasColumnName("credit_company").HasColumnType("numeric(19,4)").HasDefaultValue(0m);
            entity.Property(row => row.BranchId).HasColumnName("branch_id");
            // العمود يبقى `null`-able في SQL: `set not null` يتطلّب فحص الجدول كلّه،
            // وهو يسقط على دفتر عامر سبق الثابتة — وذاك دفترٌ نرفض إعادة كتابته.
            // والقيد أعلاه يحمل الضمان نفسه لكل كتابة جديدة.
            entity.Property(row => row.CostCenterId).HasColumnName("cost_center_id");
            entity.Property(row => row.ProjectId).HasColumnName("project_id");
            entity.Property(row => row.PropertyId).HasColumnName("property_id");
            entity.Property(row => row.UnitId).HasColumnName("unit_id");
            // البُعدان اللذان أظهرهما أول تنفيذ فعلي للمصفوفة: 14 سطر مصفوفة تعلن
            // warehouse وثلاثة حسابات تفرضه، وسطران يعلنان boq_item. بلا عمودين
            // لهما يستحيل ترحيل تلك السطور أصلاً (انظر Persistence/README).
            entity.Property(row => row.WarehouseId).HasColumnName("warehouse_id");
            entity.Property(row => row.BoqItemId).HasColumnName("boq_item_id");
            entity.Property(row => row.SubledgerKind).HasColumnName("subledger_kind").HasDefaultValue("none");
            entity.Property(row => row.SubledgerPartyId).HasColumnName("subledger_party_id");
            entity.Property(row => row.Description).HasColumnName("description").HasDefaultValue(string.Empty);
            entity.Property(row => row.DescriptionAr).HasColumnName("description_ar").HasDefaultValue(string.Empty);
            entity.Property(row => row.DescriptionArSearch).HasColumnName("description_ar_search").HasDefaultValue(string.Empty);

            entity.HasOne(row => row.Entry).WithMany(row => row.Lines).HasForeignKey(row => row.EntryId)
                  .HasConstraintName("fk_journal_line_entry");
            entity.HasOne<AccountRow>().WithMany().HasForeignKey(row => new { row.CompanyId, row.Code })
                  .HasConstraintName("fk_journal_line_account");

            entity.HasIndex(row => new { row.EntryId, row.LineNo }).IsUnique().HasDatabaseName("uq_journal_line_no");
            entity.HasIndex(row => new { row.CompanyId, row.Code }).HasDatabaseName("ix_journal_line_account");
            entity.HasIndex(row => new { row.CompanyId, row.PropertyId }).HasDatabaseName("ix_journal_line_property");
        });

        // ── سلسلة البصمات — جدول مستقل، ونطاقه = نطاق الترقيم ─────────────
        // canonical_bytes تُخزَّن كما هي ولا تُشتقّ مجدداً: أشيع عطل إنتاجي في نظام
        // e-Defter التركي هو عدم تطابق المخزَّن مع المعتمَد بسبب إعادة التوليد (فخ-20).
        modelBuilder.Entity<ChainLinkRow>(entity =>
        {
            entity.ToTable("chain_link", t =>
            {
                t.HasCheckConstraint("ck_chain_link_hash_length",
                    "octet_length(entry_hash) = 32 and octet_length(prev_hash) = 32");
                t.HasCheckConstraint("ck_chain_link_seq_positive", "chain_seq >= 1");
            });
            entity.HasKey(row => new { row.CompanyId, row.BookId, row.FiscalYear, row.ChainSeq }).HasName("pk_chain_link");
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.BookId).HasColumnName("book_id");
            entity.Property(row => row.FiscalYear).HasColumnName("fiscal_year");
            entity.Property(row => row.ChainSeq).HasColumnName("chain_seq");
            entity.Property(row => row.EntryId).HasColumnName("entry_id");
            entity.Property(row => row.CanonVersion).HasColumnName("canon_version");
            entity.Property(row => row.PreviousHash).HasColumnName("prev_hash");
            entity.Property(row => row.EntryHash).HasColumnName("entry_hash");
            entity.Property(row => row.CanonicalBytes).HasColumnName("canonical_bytes");
            entity.HasIndex(row => row.EntryId).IsUnique().HasDatabaseName("uq_chain_link_entry");
            entity.HasOne<JournalEntryRow>().WithMany().HasForeignKey(row => row.EntryId)
                  .HasConstraintName("fk_chain_link_entry");
        });

        // ── إسقاط الأرصدة — يُصان داخل معاملة الترحيل نفسها (ADR-0004) ─────
        // الرصيد ليس تقريراً؛ هو الرقم الذي يمنع السحب على المكشوف.
        modelBuilder.Entity<AccountBalanceRow>(entity =>
        {
            entity.ToTable("account_balance", t => t.HasCheckConstraint(
                "ck_account_balance_sign", "debit >= 0 and credit >= 0"));
            entity.HasKey(row => new { row.CompanyId, row.BookId, row.PeriodCode, row.Code }).HasName("pk_account_balance");
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.BookId).HasColumnName("book_id");
            entity.Property(row => row.PeriodCode).HasColumnName("period_code");
            entity.Property(row => row.Code).HasColumnName("account_code");
            entity.Property(row => row.Debit).HasColumnName("debit").HasColumnType("numeric(19,4)").HasDefaultValue(0m);
            entity.Property(row => row.Credit).HasColumnName("credit").HasColumnType("numeric(19,4)").HasDefaultValue(0m);
            entity.Property(row => row.EntryCount).HasColumnName("entry_count").HasDefaultValue(0L);
            entity.Property(row => row.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne<AccountRow>().WithMany().HasForeignKey(row => new { row.CompanyId, row.Code })
                  .HasConstraintName("fk_account_balance_account");
        });

        // ── سجلّ العمليات — يسجّل ما فُشل أيضاً ────────────────────────────
        // مخزن الأحداث يسجّل ما نجح فقط بحكم البناء (فخ-08). والمرفوض هو ما يُثبت
        // أن الرقابة عملت. المفتاح identity مقبول هنا عمداً: رقم داخلي لا يراه
        // مدقّق، والفجوة فيه لا تعني شيئاً — بخلاف رقم القيد (فخ-12).
        modelBuilder.Entity<ProcessEventRow>(entity =>
        {
            entity.ToTable("process_event");
            entity.HasKey(row => row.ProcessEventId).HasName("pk_process_event");
            entity.Property(row => row.ProcessEventId).HasColumnName("process_event_id").UseIdentityAlwaysColumn();
            entity.Property(row => row.CompanyId).HasColumnName("company_id");
            entity.Property(row => row.OccurredAt).HasColumnName("occurred_at").HasDefaultValueSql("now()");
            entity.Property(row => row.Kind).HasColumnName("kind");
            entity.Property(row => row.Outcome).HasColumnName("outcome");
            entity.Property(row => row.Actor).HasColumnName("actor");
            entity.Property(row => row.EventCode).HasColumnName("event_code");
            entity.Property(row => row.SourceDocType).HasColumnName("source_doc_type");
            entity.Property(row => row.SourceDocId).HasColumnName("source_doc_id");
            entity.Property(row => row.ReasonCode).HasColumnName("reason_code");
            entity.Property(row => row.MessageAr).HasColumnName("message_ar");
            entity.Property(row => row.MessageEn).HasColumnName("message_en");
            entity.Property(row => row.Detail).HasColumnName("detail");
            entity.HasIndex(row => new { row.CompanyId, row.OccurredAt }).HasDatabaseName("ix_process_event_company");
        });
    }
}
