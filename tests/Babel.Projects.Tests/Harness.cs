using System.Collections.Immutable;
using Babel.Contracts.Posting;
using Babel.Core.CapabilityProfile;
using Babel.Ledger;
using Babel.Ledger.Posting;
using Babel.Projects.Application;
using Babel.SharedKernel;
using Babel.Tests.Shared;
using Xunit;

namespace Babel.Projects.Tests;

/// <summary>
/// التجميعة كلها في مجموعة واحدة: الاختبارات تتشارك قاعدة بيانات حقيقية وعدّاد ترقيم
/// حقيقياً ودفترين مساعدين، وتوازيها يجعل «انحراف في المطابقة» تعني «اختباران تسابقا»
/// لا «الدفتر المساعد ينحرف».
/// </summary>
[CollectionDefinition("projects", DisableParallelization = true)]
public sealed class ProjectsTestGroup;

/// <summary>
/// تركيب الاختبار بلا حاوية اعتماديات: كل منشئ عام، وكل خدمة تأخذ نفس
/// <see cref="ProjectsRuntime"/> فتشترك في سياق واحد — وهو ما يفعله النطاق في الإنتاج.
/// </summary>
internal sealed class Harness : IDisposable
{
    private static LedgerRuntime? _ledger;
    private static readonly Lock LedgerGate = new();

    private Harness(ProjectsRuntime runtime, LedgerRuntime ledger)
    {
        Runtime = runtime;
        LedgerRuntime = ledger;
        AlwaysEntitled enforcer = new();
        Posting = new PostingService(enforcer, ledger);
        Profiles = new InMemoryCapabilityProfileStore();
        Registry = new ProjectRegistryService(enforcer, runtime);
        Subcontractors = new SubcontractorRegistryService(enforcer, runtime);
        ClientCertificates = new ClientCertificateService(enforcer, runtime, Profiles);
        SubcontractorCertificates = new SubcontractorCertificateService(enforcer, runtime);
        Advances = new SubcontractorAdvanceService(enforcer, runtime, Posting);
        Retention = new RetentionService(enforcer, runtime, Posting, Profiles);
        Reconciliation = new ProjectsReconciliationService(
            enforcer, runtime, new LedgerControlPointReader(ProjectsTestEnvironment.Ledger.AppConnectionString));

        // بوّابة الترحيل نفسها — الطريق الذي تسلكه كل خدمة في الوحدة. وإثبات هوية
        // الإحكام يمرّ من هنا لا بإدراج خام: الإدراج الخام يصيب الفهرس ويترك استعلام
        // «هل رُحّل من قبل؟» بلا شاهد.
        Gateway = new ProjectsPostingGateway(runtime.Database, Posting, runtime.CostCenters);
    }

    /// <summary>مخزن ملفّات القدرات — <b>لهذه التجهيزة وحدها</b>.</summary>
    public InMemoryCapabilityProfileStore Profiles { get; }

    public ProjectsRuntime Runtime { get; }

    public LedgerRuntime LedgerRuntime { get; }

    public IPostingService Posting { get; }

    public ProjectRegistryService Registry { get; }

    public SubcontractorRegistryService Subcontractors { get; }

    public ClientCertificateService ClientCertificates { get; }

    public SubcontractorCertificateService SubcontractorCertificates { get; }

    public SubcontractorAdvanceService Advances { get; }

    public RetentionService Retention { get; }

    public ProjectsReconciliationService Reconciliation { get; }

    public ProjectsPostingGateway Gateway { get; }

    public static UserId Actor { get; } = new(new Guid("00000000-0000-4000-8000-0000000000b1"));

    /// <summary>تجهيزة <b>بلا ملفّ قدرات محفوظ لأي مستأجر</b> — لإثبات أن الغياب رفضٌ لا فتح.</summary>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static Task<Harness> CreateWithoutProfilesAsync(CancellationToken cancellationToken = default)
        => BuildAsync(seedProfiles: false, cancellationToken);

    /// <summary>تجهيزة بملفّات قدرات مكتوبة للمستأجر الأساسي.</summary>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static Task<Harness> CreateAsync(CancellationToken cancellationToken = default)
        => BuildAsync(seedProfiles: true, cancellationToken);

    private static async Task<Harness> BuildAsync(bool seedProfiles, CancellationToken cancellationToken)
    {
        await ProjectsTestEnvironment.EnsureAsync(cancellationToken).ConfigureAwait(false);

        lock (LedgerGate)
        {
            _ledger ??= new LedgerRuntime(ProjectsTestEnvironment.Ledger);
        }

        // المنشآت مؤسَّسة قبل أول ترحيل: البوّابة تسأل النواة عن مركز التكلفة، ومنشأةٌ
        // لم تُؤسَّس لا مركز لها أصلاً (ADR-0026).
        Harness harness = new(
            new ProjectsRuntime(
                ProjectsTestEnvironment.Projects,
                FoundedTenants.ResolverFor(ProjectsTestEnvironment.AllTenants)),
            _ledger);

        if (seedProfiles)
        {
            await harness.SaveProfileAsync(ProjectsTestEnvironment.Tenant, advance: true, retention: true, cancellationToken)
                .ConfigureAwait(false);
        }

        return harness;
    }

    /// <summary>
    /// يكتب ملفّ قدرات المستأجر لنوع <c>projects.client_certificate</c> — قدرتان تُشغَّلان أو تُطفآن.
    /// <para>
    /// ويمرّ بـ<see cref="ValidatedCapabilityProfile.Create"/> نفسها التي يمرّ بها
    /// الإنتاج: ملفٌّ لم يُطابَق بالمصفوفة لا يدخل المخزن أصلاً.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="advance">قدرة الدفعة المقدمة.</param>
    /// <param name="retention">قدرة المحتجز.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async Task SaveProfileAsync(
        TenantId tenant,
        bool advance,
        bool retention,
        CancellationToken cancellationToken = default)
    {
        CapabilityProfileDraft draft = new(
            new Dictionary<string, DocumentProfileDraft>(StringComparer.Ordinal)
            {
                ["projects.client_certificate"] = new DocumentProfileDraft(
                    new Dictionary<string, bool>(StringComparer.Ordinal)
                    {
                        ["advance"] = advance,
                        ["retention"] = retention,
                    },
                    ImmutableSortedDictionary<string, string>.Empty),
            });

        Result<ValidatedCapabilityProfile> profile =
            ValidatedCapabilityProfile.Create(draft, EmbeddedPostingEventDirectory.Default);

        if (profile.IsFailure)
        {
            throw new InvalidOperationException(
                "تعذّر بناء ملفّ قدرات صالح: " + string.Join(" | ", profile.Errors.Select(static e => e.ToString())));
        }

        await Profiles.SaveAsync(tenant, profile.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>يسجّل مشروعاً ويُرجع معرّفه ورمزه.</summary>
    /// <param name="code">رمز المشروع.</param>
    /// <param name="tenant">المستأجر، أو الافتراضي.</param>
    public async Task<Guid> ProjectAsync(string code, TenantId? tenant = null)
    {
        Result<ProjectView> created = await Registry
            .CreateProjectAsync(
                tenant ?? ProjectsTestEnvironment.Tenant,
                Actor,
                new ProjectDraft(code, new TranslatedName("مشروع " + code), new DateOnly(2026, 1, 1)),
                TestContext.Current.CancellationToken)
            .ConfigureAwait(false);

        return created.IsFailure
            ? throw new InvalidOperationException(created.Errors[0].ToString())
            : created.Value.Id;
    }

    /// <summary>يسجّل مقاولاً من الباطن ويُرجع معرّفه.</summary>
    /// <param name="code">رمز المقاول.</param>
    /// <param name="tenant">المستأجر، أو الافتراضي.</param>
    public async Task<Guid> SubcontractorAsync(string code, TenantId? tenant = null)
    {
        Result<SubcontractorView> created = await Subcontractors
            .CreateSubcontractorAsync(
                tenant ?? ProjectsTestEnvironment.Tenant,
                Actor,
                new SubcontractorDraft(code, new TranslatedName("مقاول " + code), string.Empty),
                TestContext.Current.CancellationToken)
            .ConfigureAwait(false);

        return created.IsFailure
            ? throw new InvalidOperationException(created.Errors[0].ToString())
            : created.Value.Id;
    }

    /// <summary>
    /// يُنشئ عقد باطن ببندٍ واحد.
    /// <para>
    /// <b>ولا نسبة محتجز في هذه التجهيزة:</b> النسبة قيمةٌ يقولها العقد، ووعاؤها قرارُ
    /// محاسب — فاختبارٌ يكتب نسبةً من عنده يُثبّت رقماً لم يقله أحد. والصفر هنا يعني
    /// «العقد لا ينصّ على محتجز»، وهو ما يقوله العقد نفسه لا ما يقوله الاختبار.
    /// </para>
    /// </summary>
    /// <param name="number">رقم العقد.</param>
    /// <param name="projectId">المشروع.</param>
    /// <param name="subcontractorId">المقاول.</param>
    /// <param name="tenant">المستأجر، أو الافتراضي.</param>
    public async Task<Guid> SubcontractAsync(
        string number,
        Guid projectId,
        Guid subcontractorId,
        TenantId? tenant = null)
    {
        Result<SubcontractView> created = await Subcontractors
            .CreateSubcontractAsync(
                tenant ?? ProjectsTestEnvironment.Tenant,
                Actor,
                new SubcontractDraft(
                    number,
                    projectId,
                    subcontractorId,
                    new DateOnly(2026, 1, 5),
                    RetentionRate: 0m,
                    GuaranteeMonths: 12,
                    Lines:
                    [
                        new SubcontractLineDraft(
                            "L-1",
                            "بند اختبار",
                            new ProjectQuantity(10m, "m3"),
                            Money.Of(1m, CurrencyCode.Sar)),
                    ]),
                TestContext.Current.CancellationToken)
            .ConfigureAwait(false);

        return created.IsFailure
            ? throw new InvalidOperationException(created.Errors[0].ToString())
            : created.Value.Id;
    }

    public static Money Sar(decimal value) => Money.Of(value, CurrencyCode.Sar);

    public void Dispose() => Runtime.Dispose();
}
