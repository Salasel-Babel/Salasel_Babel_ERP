using Babel.Contracts.Posting;
using Babel.SharedKernel;

namespace Babel.Ledger.Vouchers;

/// <summary>
/// سطرُ قيدٍ يدوي يسمّي <b>حساباً</b> من دليل هذه الشركة — لا دوراً (ADR-0096).
/// <para>
/// <b>ولماذا هنا لا في <c>Babel.Contracts</c>:</b> القاعدة 2 تمنع العقدَ المشترك من أن
/// يكشف عضواً يسمّي حساباً، كي لا تستطيع وحدةٌ أن تختار حسابها. والقيدُ اليدوي ليس
/// وحدةً تصف واقعة بل محاسبٌ يسمّي حسابه عمداً، فسطحُه في الدفتر وحده، ولا يبلغه إلا
/// الجذرُ التركيبي الذي يخدم الإنسان. وما عدا رمز الحساب مرآةٌ لـ<see cref="PostingLine"/>.
/// </para>
/// </summary>
public sealed record ManualVoucherLine
{
    /// <summary>رمز الحساب كما في الدليل — أرقام ASCII فقط، ويُتحقّق منه الدفتر لا المُستدعي.</summary>
    public required string AccountCode { get; init; }

    /// <summary>جانب السطر: مدين أو دائن.</summary>
    public required PostingSide Side { get; init; }

    /// <summary>المبلغ بعملة الحركة.</summary>
    public required Money Amount { get; init; }

    /// <summary>النطاق التحليلي — مركز التكلفة فيه غير فارغ (ADR-0026).</summary>
    public required PostingScope Scope { get; init; }

    /// <summary>الطرف في الدفتر المساعد، إن وُجد.</summary>
    public SubledgerReference Subledger { get; init; } = SubledgerReference.None;

    /// <summary>بيان السطر ثنائي اللغة، إن وُجد.</summary>
    public LocalizedName? Narration { get; init; }

    /// <summary>الأبعاد التحليلية الخاصة بهذا السطر، فوق أبعاد الطلب.</summary>
    public IReadOnlyList<PostingDimension> Dimensions { get; init; } = [];
}
