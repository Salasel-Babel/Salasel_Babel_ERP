namespace Babel.Contracts.Posting;

/// <summary>
/// واقعة من سياق المستند تُقيَّم عليها الشروط وقواعد الحجب، مثل
/// <c>property.ownership_model = managed_for_others</c> أو <c>unit.vat_treatment = standard</c>.
/// <para>
/// نصّ مقابل نصّ عمداً: الدفتر لا يعرف جداول الوحدات (القاعدة 5)، والوحدة هي التي
/// تُصرّح بالوقائع التي تخصّ حدثها. وقاعدة حجب مثل <c>GR-RE-001</c> تُقيَّم على هذه
/// الوقائع قبل كتابة أي سطر.
/// </para>
/// </summary>
/// <param name="Path">مسار الواقعة بالنقاط، كما تكتبه تعابير المصفوفة.</param>
/// <param name="Value">قيمتها النصّية.</param>
public sealed record PostingFact(string Path, string Value);
