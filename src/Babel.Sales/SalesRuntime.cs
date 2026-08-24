using Babel.Sales.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Babel.Sales;

/// <summary>
/// موارد وحدة المبيعات المشتركة داخل نطاق الطلب.
/// <para>
/// النوع عام لأن الحاوية تحقنه في مُنشئ عام، وأعضاؤه <c>internal</c> لأن جداول الوحدة
/// لا تعبر حدّها (القاعدة 5): الجذر التركيبي يستطيع أن <b>يمرّره</b> ولا يستطيع أن
/// <b>يقرأ منه</b>. نفس شكل <c>LedgerRuntime</c> وللسبب نفسه.
/// </para>
/// </summary>
public sealed class SalesRuntime : IDisposable
{
    private readonly SalesDbContext _database;

    /// <summary>ينشئ الموارد من الإعدادات.</summary>
    /// <param name="options">إعدادات الوحدة.</param>
    public SalesRuntime(SalesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
        _database = Build(options);
    }

    internal SalesOptions Options { get; }

    internal SalesDbContext Database => _database;

    /// <inheritdoc />
    public void Dispose() => _database.Dispose();

    internal static SalesDbContext Build(SalesOptions options)
        => new(new DbContextOptionsBuilder<SalesDbContext>().UseNpgsql(options.ConnectionString).Options);
}

/// <summary>
/// ناشر مخطّط المبيعات.
/// <para>
/// خارج حاوية الاعتماديات عمداً: نشر المخطّط عملية مالك، ومسار التطبيق لا يحتاجها
/// ولا يجوز أن يملكها.
/// </para>
/// </summary>
public static class SalesSchemaDeployer
{
    /// <summary>ينشئ مخطّط <c>sales</c> وجداوله إن لم توجد.</summary>
    /// <param name="options">إعدادات الوحدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task DeployAsync(SalesOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        await using SalesDbContext database = SalesRuntime.Build(options);
        await database.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }
}
