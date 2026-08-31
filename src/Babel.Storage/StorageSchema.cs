using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Babel.Storage.Persistence;

namespace Babel.Storage;

/// <summary>
/// <b>نقطة الدخول المعلَنة لنشر مخطّط المخزن</b> — بدور المالك حصراً.
/// <para>
/// ثلاث خطوات بترتيب لا يجوز أن ينقلب:
/// <list type="number">
///   <item><c>EnsureCreated</c> ينشئ الشكل الحالي في قاعدة فارغة، ولا يفعل شيئاً في
///         قاعدة قائمة — ولذلك تُشغَّل نصوص الترقية بعده.</item>
///   <item><c>001_AttachmentsAreAppendOnly.sql</c>: مشغّل الرفض والمفاتيح الخارجية —
///         ما لا يعبّر عنه نموذج EF.</item>
///   <item><c>002_AttachmentsCarryTheirSourceDocument.sql</c>: عمودا المستند المصدر
///         وقيد اقترانهما وفهرس الجرد — لقاعدةٍ أُنشئت قبل أن يوجدا.</item>
///   <item><c>StorageGrants.sql</c>: الصلاحيات، آخر خطوة لأنها تحتاج اسم دور التطبيق
///         وقت النشر، واسم بيئة لا يُثبَّت في نصّ نشر.</item>
/// </list>
/// </para>
/// <para>
/// <b>وما لا يفتحه هذا الباب:</b> الخادم لا يملك DDL. لو ملكه لأسقط مشغّل «يُضاف ولا
/// يُعدَّل» ثم كتب فوق سند إثبات (‏ADR-0003).
/// </para>
/// </summary>
public static class StorageSchema
{
    /// <summary>نصوص الترقية بترتيب تطبيقها.</summary>
    private static readonly string[] Migrations =
    [
        "001_AttachmentsAreAppendOnly.sql",
        "002_AttachmentsCarryTheirSourceDocument.sql",
    ];

    /// <summary>ينشر المخطّط كاملاً ويمنح الصلاحيات لدور التطبيق.</summary>
    /// <param name="options">الإعدادات — يُقرأ منها اتصال المالك واسم الدور.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task DeployAsync(StorageOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        await using (StorageDbContext database = StorageRuntime.Build(options.OwnerConnectionString))
        {
            await database.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        }

        await using NpgsqlConnection connection = new(options.OwnerConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (string migration in Migrations)
        {
            await using NpgsqlCommand command = new(Script(migration), connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // اسم الدور يصل عبر إعداد الجلسة كي لا يُثبَّت اسم بيئة داخل نصّ نشر.
        await using (NpgsqlCommand carry = new("select set_config('babel.storage_app_role', $1, false)", connection))
        {
            carry.Parameters.AddWithValue(options.AppRole);
            await carry.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using NpgsqlCommand grants = new(Script("StorageGrants.sql"), connection);
        await grants.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>نصّ مضمَّن في التجميعة — النشر لا يفترض وجود شجرة المستودع.</summary>
    /// <param name="name">اسم الملفّ.</param>
    /// <returns>نصّ السكربت.</returns>
    internal static string Script(string name)
    {
        Assembly assembly = typeof(StorageSchema).Assembly;
        string resource = assembly.GetManifestResourceNames()
            .Single(candidate => candidate.EndsWith(name, StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>بناء سياق المخزن. <c>internal</c> لأن الجداول لا تعبر الحدّ (القاعدة 5).</summary>
internal static class StorageRuntime
{
    public static StorageDbContext Build(string connectionString)
        => new(new DbContextOptionsBuilder<StorageDbContext>().UseNpgsql(connectionString).Options);
}
