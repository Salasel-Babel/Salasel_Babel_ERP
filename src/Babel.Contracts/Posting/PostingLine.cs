using Babel.SharedKernel;

namespace Babel.Contracts.Posting;

/// <summary>
/// سطر في طلب ترحيل.
/// <para>
/// <b>لاحظ ما ليس هنا:</b> لا حقل حساب، ولا رقم حساب، ولا نوع حساب. غياب هذا الحقل
/// هو القاعدة 2 مطبَّقة بنيوياً؛ الاختبار في Rule02 يحرس الغياب حتى لا يعود أحد فيضيفه.
/// </para>
/// </summary>
public sealed record PostingLine
{
    /// <summary>دور السطر في الحدث التجاري.</summary>
    public required PostingRole Role { get; init; }

    /// <summary>جانب السطر: مدين أو دائن.</summary>
    public required PostingSide Side { get; init; }

    /// <summary>المبلغ بعملة الحركة. <see cref="Money"/> يفرض decimal ومقياس 4.</summary>
    public required Money Amount { get; init; }

    /// <summary>النطاق التحليلي: فرع ومركز تكلفة ومشروع.</summary>
    public PostingScope Scope { get; init; }

    /// <summary>الطرف في الدفتر المساعد، إن وُجد.</summary>
    public SubledgerReference Subledger { get; init; } = SubledgerReference.None;

    /// <summary>بيان السطر ثنائي اللغة، إن وُجد.</summary>
    public LocalizedName? Narration { get; init; }
}
