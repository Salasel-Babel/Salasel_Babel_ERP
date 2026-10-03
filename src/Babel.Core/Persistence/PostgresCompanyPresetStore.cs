using Babel.Core.Presets;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Babel.Core.Persistence;

/// <summary>
/// مخزن ثوابت الشركة وعدّاداتها فوق PostgreSQL.
/// <para>
/// <b>العدّاد يُقرأ بقفل الصفّ</b> (<c>select … for update</c>) داخل العملية التي تُقدّمه
/// (ADR-0008)، فطلبان متزامنان يخرجان برقمين مختلفين لا برقمٍ واحد. والصفّ الأول لسلسلةٍ
/// وسنة يُدرَج عند أول طلب؛ وسباقُ الإدراج يُحسم بالمفتاح الأوّلي ثم يُعاد الطلب مرّة.
/// </para>
/// </summary>
internal sealed class PostgresCompanyPresetStore : ICompanyPresetStore
{
    private readonly DbContextOptions<CoreDbContext> _options;

    /// <summary>ينشئ المخزن.</summary>
    /// <param name="options">إعدادات النواة — اتصال <b>دور التطبيق</b> وحده.</param>
    public PostgresCompanyPresetStore(CoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        DbContextOptionsBuilder<CoreDbContext> builder = new();
        builder.UseNpgsql(options.AppConnectionString);
        _options = builder.Options;
    }

    /// <inheritdoc />
    public async ValueTask<CompanyPresets> ReadAsync(TenantId tenant, CancellationToken cancellationToken = default)
    {
        await using CoreDbContext context = new(_options);
        Guid company = tenant.Value;

        List<CompanyPresetRow> rows = await context.CompanyPresets
            .AsNoTracking()
            .Where(row => row.CompanyId == company)
            .OrderBy(row => row.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return CompanyPresets.Rehydrate(rows.Select(static row => new KeyValuePair<string, string>(row.Key, row.Value)));
    }

    /// <inheritdoc />
    public async ValueTask ReplaceAsync(TenantId tenant, CompanyPresets presets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(presets);

        await using CoreDbContext context = new(_options);
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        Guid company = tenant.Value;

        List<CompanyPresetRow> existing = await context.CompanyPresets
            .Where(row => row.CompanyId == company)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, CompanyPresetRow> byKey = existing.ToDictionary(static row => row.Key, StringComparer.Ordinal);

        foreach ((string key, string value) in presets.Values)
        {
            if (byKey.Remove(key, out CompanyPresetRow? row))
            {
                row.Value = value;
            }
            else
            {
                context.CompanyPresets.Add(new CompanyPresetRow { CompanyId = company, Key = key, Value = value });
            }
        }

        /* ما لم يعد في الثوابت يُحذف: المورد يُستبدل كلّه، والثابت المُزال ثابتٌ لم يعد مضبوطاً. */
        context.CompanyPresets.RemoveRange(byKey.Values);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<int> AllocateAsync(TenantId tenant, string series, int fiscalYear, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(series);

        for (int attempt = 0; ; attempt++)
        {
            await using CoreDbContext context = new(_options);
            await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
                await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            Guid company = tenant.Value;

            DocumentCounterRow? counter = await context.DocumentCounters
                .FromSql($"select * from core.document_counter where company_id = {company} and series = {series} and fiscal_year = {fiscalYear} for update")
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            int allocated;

            if (counter is null)
            {
                allocated = 1;
                context.DocumentCounters.Add(new DocumentCounterRow
                {
                    CompanyId = company,
                    Series = series,
                    FiscalYear = fiscalYear,
                    NextNo = 2,
                });
            }
            else
            {
                allocated = counter.NextNo;
                counter.NextNo = allocated + 1;
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return allocated;
            }
            catch (DbUpdateException failure) when (attempt == 0 && IsUniqueViolation(failure))
            {
                // سباقُ إدراج الصفّ الأول: الطرف الآخر أدرجه قبلنا، فيُعاد الطلب مرّةً ويقع القفل على صفّه.
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsUniqueViolation(DbUpdateException failure) =>
        failure.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
