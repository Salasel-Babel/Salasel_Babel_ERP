namespace Babel.Contracts.Posting;

/// <summary>
/// إيصال الترحيل. ما يُعاد إلى الوحدة الطالبة — ولا شيء غيره:
/// لا حسابات، ولا أرصدة، ولا سطور الدفتر.
/// </summary>
/// <param name="JournalEntryId">معرّف القيد الناتج.</param>
/// <param name="EntryNumber">رقم القيد من العدّاد بلا فجوات لكل (مستأجر × دفتر) — وثيقة المعمارية §7.3.</param>
/// <param name="EntryHash">بصمة القيد في سلسلة التجزئة، بصيغة سداسية عشرية صغيرة.</param>
/// <param name="WasAlreadyPosted">
/// صحيح إذا كان مفتاح الحصانة قد رُحِّل من قبل. الوصول الثاني بالمفتاح نفسه لا يفعل شيئاً
/// ولا يُعدّ خطأ — مهما كان ترتيب الوصول (القاعدة المعمارية 4).
/// </param>
public sealed record PostingReceipt(Guid JournalEntryId, long EntryNumber, string EntryHash, bool WasAlreadyPosted);
