using System.Text.Json.Serialization;

namespace BabelPosOffline.Server;

/// <summary>
/// حمولة المزامنة. <b>كل مبلغ نصّ بمقياس ثابت</b> ("115.0000") لا رقم JSON —
/// لأن أي مُحلِّل JSON في أي لغة قد يقرأ الرقم إلى <c>double</c>. النص لا يفعل ذلك أبداً.
/// Every amount is a fixed-scale STRING, never a JSON number: some JSON parser
/// somewhere will read a JSON number into a double, and that is forbidden in every layer.
/// </summary>
public sealed record SyncLine(
    [property: JsonPropertyName("line_no")] int LineNo,
    [property: JsonPropertyName("item_code")] string ItemCode,
    [property: JsonPropertyName("qty")] string Qty,
    [property: JsonPropertyName("unit_price")] string UnitPrice,
    [property: JsonPropertyName("line_net")] string LineNet,
    [property: JsonPropertyName("line_vat")] string LineVat);

public sealed record SyncJournalLine(
    [property: JsonPropertyName("line_no")] int LineNo,
    [property: JsonPropertyName("account")] string Account,
    [property: JsonPropertyName("debit")] string Debit,
    [property: JsonPropertyName("credit")] string Credit);

public sealed record SyncEntry
{
    public required string IdemKey { get; init; }
    public required string SaleId { get; init; }
    public required string TenantId { get; init; }
    public required string DeviceId { get; init; }
    public required string DocType { get; init; }
    public required long InvoiceNo { get; init; }
    public required long DeviceSeq { get; init; }
    public required string BusinessDate { get; init; }
    public required DateTime DeviceClockAt { get; init; }
    public required string ShiftId { get; init; }
    public string? OriginalIdemKey { get; init; }
    public required decimal TotalNet { get; init; }
    public required decimal TotalVat { get; init; }
    public required decimal TotalGross { get; init; }
    public required byte[] PrevHash { get; init; }
    public required byte[] EntryHash { get; init; }
    public required byte[] PayloadHash { get; init; }
    public required bool PastCeiling { get; init; }
    public required IReadOnlyList<SyncLine> Lines { get; init; }
    public required IReadOnlyList<SyncJournalLine> JournalLines { get; init; }
}

public enum EntryOutcome
{
    /// <summary>رُحِّل إلى دفتر الأستاذ لأول مرة.</summary>
    Posted,
    /// <summary>وصل من قبل بالمفتاح نفسه <b>وبالمحتوى نفسه</b> — لا شيء يُفعل. هذا هو الطريق السعيد لإعادة الإرسال.</summary>
    Duplicate,
    /// <summary>المفتاح نفسه بمحتوى <b>مختلف</b>: خطر فقدان بيانات صامت. يُرفع كاستثناء بشري.</summary>
    ConflictMismatch,
    /// <summary>مقبول ومحفوظ لكنه غير مُرحَّل: ينتظر قراراً أو مستنداً أصلياً.</summary>
    Quarantined,
    /// <summary>مرفوض نهائياً (مثلاً رقم فاتورة خارج مدى الجهاز).</summary>
    Rejected,
    /// <summary>عطل عابر في الخادم: على الجهاز إعادة الإرسال (والحصانة تحميه).</summary>
    TransientError
}

public sealed record EntryAck(string IdemKey, EntryOutcome Outcome, string Note, long? LedgerEntryNo);

public sealed record SyncResponse(
    bool Accepted,
    string RejectReason,
    IReadOnlyList<EntryAck> Acks,
    int MaxBatchSize,
    int RetryAfterMs,
    DateTime ServerUtc,
    long ServerMinusDeviceMs);

public sealed record RangeGrant(string RangeId, long Start, long End);
