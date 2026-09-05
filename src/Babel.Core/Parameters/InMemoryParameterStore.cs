using System.Collections.Concurrent;
using Babel.Contracts.Parameters;
using Babel.SharedKernel;

namespace Babel.Core.Parameters;

/// <summary>
/// مخزنٌ في الذاكرة — <b>لمن لا قاعدة بيانات له</b>: اختبارات الوحدة والتحميل الزائد
/// الذي لا قاعدة فيه. وهو <b>ليس تنفيذ الخادم</b>؛ الخادم يركّب
/// <c>PostgresParameterStore</c>، وحارسٌ يبني الجذر التركيبي ويسأل الحاوية عن النوع
/// الفعلي — كما في نظيره في التأسيس تماماً.
/// <para>
/// وهو يُحمَّل بافتراضات المنصّة نفسها من الملفّ نفسه، فلا يختلف سلوكُ اختبارٍ عن
/// سلوك خادم في «ما الذي يُقرأ حين لا تجاوز؟».
/// </para>
/// </summary>
public sealed class InMemoryParameterStore : IParameterStore
{
    private readonly ConcurrentDictionary<Guid, ParameterVersionView> _versions = new();
    private readonly ConcurrentDictionary<Guid, Guid> _versionTenants = new();
    private readonly ConcurrentDictionary<string, byte> _levels = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ParameterUsageEntry> _usage = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock;

    /// <summary>ينشئ المخزن محمَّلاً بافتراضات المنصّة.</summary>
    /// <param name="clock">مصدر الوقت.</param>
    public InMemoryParameterStore(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;

        foreach (ParameterVersionView shipped in PlatformDefaults.All)
        {
            _versions[shipped.Id] = shipped;
            _versionTenants[shipped.Id] = PlatformDefaults.PlatformTenant;
            _levels[LevelKey(PlatformDefaults.PlatformTenant, shipped.Scope, shipped.SetCode, shipped.EffectiveFrom)] = 1;
        }
    }

    /// <inheritdoc />
    public ValueTask<ParameterVersionView?> FindEffectiveAsync(
        TenantId tenant, string setCode, DateOnly on, CancellationToken cancellationToken = default)
    {
        ParameterVersionView? tenantVersion = Newest(tenant.Value, setCode, on);
        ParameterVersionView? platformVersion = Newest(PlatformDefaults.PlatformTenant, setCode, on);

        return ValueTask.FromResult(tenantVersion ?? platformVersion);
    }

    /// <inheritdoc />
    public ValueTask<bool> TryDepositAsync(
        TenantId tenant, ParameterVersionView version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        if (!_levels.TryAdd(LevelKey(tenant.Value, version.Scope, version.SetCode, version.EffectiveFrom), 1))
        {
            return ValueTask.FromResult(false);
        }

        _versions[version.Id] = version;
        _versionTenants[version.Id] = tenant.Value;
        return ValueTask.FromResult(true);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<ParameterVersionView>> ListAsync(
        TenantId tenant, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyList<ParameterVersionView>>([.. Visible(tenant.Value)
            .OrderBy(static version => version.SetCode, StringComparer.Ordinal)
            .ThenBy(static version => version.EffectiveFrom)
            .ThenBy(static version => version.Scope)]);

    /// <inheritdoc />
    public ValueTask RecordUsageAsync(TenantId tenant, ParameterUsage usage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usage);

        _usage.TryAdd(
            UsageKey(tenant.Value, usage),
            new ParameterUsageEntry(tenant.Value, usage, _clock.GetUtcNow()));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<ParameterReviewRow>> ReviewAsync(
        TenantId tenant, CancellationToken cancellationToken = default)
    {
        List<ParameterReviewRow> rows = [];

        foreach (ParameterVersionView version in Visible(tenant.Value)
            .Where(static version => !ParameterApprovalInfo.IsSigned(version.Approval))
            .OrderBy(static version => version.SetCode, StringComparer.Ordinal)
            .ThenBy(static version => version.EffectiveFrom))
        {
            List<ParameterUsageEntry> used = [.. _usage.Values
                .Where(entry => entry.Tenant == tenant.Value && entry.Usage.VersionId == version.Id)
                .OrderBy(static entry => entry.RecordedAt)];

            if (used.Count == 0)
            {
                rows.Add(new ParameterReviewRow(version, null));
                continue;
            }

            rows.AddRange(used.Select(entry => new ParameterReviewRow(
                version,
                new ParameterUsageView(
                    entry.Usage.Module, entry.Usage.DocumentType, entry.Usage.DocumentId, entry.Usage.PostedOn))));
        }

        return ValueTask.FromResult<IReadOnlyList<ParameterReviewRow>>(rows);
    }

    private IEnumerable<ParameterVersionView> Visible(Guid tenant)
        => _versions.Values.Where(version =>
            _versionTenants.TryGetValue(version.Id, out Guid owner)
            && (owner == tenant || owner == PlatformDefaults.PlatformTenant));

    private ParameterVersionView? Newest(Guid owner, string setCode, DateOnly on)
        => _versions.Values
            .Where(version =>
                _versionTenants.TryGetValue(version.Id, out Guid holder) && holder == owner
                && string.Equals(version.SetCode, setCode, StringComparison.Ordinal)
                && version.EffectiveFrom <= on)
            .OrderByDescending(static version => version.EffectiveFrom)
            .ThenByDescending(static version => version.Id)
            .FirstOrDefault();

    private static string LevelKey(Guid owner, ParameterScope scope, string setCode, DateOnly effectiveFrom)
        => owner.ToString("D") + "|" + ParameterApprovalInfo.TokenOf(scope) + "|" + setCode + "|"
           + effectiveFrom.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static string UsageKey(Guid tenant, ParameterUsage usage)
        => tenant.ToString("D") + "|" + usage.VersionId.ToString("D") + "|" + (int)usage.Module + "|"
           + usage.DocumentType + "|" + usage.DocumentId.ToString("D");

    private sealed record ParameterUsageEntry(Guid Tenant, ParameterUsage Usage, DateTimeOffset RecordedAt);
}
