namespace Babel.Ledger.Persistence;

/// <summary>
/// رأس القيد. جدول <b>يُضاف إليه فقط</b>: لا تعديل ولا حذف بعد الترحيل، والتصحيح
/// بقيد عكسي حصراً (ADR-0002 · ADR-0003).
/// <para>
/// الفاعل والمصدر ونوع المستند والمعرّف <b>أعمدة في الرأس</b> لا بيانات وصفية جانبية:
/// المدقّق يحتاج الارتباط والسببية واسم المستخدم في نفس الصف (فخ-07).
/// </para>
/// </summary>
internal sealed class JournalEntryRow
{
    public Guid EntryId { get; set; }
    public Guid CompanyId { get; set; }
    public string BookId { get; set; } = string.Empty;
    public int FiscalYear { get; set; }

    /// <summary>رقم من صفّ العدّاد بلا فجوات لكل (شركة × دفتر × سنة مالية). ليس <c>SEQUENCE</c>.</summary>
    public long EntryNo { get; set; }

    public DateOnly EntryDate { get; set; }
    public string PeriodCode { get; set; } = string.Empty;

    /// <summary>لحظة الترحيل، مقصوصة إلى الميكروثانية قبل التجزئة وقبل التخزين (فخ-16).</summary>
    public DateTimeOffset PostedAt { get; set; }

    public string Status { get; set; } = "POSTED";
    public string Actor { get; set; } = string.Empty;
    public string ActorSearch { get; set; } = string.Empty;
    public string Memo { get; set; } = string.Empty;

    /// <summary>البيان العربي المُوقَّع — كما أدخله المستخدم، NFC فقط.</summary>
    public string MemoAr { get; set; } = string.Empty;

    /// <summary>عمود البحث المشتقّ. <b>لا يدخل البايتات المُجزَّأة أبداً</b> (فخ-26).</summary>
    public string MemoArSearch { get; set; } = string.Empty;

    public string SourceModule { get; set; } = string.Empty;
    public string SourceDocType { get; set; } = string.Empty;
    public string SourceDocId { get; set; } = string.Empty;
    public string PostingTriggerCode { get; set; } = string.Empty;

    /// <summary>جيل الترحيل — يزيد بعد عكس مشروع فقط، فيُتيح إعادة الترحيل مصحَّحاً.</summary>
    public int PostingGeneration { get; set; } = 1;

    public string EventCode { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;

    /// <summary>القيد الذي يعكسه هذا القيد. القيد الأصلي لا يُمسّ إطلاقاً.</summary>
    public Guid? ReversesEntryId { get; set; }

    public string? ReversalReasonAr { get; set; }
    public string? ReversalReasonEn { get; set; }
    public string? ClosedPeriodPermission { get; set; }
    public string? ClosedPeriodAuthoriser { get; set; }

    public ICollection<JournalLineRow> Lines { get; } = [];
}
