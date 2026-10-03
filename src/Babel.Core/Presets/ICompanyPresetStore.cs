using System.Collections.Concurrent;
using Babel.SharedKernel;

namespace Babel.Core.Presets;

/// <summary>
/// مخزن ثوابت الشركة وعدّادات ترقيمها.
/// <para>
/// <b>الثوابت تُستبدل كلّها دفعةً واحدة</b> — مورد واحد يُقرأ ويُكتب، على نمط ملفّ القدرات.
/// <b>والعدّاد صفٌّ لكل (منشأة × سلسلة × سنة) يُقفل عند القراءة</b> (ADR-0008): لا
/// <c>SEQUENCE</c> لرقمٍ يراه مستخدم، لأن التسلسل يبقى عندها ولا يرجع عند التراجع.
/// </para>
/// </summary>
public interface ICompanyPresetStore
{
    /// <summary>يقرأ ثوابت منشأة. منشأةٌ لم تضبط شيئاً تُقرأ فارغة لا غائبة.</summary>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<CompanyPresets> ReadAsync(TenantId tenant, CancellationToken cancellationToken = default);

    /// <summary>يستبدل ثوابت منشأة كلّها بالمُصدَّقة الواصلة.</summary>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="presets">الثوابت المُصدَّقة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask ReplaceAsync(TenantId tenant, CompanyPresets presets, CancellationToken cancellationToken = default);

    /// <summary>
    /// يخصّص التسلسل التالي في سلسلةٍ وسنة، ويتقدّم العدّاد في العملية نفسها. التسلسل الأول 1.
    /// </summary>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="series">رمز السلسلة.</param>
    /// <param name="fiscalYear">السنة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<int> AllocateAsync(TenantId tenant, string series, int fiscalYear, CancellationToken cancellationToken = default);
}

/// <summary>تنفيذ في الذاكرة — للاختبارات، على نمط بقيّة مخازن النواة.</summary>
public sealed class InMemoryCompanyPresetStore : ICompanyPresetStore
{
    private readonly ConcurrentDictionary<TenantId, CompanyPresets> _presets = new();
    private readonly ConcurrentDictionary<(TenantId, string, int), int> _counters = new();
    private readonly Lock _gate = new();

    /// <inheritdoc />
    public ValueTask<CompanyPresets> ReadAsync(TenantId tenant, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_presets.TryGetValue(tenant, out CompanyPresets? found) ? found : CompanyPresets.Empty);
    }

    /// <inheritdoc />
    public ValueTask ReplaceAsync(TenantId tenant, CompanyPresets presets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(presets);
        cancellationToken.ThrowIfCancellationRequested();
        _presets[tenant] = presets;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<int> AllocateAsync(TenantId tenant, string series, int fiscalYear, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            int next = _counters.TryGetValue((tenant, series, fiscalYear), out int current) ? current : 1;
            _counters[(tenant, series, fiscalYear)] = next + 1;
            return ValueTask.FromResult(next);
        }
    }
}
