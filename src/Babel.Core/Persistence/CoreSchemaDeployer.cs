using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Babel.Core.Persistence;

/// <summary>
/// مصنع وقت التصميم لأداة <c>dotnet ef</c> وحدها. لا يُستدعى في التشغيل أبداً،
/// ويقرأ اتصال <b>المالك</b> لأن الهجرات ملك المالك لا التطبيق.
/// </summary>
internal sealed class CoreDbContextFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    public CoreDbContext CreateDbContext(string[] args)
    {
        CoreOptions options = new();
        DbContextOptionsBuilder<CoreDbContext> builder = new();
        builder.UseNpgsql(options.OwnerConnectionString, CoreSchema.MigrationHistory);
        return new CoreDbContext(builder.Options);
    }
}

/// <summary>
/// نشر مخطّط النواة — <b>بدور المالك حصراً</b>، على نمط <c>LedgerSchemaDeployer</c>.
/// <para>
/// خطوتان بترتيب لا يجوز أن ينقلب:
/// <list type="number">
///   <item>هجرات EF Core: الجداول والفهارس وقيود التحقق، <b>وداخل الهجرة نفسها</b>
///         <c>CoreTriggers.sql</c> — ما لا يعبّر عنه نموذج EF. ووجوده داخل الهجرة
///         يعني أن <c>dotnet ef database update</c> وحده ينتج مخطّطاً صحيحاً كاملاً.</item>
///   <item><c>CoreGrants.sql</c>: الصلاحيات، وهي آخر خطوة و<b>خارج</b> الهجرة لأنها
///         تحتاج اسم دور التطبيق وقت النشر، واسم بيئة لا يُثبَّت في ترحيل.</item>
/// </list>
/// </para>
/// <para>
/// وهذا النوع <b>عام</b> — خلافاً لنظيره في الدفتر — لأن مُنشئ الشركة التجريبية يستدعيه
/// من خارج التجميعة، تماماً كما يستدعي <c>SalesSchemaDeployer</c>. وما بقي داخلياً هو
/// السياق والصفوف: النشر فعلٌ معلن، والجداول ليست كذلك.
/// </para>
/// </summary>
public static class CoreSchema
{
    /// <summary>ينشر المخطّط كاملاً ويمنح الصلاحيات لدور التطبيق.</summary>
    /// <param name="options">إعدادات النواة — اتصال المالك واسم دور التطبيق.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task DeployAsync(CoreOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        DbContextOptionsBuilder<CoreDbContext> builder = new();
        builder.UseNpgsql(options.OwnerConnectionString, MigrationHistory);

        await using (CoreDbContext context = new(builder.Options))
        {
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }

        await using NpgsqlConnection connection = new(options.OwnerConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // اسم الدور يصل عبر إعداد الجلسة كي لا يُثبَّت اسم بيئة داخل نصّ ترحيل.
        await using (NpgsqlCommand carry = new("select set_config('babel.core_app_role', $1, false)", connection))
        {
            carry.Parameters.AddWithValue(options.AppRole);
            await carry.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using NpgsqlCommand grants = new(Script("CoreGrants.sql"), connection);
        await grants.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// جدول تاريخ الهجرات في مخطّط <c>core</c> لا في <c>public</c>.
    /// <para>
    /// وهذا ليس ترتيباً: مخطّطٌ يترك سجلّ هجراته في <c>public</c> يجعل قاعدةً تحمل
    /// وحدتين تخلط تاريخيهما في جدول واحد، ثم يصير حذف مخطّط أو نقله عمليةً تلمس
    /// تاريخ غيره. ويجعل منح «قراءة تاريخ الهجرات» لدور التطبيق منحاً على
    /// <c>public</c> — وهو ما لا يُقصد.
    /// </para>
    /// </summary>
    internal static void MigrationHistory(Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder npgsql)
        => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "core");

    /// <summary>نصّ مضمَّن في التجميعة — النشر لا يفترض وجود شجرة المستودع.</summary>
    /// <param name="name">اسم الملفّ.</param>
    public static string Script(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Assembly assembly = typeof(CoreSchema).Assembly;
        string resource = assembly.GetManifestResourceNames()
            .Single(candidate => candidate.EndsWith(name, StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
