using Babel.Core.Application;
using Babel.Core.Audit;
using Babel.Core.Entitlement;
using Babel.SharedKernel;

namespace Babel.Core.CompanySetup;

/// <summary>طلب تأسيس منشأة.</summary>
/// <param name="Company">المنشأة.</param>
/// <param name="Actor">الفاعل.</param>
/// <param name="Draft">المسوّدة الواصلة.</param>
public sealed record CompanyInitialisationRequest(TenantId Company, UserId Actor, CompanySetupDraft Draft);

/// <summary>
/// خدمة تأسيس المنشأة ومراكز تكلفتها.
/// <para>
/// كل نقطة دخول تمرّ بالاستحقاق أولاً (القاعدة 6)، وكل تغيير يُسجَّل في سجلّ التدقيق
/// بمن فعله ومتى وبسببه حين يكون له سبب (ADR-0006).
/// </para>
/// <para>
/// <b>ولا قرار في هذه الخدمة:</b> صحّة التأسيس في <see cref="FoundedCompany.Found"/>، وثابتة
/// عدم الخلوّ من مركز تكلفة في <see cref="CostCenterRegister"/>، وذرّية التأسيس الأول في
/// المخزن. وما هنا ترتيبٌ ونقلٌ وتسجيل.
/// </para>
/// </summary>
public sealed class CompanySetupService : IApplicationService
{
    private readonly ICompanySetupStore _store;
    private readonly IEntitlementEnforcer _enforcer;
    private readonly IAuditLog _audit;
    private readonly TimeProvider _clock;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="store">مخزن التأسيس.</param>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="audit">سجل التدقيق.</param>
    /// <param name="clock">مصدر الوقت.</param>
    public CompanySetupService(
        ICompanySetupStore store,
        IEntitlementEnforcer enforcer,
        IAuditLog audit,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);

        _store = store;
        _enforcer = enforcer;
        _audit = audit;
        _clock = clock;
    }

    /// <summary>يقرأ تأسيس المنشأة بمقياسها ومراكز تكلفتها.</summary>
    /// <param name="company">المنشأة.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Read)]
    public async ValueTask<Result<FoundedCompany>> GetAsync(
        TenantId company,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(company, actor, BabelModule.Core, EntitlementAccess.Read, "Core.FoundedCompany.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<FoundedCompany>.Failure(gate.Errors);
        }

        FoundedCompany? setup = await _store.FindAsync(company, cancellationToken).ConfigureAwait(false);

        return setup is null
            ? Result<FoundedCompany>.Failure(CompanySetupErrors.NotFound)
            : Result<FoundedCompany>.Success(setup);
    }

    /// <summary>
    /// يؤسّس المنشأة مرّة واحدة. المحاولة الثانية تُرفض بـ
    /// <c>company_setup.already_initialised</c> مهما تغيّرت حمولتها — <b>وبالأخصّ عدد
    /// الخانات العشرية</b>.
    /// </summary>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Write)]
    public async ValueTask<Result<FoundedCompany>> InitialiseAsync(
        CompanyInitialisationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result gate = await _enforcer
            .EnsureAsync(request.Company, request.Actor, BabelModule.Core, EntitlementAccess.Write, "Core.FoundedCompany.Initialise", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<FoundedCompany>.Failure(gate.Errors);
        }

        Result<FoundedCompany> founded = FoundedCompany.Found(request.Company, request.Draft);

        if (founded.IsFailure)
        {
            return founded;
        }

        bool accepted = await _store.TryFoundAsync(founded.Value, cancellationToken).ConfigureAwait(false);

        if (!accepted)
        {
            return Result<FoundedCompany>.Failure(CompanySetupErrors.AlreadyInitialised);
        }

        await RecordAsync(
                request.Company,
                request.Actor,
                "company_setup.founded",
                founded.Value.NameAr,
                "مقياس العرض: " + founded.Value.DisplayScale
                    + " · المركز الافتراضي: " + founded.Value.CostCenters.DefaultCenter.NameAr
                    + " (" + founded.Value.CostCenters.Default + ")",
                cancellationToken)
            .ConfigureAwait(false);

        return founded;
    }

    /// <summary>يضيف مركز تكلفة.</summary>
    /// <param name="company">المنشأة.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="nameAr">الاسم العربي.</param>
    /// <param name="translations">الترجمات، إن وُجدت.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Write)]
    public ValueTask<Result<FoundedCompany>> AddCostCenterAsync(
        TenantId company,
        UserId actor,
        string nameAr,
        IReadOnlyDictionary<string, string>? translations,
        CancellationToken cancellationToken = default)
        => MutateAsync(
            company,
            actor,
            "Core.FoundedCompany.AddCostCenter",
            "cost_center.added",
            register => register.Add(nameAr, translations),
            (before, after) => Added(before, after),
            cancellationToken);

    /// <summary>يعيد تسمية مركز تكلفة. الهوية هي الرمز، فالتاريخ المُرحَّل يبقى مربوطاً.</summary>
    /// <param name="company">المنشأة.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="code">رمز المركز.</param>
    /// <param name="nameAr">الاسم العربي الجديد.</param>
    /// <param name="translations">الترجمات الجديدة، إن وُجدت.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Write)]
    public ValueTask<Result<FoundedCompany>> RenameCostCenterAsync(
        TenantId company,
        UserId actor,
        CostCenterCode code,
        string nameAr,
        IReadOnlyDictionary<string, string>? translations,
        CancellationToken cancellationToken = default)
        => MutateAsync(
            company,
            actor,
            "Core.FoundedCompany.RenameCostCenter",
            "cost_center.renamed",
            register => register.Rename(code, nameAr, translations),
            (before, after) => code + ": «" + (before.Find(code)?.NameAr ?? string.Empty)
                + "» ← «" + (after.Find(code)?.NameAr ?? string.Empty) + "»",
            cancellationToken);

    /// <summary>يوقف مركز تكلفة عن الترحيل بسبب مكتوب. الافتراضي لا يُوقَف.</summary>
    /// <param name="company">المنشأة.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="code">رمز المركز.</param>
    /// <param name="reason">السبب المكتوب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Write)]
    public ValueTask<Result<FoundedCompany>> SuspendCostCenterAsync(
        TenantId company,
        UserId actor,
        CostCenterCode code,
        string? reason,
        CancellationToken cancellationToken = default)
        => MutateAsync(
            company,
            actor,
            "Core.FoundedCompany.SuspendCostCenter",
            "cost_center.suspended",
            register => register.Suspend(code, reason),
            (_, after) => code + " — السبب: " + (after.Find(code)?.SuspensionReason ?? string.Empty),
            cancellationToken);

    /// <summary>يعيد مركز تكلفة موقوفاً إلى العمل.</summary>
    /// <param name="company">المنشأة.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="code">رمز المركز.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Write)]
    public ValueTask<Result<FoundedCompany>> ReinstateCostCenterAsync(
        TenantId company,
        UserId actor,
        CostCenterCode code,
        CancellationToken cancellationToken = default)
        => MutateAsync(
            company,
            actor,
            "Core.FoundedCompany.ReinstateCostCenter",
            "cost_center.reinstated",
            register => register.Reinstate(code),
            (_, _) => code.ToString(),
            cancellationToken);

    /// <summary>ينقل صفة «الافتراضي» إلى مركز عامل آخر.</summary>
    /// <param name="company">المنشأة.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="code">رمز المركز الذي يصير افتراضياً.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Write)]
    public ValueTask<Result<FoundedCompany>> MoveDefaultCostCenterAsync(
        TenantId company,
        UserId actor,
        CostCenterCode code,
        CancellationToken cancellationToken = default)
        => MutateAsync(
            company,
            actor,
            "Core.FoundedCompany.MoveDefaultCostCenter",
            "cost_center.default_moved",
            register => register.MoveDefault(code),
            (before, _) => before.Default + " ← " + code,
            cancellationToken);

    /// <summary>
    /// يحلّ مركز التكلفة الواصل على مستند إلى رمز <b>غير فارغ دائماً</b>: المذكور إن كان
    /// عاملاً، والافتراضي إن لم يُذكر شيء.
    /// </summary>
    /// <param name="company">المنشأة.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="requested">الرمز المذكور على المستند، أو غيابه.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Read)]
    public async ValueTask<Result<CostCenterCode>> ResolveCostCenterAsync(
        TenantId company,
        UserId actor,
        string? requested,
        CancellationToken cancellationToken = default)
    {
        Result<FoundedCompany> setup = await GetAsync(company, actor, cancellationToken).ConfigureAwait(false);

        return setup.IsFailure
            ? Result<CostCenterCode>.Failure(setup.Errors)
            : setup.Value.CostCenters.Resolve(requested);
    }

    private async ValueTask<Result<FoundedCompany>> MutateAsync(
        TenantId company,
        UserId actor,
        string operation,
        string action,
        Func<CostCenterRegister, Result<CostCenterRegister>> change,
        Func<CostCenterRegister, CostCenterRegister, string> describe,
        CancellationToken cancellationToken)
    {
        Result gate = await _enforcer
            .EnsureAsync(company, actor, BabelModule.Core, EntitlementAccess.Write, operation, cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<FoundedCompany>.Failure(gate.Errors);
        }

        FoundedCompany? setup = await _store.FindAsync(company, cancellationToken).ConfigureAwait(false);

        if (setup is null)
        {
            return Result<FoundedCompany>.Failure(CompanySetupErrors.NotFound);
        }

        Result<CostCenterRegister> changed = change(setup.CostCenters);

        if (changed.IsFailure)
        {
            return Result<FoundedCompany>.Failure(changed.Errors);
        }

        bool stored = await _store
            .TryReplaceCostCentersAsync(company, changed.Value, cancellationToken)
            .ConfigureAwait(false);

        if (!stored)
        {
            return Result<FoundedCompany>.Failure(CompanySetupErrors.NotFound);
        }

        await RecordAsync(company, actor, action, setup.NameAr, describe(setup.CostCenters, changed.Value), cancellationToken)
            .ConfigureAwait(false);

        return Result<FoundedCompany>.Success(setup.WithCostCenters(changed.Value));
    }

    private static string Added(CostCenterRegister before, CostCenterRegister after)
    {
        CostCenter? added = after.All.FirstOrDefault(center => before.Find(center.Code) is null);
        return added is null ? string.Empty : added.Code + ": " + added.NameAr;
    }

    private async ValueTask RecordAsync(
        TenantId company,
        UserId actor,
        string action,
        string subject,
        string? details,
        CancellationToken cancellationToken)
        => await _audit
            .RecordAsync(new AuditEntry(company, actor, _clock.GetUtcNow(), action, subject, details), cancellationToken)
            .ConfigureAwait(false);
}
