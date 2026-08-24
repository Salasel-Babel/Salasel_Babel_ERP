using Babel.Contracts.Posting;

namespace Babel.Ledger.PostingMatrix;

/// <summary>
/// جسر بين <see cref="PostingRole"/> — المجموعة <b>المغلقة</b> في العقد — ورموز الأدوار
/// الـ76 في <c>data/posting-matrix/account-roles.csv</c>.
/// <para>
/// <b>هذا الجسر يكشف فجوة حقيقية في البيانات لا في الكود:</b> العقد يحمل 14 دوراً
/// والمصفوفة تحمل 76. أي أن <b>المسار الصريح</b> (وحدة تُسلّم سطورها بأدوارها) يصل
/// إلى 14 دوراً فقط، بينما <b>مسار الحدث</b> (‏<see cref="PostingRequest.Event"/>) يصل
/// إلى الـ76 كلها لأن القالب يحمل رمز الدور نصّاً من المصفوفة.
/// ولذلك مسار الحدث هو المسار الأساسي، والمسار الصريح للقيود اليدوية وما شابهها.
/// </para>
/// <para>
/// ⚠️ التعيينات المعلَّمة أدناه <b>اختيارات هذا التسليم</b> ولا تسندها وثيقة تحليل:
/// تحتاج مراجعة محاسب قبل أن يُبنى عليها شيء، تماماً كما يفرض <c>data/README.md</c> §6
/// على كل ما هو <c>proposed</c>.
/// </para>
/// </summary>
internal static class PostingRoleCodes
{
    /// <summary>رمز الدور في المصفوفة المقابل لقيمة العقد.</summary>
    public static string Of(PostingRole role) => role switch
    {
        PostingRole.NetAmount => "sales_revenue",
        PostingRole.OutputTax => "vat_output",
        PostingRole.InputTax => "vat_input",

        // ⚠️ الإجمالي شامل الضريبة يقع على ذمة العميل — تعيين هذا التسليم.
        PostingRole.GrossAmount => "ar_customer_control",

        // ⚠️ الخصم يُحمَّل على مردودات ومسموحات المبيعات — تعيين هذا التسليم.
        PostingRole.Discount => "sales_returns",

        PostingRole.Retention => "retention_payable",
        PostingRole.AdvanceSettlement => "customer_advance",
        PostingRole.CostOfGoodsSold => "cogs",
        PostingRole.InventoryMovement => "inventory_control",
        PostingRole.Settlement => "settlement_account",
        PostingRole.RoundingDifference => "cash_over_short",

        // ⚠️ فرق العملة له دوران في المصفوفة (ربح وخسارة)؛ الجانب يحسمه السطر.
        PostingRole.ExchangeDifference => "fx_gain",

        // ⚠️ الاستحقاق دور عام في العقد وله في المصفوفة أدوار متخصّصة.
        PostingRole.Accrual => "accrued_unbilled_rental_income",

        PostingRole.Depreciation => "depreciation_expense",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "دور ترحيل غير معروف. / Unknown posting role."),
    };

    /// <summary>الدور المقابل للجانب الآخر حين يكون للدور وجهان (ربح/خسارة).</summary>
    public static string OfSide(PostingRole role, PostingSide side)
        => role == PostingRole.ExchangeDifference && side == PostingSide.Debit ? "fx_loss" : Of(role);
}
