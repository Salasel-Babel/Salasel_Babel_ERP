using System.Reflection;
using Babel.Core.CompanySetup;
using Babel.Hr.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Babel.Hr;

/// <summary>
/// موارد وحدة الموارد البشرية المشتركة داخل نطاق الطلب.
/// <para>
/// النوع عام لأن الحاوية تحقنه في مُنشئ عام، وأعضاؤه <c>internal</c> لأن جداول الوحدة
/// لا تعبر حدّها (القاعدة 5): الجذر التركيبي يستطيع أن <b>يمرّره</b> ولا يستطيع أن
/// <b>يقرأ منه</b>. نفس شكل <c>SalesRuntime</c> و<c>LedgerRuntime</c>.
/// </para>
/// </summary>
public sealed class HrRuntime : IDisposable
{
    private readonly HrDbContext _database;

    /// <summary>ينشئ الموارد من الإعدادات.</summary>
    /// <param name="options">إعدادات الوحدة — ويُرفض غياب نصّ الاتصال هنا برمزه.</param>
    /// <param name="costCenters">
    /// حالُّ مركز التكلفة من النواة (‏ADR-0026). <b>بوّابة الترحيل تسأله قبل أن تبني
    /// طلباً</b>: الوحدة لا تعرف شجرة المراكز ولا المركز الافتراضي.
    /// </param>
    public HrRuntime(HrOptions options, ICostCenterResolver costCenters)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(costCenters);
        options.EnsureConfigured();
        Options = options;
        CostCenters = costCenters;
        _database = Build(options);
    }

    internal HrOptions Options { get; }

    /// <summary>حالُّ مركز التكلفة — يُمرَّر إلى بوّابة الترحيل ولا يُقرأ في مكان آخر.</summary>
    internal ICostCenterResolver CostCenters { get; }

    internal HrDbContext Database => _database;

    /// <inheritdoc />
    public void Dispose() => _database.Dispose();

    internal static HrDbContext Build(HrOptions options)
        => new(new DbContextOptionsBuilder<HrDbContext>().UseNpgsql(options.ConnectionString).Options);
}

/// <summary>
/// ناشر مخطّط الموارد البشرية.
/// <para>
/// خارج حاوية الاعتماديات عمداً: نشر المخطّط عملية مالك، ومسار التطبيق لا يحتاجها
/// ولا يجوز أن يملكها.
/// </para>
/// <para>
/// <b>خطوتان بترتيب لا يجوز أن ينقلب:</b> <c>EnsureCreated</c> ينشئ الشكل الحالي كاملاً
/// في قاعدة فارغة <b>ولا يفعل شيئاً في قاعدة قائمة</b> — وذلك بالضبط ما يجعل نصوص
/// الترقية ضرورية. ثم تُشغَّل بالترتيب، وكلٌّ منها مكتوب ليُعاد تشغيله بلا أثر.
/// </para>
/// <para>
/// <b>ولا يبذر هذا الناشر صفّاً واحداً من النِّسَب.</b> جدول <c>hr.payroll_settings</c>
/// يُسلَّم فارغاً، ومسيّرٌ لفترةٍ لا يغطّيها صفٌّ سارٍ معتمد يُرفض صراحةً.
/// </para>
/// </summary>
public static class HrSchemaDeployer
{
    /// <summary>نصوص الترقية بترتيب تطبيقها.</summary>
    private static readonly string[] Migrations =
    [
        "001_TheEmployeeIsTheGrainAndTheRateTableIsEmpty.sql",
    ];

    /// <summary>ينشئ مخطّط <c>hr</c> وجداوله إن لم توجد، ثم يُطبّق نصوص الترقية.</summary>
    /// <param name="options">إعدادات الوحدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task DeployAsync(HrOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.EnsureConfigured();

        await using (HrDbContext database = HrRuntime.Build(options))
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
        Assembly assembly = typeof(HrSchemaDeployer).Assembly;
        string resource = assembly.GetManifestResourceNames()
            .Single(candidate => candidate.EndsWith(name, StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
