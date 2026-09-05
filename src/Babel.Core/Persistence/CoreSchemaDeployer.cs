using System.Reflection;
using Babel.Contracts.Parameters;
using Babel.Core.Parameters;
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
        builder.UseNpgsql(options.OwnerConnectionString, CoreSchemaDeployer.MigrationHistory);
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
/// وهو <c>internal</c> كنظيره في الدفتر (القاعدة 5: لا نوع في <c>*.Persistence</c>
/// يُرى خارج وحدته). والباب المعلَن لمن ينشر — أداة الترحيل وبيئات الاختبار — هو
/// <see cref="CoreSchema"/>، على نمط <c>LedgerSchema</c> حرفياً.
/// </para>
/// </summary>
internal static class CoreSchemaDeployer
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

        await using (NpgsqlCommand grants = new(Script("CoreGrants.sql"), connection))
        {
            await grants.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await SeedPlatformDefaultsAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// يبذر افتراضات المنصّة — <b>بدور المالك، وبعد الصلاحيات، وآمنَ التكرار</b>.
    /// <para>
    /// <b>ولماذا هنا لا في الهجرة:</b> الهجرة تصف <b>الشكل</b>، وهذه <b>بيانات</b>.
    /// وافتراضٌ جديد يُشحن غداً لمجموعةٍ جديدة يجب أن يبلغ نسخةً قائمة <b>بلا هجرة
    /// مخطّط</b> — فالنشر هو ما يحمله، لا تغييرُ جدول. والمعرّفات ثابتة في ملفّ
    /// البيانات، فنشرٌ ثانٍ لا يكتب صفّاً ثانياً.
    /// </para>
    /// <para>
    /// <b>ولماذا بدور المالك:</b> صفُّ المنصّة هو الصفّ الوحيد الذي لا يحمل اسم
    /// معتمِد. ولو كتبه دورُ التطبيق لصار بوسع مسار طلبٍ أن يخلق «افتراضَ منصّة»
    /// لم تشحنه المنصّة.
    /// </para>
    /// </summary>
    /// <param name="connection">اتصال المالك، مفتوحاً.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    private static async Task SeedPlatformDefaultsAsync(
        NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string HeaderSql = @"
            insert into core.parameter_version
                (version_id, tenant_id, set_code, scope, effective_from,
                 approval, approved_by, approved_on, source_ref, deposited_at)
            values ($1, $2, $3, 'platform', $4, 'platform_default', '', null, $5, now())
            on conflict do nothing";

        const string ValueSql = @"
            insert into core.parameter_value (version_id, key, kind, value)
            values ($1, $2, $3, $4)
            on conflict do nothing";

        foreach (ParameterVersionView version in PlatformDefaults.All)
        {
            await using (NpgsqlCommand header = new(HeaderSql, connection))
            {
                header.Parameters.AddWithValue(version.Id);
                header.Parameters.AddWithValue(PlatformDefaults.PlatformTenant);
                header.Parameters.AddWithValue(version.SetCode);
                header.Parameters.AddWithValue(version.EffectiveFrom);
                header.Parameters.AddWithValue(version.SourceRef);
                await header.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (ParameterValueView value in version.Values)
            {
                await using NpgsqlCommand row = new(ValueSql, connection);

                row.Parameters.AddWithValue(version.Id);
                row.Parameters.AddWithValue(value.Key);
                row.Parameters.AddWithValue(ParameterApprovalInfo.TokenOf(value.Kind));
                row.Parameters.AddWithValue(value.Value);
                await row.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
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

        Assembly assembly = typeof(CoreSchemaDeployer).Assembly;
        string resource = assembly.GetManifestResourceNames()
            .Single(candidate => candidate.EndsWith(name, StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
