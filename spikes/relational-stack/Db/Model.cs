using Microsoft.EntityFrameworkCore;

namespace BabelRelationalSpike.Db;

public class JournalEntry
{
    public Guid EntryId { get; set; }
    public string BookId { get; set; } = "";
    public string TenantId { get; set; } = "";
    public long EntryNo { get; set; }
    public long ChainSeq { get; set; }
    public DateOnly EntryDate { get; set; }
    public string Memo { get; set; } = "";
    public string MemoAr { get; set; } = "";
    public DateTime PostedAt { get; set; }
    public string Actor { get; set; } = "";
    public byte[] PrevHash { get; set; } = [];
    public byte[] EntryHash { get; set; } = [];
    public List<JournalLine> Lines { get; set; } = [];
}

public class JournalLine
{
    public Guid LineId { get; set; }
    public Guid EntryId { get; set; }
    public int LineNo { get; set; }
    public string AccountCode { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

/// <summary>Append-only process narrative row. Payload is raw JSONB.</summary>
public class ProcessEvent
{
    public Guid EventId { get; set; }
    public string TenantId { get; set; } = "";
    public string StreamType { get; set; } = "";
    public Guid StreamId { get; set; }
    public int StreamSeq { get; set; }
    public string EventType { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string Actor { get; set; } = "";
    public Guid CorrelationId { get; set; }
    public Guid? CausationId { get; set; }
    public string Payload { get; set; } = "{}";
}

/// <summary>Per-tenant flexible document: settings, form definitions, custom fields, report templates.</summary>
public class TenantDocument
{
    public string TenantId { get; set; } = "";
    public string DocType { get; set; } = "";
    public string DocKey { get; set; } = "";
    public string Doc { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}

public class EntryCounter
{
    public string BookId { get; set; } = "";
    public long NextNo { get; set; }
    public long NextSeq { get; set; }
}

public class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<ProcessEvent> ProcessEvents => Set<ProcessEvent>();
    public DbSet<TenantDocument> TenantDocuments => Set<TenantDocument>();
    public DbSet<EntryCounter> EntryCounters => Set<EntryCounter>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<JournalEntry>(e =>
        {
            e.ToTable("journal_entry", "ledger");
            e.HasKey(x => x.EntryId);
            e.Property(x => x.EntryId).HasColumnName("entry_id");
            e.Property(x => x.BookId).HasColumnName("book_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.EntryNo).HasColumnName("entry_no");
            e.Property(x => x.ChainSeq).HasColumnName("chain_seq");
            e.Property(x => x.EntryDate).HasColumnName("entry_date");
            e.Property(x => x.Memo).HasColumnName("memo");
            e.Property(x => x.MemoAr).HasColumnName("memo_ar");
            e.Property(x => x.PostedAt).HasColumnName("posted_at");
            e.Property(x => x.Actor).HasColumnName("actor");
            e.Property(x => x.PrevHash).HasColumnName("prev_hash");
            e.Property(x => x.EntryHash).HasColumnName("entry_hash");
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.EntryId);
        });

        b.Entity<JournalLine>(e =>
        {
            e.ToTable("journal_line", "ledger");
            e.HasKey(x => x.LineId);
            e.Property(x => x.LineId).HasColumnName("line_id");
            e.Property(x => x.EntryId).HasColumnName("entry_id");
            e.Property(x => x.LineNo).HasColumnName("line_no");
            e.Property(x => x.AccountCode).HasColumnName("account_code");
            e.Property(x => x.Description).HasColumnName("description");
            // the money columns: real NUMERIC(19,4), never float, never JSON
            e.Property(x => x.Debit).HasColumnName("debit").HasColumnType("numeric(19,4)");
            e.Property(x => x.Credit).HasColumnName("credit").HasColumnType("numeric(19,4)");
        });

        b.Entity<ProcessEvent>(e =>
        {
            e.ToTable("process_event", "ledger");
            e.HasKey(x => x.EventId);
            e.Property(x => x.EventId).HasColumnName("event_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.StreamType).HasColumnName("stream_type");
            e.Property(x => x.StreamId).HasColumnName("stream_id");
            e.Property(x => x.StreamSeq).HasColumnName("stream_seq");
            e.Property(x => x.EventType).HasColumnName("event_type");
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            e.Property(x => x.Actor).HasColumnName("actor");
            e.Property(x => x.CorrelationId).HasColumnName("correlation_id");
            e.Property(x => x.CausationId).HasColumnName("causation_id");
            e.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
        });

        b.Entity<TenantDocument>(e =>
        {
            e.ToTable("tenant_document", "app");
            e.HasKey(x => new { x.TenantId, x.DocType, x.DocKey });
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.DocType).HasColumnName("doc_type");
            e.Property(x => x.DocKey).HasColumnName("doc_key");
            e.Property(x => x.Doc).HasColumnName("doc").HasColumnType("jsonb");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        b.Entity<TenantSettings>(e =>
        {
            e.ToTable("tenant_settings", "app");
            e.HasKey(x => x.TenantId);
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            // EF Core 10 JSON column mapping: the whole POCO graph lives in one jsonb column
            e.OwnsOne(x => x.Settings, s =>
            {
                s.ToJson("settings");
                s.OwnsOne(z => z.Zatca);
                s.OwnsMany(z => z.CustomFields);
                s.OwnsMany(z => z.ReportTemplates);
            });
        });

        b.Entity<EntryCounter>(e =>
        {
            e.ToTable("entry_counter", "ledger");
            e.HasKey(x => x.BookId);
            e.Property(x => x.BookId).HasColumnName("book_id");
            e.Property(x => x.NextNo).HasColumnName("next_no");
            e.Property(x => x.NextSeq).HasColumnName("next_seq");
        });
    }
}
