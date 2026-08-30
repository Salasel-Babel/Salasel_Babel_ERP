using Microsoft.EntityFrameworkCore;

namespace Babel.Hr.Persistence;

/// <summary>
/// جداول الموارد البشرية. <c>internal</c>: كل وحدة تملك جداولها، والقراءة العابرة عبر
/// واجهات معلنة لا عبر <c>JOIN</c> مباشر (القاعدة 5).
/// <para>
/// ولاحظ ما ليس هنا: لا مفتاح خارجي إلى <c>ledger.account</c>، ولا كيان من وحدة أخرى،
/// ولا عمود يحمل رقم حساب، ولا عمود <c>name_en</c> — الترجمة صفٌّ لا عمود (القاعدة 14).
/// </para>
/// <para>
/// وكل عمود مالي <c>numeric(19,4)</c> صراحةً (فخ-17)، وكل نسبة <c>numeric(19,8)</c>:
/// النسبة ليست مبلغاً ولا تُقرَّب إلى الهللة.
/// </para>
/// </summary>
internal sealed class HrDbContext(DbContextOptions<HrDbContext> options) : DbContext(options)
{
    private const string Money = "numeric(19,4)";

    private const string Rate = "numeric(19,8)";

    public DbSet<EmployeeRow> Employees => Set<EmployeeRow>();

    public DbSet<EmployeeIdentityRow> Identities => Set<EmployeeIdentityRow>();

    public DbSet<EmployeeNameTranslationRow> EmployeeNames => Set<EmployeeNameTranslationRow>();

    public DbSet<EmploymentRow> Employments => Set<EmploymentRow>();

    public DbSet<PayComponentRow> PayComponents => Set<PayComponentRow>();

    public DbSet<PayElementRow> PayElements => Set<PayElementRow>();

    public DbSet<PayrollSettingsRow> PayrollSettings => Set<PayrollSettingsRow>();

    public DbSet<PayrollRunRow> PayrollRuns => Set<PayrollRunRow>();

    public DbSet<PayslipRow> Payslips => Set<PayslipRow>();

    public DbSet<PayslipComponentRow> PayslipComponents => Set<PayslipComponentRow>();

    public DbSet<PayrollPaymentRow> PayrollPayments => Set<PayrollPaymentRow>();

    public DbSet<PayrollPaymentLineRow> PayrollPaymentLines => Set<PayrollPaymentLineRow>();

    public DbSet<SocialInsurancePaymentRow> SocialInsurancePayments => Set<SocialInsurancePaymentRow>();

    public DbSet<EmployeeDeductionRow> Deductions => Set<EmployeeDeductionRow>();

    public DbSet<EmployeeAdvanceRow> Advances => Set<EmployeeAdvanceRow>();

    public DbSet<AdvanceInstalmentRow> AdvanceInstalments => Set<AdvanceInstalmentRow>();

    public DbSet<EndOfServiceProvisionRow> Provisions => Set<EndOfServiceProvisionRow>();

    public DbSet<EndOfServiceMovementRow> ProvisionMovements => Set<EndOfServiceMovementRow>();

    public DbSet<EndOfServiceSettlementRow> Settlements => Set<EndOfServiceSettlementRow>();

    public DbSet<DocumentPostingRow> Postings => Set<DocumentPostingRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("hr");

        modelBuilder.Entity<EmployeeRow>(entity =>
        {
            entity.ToTable("employee");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Code).HasMaxLength(64).IsRequired();
            entity.Property(row => row.NameAr).HasMaxLength(200).IsRequired();
            entity.Property(row => row.CostCenterId).HasMaxLength(64);
            entity.Property(row => row.ClassCode).HasMaxLength(64).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.Code }).IsUnique().HasDatabaseName("uq_hr_employee_code");
        });

        modelBuilder.Entity<EmployeeIdentityRow>(entity =>
        {
            entity.ToTable("employee_identity");
            entity.HasKey(row => new { row.TenantId, row.EmployeeId });
            entity.Property(row => row.NationalId).HasMaxLength(32).IsRequired();
            entity.Property(row => row.Iban).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<EmployeeNameTranslationRow>(entity =>
        {
            entity.ToTable("employee_name_translation");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.EmployeeCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.Locale).HasMaxLength(35).IsRequired();
            entity.Property(row => row.Text).HasMaxLength(256).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.EmployeeCode, row.Locale })
                  .IsUnique().HasDatabaseName("uq_hr_employee_name_translation");
        });

        modelBuilder.Entity<EmploymentRow>(entity =>
        {
            entity.ToTable("employment");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.TerminationReasonKey).HasMaxLength(64);
            entity.HasIndex(row => new { row.TenantId, row.EmployeeId, row.StartedOn })
                  .IsUnique().HasDatabaseName("uq_hr_employment_span");
        });

        modelBuilder.Entity<PayComponentRow>(entity =>
        {
            entity.ToTable("pay_component");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Code).HasMaxLength(64).IsRequired();
            entity.Property(row => row.NameAr).HasMaxLength(200).IsRequired();
            entity.Property(row => row.Kind).HasMaxLength(16).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.Code }).IsUnique().HasDatabaseName("uq_hr_pay_component_code");
        });

        modelBuilder.Entity<PayElementRow>(entity =>
        {
            entity.ToTable("pay_element");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.ComponentCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.Amount).HasColumnType(Money);

            // ‏**يُضاف إليه ولا يُعدَّل**: الزيادة صفٌّ جديد بتاريخ سريان، وإلا تعذّر
            // إعادة حساب مسيّر ماضٍ ليطابق قيده المُرحَّل.
            entity.HasIndex(row => new { row.TenantId, row.EmploymentId, row.ComponentCode, row.EffectiveFrom })
                  .IsUnique().HasDatabaseName("uq_hr_pay_element_effective");
        });

        modelBuilder.Entity<PayrollSettingsRow>(entity =>
        {
            entity.ToTable("payroll_settings");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.ClassCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.EmployerRate).HasColumnType(Rate);
            entity.Property(row => row.EmployeeRate).HasColumnType(Rate);
            entity.Property(row => row.MinimumContributoryWage).HasColumnType(Money);
            entity.Property(row => row.MaximumContributoryWage).HasColumnType(Money);
            entity.Property(row => row.ApprovedBy).HasMaxLength(64).IsRequired();
            entity.Property(row => row.SourceRef).HasMaxLength(400).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.ClassCode, row.EffectiveFrom })
                  .IsUnique().HasDatabaseName("uq_hr_payroll_settings_effective");

            // ‏**مرجعٌ فارغ يجعل الصفّ نسبةً بلا مصدر** — وهو بالضبط ما تمنعه هذه الوحدة:
            // النسبة تدخل من هنا وحدها، فمن يُدخلها يُسمّي مصدرها ومعتمِدها.
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_hr_payroll_settings_source_ref", """length(btrim("SourceRef")) > 0"""));
        });

        modelBuilder.Entity<PayrollRunRow>(entity =>
        {
            entity.ToTable("payroll_run");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.PeriodCode).HasMaxLength(7).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.GrossEntitlements).HasColumnType(Money);
            entity.Property(row => row.EmployerSocialInsurance).HasColumnType(Money);
            entity.Property(row => row.EmployeeSocialInsurance).HasColumnType(Money);
            entity.Property(row => row.AdvanceInstalment).HasColumnType(Money);
            entity.Property(row => row.Deductions).HasColumnType(Money);
            entity.Property(row => row.NetPayable).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number }).IsUnique().HasDatabaseName("uq_hr_payroll_run_number");

            // ‏**ولا فهرس فريد على (TenantId, PeriodCode) — عمداً، وحتى يُجاب سؤالٌ مفتوح.**
            // «هل يُسمح بأكثر من مسيّر مُرحَّل للفترة الواحدة؟» سؤالٌ يملكه المالك
            // (مسيّر خارج الدورة · مكافآت · دفعة تصحيحية)، وفهرسٌ اليوم بشرط
            // ‏RunKind='regular' يفترض جوابه ويُدخل «نوع المسيّر» مفهوماً في المخطّط.
            // فالمنع مؤقّتاً في خدمة التطبيق برسالة صريحة قابلة للرفع، والفهرس يُضاف
            // حين يُغلق البند. وإدخالُه لاحقاً على جدولٍ عامر أثقل — وذلك مُسجَّل ديناً.
            entity.HasIndex(row => new { row.TenantId, row.PeriodCode }).HasDatabaseName("ix_hr_payroll_run_period");
        });

        modelBuilder.Entity<PayslipRow>(entity =>
        {
            entity.ToTable("payslip");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.EmployeeCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.CostCenterId).HasMaxLength(64);
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.GrossEntitlements).HasColumnType(Money);
            entity.Property(row => row.EmployerSocialInsurance).HasColumnType(Money);
            entity.Property(row => row.EmployeeSocialInsurance).HasColumnType(Money);
            entity.Property(row => row.AdvanceInstalment).HasColumnType(Money);
            entity.Property(row => row.Deductions).HasColumnType(Money);
            entity.Property(row => row.NetPayable).HasColumnType(Money);
            entity.Property(row => row.ContributoryWage).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.RunId, row.EmployeeId })
                  .IsUnique().HasDatabaseName("uq_hr_payslip");

            // المتطابقة المعلَنة في المصفوفة، مفروضةً في القاعدة لا في الشيفرة وحدها:
            // net_payable = gross_entitlements - employee_social_insurance
            //               - advance_installment - deductions
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_hr_payslip_net_identity",
                """
                "NetPayable" = "GrossEntitlements" - "EmployeeSocialInsurance"
                               - "AdvanceInstalment" - "Deductions"
                """));
        });

        modelBuilder.Entity<PayslipComponentRow>(entity =>
        {
            entity.ToTable("payslip_component");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.ComponentCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.Kind).HasMaxLength(16).IsRequired();
            entity.Property(row => row.Amount).HasColumnType(Money);
            entity.HasIndex(row => new { row.PayslipId, row.LineNo })
                  .IsUnique().HasDatabaseName("uq_hr_payslip_component_line");
        });

        modelBuilder.Entity<PayrollPaymentRow>(entity =>
        {
            entity.ToTable("payroll_payment");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.SettlementMethod).HasMaxLength(32).IsRequired();
            entity.Property(row => row.TreasuryPartyId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.NetPayable).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number })
                  .IsUnique().HasDatabaseName("uq_hr_payroll_payment_number");
        });

        modelBuilder.Entity<PayrollPaymentLineRow>(entity =>
        {
            entity.ToTable("payroll_payment_line");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.EmployeeCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.CostCenterId).HasMaxLength(64);
            entity.Property(row => row.Amount).HasColumnType(Money);
            entity.HasIndex(row => new { row.PaymentId, row.LineNo })
                  .IsUnique().HasDatabaseName("uq_hr_payroll_payment_line");
            entity.HasIndex(row => new { row.PaymentId, row.PayslipId })
                  .IsUnique().HasDatabaseName("uq_hr_payroll_payment_payslip");
        });

        modelBuilder.Entity<SocialInsurancePaymentRow>(entity =>
        {
            entity.ToTable("social_insurance_payment");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.PeriodCode).HasMaxLength(7).IsRequired();
            entity.Property(row => row.SettlementMethod).HasMaxLength(32).IsRequired();
            entity.Property(row => row.TreasuryPartyId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.Amount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number })
                  .IsUnique().HasDatabaseName("uq_hr_social_insurance_payment_number");
        });

        modelBuilder.Entity<EmployeeDeductionRow>(entity =>
        {
            entity.ToTable("employee_deduction");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.PeriodCode).HasMaxLength(7).IsRequired();
            entity.Property(row => row.CategoryKey).HasMaxLength(64).IsRequired();
            entity.Property(row => row.ApprovedBy).HasMaxLength(64).IsRequired();
            entity.Property(row => row.Amount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.EmployeeId, row.PeriodCode })
                  .HasDatabaseName("ix_hr_employee_deduction_period");
        });

        modelBuilder.Entity<EmployeeAdvanceRow>(entity =>
        {
            entity.ToTable("employee_advance");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.SettlementMethod).HasMaxLength(32).IsRequired();
            entity.Property(row => row.TreasuryPartyId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.Amount).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number })
                  .IsUnique().HasDatabaseName("uq_hr_employee_advance_number");
        });

        modelBuilder.Entity<AdvanceInstalmentRow>(entity =>
        {
            entity.ToTable("employee_advance_instalment");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.PeriodCode).HasMaxLength(7).IsRequired();
            entity.Property(row => row.Amount).HasColumnType(Money);
            entity.HasIndex(row => new { row.AdvanceId, row.LineNo })
                  .IsUnique().HasDatabaseName("uq_hr_advance_instalment_line");
        });

        modelBuilder.Entity<EndOfServiceProvisionRow>(entity =>
        {
            entity.ToTable("eos_provision");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.PeriodCode).HasMaxLength(7).IsRequired();
            entity.Property(row => row.MeasurementRef).HasMaxLength(400).IsRequired();
            entity.Property(row => row.ApprovedBy).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.PeriodShare).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number })
                  .IsUnique().HasDatabaseName("uq_hr_eos_provision_number");

            // مرجعُ أساس القياس غير فارغ في القاعدة: مبلغٌ بلا أساسٍ مكتوب هو تقديرٌ
            // بلا مصدر، وطريقة القياس بندٌ مفتوح على المالك لا تخترعه هذه الوحدة.
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_hr_eos_provision_measurement_ref", """length(btrim("MeasurementRef")) > 0"""));
        });

        modelBuilder.Entity<EndOfServiceMovementRow>(entity =>
        {
            entity.ToTable("eos_provision_movement");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.EmployeeCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.CostCenterId).HasMaxLength(64);
            entity.Property(row => row.PeriodCode).HasMaxLength(7).IsRequired();
            entity.Property(row => row.PeriodShare).HasColumnType(Money);

            // ‏**مفتاحه (المستأجر · علاقة العمل · الفترة) لا الفترة وحدها**: المخالصة
            // تحتاج الرصيد **لهذا الموظف**، ومفتاحٌ بالفترة وحدها يجعل الرصيد رقماً
            // للمنشأة لا للطرف — فينحرف الدفتر المساعد عن ضابطه من أول مخالصة.
            entity.HasIndex(row => new { row.TenantId, row.EmploymentId, row.PeriodCode })
                  .IsUnique().HasDatabaseName("uq_hr_eos_movement_period");
        });

        modelBuilder.Entity<EndOfServiceSettlementRow>(entity =>
        {
            entity.ToTable("eos_settlement");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Number).HasMaxLength(64).IsRequired();
            entity.Property(row => row.EmployeeCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.CostCenterId).HasMaxLength(64);
            entity.Property(row => row.ScenarioCode).HasMaxLength(16).IsRequired();
            entity.Property(row => row.MeasurementRef).HasMaxLength(400).IsRequired();
            entity.Property(row => row.SettlementMethod).HasMaxLength(32).IsRequired();
            entity.Property(row => row.TreasuryPartyId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.SettlementDue).HasColumnType(Money);
            entity.Property(row => row.ProvisionBalance).HasColumnType(Money);
            entity.Property(row => row.AmountPaid).HasColumnType(Money);
            entity.Property(row => row.Shortfall).HasColumnType(Money);
            entity.Property(row => row.Excess).HasColumnType(Money);
            entity.Property(row => row.ProvisionUtilised).HasColumnType(Money);
            entity.HasIndex(row => new { row.TenantId, row.Number })
                  .IsUnique().HasDatabaseName("uq_hr_eos_settlement_number");

            // المتطابقة المعلَنة في المصفوفة: provision_utilised = amount_paid - shortfall + excess
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_hr_eos_settlement_identity",
                """ "ProvisionUtilised" = "AmountPaid" - "Shortfall" + "Excess" """));
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
            entity.HasIndex(row => new { row.TenantId, row.State }).HasDatabaseName("ix_hr_posting_state");

            // هوية الإحكام كما يعرّفها المحرك بالضبط: ستّة حقول **ومنها رمز الحدث**،
            // و<c>DocumentId</c> فيها **معرّف القسيمة لا معرّف المسيّر** — لأن مسار
            // القالب يحلّ الطرف من واقعةٍ واحدة لكل طلب ويقرأ الأبعاد من قاموسٍ واحد،
            // فطلبٌ واحد لا يحمل إلا طرفاً واحداً ومركزاً واحداً. وقيدٌ واحد للمسيّر
            // كان سيكتب ذمّة ثلاثمئة موظف على موظفٍ واحد، **والقيد متوازن تماماً
            // والسلسلة سليمة وميزان المراجعة صحيح** — ولا شيء يُظهره.
            entity.HasIndex(row => new
            {
                row.TenantId, row.DocumentType, row.DocumentId, row.TriggerCode, row.Generation, row.EventCode,
            }).IsUnique().HasDatabaseName("uq_hr_posting_identity");

            // ورمزٌ فارغ يُعيد تركيب العطب داخل مفتاح موسَّع: القيمة الفارغة تجعل كل
            // حدث بلا رمز مساوياً لكل حدث آخر بلا رمز.
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_hr_document_posting_event_code", """length(btrim("EventCode")) > 0"""));
        });
    }
}
