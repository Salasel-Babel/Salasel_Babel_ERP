using Microsoft.EntityFrameworkCore;

namespace Babel.Ledger.Persistence;

/// <summary>
/// جداول الدفتر. <c>internal</c>: <b>القاعدة 1</b> مفروضة بثلاث طبقات، وهذه الثانية.
/// <list type="number">
///   <item>لا مرجع مشروع من أي وحدة أفقية إلى Babel.Ledger — لا يوجد ما يُستدعى أصلاً.</item>
///   <item>أنواع الاستمرارية <c>internal</c> — لا يراها حتى الجذر التركيبي.</item>
///   <item>صلاحيات PostgreSQL: الدور التطبيقي <c>INSERT</c> و<c>SELECT</c> فقط،
///         مع <c>REVOKE UPDATE, DELETE, TRUNCATE</c> والهجرات بدور مالك منفصل
///         (وثيقة المعمارية §3.2 — مقيس، رمز الرفض 42501).</item>
/// </list>
/// أي طبقة وحدها قابلة للالتفاف؛ الثلاث معاً ليست كذلك.
/// </summary>
internal sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<AccountRow> Accounts => Set<AccountRow>();

    public DbSet<JournalEntryRow> JournalEntries => Set<JournalEntryRow>();

    public DbSet<JournalLineRow> JournalLines => Set<JournalLineRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("ledger");

        modelBuilder.Entity<AccountRow>(entity =>
        {
            entity.ToTable("account");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Code).HasMaxLength(32).IsRequired();
            entity.Property(row => row.NameAr).HasMaxLength(200).IsRequired();
            entity.Property(row => row.NameEn).HasMaxLength(200).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.Code }).IsUnique();
        });

        modelBuilder.Entity<JournalEntryRow>(entity =>
        {
            entity.ToTable("journal_entry");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.Property(row => row.EntryHash).HasMaxLength(64).IsRequired();
            entity.Property(row => row.PreviousHash).HasMaxLength(64);
            // القاعدة المعمارية 4: الحصانة ضد التكرار مفتاح فريد، لا حارس تسلسل.
            entity.HasIndex(row => new { row.TenantId, row.IdempotencyKey }).IsUnique();
            // العدّاد بلا فجوات لكل (مستأجر × دفتر) — وثيقة المعمارية §7.3.
            entity.HasIndex(row => new { row.TenantId, row.EntryNumber }).IsUnique();
        });

        modelBuilder.Entity<JournalLineRow>(entity =>
        {
            entity.ToTable("journal_line");
            entity.HasKey(row => row.Id);
            entity.HasOne(row => row.Entry).WithMany(row => row.Lines).HasForeignKey(row => row.JournalEntryId);
            // المقياس القانوني للمبالغ: numeric(19,4) في كل النطاق (وثيقة المعمارية §8.2 مصيدة 2).
            entity.Property(row => row.DebitAmount).HasColumnType("numeric(19,4)");
            entity.Property(row => row.CreditAmount).HasColumnType("numeric(19,4)");
            entity.Property(row => row.ExchangeRate).HasColumnType("numeric(19,8)");
        });
    }
}
