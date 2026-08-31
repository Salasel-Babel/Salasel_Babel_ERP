namespace Babel.Contracts.Posting;

/// <summary>
/// إيصال الترحيل — القيد المُرحَّل كما تراه الوحدة الطالبة، ولا شيء غيره:
/// لا حسابات، ولا أرصدة، ولا سطور الدفتر.
/// </summary>
/// <param name="JournalEntryId">معرّف القيد الناتج.</param>
/// <param name="EntryNumber">
/// رقم القيد من العدّاد بلا فجوات لكل (شركة × دفتر × سنة مالية) — صفّ عدّاد بـ<c>FOR UPDATE</c>،
/// لا <c>SEQUENCE</c>: التسلسل يُهدر أرقاماً عند التراجع (ADR-0008 · فخ-12).
/// </param>
/// <param name="EntryHash">
/// بصمة القيد في سلسلة التجزئة، hex صغير. رقم التسلسل والبصمة السابقة <b>داخل</b>
/// البايتات المُجزَّأة، لا في عمودين مجاورين (ADR-0007 · فخ-22).
/// </param>
/// <param name="WasAlreadyPosted">
/// صحيح إذا كان مفتاح الحصانة قد رُحِّل من قبل. الوصول الثاني بالمفتاح نفسه لا يفعل شيئاً
/// ولا يُعدّ خطأ — <b>مهما كان ترتيب الوصول</b> (القاعدة المعمارية 4 · فخ-13).
/// </param>
/// <param name="ChainSequence">موقع القيد في سلسلة نطاقه.</param>
/// <param name="PeriodCode">الفترة المالية التي وقع فيها القيد، بصيغة <c>yyyy-MM</c>.</param>
/// <param name="Generation">جيل الترحيل الذي كُتب به هذا القيد.</param>
/// <param name="LineCount">عدد سطور القيد الناتج بعد تقييم الشروط.</param>
public sealed record PostingReceipt(
    Guid JournalEntryId,
    long EntryNumber,
    string EntryHash,
    bool WasAlreadyPosted,
    long ChainSequence = 0,
    string PeriodCode = "",
    int Generation = 1,
    int LineCount = 0);
