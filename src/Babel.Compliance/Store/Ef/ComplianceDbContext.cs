using System.Text.Json;
using Babel.Compliance.Abstractions;
using Babel.Compliance.Model;
using Babel.Compliance.Reconciliation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Babel.Compliance.Store.Ef;

/// <summary>
/// المخطط العلائقي للالتزام — بنفس مقاس تجربة <c>spikes/relational-stack</c>:
/// EF Core 10 + Npgsql، وأعمدة مال <c>numeric(19,4)</c>، ولا <c>float</c> في أي مكان.
/// <para/>
/// مخطط مستقل باسم <c>compliance</c> عن مخطط <c>ledger</c>: <b>الالتزام لا يكتب في الدفتر
/// ولا يشاركه جدولاً واحداً</b>، ويستطيع أن يعيش في قاعدة بيانات أخرى إن فُصل لاحقاً.
/// </summary>
public sealed class ComplianceDbContext(DbContextOptions<ComplianceDbContext> options) : DbContext(options)
{
    public const string Schema = "compliance";

    public DbSet<ComplianceRecord> Documents => Set<ComplianceRecord>();
    public DbSet<SubmissionAttempt> Attempts => Set<SubmissionAttempt>();
    public DbSet<StatusTransition> Transitions => Set<StatusTransition>();
    public DbSet<IssuingUnitChainHead> ChainHeads => Set<IssuingUnitChainHead>();
    public DbSet<ComplianceWorkItem> WorkItems => Set<ComplianceWorkItem>();
    public DbSet<ReconciliationFinding> Findings => Set<ReconciliationFinding>();

    private static readonly JsonSerializerOptions NoticeJson = new(JsonSerializerDefaults.Web);

    protected override void OnModelCreating(ModelBuilder b)
    {
        var tenant = new ValueConverter<TenantId, string>(v => v.Value, v => new TenantId(v));
        var unit = new ValueConverter<IssuingUnitId, string>(v => v.Value, v => new IssuingUnitId(v));
        var docId = new ValueConverter<ComplianceDocumentId, Guid>(v => v.Value, v => new ComplianceDocumentId(v));
        var attemptId = new ValueConverter<AttemptId, Guid>(v => v.Value, v => new AttemptId(v));
        var attemptIdNull = new ValueConverter<AttemptId?, Guid?>(
            v => v == null ? null : v.Value.Value, v => v == null ? null : new AttemptId(v.Value));
        var entryRef = new ValueConverter<JournalEntryRef, Guid>(v => v.Value, v => new JournalEntryRef(v));
        var entryRefNull = new ValueConverter<JournalEntryRef?, Guid?>(
            v => v == null ? null : v.Value.Value, v => v == null ? null : new JournalEntryRef(v.Value));
        var unitNull = new ValueConverter<IssuingUnitId?, string?>(
            v => v == null ? null : v.Value.Value, v => v == null ? null : new IssuingUnitId(v!));
        var docIdNull = new ValueConverter<ComplianceDocumentId?, Guid?>(
            v => v == null ? null : v.Value.Value, v => v == null ? null : new ComplianceDocumentId(v.Value));
        var notices = new ValueConverter<List<ComplianceNotice>, string>(
            v => JsonSerializer.Serialize(v, NoticeJson),
            v => JsonSerializer.Deserialize<List<ComplianceNotice>>(v, NoticeJson) ?? new());

        b.Entity<ComplianceRecord>(e =>
        {
            e.ToTable("document", Schema);
            e.HasKey(x => x.DocumentId);
            e.Property(x => x.DocumentId).HasColumnName("document_id").HasConversion(docId);
            e.Property(x => x.DocumentUuid).HasColumnName("document_uuid");
            e.Property(x => x.Tenant).HasColumnName("tenant_id").HasConversion(tenant);
            e.Property(x => x.IssuingUnit).HasColumnName("issuing_unit_id").HasConversion(unit);
            e.Property(x => x.Environment).HasColumnName("environment").HasConversion<string>();
            e.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>();
            e.Property(x => x.Flow).HasColumnName("flow").HasConversion<string>();
            e.Property(x => x.DocumentNumber).HasColumnName("document_number");
            e.Property(x => x.JournalEntry).HasColumnName("journal_entry_id").HasConversion(entryRef);
            e.Property(x => x.IssuedAt).HasColumnName("issued_at");
            e.Property(x => x.Counter).HasColumnName("counter");
            e.Property(x => x.PreviousHash).HasColumnName("previous_hash");
            e.Property(x => x.DocumentHash).HasColumnName("document_hash");
            e.Property(x => x.FrozenPayload).HasColumnName("frozen_payload");
            e.Property(x => x.SealState).HasColumnName("seal_state").HasConversion<string>();
            e.Property(x => x.SubmissionFingerprint).HasColumnName("submission_fingerprint");
            e.Property(x => x.RenderedBody).HasColumnName("rendered_body");
            // المال: numeric(19,4) دائماً، ولا float أبداً (CONTRIBUTING §3 بند 2)
            e.Property(x => x.NetTotal).HasColumnName("net_total").HasColumnType("numeric(19,4)");
            e.Property(x => x.TaxTotal).HasColumnName("tax_total").HasColumnType("numeric(19,4)");
            e.Property(x => x.GrossTotal).HasColumnName("gross_total").HasColumnType("numeric(19,4)");
            e.Property(x => x.CurrencyCode).HasColumnName("currency_code");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
            e.Property(x => x.AttemptCount).HasColumnName("attempt_count");
            e.Property(x => x.ResolutionAttemptCount).HasColumnName("resolution_attempt_count");
            e.Property(x => x.QueuedAt).HasColumnName("queued_at");
            e.Property(x => x.SettledAt).HasColumnName("settled_at");
            e.Property(x => x.ProviderReference).HasColumnName("provider_reference");
            e.Property(x => x.StampedDocument).HasColumnName("stamped_document");
            e.Property(x => x.Notices).HasColumnName("notices").HasColumnType("jsonb").HasConversion(notices);
            e.Property(x => x.HumanReviewReasonAr).HasColumnName("human_review_reason_ar");
            e.Property(x => x.HumanReviewReasonEn).HasColumnName("human_review_reason_en");
            e.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();

            // التسلسل بلا فجوات شرط، والفهرس الفريد يجعله مضموناً في القاعدة لا في الكود وحده.
            e.HasIndex(x => new { x.IssuingUnit, x.Counter }).IsUnique().HasDatabaseName("ux_document_unit_counter");
            e.HasIndex(x => new { x.Tenant, x.Status });
            e.HasIndex(x => x.JournalEntry);
        });

        b.Entity<SubmissionAttempt>(e =>
        {
            e.ToTable("submission_attempt", Schema);
            e.HasKey(x => x.AttemptId);
            e.Property(x => x.AttemptId).HasColumnName("attempt_id").HasConversion(attemptId);
            e.Property(x => x.DocumentId).HasColumnName("document_id").HasConversion(docId);
            e.Property(x => x.AttemptNo).HasColumnName("attempt_no");
            e.Property(x => x.StartedAt).HasColumnName("started_at");
            e.Property(x => x.CompletedAt).HasColumnName("completed_at");
            e.Property(x => x.PayloadFingerprint).HasColumnName("payload_fingerprint");
            e.Property(x => x.IsResolution).HasColumnName("is_resolution");
            e.Property(x => x.Outcome).HasColumnName("outcome").HasConversion<string>();
            e.Property(x => x.FaultClass).HasColumnName("fault_class").HasConversion<string?>();
            e.Property(x => x.FaultCode).HasColumnName("fault_code");
            e.Property(x => x.FaultMessageAr).HasColumnName("fault_message_ar");
            e.Property(x => x.FaultMessageEn).HasColumnName("fault_message_en");
            e.Property(x => x.ProviderReference).HasColumnName("provider_reference");
            e.Property(x => x.ProviderReportedDuplicate).HasColumnName("provider_reported_duplicate");
            e.HasIndex(x => new { x.DocumentId, x.AttemptNo }).IsUnique();
        });

        b.Entity<StatusTransition>(e =>
        {
            e.ToTable("status_transition", Schema);
            e.HasKey(x => x.TransitionId);
            e.Property(x => x.TransitionId).HasColumnName("transition_id");
            e.Property(x => x.DocumentId).HasColumnName("document_id").HasConversion(docId);
            e.Property(x => x.Seq).HasColumnName("seq");
            e.Property(x => x.From).HasColumnName("status_from").HasConversion<string>();
            e.Property(x => x.To).HasColumnName("status_to").HasConversion<string>();
            e.Property(x => x.At).HasColumnName("at");
            e.Property(x => x.Actor).HasColumnName("actor");
            e.Property(x => x.ReasonAr).HasColumnName("reason_ar");
            e.Property(x => x.ReasonEn).HasColumnName("reason_en");
            e.Property(x => x.Attempt).HasColumnName("attempt_id").HasConversion(attemptIdNull);
            e.HasIndex(x => new { x.DocumentId, x.Seq }).IsUnique();
        });

        b.Entity<IssuingUnitChainHead>(e =>
        {
            e.ToTable("chain_head", Schema);
            e.HasKey(x => new { x.Tenant, x.IssuingUnit });
            e.Property(x => x.Tenant).HasColumnName("tenant_id").HasConversion(tenant);
            e.Property(x => x.IssuingUnit).HasColumnName("issuing_unit_id").HasConversion(unit);
            e.Property(x => x.NextCounter).HasColumnName("next_counter");
            e.Property(x => x.HeadHash).HasColumnName("head_hash");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.IsHalted).HasColumnName("is_halted");
            e.Property(x => x.HaltReasonAr).HasColumnName("halt_reason_ar");
            e.Property(x => x.HaltReasonEn).HasColumnName("halt_reason_en");
        });

        b.Entity<ComplianceWorkItem>(e =>
        {
            e.ToTable("work_item", Schema);
            e.HasKey(x => x.WorkItemId);
            e.Property(x => x.WorkItemId).HasColumnName("work_item_id");
            e.Property(x => x.DocumentId).HasColumnName("document_id").HasConversion(docId);
            e.Property(x => x.Tenant).HasColumnName("tenant_id").HasConversion(tenant);
            e.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>();
            e.Property(x => x.NotBefore).HasColumnName("not_before");
            e.Property(x => x.Attempts).HasColumnName("attempts");
            e.Property(x => x.EnqueuedAt).HasColumnName("enqueued_at");
            e.Property(x => x.LastErrorAr).HasColumnName("last_error_ar");
            e.Property(x => x.LastErrorEn).HasColumnName("last_error_en");
            e.Property(x => x.Done).HasColumnName("done");
            e.HasIndex(x => new { x.Done, x.NotBefore });
        });

        b.Entity<ReconciliationFinding>(e =>
        {
            e.ToTable("reconciliation_finding", Schema);
            e.HasKey(x => x.FindingId);
            e.Property(x => x.FindingId).HasColumnName("finding_id");
            e.Property(x => x.Tenant).HasColumnName("tenant_id").HasConversion(tenant);
            e.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>();
            e.Property(x => x.Severity).HasColumnName("severity").HasConversion<string>();
            e.Property(x => x.DetectedAt).HasColumnName("detected_at");
            e.Property(x => x.DocumentId).HasColumnName("document_id").HasConversion(docIdNull);
            e.Property(x => x.IssuingUnit).HasColumnName("issuing_unit_id").HasConversion(unitNull);
            e.Property(x => x.Counter).HasColumnName("counter");
            e.Property(x => x.JournalEntry).HasColumnName("journal_entry_id").HasConversion(entryRefNull);
            e.Property(x => x.ExpectedAmount).HasColumnName("expected_amount").HasColumnType("numeric(19,4)");
            e.Property(x => x.ObservedAmount).HasColumnName("observed_amount").HasColumnType("numeric(19,4)");
            e.Property(x => x.SummaryAr).HasColumnName("summary_ar");
            e.Property(x => x.SummaryEn).HasColumnName("summary_en");
            e.Property(x => x.NextStepAr).HasColumnName("next_step_ar");
            e.Property(x => x.NextStepEn).HasColumnName("next_step_en");
            e.Property(x => x.AutoResolved).HasColumnName("auto_resolved");
            e.Property(x => x.Resolved).HasColumnName("resolved");
            e.Property(x => x.ResolvedAt).HasColumnName("resolved_at");
            e.Property(x => x.ResolvedBy).HasColumnName("resolved_by");
            e.Property(x => x.ResolutionNoteAr).HasColumnName("resolution_note_ar");
            e.Property(x => x.ResolutionNoteEn).HasColumnName("resolution_note_en");
            e.HasIndex(x => new { x.Tenant, x.Resolved });
        });
    }
}
