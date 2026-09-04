using System.Reflection;
using Babel.Core.CompanySetup;
using Babel.RealEstate.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Babel.RealEstate;

/// <summary>
/// موارد وحدة العقارات المشتركة داخل نطاق الطلب.
/// <para>
/// النوع عام لأن الحاوية تحقنه في مُنشئ عام، وأعضاؤه <c>internal</c> لأن جداول الوحدة
/// لا تعبر حدّها (القاعدة 5). نفس شكل <c>SalesRuntime</c> و<c>InventoryRuntime</c>
/// وللسبب نفسه.
/// </para>
/// </summary>
public sealed class RealEstateRuntime : IDisposable
{
    private readonly RealEstateDbContext _database;

    /// <summary>ينشئ الموارد من الإعدادات.</summary>
    /// <param name="options">إعدادات الوحدة.</param>
    /// <param name="costCenters">
    /// حالُّ مركز التكلفة من النواة. <b>بوّابة الترحيل تسأله قبل أن تبني طلباً</b>
    /// (‏ADR-0026). والعقار والوحدة <b>بُعدان لا مركزا تكلفة</b>: البيانات المشحونة
    /// تُعلن <c>required_dimensions=property</c> على كل حساب عقاري ولا تُعلن
    /// <c>cost_center</c> على واحد منها، فيحمل كل سطر مركزاً <b>إدارياً</b> واحداً.
    /// </param>
    public RealEstateRuntime(RealEstateOptions options, ICostCenterResolver costCenters)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(costCenters);
        Options = options;
        CostCenters = costCenters;
        _database = Build(options);
    }

    internal RealEstateOptions Options { get; }

    /// <summary>حالُّ مركز التكلفة — يُمرَّر إلى بوّابة الترحيل ولا يُقرأ في مكان آخر.</summary>
    internal ICostCenterResolver CostCenters { get; }

    internal RealEstateDbContext Database => _database;

    /// <inheritdoc />
    public void Dispose() => _database.Dispose();

    internal static RealEstateDbContext Build(RealEstateOptions options)
        => new(new DbContextOptionsBuilder<RealEstateDbContext>().UseNpgsql(options.ConnectionString).Options);
}

/// <summary>
/// ناشر مخطّط العقارات — خارج حاوية الاعتماديات عمداً: نشر المخطّط عملية مالك.
/// <para>
/// <b>خطوتان بترتيب لا يجوز أن ينقلب:</b> <c>EnsureCreated</c> ينشئ الشكل الحالي كاملاً
/// في قاعدة فارغة <b>ولا يفعل شيئاً في قاعدة قائمة</b> — وذلك بالضبط ما يجعل نصوص
/// الترقية ضرورية. ثم تُشغَّل النصوص بالترتيب، وكلٌّ منها مكتوب ليُعاد تشغيله بلا أثر.
/// </para>
/// <para>
/// <b>وأول نصٍّ هنا يحمل تبعية بلا سابقة في هذا المستودع:</b> الامتداد
/// <c>btree_gist</c>. مقيس أن <c>create extension</c> و<c>EXCLUDE USING</c> لهما صفر
/// مطابقة في <c>src/</c> كلها قبل هذه الوحدة. وهو <b>امتداد موثوق</b> منذ PostgreSQL 13
/// فيركّبه مالك القاعدة بلا امتياز خارق — ومع ذلك يبقى فعل تركيبه فعلَ مالك، ولذلك
/// موضعه هنا لا في مسار التطبيق.
/// </para>
/// </summary>
public static class RealEstateSchemaDeployer
{
    /// <summary>نصوص الترقية بترتيب تطبيقها.</summary>
    private static readonly string[] Migrations =
    [
        "001_OneLiveTermPerUnitIsADatabaseConstraint.sql",
        "002_TheBillingApprovalReplacesActivation.sql",
    ];

    /// <summary>ينشئ مخطّط <c>realestate</c> وجداوله إن لم توجد، ثم يُطبّق نصوص الترقية.</summary>
    /// <param name="options">إعدادات الوحدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task DeployAsync(RealEstateOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        await using (RealEstateDbContext database = RealEstateRuntime.Build(options))
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
    /// <param name="name">اسم النصّ.</param>
    internal static string Script(string name)
    {
        Assembly assembly = typeof(RealEstateSchemaDeployer).Assembly;
        string resource = assembly.GetManifestResourceNames()
            .Single(candidate => candidate.EndsWith(name, StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
