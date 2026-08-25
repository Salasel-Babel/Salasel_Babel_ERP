using System.Collections.Concurrent;
using Babel.SharedKernel;

namespace Babel.Core.CompanySetup;

/// <summary>
/// مخزن تأسيس المنشآت.
/// <para>
/// <b>لاحظ التواقيع — فيها القرار كلّه:</b>
/// </para>
/// <list type="number">
///   <item><description>
///     <see cref="TryFoundAsync"/> تقبل <see cref="FoundedCompany"/> — أي مؤسَّسةً مُصدَّقة لا
///     مسوّدة — و<b>تُرجع <c>bool</c></b>: <c>false</c> تعني «مؤسَّسة من قبل». فالتأسيس
///     الثاني ليس شيئاً يُفحَص بترتيب صحيح عند مستدعٍ منضبط، بل عمليةٌ ذرّية لا تنجح مرّتين.
///   </description></item>
///   <item><description>
///     <see cref="TryReplaceCostCentersAsync"/> تقبل <b>سجلّ مراكز التكلفة وحده</b>. ولا يوجد
///     في هذه الواجهة — ولا في أي مكان آخر في الشجرة — توقيعٌ يحمل <see cref="DisplayScale"/>
///     إلى منشأة قائمة. <b>ثبات المقياس غيابُ باب، لا قفلٌ على باب.</b>
///   </description></item>
/// </list>
/// </summary>
public interface ICompanySetupStore
{
    /// <summary>يقرأ تأسيس منشأة، أو <c>null</c> إن لم تُؤسَّس بعد.</summary>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<FoundedCompany?> FindAsync(TenantId tenant, CancellationToken cancellationToken = default);

    /// <summary>
    /// يؤسّس منشأة إن لم تكن مؤسَّسة. يُرجع <c>false</c> — ولا يستبدل — إن كانت.
    /// </summary>
    /// <param name="setup">التأسيس المُصدَّق. المنشأة تُقرأ منه لا من وسيط ثانٍ يخالفه.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<bool> TryFoundAsync(FoundedCompany setup, CancellationToken cancellationToken = default);

    /// <summary>
    /// يستبدل سجلّ مراكز التكلفة لمنشأة مؤسَّسة. يُرجع <c>false</c> إن لم تكن مؤسَّسة.
    /// </summary>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="costCenters">السجلّ الجديد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<bool> TryReplaceCostCentersAsync(
        TenantId tenant,
        CostCenterRegister costCenters,
        CancellationToken cancellationToken = default);
}

/// <summary>تنفيذ في الذاكرة — موجة الهيكل، على نمط الاستحقاق وملفّ القدرات.</summary>
public sealed class InMemoryCompanySetupStore : ICompanySetupStore
{
    private readonly ConcurrentDictionary<TenantId, FoundedCompany> _byTenant = new();

    /// <inheritdoc />
    public ValueTask<FoundedCompany?> FindAsync(TenantId tenant, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_byTenant.TryGetValue(tenant, out FoundedCompany? found) ? found : null);
    }

    /// <inheritdoc />
    public ValueTask<bool> TryFoundAsync(FoundedCompany setup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setup);
        cancellationToken.ThrowIfCancellationRequested();

        // ‏TryAdd لا الإسناد: الإسناد يجعل التأسيس الثاني ينجح صامتاً ويستبدل المقياس.
        return ValueTask.FromResult(_byTenant.TryAdd(setup.Company, setup));
    }

    /// <inheritdoc />
    public ValueTask<bool> TryReplaceCostCentersAsync(
        TenantId tenant,
        CostCenterRegister costCenters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(costCenters);
        cancellationToken.ThrowIfCancellationRequested();

        while (_byTenant.TryGetValue(tenant, out FoundedCompany? existing))
        {
            if (_byTenant.TryUpdate(tenant, existing.WithCostCenters(costCenters), existing))
            {
                return ValueTask.FromResult(true);
            }
        }

        return ValueTask.FromResult(false);
    }
}
