using Babel.SharedKernel;

namespace Babel.Purchasing.Subledger;

/// <summary>
/// حدّ قراءة <b>نقطة الضبط</b> في دفتر الأستاذ.
/// <para>
/// الوحدة لا تسمّي حساباً ولا تقرأ جداول الدفتر (القاعدتان 1 و2)، لكن الدفتر المساعد
/// بلا مطابقة مع نقطة ضبطه دفترٌ يجرف بصمت. ولذلك تُعلن الوحدة <b>منفذاً</b> يتكلّم
/// بمفردات الدفاتر المساعدة (‏<c>supplier</c>) لا بمفردات الحسابات، ويصله الجذر
/// التركيبي بالدفتر.
/// </para>
/// <para>
/// ⚠️ مكان هذا العقد الطبيعي <c>Babel.Contracts</c> كي لا يُكرَّر في كل دفتر مساعد —
/// وهو بند في تقرير هذا التسليم، لا تعديل يُتخذ ضمناً.
/// </para>
/// </summary>
public interface IControlPointReader
{
    /// <summary>
    /// يقرأ صافي حركة نقطة الضبط لنوع دفتر مساعد حتى تاريخ، مفصّلة بالمستند.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="subledgerKind">نوع الدفتر المساعد كما تعرّفه بيانات الدفتر (<c>supplier</c>).</param>
    /// <param name="asOf">التاريخ الذي تُقرأ الحركة حتى نهايته.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<ControlPointSnapshot>> ReadAsync(
        TenantId tenant,
        string subledgerKind,
        DateOnly asOf,
        CancellationToken cancellationToken = default);
}

/// <summary>لقطة نقطة الضبط: الصافي وتفصيله بالمستند.</summary>
/// <param name="Net">صافي الحركة بمنطق «مدين ناقص دائن» بعملة الشركة.</param>
/// <param name="Movements">حركة كل مستند على حدة.</param>
public sealed record ControlPointSnapshot(decimal Net, IReadOnlyList<ControlPointMovement> Movements);

/// <summary>حركة مستند واحد على نقطة الضبط.</summary>
/// <param name="DocumentType">نوع المستند كما أرسلته الوحدة.</param>
/// <param name="DocumentId">معرّف المستند.</param>
/// <param name="PartyId">الطرف في الدفتر المساعد.</param>
/// <param name="Net">صافي «مدين ناقص دائن» لسطور هذا المستند على نقطة الضبط.</param>
public sealed record ControlPointMovement(string DocumentType, string DocumentId, string PartyId, decimal Net);

/// <summary>سبب انحراف مستند بين الدفتر المساعد ونقطة الضبط.</summary>
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

/// <summary>سطر انحراف واحد في تقرير المطابقة.</summary>
/// <param name="DocumentType">نوع المستند.</param>
/// <param name="DocumentId">معرّفه.</param>
/// <param name="PartyId">الطرف.</param>
/// <param name="SubledgerEffect">أثره كما يعرفه الدفتر المساعد.</param>
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
/// تقرير المطابقة بين الدفتر المساعد ونقطة ضبطه.
/// <para>
/// ليس تقريراً يُطبع بل <b>وظيفة</b>: انحرافٌ واحد بريال واحد يُسمّي المستندات المسؤولة
/// عنه. دفترٌ مساعد ينحرف بصمت عن نقطة ضبطه هو أشيع عيب في الأنظمة المحاسبية،
/// ولا يُكتشف إلا بعد شهور.
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
