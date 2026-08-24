using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.SharedKernel;

namespace Babel.Purchasing.Application;

/// <summary>
/// اعتماد فواتير المشتريات وقراءتها.
/// <para>
/// شكل كل خدمة تطبيق في المنتج: علامة <see cref="IApplicationService"/>، وسمة استحقاق
/// على كل نقطة دخول عامة، ونداء <see cref="IEntitlementEnforcer"/> قبل أي عمل.
/// إسقاط السمة يُفشل البناء (Rule06)، لا يمرّ إلى الإنتاج.
/// </para>
/// <para>لا منطق أعمال هنا: هذه موجة الهيكل.</para>
/// </summary>
public sealed class PurchaseBillService : IApplicationService
{
    private static readonly Error NotImplemented = new(
        "purchasing.not_implemented",
        "لم يُنفَّذ بعد: هذه موجة الهيكل.",
        "Not implemented yet: this is the skeleton wave.");

    private readonly IEntitlementEnforcer _enforcer;
    private readonly IPostingService _posting;

    /// <summary>ينشئ الخدمة.</summary>
    public PurchaseBillService(IEntitlementEnforcer enforcer, IPostingService posting)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(posting);
        _enforcer = enforcer;
        _posting = posting;
    }

    /// <summary>نقطة دخول كتابة: تعمل عند <see cref="EntitlementState.Entitled"/> فقط.</summary>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<PostingReceipt>> ApproveBillAsync(TenantId tenant, UserId actor, CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Write, "Purchasing.ApproveBillAsync", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PostingReceipt>.Failure(gate.Errors);
        }

        // الترحيل عبر العقد وحده. لا DbContext للدفتر، ولا رقم حساب، ولا JOIN عابر للوحدات.
        _ = _posting;
        return Result<PostingReceipt>.Failure(NotImplemented);
    }

    /// <summary>نقطة دخول قراءة: تعمل عند <see cref="EntitlementState.ReadOnly"/> أيضاً.</summary>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Read)]
    public async ValueTask<Result> ReadBillAsync(TenantId tenant, UserId actor, CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Read, "Purchasing.ReadBillAsync", cancellationToken)
            .ConfigureAwait(false);

        return gate.IsFailure ? gate : Result.Failure(NotImplemented);
    }
}
