using System.Reflection;
using Npgsql;

namespace Babel.Core.NameRegister;

/// <summary>
/// <b>نشر سجلّ الأسماء — فعلُ مالكٍ لا خدمةٌ في الحاوية.</b>
/// <para>
/// خطوتان بترتيب لا ينقلب: تُنشر الدوالّ المشتركة مرّةً في القاعدة (<see cref="DeployAsync"/>)،
/// ثم تربط كل وحدةٍ مالكة جدولها بها (<see cref="AttachAsync"/>). وهو الشكل نفسه الذي
/// يمشي عليه <c>RealEstateSchemaDeployer</c>، ونصوصه مضمَّنة في التجميعة كي لا يفترض
/// النشر وجود شجرة المستودع على القرص.
/// </para>
/// <para>
/// <b>وموضعه <c>Babel.Core</c> بالإكراه لا بالذوق.</b> ‏<c>ModuleMap.AllowedProjectReferences</c>
/// يعطي كل وحدة أفقية <c>{SharedKernel, Contracts, Core}</c>، فالنواة هي الموضع الوحيد
/// الذي تراه الوحدات الستّ المالكة للأسماء <b>ووحدةُ الذكاء</b> معاً. وحارسٌ قائم في
/// <c>tests/Babel.Ai.Tests/Architecture/TheCapturedDraftCannotReachTheLedger.cs</c> يمنع
/// أصلاً وجود نصّ SQL أو مرجع Npgsql داخل <c>Babel.Ai</c> — «ولا تفتح لنفسها طريقاً
/// ثانياً إلى قاعدة البيانات». فالطيّ يعيش هنا، والقاعدة التي تقرأ نتيجته تعيش هناك.
/// </para>
/// </summary>
public static class NameRegisterSchema
{
    /// <summary>نصوص النشر بترتيب تطبيقها.</summary>
    private static readonly string[] Migrations =
    [
        "001_TheNameRegisterFoldsBeforeItMatches.sql",
        "002_TheRegisterCarriesItsSearchKeysAndItsIndexes.sql",
    ];

    /// <summary>
    /// ينشر الدوالّ المشتركة في القاعدة المطلوبة. <b>يرفع صوتاً إن غاب <c>pg_trgm</c></b>
    /// ولا يمضي بمطابقةٍ نصفَ عاملة.
    /// </summary>
    /// <param name="ownerConnectionString">اتصال المالك — النشر لا يجري باتصال التطبيق (‏ADR-0003).</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task DeployAsync(string ownerConnectionString, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerConnectionString);

        await using NpgsqlConnection connection = new(ownerConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (string migration in Migrations)
        {
            await using NpgsqlCommand command = new(Script(migration), connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// يربط جدولاً بعمودَي البحث وفهرسيهما. يُنادى من ناشر مخطّط الوحدة <b>المالكة</b>.
    /// </summary>
    /// <param name="ownerConnectionString">اتصال المالك.</param>
    /// <param name="table">وصف الجدول.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task AttachAsync(
        string ownerConnectionString,
        NameRegisterTable table,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerConnectionString);
        ArgumentNullException.ThrowIfNull(table);

        await using NpgsqlConnection connection = new(ownerConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand command = new(
            "select babel.attach_name_register(@schema, @table, @name, @scope)",
            connection);

        command.Parameters.AddWithValue("schema", table.Schema);
        command.Parameters.AddWithValue("table", table.Table);
        command.Parameters.AddWithValue("name", table.NameColumn);
        command.Parameters.AddWithValue("scope", table.ScopeColumns.ToArray());

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>نصّ مضمَّن في التجميعة.</summary>
    /// <param name="name">اسم النصّ.</param>
    internal static string Script(string name)
    {
        Assembly assembly = typeof(NameRegisterSchema).Assembly;
        string resource = assembly.GetManifestResourceNames()
            .Single(candidate => candidate.EndsWith(name, StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
