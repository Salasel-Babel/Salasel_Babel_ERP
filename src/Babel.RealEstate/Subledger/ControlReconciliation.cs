using Babel.SharedKernel;

namespace Babel.RealEstate.Subledger;

/// <summary>سبب انحراف بين الدفتر المساعد للمستأجرين ونقطة ضبطه.</summary>
public static class DivergenceReason
{
    /// <summary>الوحدة تعدّه مُرحَّلاً ولا حركة له في نقطة الضبط.</summary>
    public const string MissingInControl = "missing_in_control";

    /// <summary>حركة في نقطة الضبط بلا مستند مقابل في الدفتر المساعد.</summary>
    public const string MissingInSubledger = "missing_in_subledger";

    /// <summary>المستند موجود على الطرفين والمبلغان مختلفان.</summary>
    public const string AmountMismatch = "amount_mismatch";

    /// <summary>محاولة ترحيل عالقة: سُجّلت النية ولم يُعرف مصيرها بعد.</summary>
    public const string PostingUnresolved = "posting_unresolved";
}

/// <summary>سطر انحراف واحد.</summary>
/// <param name="DocumentType">نوع المستند.</param>
/// <param name="DocumentId">معرّفه.</param>
/// <param name="PartyId">الطرف.</param>
/// <param name="SubledgerEffect">أثره كما تعرفه الوحدة.</param>
/// <param name="ControlEffect">أثره كما هو في نقطة الضبط.</param>
/// <param name="Divergence">الفارق.</param>
/// <param name="ReasonCode">سبب الانحراف.</param>
public sealed record ReconciliationDivergence(
    string DocumentType,
    string DocumentId,
    string PartyId,
    Money SubledgerEffect,
    Money ControlEffect,
    Money Divergence,
    string ReasonCode);

/// <summary>
/// مطابقة دفتر المستأجرين المساعد بنقطة ضبطه.
/// <para>
/// <b>وهي ما يفرضه <c>subledger-types.csv</c> على دفتر <c>tenant</c> نصّاً</b>: «مجموع
/// أرصدة المستأجرين = رصيد الحساب». ودفترٌ مساعد ينحرف بصمت عن نقطة ضبطه أشيع عيب في
/// الأنظمة المحاسبية ولا يُكتشف إلا بعد شهور — ولذلك يُبنى الكشف عنه اليوم لا حين يُطلَب،
/// ويُنشر <b>في الجواب نفسه</b> لا في تقرير ثانٍ قد لا يُفتح.
/// </para>
/// </summary>
/// <param name="AsOf">تاريخ المطابقة.</param>
/// <param name="SubledgerTotal">مجموع الدفتر المساعد المحسوب من مستنداته.</param>
/// <param name="ControlTotal">رصيد نقطة الضبط في دفتر الأستاذ.</param>
/// <param name="Divergence">الفارق: مجموع الدفتر المساعد ناقص نقطة الضبط.</param>
/// <param name="IsReconciled">هل الفارق صفر بالضبط؟ لا «قريب من الصفر».</param>
/// <param name="Divergences">المستندات المسؤولة عن الفارق.</param>
public sealed record ControlReconciliationReport(
    DateOnly AsOf,
    Money SubledgerTotal,
    Money ControlTotal,
    Money Divergence,
    bool IsReconciled,
    IReadOnlyList<ReconciliationDivergence> Divergences);
