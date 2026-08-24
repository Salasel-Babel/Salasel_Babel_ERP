using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.SharedKernel;

namespace Babel.Ledger.Posting;

/// <summary>
/// محرك الترحيل — الجهة الوحيدة التي تكتب قيداً في النظام كله.
/// <para>
/// المنطق المحاسبي (التوازن، الفترة، العدّاد، السلسلة، الأرصدة) موجة لاحقة.
/// الموجود هنا هو <b>الحدّ</b>: توقيع العقد، وبوابة الاستحقاق، والمكان الوحيد الذي
/// يجوز أن يعرف رقم حساب.
/// </para>
/// <para>
/// وحين يُكتب المنطق، تُطبَّق القواعد الأربع المقيسة من أول سطر (وثيقة المعمارية §6):
/// صفر ذهاب وإياب داخل قفل متنازَع عليه · تحديث الأرصدة عبارة واحدة بصفوف مرتّبة
/// <c>ORDER BY account_id ASC</c> · <c>INSERT ... ON CONFLICT DO UPDATE</c> دائماً مع تأكيد
/// عدد الصفوف · حصانة لكل قيد مستقلة عن الترتيب.
/// </para>
/// </summary>
public sealed class PostingService : IPostingService, IApplicationService
{
    private static readonly Error NotImplemented = new(
        "ledger.posting.not_implemented",
        "محرك الترحيل لم يُنفَّذ بعد: هذه موجة الهيكل، والمنطق المحاسبي موجة لاحقة.",
        "The posting engine is not implemented yet: this is the skeleton wave; accounting logic comes later.");

    private readonly IEntitlementEnforcer _enforcer;

    /// <summary>ينشئ محرك الترحيل.</summary>
    public PostingService(IEntitlementEnforcer enforcer)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        _enforcer = enforcer;
    }

    /// <inheritdoc />
    [RequiresEntitlement(BabelModule.Ledger, EntitlementAccess.Write)]
    public async ValueTask<Result<PostingReceipt>> PostAsync(PostingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // بوابتان لا واحدة: استحقاق الدفتر (الكتابة فيه)، واستحقاق الوحدة المصدر.
        // وحدة انقضى اشتراكها تبقى مقروءة بالكامل، ولا تُنشئ قيداً جديداً.
        Result ledgerGate = await _enforcer
            .EnsureAsync(request.Tenant, UserId.SystemActor, BabelModule.Ledger, EntitlementAccess.Write, "Ledger.Post", cancellationToken)
            .ConfigureAwait(false);

        if (ledgerGate.IsFailure)
        {
            return Result<PostingReceipt>.Failure(ledgerGate.Errors);
        }

        Result sourceGate = await _enforcer
            .EnsureAsync(request.Tenant, UserId.SystemActor, request.Source.Module, EntitlementAccess.Write, "Ledger.Post.Source", cancellationToken)
            .ConfigureAwait(false);

        return sourceGate.IsFailure
            ? Result<PostingReceipt>.Failure(sourceGate.Errors)
            : Result<PostingReceipt>.Failure(NotImplemented);
    }
}
