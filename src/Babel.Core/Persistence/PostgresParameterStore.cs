using System.Globalization;
using Babel.Contracts.Parameters;
using Babel.Core.Parameters;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Babel.Core.Persistence;

/// <summary>
/// مخزن المعامِلات فوق PostgreSQL — <b>وهو ما يجعل «غيّرتُ النسبة» جملةً تصمد بعد
/// إعادة الإقلاع، و«لم تُغيَّر نسبةُ الماضي» جملةً يمكن إثباتها</b>.
/// <para>
/// <c>internal</c> بحكم القاعدة 5، ويُسجَّل من <c>CoreModuleRegistration</c> وحدها.
/// </para>
/// </summary>
internal sealed class PostgresParameterStore : IParameterStore
{
    private readonly DbContextOptions<CoreDbContext> _options;
    private readonly string _connectionString;
    private readonly TimeProvider _clock;

    /// <summary>ينشئ المخزن.</summary>
    /// <param name="options">إعدادات النواة — اتصال <b>دور التطبيق</b> وحده.</param>
    /// <param name="clock">مصدر الوقت.</param>
    public PostgresParameterStore(CoreOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        DbContextOptionsBuilder<CoreDbContext> builder = new();
        builder.UseNpgsql(options.AppConnectionString);
        _options = builder.Options;
        _connectionString = options.AppConnectionString;
        _clock = clock;
    }

    /// <inheritdoc />
    public async ValueTask<ParameterVersionView?> FindEffectiveAsync(
        TenantId tenant, string setCode, DateOnly on, CancellationToken cancellationToken = default)
    {
        await using CoreDbContext context = new(_options);

        // ‏**نطاق المستأجر مفروضٌ في الاستعلام**: صفوف هذه المنشأة وصفوف المنصّة وحدها.
        List<ParameterVersionRow> candidates = await context.ParameterVersions
            .Where(row => (row.TenantId == tenant.Value || row.TenantId == Guid.Empty)
                          && row.SetCode == setCode
                          && row.EffectiveFrom <= on)
            .OrderByDescending(row => row.EffectiveFrom)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // ‏**تجاوزُ المستأجر أوّلاً، ثم افتراضُ المنصّة** — والترتيب هو المعنى كلّه.
        ParameterVersionRow? chosen =
            candidates.FirstOrDefault(row => row.TenantId == tenant.Value)
            ?? candidates.FirstOrDefault(row => row.TenantId == Guid.Empty);

        return chosen is null ? null : await HydrateAsync(context, chosen, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryDepositAsync(
        TenantId tenant, ParameterVersionView version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        await using CoreDbContext context = new(_options);

        context.ParameterVersions.Add(new ParameterVersionRow
        {
            Id = version.Id,
            TenantId = tenant.Value,
            SetCode = version.SetCode,
            Scope = ParameterApprovalInfo.TokenOf(version.Scope),
            EffectiveFrom = version.EffectiveFrom,
            Approval = ParameterApprovalInfo.TokenOf(version.Approval),
            ApprovedBy = version.ApprovedBy,
            ApprovedOn = version.ApprovedOn,
            SourceRef = version.SourceRef,
            DepositedAt = _clock.GetUtcNow(),
        });

        foreach (ParameterValueView value in version.Values)
        {
            context.ParameterValues.Add(new ParameterValueRow
            {
                VersionId = version.Id,
                Key = value.Key,
                Kind = ParameterApprovalInfo.TokenOf(value.Kind),
                Value = value.Value,
            });
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException failure) when (IsUniqueViolation(failure))
        {
            // إصدارٌ ثانٍ على (المستوى · المجموعة · تاريخ السريان) نفسه ليس عطلاً بل
            // الجواب. والذرّية فهرسٌ فريد لا فحصٌ يسبق كتابةً — والفحص السابق سباق.
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ParameterVersionView>> ListAsync(
        TenantId tenant, CancellationToken cancellationToken = default)
    {
        await using CoreDbContext context = new(_options);

        List<ParameterVersionRow> rows = await context.ParameterVersions
            .Where(row => row.TenantId == tenant.Value || row.TenantId == Guid.Empty)
            .OrderBy(row => row.SetCode)
            .ThenBy(row => row.EffectiveFrom)
            .ThenBy(row => row.Scope)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid[] ids = [.. rows.Select(row => row.Id)];

        List<ParameterValueRow> values = await context.ParameterValues
            .Where(row => ids.Contains(row.VersionId))
            .OrderBy(row => row.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(row => View(row, values.Where(value => value.VersionId == row.Id)))];
    }

    /// <inheritdoc />
    public async ValueTask RecordUsageAsync(
        TenantId tenant, ParameterUsage usage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usage);

        await using CoreDbContext context = new(_options);

        context.ParameterUsage.Add(new ParameterUsageRow
        {
            TenantId = tenant.Value,
            VersionId = usage.VersionId,
            Module = (int)usage.Module,
            DocumentType = usage.DocumentType,
            DocumentId = usage.DocumentId,
            PostedOn = usage.PostedOn,
            RecordedAt = _clock.GetUtcNow(),
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException failure) when (IsUniqueViolation(failure))
        {
            // ‏**الترحيل آمنُ التكرار.** ترحيلٌ ثانٍ للمستند نفسه بالإصدار نفسه واقعةٌ
            // مسجَّلةٌ سلفاً، لا واقعةٌ ثانية — فيُبتلع الرفض ولا يُرفع إلى المستدعي.
        }
    }

    /// <summary>
    /// <b>الاستعلام الواحد</b> الذي تقف عليه قائمةُ مراجعة المحاسب. مكتوبٌ نصّاً لا
    /// بمولّد استعلامات لسببين: أنه <b>واحد</b> فعلاً وليس ثلاثة يجمعها المستدعي،
    /// وأن نطاق المستأجر فيه <b>مقروءٌ بالعين</b> في الشرط لا مستنتَجٌ من تعبير.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<IReadOnlyList<ParameterReviewRow>> ReviewAsync(
        TenantId tenant, CancellationToken cancellationToken = default)
    {
        const string Sql = """
            select v.version_id,
                   v.tenant_id,
                   v.set_code,
                   v.scope,
                   v.effective_from,
                   v.approval,
                   v.approved_by,
                   v.approved_on,
                   v.source_ref,
                   coalesce((select string_agg(pv.key || '=' || pv.kind || '=' || pv.value::text, ';'
                                               order by pv.key)
                             from core.parameter_value pv
                             where pv.version_id = v.version_id), '') as packed_values,
                   u.module,
                   u.document_type,
                   u.document_id,
                   u.posted_on
            from core.parameter_version v
            left join core.parameter_usage u
                   on u.version_id = v.version_id
                  and u.tenant_id = $1
            where v.approval <> 'auditor_signed'
              and (v.tenant_id = $1 or v.tenant_id = '00000000-0000-0000-0000-000000000000')
            order by v.set_code, v.effective_from, v.version_id, u.posted_on, u.document_id
            """;

        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand command = new(Sql, connection);
        command.Parameters.AddWithValue(tenant.Value);

        List<ParameterReviewRow> rows = [];

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ParameterVersionView version = new(
                reader.GetGuid(0),
                reader.GetString(2),
                ParameterApprovalInfo.ScopeFrom(reader.GetString(3)),
                DateOnly.FromDateTime(reader.GetDateTime(4)),
                ParameterApprovalInfo.ApprovalFrom(reader.GetString(5)),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : DateOnly.FromDateTime(reader.GetDateTime(7)),
                reader.GetString(8),
                Unpack(reader.GetString(9)));

            ParameterUsageView? usage = reader.IsDBNull(10)
                ? null
                : new ParameterUsageView(
                    (BabelModule)reader.GetInt32(10),
                    reader.GetString(11),
                    reader.GetGuid(12),
                    DateOnly.FromDateTime(reader.GetDateTime(13)));

            rows.Add(new ParameterReviewRow(version, usage));
        }

        return rows;
    }

    private static async Task<ParameterVersionView> HydrateAsync(
        CoreDbContext context, ParameterVersionRow row, CancellationToken cancellationToken)
    {
        List<ParameterValueRow> values = await context.ParameterValues
            .Where(value => value.VersionId == row.Id)
            .OrderBy(value => value.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return View(row, values);
    }

    private static ParameterVersionView View(ParameterVersionRow row, IEnumerable<ParameterValueRow> values) => new(
        row.Id,
        row.SetCode,
        ParameterApprovalInfo.ScopeFrom(row.Scope),
        row.EffectiveFrom,
        ParameterApprovalInfo.ApprovalFrom(row.Approval),
        row.ApprovedBy,
        row.ApprovedOn,
        row.SourceRef,
        [.. values
            .OrderBy(static value => value.Key, StringComparer.Ordinal)
            .Select(static value => new ParameterValueView(value.Key, ParameterApprovalInfo.KindFrom(value.Kind), value.Value))]);

    private static List<ParameterValueView> Unpack(string packed)
    {
        if (packed.Length == 0)
        {
            return [];
        }

        List<ParameterValueView> values = [];

        foreach (string entry in packed.Split(';'))
        {
            string[] parts = entry.Split('=');
            values.Add(new ParameterValueView(
                parts[0],
                ParameterApprovalInfo.KindFrom(parts[1]),
                decimal.Parse(parts[2], NumberStyles.Number, CultureInfo.InvariantCulture)));
        }

        return values;
    }

    private static bool IsUniqueViolation(DbUpdateException failure)
        => failure.InnerException is PostgresException postgres
            && string.Equals(postgres.SqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal);
}
