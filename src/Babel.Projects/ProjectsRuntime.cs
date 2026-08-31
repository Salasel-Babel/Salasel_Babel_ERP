using Babel.Core.CompanySetup;
using Babel.Projects.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Babel.Projects;

/// <summary>
/// موارد وحدة المقاولات المشتركة داخل نطاق الطلب.
/// <para>
/// النوع عام لأن الحاوية تحقنه في مُنشئ عام، وأعضاؤه <c>internal</c> لأن جداول الوحدة
/// لا تعبر حدّها (القاعدة 5): الجذر التركيبي يستطيع أن <b>يمرّره</b> ولا يستطيع أن
/// <b>يقرأ منه</b>. نفس شكل <c>SalesRuntime</c> وللسبب نفسه.
/// </para>
/// </summary>
public sealed class ProjectsRuntime : IDisposable
{
    private readonly ProjectsDbContext _database;

    /// <summary>ينشئ الموارد من الإعدادات.</summary>
    /// <param name="options">إعدادات الوحدة.</param>
    /// <param name="costCenters">
    /// حالُّ مركز التكلفة من النواة. <b>بوّابة الترحيل تسأله قبل أن تبني طلباً</b>
    /// (‏ADR-0026): بلا حقن المركز يرفض المُخطِّط <b>كل</b> سطر، ويرفض قيدُ قاعدة
    /// البيانات ما نجا منه.
    /// </param>
    public ProjectsRuntime(ProjectsOptions options, ICostCenterResolver costCenters)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(costCenters);
        Options = options;
        CostCenters = costCenters;
        _database = Build(options);
    }

    internal ProjectsOptions Options { get; }

    /// <summary>حالُّ مركز التكلفة — يُمرَّر إلى بوّابة الترحيل ولا يُقرأ في مكان آخر.</summary>
    internal ICostCenterResolver CostCenters { get; }

    internal ProjectsDbContext Database => _database;

    /// <inheritdoc />
    public void Dispose() => _database.Dispose();

    internal static ProjectsDbContext Build(ProjectsOptions options)
        => new(new DbContextOptionsBuilder<ProjectsDbContext>().UseNpgsql(options.ConnectionString).Options);
}

/// <summary>
/// ناشر مخطّط المقاولات.
/// <para>
/// خارج حاوية الاعتماديات عمداً: نشر المخطّط عملية مالك، ومسار التطبيق لا يحتاجها ولا
/// يجوز أن يملكها. و<c>EnsureCreated</c> ينشئ الشكل الحالي كاملاً في قاعدة فارغة ولا
/// يفعل شيئاً في قاعدة قائمة — ولذلك تُشغَّل بعده نصوص الترقية بالترتيب، وكلٌّ منها
/// مكتوب ليُعاد تشغيله بلا أثر.
/// </para>
/// <para>
/// <b>ولا نصّ ترقية واحداً اليوم</b>: هذه أول هجرة للوحدة، والقائمة فارغة عن قصد لا عن
/// سهو. وأولُ تعديل على مخطّطٍ نُشر لعميل يُضاف هنا بنصّه.
/// </para>
/// </summary>
public static class ProjectsSchemaDeployer
{
    /// <summary>ينشئ مخطّط <c>projects</c> وجداوله إن لم توجد.</summary>
    /// <param name="options">إعدادات الوحدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task DeployAsync(ProjectsOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        await using ProjectsDbContext database = ProjectsRuntime.Build(options);
        await database.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }
}
