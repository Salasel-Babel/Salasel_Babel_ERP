using System.Collections.Concurrent;
using Babel.SharedKernel;

namespace Babel.Core.CapabilityProfile;

/// <summary>
/// مخزن ملفّات القدرات.
/// <para>
/// <b>لاحظ التوقيع:</b> الحفظ يقبل <see cref="ValidatedCapabilityProfile"/> ولا يقبل
/// مسودّة. أي أن «تحقَّق قبل الحفظ» ليس تعليمةً في وثيقة يلتزم بها من قرأها، بل شرطٌ
/// لا يُصرَّف الكود بدونه.
/// </para>
/// </summary>
public interface ICapabilityProfileStore
{
    /// <summary>يقرأ ملفّ مستأجر، أو <c>null</c> إن لم يكن له ملفّ.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<ValidatedCapabilityProfile?> FindAsync(TenantId tenant, CancellationToken cancellationToken = default);

    /// <summary>يحفظ ملفّ مستأجر.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="profile">الملفّ الصالح.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask SaveAsync(
        TenantId tenant,
        ValidatedCapabilityProfile profile,
        CancellationToken cancellationToken = default);
}

/// <summary>تنفيذ في الذاكرة — موجة الهيكل، على نمط الاستحقاق وقياس الاستخدام.</summary>
public sealed class InMemoryCapabilityProfileStore : ICapabilityProfileStore
{
    private readonly ConcurrentDictionary<TenantId, ValidatedCapabilityProfile> _byTenant = new();

    /// <inheritdoc />
    public ValueTask<ValidatedCapabilityProfile?> FindAsync(
        TenantId tenant,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_byTenant.TryGetValue(tenant, out ValidatedCapabilityProfile? found) ? found : null);
    }

    /// <inheritdoc />
    public ValueTask SaveAsync(
        TenantId tenant,
        ValidatedCapabilityProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();
        _byTenant[tenant] = profile;
        return ValueTask.CompletedTask;
    }
}
