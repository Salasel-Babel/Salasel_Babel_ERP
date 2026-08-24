using Babel.Contracts.Posting;
using Babel.Ledger.Accounts;
using Babel.SharedKernel;

namespace Babel.Ledger.PostingMatrix;

/// <summary>
/// مصفوفة الترحيل: تحوّل (نوع المستند × الدور × الطرف) إلى رقم حساب.
/// <para>
/// <c>internal</c> عمداً: هذه الدالة هي المكان الوحيد في النظام الذي يُختار فيه حساب.
/// كشفها للخارج يعيد فتح الباب الذي أغلقته القاعدة 2.
/// </para>
/// المرجع: docs/reference/posting-matrix.md
/// </summary>
internal interface IAccountResolver
{
    AccountCode Resolve(TenantId tenant, SourceDocument source, PostingRole role, SubledgerReference subledger);
}
