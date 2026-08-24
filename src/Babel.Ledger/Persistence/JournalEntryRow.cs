namespace Babel.Ledger.Persistence;

/// <summary>
/// رأس القيد. جدول يُضاف إليه فقط: لا تعديل ولا حذف بعد الترحيل
/// (03-accounting-core.md §2 · وثيقة المعمارية §3).
/// </summary>
internal sealed class JournalEntryRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>رقم من العدّاد بلا فجوات لكل (مستأجر × دفتر). ليس <c>SEQUENCE</c> — وثيقة المعمارية §7.3.</summary>
    public long EntryNumber { get; set; }

    /// <summary>مفتاح الحصانة الذي وفّره العميل. مفتاح فريد، لا حارس تسلسل.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>بصمة القيد، محسوبة على الحقيقة المجالية لا على جدول مكتبة (وثيقة المعمارية §7.1).</summary>
    public string EntryHash { get; set; } = string.Empty;

    /// <summary>بصمة القيد السابق في السلسلة. <c>null</c> لقيد النشأة.</summary>
    public string? PreviousHash { get; set; }

    /// <summary>لحظة الإنشاء، مقصوصة إلى الميكروثانية قبل التجزئة وقبل التخزين (وثيقة المعمارية §8.2 مصيدة 1).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<JournalLineRow> Lines { get; } = [];
}
