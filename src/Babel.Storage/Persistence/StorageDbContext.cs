using Microsoft.EntityFrameworkCore;

namespace Babel.Storage.Persistence;

/// <summary>
/// صفّ مرفق — <b>يُدرَج ولا يُعدَّل أبداً</b>.
/// <para>
/// ولذلك <b>لا عمود «مسحوب» ولا عمود «خلَفه»</b> في هذا الصفّ: كلاهما كان سيتطلّب
/// <c>UPDATE</c> على صفٍّ قائم، وهو ما نزعناه من دور التطبيق في PostgreSQL نفسها.
/// السحبُ صفٌّ في جدول ثانٍ، والتصحيحُ صفٌّ جديد يحمل <see cref="SupersedesId"/>؛
/// وكلاهما يُقرأ عند العرض ولا يُكتب فوق شيء.
/// </para>
/// </summary>
internal sealed class AttachmentRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string MediaType { get; set; } = string.Empty;

    public long ByteLength { get; set; }

    /// <summary>‏SHA-256 ستّ‌عشرياً صغيراً — أربعة وستون محرفاً، والقيد في القاعدة.</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>المسار النسبي داخل المخزن. غامضٌ ولا يُشتقّ من المعرّف ولا من الاسم.</summary>
    public string ObjectKey { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public DateTimeOffset StoredAt { get; set; }

    public Guid StoredBy { get; set; }

    public int Version { get; set; }

    /// <summary>سلفُ هذا الإصدار، أو <c>null</c> للإصدار الأول.</summary>
    public Guid? SupersedesId { get; set; }
}

/// <summary>
/// علامة سحب — <b>جدول ثانٍ لأن الأول لا يُعدَّل</b>. والبايتات لا تُمسّ: احتفاظ
/// الهيئة بسند القيد واجب، والسحب إعلانُ حالة لا محو.
/// </summary>
internal sealed class AttachmentWithdrawalRow
{
    public Guid AttachmentId { get; set; }

    public Guid TenantId { get; set; }

    public DateTimeOffset WithdrawnAt { get; set; }

    public Guid WithdrawnBy { get; set; }

    public string ReasonKey { get; set; } = string.Empty;
}

/// <summary>
/// جداول المخزن. <c>internal</c> — لا يعبر الحدّ منها شيء (القاعدة 5)، والعابر هو
/// <c>Babel.Contracts.Storage.StoredAttachment</c> وحده.
/// </summary>
internal sealed class StorageDbContext(DbContextOptions<StorageDbContext> options) : DbContext(options)
{
    public DbSet<AttachmentRow> Attachments => Set<AttachmentRow>();

    public DbSet<AttachmentWithdrawalRow> Withdrawals => Set<AttachmentWithdrawalRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("storage");

        modelBuilder.Entity<AttachmentRow>(entity =>
        {
            entity.ToTable("attachment");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.MediaType).HasMaxLength(32).IsRequired();
            entity.Property(row => row.ContentHash).HasMaxLength(64).IsRequired();
            entity.Property(row => row.ObjectKey).HasMaxLength(256).IsRequired();
            entity.Property(row => row.FileName).HasMaxLength(120).IsRequired();

            // المستأجر أول أعمدة كل فهرس: القراءة داخل مستأجر لا عبره، والفهرس يعبّر
            // عن ذلك بدل أن يجعله مرشّحاً يُنسى.
            entity.HasIndex(row => new { row.TenantId, row.Id })
                  .HasDatabaseName("ix_storage_attachment_tenant");

            // مفتاح الكائن فريد على مستوى المخزن كلّه: صفّان يشيران إلى ملفّ واحد
            // يعنيان أن حذف أحدهما يوماً يسحب البايتات من تحت الآخر.
            entity.HasIndex(row => row.ObjectKey)
                  .IsUnique()
                  .HasDatabaseName("uq_storage_attachment_object_key");

            // **السلسلة خطّية ولا تتفرّع.** فرعان يصحّحان السلف نفسه يعنيان إصدارين
            // «حاليين» لمستند واحد، وهو أسوأ من غياب النسخ أصلاً. والتفرّد الجزئي
            // يفرض ذلك في القاعدة، لا في سباق بين طلبين.
            entity.HasIndex(row => row.SupersedesId)
                  .IsUnique()
                  .HasDatabaseName("uq_storage_attachment_supersedes")
                  .HasFilter("\"SupersedesId\" is not null");

            entity.ToTable(table =>
            {
                table.HasCheckConstraint("ck_storage_attachment_hash", """ "ContentHash" ~ '^[0-9a-f]{64}$' """);
                table.HasCheckConstraint("ck_storage_attachment_length", """ "ByteLength" > 0 """);
                table.HasCheckConstraint("ck_storage_attachment_version", """ "Version" >= 1 """);

                // إصدارٌ أول لا سلف له، وإصدارٌ لاحق له سلف. الشرطان معاً يمنعان
                // صفّاً يقول «الإصدار الثالث» بلا ما قبله.
                table.HasCheckConstraint(
                    "ck_storage_attachment_chain",
                    """ ("Version" = 1) = ("SupersedesId" is null) """);
            });
        });

        modelBuilder.Entity<AttachmentWithdrawalRow>(entity =>
        {
            entity.ToTable("attachment_withdrawal");

            // المفتاح هو المرفق نفسه: لا يُسحب مرّتين، والقاعدة تقولها لا الشيفرة.
            entity.HasKey(row => row.AttachmentId);
            entity.Property(row => row.ReasonKey).HasMaxLength(64).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.AttachmentId })
                  .HasDatabaseName("ix_storage_withdrawal_tenant");
        });
    }
}
