using System.Reflection;
using Babel.Sales.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
/// <para>
/// <b>خطوتان بترتيب لا يجوز أن ينقلب:</b> <c>EnsureCreated</c> ينشئ الشكل الحالي
/// كاملاً في قاعدة فارغة <b>ولا يفعل شيئاً في قاعدة قائمة</b> — وذلك بالضبط ما يجعل
/// نصوص الترقية ضرورية: قاعدة عميل قائمة لن ترى أي تغيير في النموذج بدونها. ثم
/// تُشغَّل نصوص الترقية بالترتيب، وكلٌّ منها مكتوب ليُعاد تشغيله بلا أثر.
/// </para>
/// </summary>
public static class SalesSchemaDeployer
{
    /// <summary>نصوص الترقية بترتيب تطبيقها.</summary>
    private static readonly string[] Migrations = ["001_PostingIdentityIncludesEventCode.sql"];

    /// <summary>ينشئ مخطّط <c>sales</c> وجداوله إن لم توجد، ثم يُطبّق نصوص الترقية.</summary>
    /// <param name="options">إعدادات الوحدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task DeployAsync(SalesOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        await using (SalesDbContext database = SalesRuntime.Build(options))
        {
            await database.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        }

        await using NpgsqlConnection connection = new(options.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (string migration in Migrations)
        {
            await using NpgsqlCommand command = new(Script(migration), connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>نصّ مضمَّن في التجميعة — النشر لا يفترض وجود شجرة المستودع.</summary>
    internal static string Script(string name)
    {
        Assembly assembly = typeof(SalesSchemaDeployer).Assembly;
        string resource = assembly.GetManifestResourceNames()
            .Single(candidate => candidate.EndsWith(name, StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
