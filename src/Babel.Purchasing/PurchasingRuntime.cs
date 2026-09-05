using System.Reflection;
using Babel.Core.CompanySetup;
using Babel.Purchasing.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
    /// <param name="costCenters">
    /// حالُّ مركز التكلفة من النواة. <b>بوّابة الترحيل تسأله قبل أن تبني طلباً</b>
    /// (‏ADR-0026): الوحدة لا تعرف شجرة المراكز ولا المركز الافتراضي، وسؤالُها عنه
    /// كان سيضع نسخةً ثانية من قاعدة الحلّ في كل وحدة.
    /// </param>
    public PurchasingRuntime(PurchasingOptions options, ICostCenterResolver costCenters)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(costCenters);
        Options = options;
        CostCenters = costCenters;
        _database = Build(options);
    }

    internal PurchasingOptions Options { get; }

    /// <summary>حالُّ مركز التكلفة — يُمرَّر إلى بوّابة الترحيل ولا يُقرأ في مكان آخر.</summary>
    internal ICostCenterResolver CostCenters { get; }

    internal PurchasingDbContext Database => _database;

    /// <inheritdoc />
    public void Dispose() => _database.Dispose();

    internal static PurchasingDbContext Build(PurchasingOptions options)
        => new(new DbContextOptionsBuilder<PurchasingDbContext>().UseNpgsql(options.ConnectionString).Options);
}

/// <summary>
/// ناشر مخطّط المشتريات — خارج حاوية الاعتماديات عمداً: نشر المخطّط عملية مالك.
/// <para>
/// <b>خطوتان بترتيب لا يجوز أن ينقلب:</b> <c>EnsureCreated</c> ينشئ الشكل الحالي
/// كاملاً في قاعدة فارغة <b>ولا يفعل شيئاً في قاعدة قائمة</b> — وذلك بالضبط ما يجعل
/// نصوص الترقية ضرورية: قاعدة عميل قائمة لن ترى أي تغيير في النموذج بدونها. ثم
/// تُشغَّل نصوص الترقية بالترتيب، وكلٌّ منها مكتوب ليُعاد تشغيله بلا أثر.
/// </para>
/// </summary>
public static class PurchasingSchemaDeployer
{
    /// <summary>نصوص الترقية بترتيب تطبيقها.</summary>
    private static readonly string[] Migrations =
    [
        "001_PostingIdentityIncludesEventCode.sql",
        "002_SupplierCarriesVatNumber.sql",
        "003_PurchaseLineCarriesUnit.sql",
        "004_TheBillRemembersTheParametersItUsed.sql",
    ];

    /// <summary>ينشئ مخطّط <c>purchasing</c> وجداوله إن لم توجد، ثم يُطبّق نصوص الترقية.</summary>
    /// <param name="options">إعدادات الوحدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task DeployAsync(PurchasingOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        await using (PurchasingDbContext database = PurchasingRuntime.Build(options))
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
        Assembly assembly = typeof(PurchasingSchemaDeployer).Assembly;
        string resource = assembly.GetManifestResourceNames()
            .Single(candidate => candidate.EndsWith(name, StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
