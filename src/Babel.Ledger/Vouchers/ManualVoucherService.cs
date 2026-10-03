using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.CompanySetup;
using Babel.Core.Entitlement;
using Babel.Ledger.Posting;
using Babel.SharedKernel;
using Microsoft.Extensions.Logging;

namespace Babel.Ledger.Vouchers;

/// <summary>
/// سطحُ القيد اليدوي: ترحيلُ سطورٍ تسمّي حساباتها (ADR-0096).
/// <para>
/// <b>لا محرّكَ ثانياً هنا.</b> الخدمة تُسلّم الترويسة والسطور إلى <c>PostingService</c>
/// نفسه، فالبوّابة والاستحقاق وتسجيلُ الرفض والحصانةُ ضد التكرار والنداءُ الواحد إلى
/// <c>ledger.post_entry</c> كلُّها مشتركة. وما تضيفه هو <b>بابٌ</b> لا يمرّ منه إلا سطرُ
/// حسابٍ على حدثٍ تُعلن المصفوفة سطورَه يدويةً — والمحرّك يرفض ما سوى ذلك.
/// </para>
/// </summary>
public sealed class ManualVoucherService : IApplicationService
{
    private readonly PostingService _posting;

    /// <summary>ينشئ السطح فوق المحرّك نفسه — بالمنفِّذ نفسه والموارد نفسها.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الدفتر.</param>
    /// <param name="company">عملة المنشأة.</param>
    /// <param name="logger">سجلّ الخادم.</param>
    public ManualVoucherService(
        IEntitlementEnforcer enforcer,
        LedgerRuntime runtime,
        ICompanyMoneyResolver company,
        ILogger<ManualVoucherService> logger)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(logger);
        _posting = new PostingService(enforcer, runtime, company, logger);
    }

    /// <summary>
    /// يرحّل قيداً يدوياً سطورُه تسمّي حساباتها. <paramref name="header"/> تحمل الهوية
    /// والسياق و<b>سطورها فارغة</b>؛ والسطور في <paramref name="lines"/>.
    /// </summary>
    /// <param name="header">ترويسة الطلب: المستأجر والمفتاح والمصدر والحدث والتاريخ.</param>
    /// <param name="lines">سطور الحساب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Ledger, EntitlementAccess.Write)]
    public ValueTask<Result<PostingReceipt>> PostAsync(
        PostingRequest header,
        IReadOnlyList<ManualVoucherLine> lines,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(lines);

        // البوّابة والرفض والتنفيذ في المحرّك — هنا تسليمٌ لا قرار.
        return _posting.PostManualAsync(header, lines, cancellationToken);
    }
}
