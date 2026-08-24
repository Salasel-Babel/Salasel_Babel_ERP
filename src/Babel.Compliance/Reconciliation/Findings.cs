using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Reconciliation;

/// <summary>
/// أنواع الاختلاف بين ما رحّله الدفتر وما بناه الالتزام وما أقرّت به الجهة.
/// <b>المطابقة ميزة من الدرجة الأولى، لا أثر جانبي</b>: بدونها لا يعرف أحد
/// أن فاتورة صدرت ولم تُبلَّغ، ولا أن إرسالاً تكرّر.
/// </summary>
public enum FindingKind
{
    /// <summary>الدفتر رحّل مستنداً خاضعاً ولم يُبنَ له سجل التزام إطلاقاً. الأخطر عملياً.</summary>
    PostedButNeverBuilt,

    /// <summary>بُني السجل ولم يدخل الطابور. خلل في المُنسِّق نفسه.</summary>
    BuiltButNeverQueued,

    /// <summary>في الطابور منذ مدة تتجاوز نافذة المستأجر. مؤشر مخاطرة يراه المدير المالي.</summary>
    QueuedTooLong,

    /// <summary>مهلة غامضة لم تُحسم آلياً. بند في الطابور البشري.</summary>
    UnresolvedAmbiguity,

    /// <summary>الجهة تُقرّ بمستند لا يقابله قيد مُرحَّل. لا ينبغي أن يحدث في قناة أحادية الاتجاه.</summary>
    AcknowledgedButNotPosted,

    /// <summary>المستند نفسه قُبل أكثر من مرة. الأثر المباشر للإرسال المكرّر.</summary>
    DuplicateAcceptance,

    /// <summary>فجوة في عدّاد وحدة الإصدار. التسلسل بلا فجوات شرط، والفجوة تُؤكَّد إيجاباً لا تُستنتج.</summary>
    CounterGap,

    /// <summary>رابط السلسلة لا يطابق بصمة سابقه. تنبيه بأعلى درجة يوقف الإصدار على الوحدة.</summary>
    ChainBroken,

    /// <summary>مبلغ المستند لا يطابق مبلغ القيد المقابل. مقارنة decimal، لا float.</summary>
    AmountMismatch,

    /// <summary>قُبل المستند ولم تُخزَّن النسخة المختومة العائدة.</summary>
    StampedCopyMissing
}

public enum FindingSeverity
{
    Information,
    Warning,
    Critical,

    /// <summary>يوقف الإصدار على وحدة الإصدار المتأثرة حتى المعالجة.</summary>
    Blocking
}

/// <summary>
/// نتيجة مطابقة واحدة. كل نتيجة تحمل <b>ما يكفي الإنسان للحسم</b>:
/// أي مستند، وأي وحدة، وأي عدّاد، وأي قيد، وماذا يفعل تالياً — بالعربية والإنجليزية.
/// </summary>
public sealed class ReconciliationFinding
{
    public required Guid FindingId { get; init; }
    public required TenantId Tenant { get; init; }
    public required FindingKind Kind { get; init; }
    public required FindingSeverity Severity { get; init; }
    public required DateTimeOffset DetectedAt { get; init; }

    public ComplianceDocumentId? DocumentId { get; init; }
    public IssuingUnitId? IssuingUnit { get; init; }
    public long? Counter { get; init; }
    public JournalEntryRef? JournalEntry { get; init; }

    public decimal? ExpectedAmount { get; init; }
    public decimal? ObservedAmount { get; init; }

    public required string SummaryAr { get; init; }
    public required string SummaryEn { get; init; }
    public required string NextStepAr { get; init; }
    public required string NextStepEn { get; init; }

    /// <summary>هل حسمها المُطابِق آلياً؟</summary>
    public bool AutoResolved { get; set; }

    public bool Resolved { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ResolutionNoteAr { get; set; }
    public string? ResolutionNoteEn { get; set; }
}

/// <summary>
/// أرقام المطابقة. <b>كل المبالغ decimal</b> — تُقارن بالمساواة الدقيقة لا بتقريب.
/// </summary>
public sealed record ReconciliationTotals(
    int LedgerDocuments,
    int ComplianceDocuments,
    int Accepted,
    int Pending,
    int Rejected,
    int Unresolved,
    decimal LedgerTaxTotal,
    decimal AcceptedTaxTotal,
    decimal QuarantinedTaxTotal)
{
    /// <summary>
    /// الفارق بين ضريبة الدفتر وضريبة ما قبلته الجهة. <b>هذا الرقم هو تقرير المطابقة كله
    /// في سطر واحد</b>، وهو ما يُعرض في لوحة المدير المالي.
    /// </summary>
    public decimal TaxGap => LedgerTaxTotal - AcceptedTaxTotal;
}

public sealed record ReconciliationReport(
    TenantId Tenant,
    DateTimeOffset From,
    DateTimeOffset To,
    DateTimeOffset RanAt,
    ReconciliationTotals Totals,
    IReadOnlyList<ReconciliationFinding> Findings)
{
    public bool IsClean => Findings.Count == 0;

    public IReadOnlyList<ReconciliationFinding> Blocking =>
        [.. Findings.Where(f => f.Severity == FindingSeverity.Blocking)];

    public IReadOnlyList<ReconciliationFinding> NeedingHuman =>
        [.. Findings.Where(f => !f.AutoResolved && f.Severity >= FindingSeverity.Critical)];
}
