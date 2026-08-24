using Babel.Purchasing.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Babel.Purchasing;

/// <summary>
/// موارد وحدة المشتريات المشتركة داخل نطاق الطلب.
/// <para>
/// النوع عام لأن الحاوية تحقنه، وأعضاؤه <c>internal</c> لأن جداول الوحدة لا تعبر
/// حدّها (القاعدة 5). نفس شكل <c>LedgerRuntime</c> وللسبب نفسه.
/// </para>
/// </summary>
public sealed class PurchasingRuntime : IDisposable
{
    private readonly PurchasingDbContext _database;

    /// <summary>ينشئ الموارد من الإعدادات.</summary>
    /// <param name="options">إعدادات الوحدة.</param>
    public PurchasingRuntime(PurchasingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
        _database = Build(options);
    }

    internal PurchasingOptions Options { get; }

    internal PurchasingDbContext Database => _database;

    /// <inheritdoc />
    public void Dispose() => _database.Dispose();

    internal static PurchasingDbContext Build(PurchasingOptions options)
        => new(new DbContextOptionsBuilder<PurchasingDbContext>().UseNpgsql(options.ConnectionString).Options);
}

/// <summary>ناشر مخطّط المشتريات — خارج حاوية الاعتماديات عمداً: نشر المخطّط عملية مالك.</summary>
public static class PurchasingSchemaDeployer
{
    /// <summary>ينشئ مخطّط <c>purchasing</c> وجداوله إن لم توجد.</summary>
    /// <param name="options">إعدادات الوحدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task DeployAsync(PurchasingOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        await using PurchasingDbContext database = PurchasingRuntime.Build(options);
        await database.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }
}
