using Microsoft.EntityFrameworkCore;

namespace BabelDemo.Db;

/// <summary>قيد يومية. حقول هذا الكيان بالضبط هي ما تدخل في بصمة SHA-256 (انظر Canonical).</summary>
internal sealed class JournalEntry
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

internal sealed class JournalLine
{
    public Guid LineId { get; set; }
    public Guid EntryId { get; set; }
    public int LineNo { get; set; }
    public string AccountCode { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

/// <summary>حساب في دليل الحسابات. كل صف يحمل الاسم العربي والإنجليزي معاً.</summary>
internal sealed class Account
{
    public string AccountCode { get; set; } = "";
    public string? ParentCode { get; set; }
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string AccountType { get; set; } = "";   // asset | liability | equity | revenue | expense
    public string NormalSide { get; set; } = "";    // debit | credit
    public bool IsPostable { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// إسقاط الأرصدة: يُبنى داخل معاملة الترحيل نفسها، ويمكن إعادة بنائه بالكامل من الدفتر.
/// الدفتر هو الحقيقة؛ هذا الجدول مجرد نتيجة مشتقة.
/// </summary>
internal sealed class AccountBalance
{
    public string BookId { get; set; } = "";
    public string Period { get; set; } = "";        // YYYY-MM
    public string AccountCode { get; set; } = "";
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public DateTime UpdatedAt { get; set; }
}

internal sealed class EntryCounter
{
    public string BookId { get; set; } = "";
    public long NextNo { get; set; }
    public long NextSeq { get; set; }
}

internal sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountBalance> AccountBalances => Set<AccountBalance>();
    public DbSet<EntryCounter> EntryCounters => Set<EntryCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JournalEntry>(e =>
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

        modelBuilder.Entity<JournalLine>(e =>
        {
            e.ToTable("journal_line", "ledger");
            e.HasKey(x => x.LineId);
            e.Property(x => x.LineId).HasColumnName("line_id");
            e.Property(x => x.EntryId).HasColumnName("entry_id");
            e.Property(x => x.LineNo).HasColumnName("line_no");
            e.Property(x => x.AccountCode).HasColumnName("account_code");
            e.Property(x => x.Description).HasColumnName("description");
            // أعمدة المال: NUMERIC(19,4) حقيقية — لا float ولا JSON
            e.Property(x => x.Debit).HasColumnName("debit").HasColumnType("numeric(19,4)");
            e.Property(x => x.Credit).HasColumnName("credit").HasColumnType("numeric(19,4)");
        });

        modelBuilder.Entity<Account>(e =>
        {
            e.ToTable("account", "ledger");
            e.HasKey(x => x.AccountCode);
            e.Property(x => x.AccountCode).HasColumnName("account_code");
            e.Property(x => x.ParentCode).HasColumnName("parent_code");
            e.Property(x => x.NameAr).HasColumnName("name_ar");
            e.Property(x => x.NameEn).HasColumnName("name_en");
            e.Property(x => x.AccountType).HasColumnName("account_type");
            e.Property(x => x.NormalSide).HasColumnName("normal_side");
            e.Property(x => x.IsPostable).HasColumnName("is_postable");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<AccountBalance>(e =>
        {
            e.ToTable("account_balance", "ledger");
            e.HasKey(x => new { x.BookId, x.Period, x.AccountCode });
            e.Property(x => x.BookId).HasColumnName("book_id");
            e.Property(x => x.Period).HasColumnName("period");
            e.Property(x => x.AccountCode).HasColumnName("account_code");
            e.Property(x => x.Debit).HasColumnName("debit").HasColumnType("numeric(19,4)");
            e.Property(x => x.Credit).HasColumnName("credit").HasColumnType("numeric(19,4)");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<EntryCounter>(e =>
        {
            e.ToTable("entry_counter", "ledger");
            e.HasKey(x => x.BookId);
            e.Property(x => x.BookId).HasColumnName("book_id");
            e.Property(x => x.NextNo).HasColumnName("next_no");
            e.Property(x => x.NextSeq).HasColumnName("next_seq");
        });
    }
}
