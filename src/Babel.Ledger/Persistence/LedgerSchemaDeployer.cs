using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Babel.Ledger.Persistence;

/// <summary>
/// مصنع وقت التصميم لأداة <c>dotnet ef</c> وحدها. لا يُستدعى في التشغيل أبداً،
/// ويقرأ اتصال <b>المالك</b> لأن الهجرات ملك المالك لا التطبيق.
/// </summary>
internal sealed class LedgerDbContextFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args)
    {
        LedgerOptions options = new();
        DbContextOptionsBuilder<LedgerDbContext> builder = new();
        builder.UseNpgsql(options.OwnerConnectionString);
        return new LedgerDbContext(builder.Options);
    }
}

/// <summary>
/// نشر المخطّط — <b>بدور المالك حصراً</b>.
/// <para>
/// ثلاث خطوات بترتيب لا يجوز أن ينقلب:
/// <list type="number">
///   <item>هجرات EF Core: الجداول والفهارس وقيود التحقق.</item>
///   <item>وداخل الهجرة نفسها: <c>LedgerTriggers.sql</c> و<c>PostEntryFunction.sql</c>
///         — ما لا يعبّر عنه نموذج EF. وجودهما داخل الهجرة يعني أن
///         <c>dotnet ef database update</c> وحده ينتج مخطّطاً صحيحاً كاملاً.</item>
///   <item><c>LedgerGrants.sql</c>: الصلاحيات، وهي آخر خطوة وخارج الهجرة لأنها
///         تحتاج <b>اسم دور التطبيق وقت النشر</b>، واسم بيئة لا يُثبَّت في ترحيل.</item>
/// </list>
/// </para>
/// <para>
/// وهذا المسار <b>لا يوجد في حاوية اعتماديات التطبيق</b>: التطبيق لا يملك DDL،
/// ولو ملكه لأسقط المشغّل المؤجَّل ثم كتب ما شاء (ADR-0003).
/// </para>
/// </summary>
internal static class LedgerSchemaDeployer
{
    /// <summary>ينشر المخطّط كاملاً ويمنح الصلاحيات لدور التطبيق.</summary>
    public static async Task DeployAsync(LedgerOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        DbContextOptionsBuilder<LedgerDbContext> builder = new();
        builder.UseNpgsql(options.OwnerConnectionString);

        await using (LedgerDbContext context = new(builder.Options))
        {
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }

        await using NpgsqlConnection connection = new(options.OwnerConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // اسم الدور يصل عبر إعداد الجلسة كي لا يُثبَّت اسم بيئة داخل نصّ ترحيل.
        await ExecuteAsync(
            connection,
            $"select set_config('babel.app_role', {Literal(options.AppRole)}, false)",
            cancellationToken).ConfigureAwait(false);

        await ExecuteAsync(connection, Script("LedgerGrants.sql"), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>نصّ مضمَّن في التجميعة — النشر لا يفترض وجود شجرة المستودع.</summary>
    public static string Script(string name)
    {
        Assembly assembly = typeof(LedgerSchemaDeployer).Assembly;
        string resource = assembly.GetManifestResourceNames()
            .Single(candidate => candidate.EndsWith(name, StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string Literal(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
