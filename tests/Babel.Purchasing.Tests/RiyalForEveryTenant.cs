using Babel.Core.CompanySetup;
using Babel.SharedKernel;

namespace Babel.Purchasing.Tests;

/// <summary>
/// عملةٌ ثابتة لكلّ منشأة في اختباراتٍ لا تملك مخزنَ تأسيس: <b>معطى اختبار</b> لا افتراضُ
/// منتج — الوحدةُ في الإنتاج تسأل صفَّ التأسيس (ADR-0089).
/// </summary>
internal sealed class RiyalForEveryTenant : ICompanyMoneyResolver
{
    /// <inheritdoc />
    public ValueTask<Result<CompanyMoney>> ResolveAsync(TenantId company, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(CompanyMoney.Of("SAR"));
}
