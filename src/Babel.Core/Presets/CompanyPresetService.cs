using Babel.Core.Application;
using Babel.Core.Audit;
using Babel.Core.Entitlement;
using Babel.SharedKernel;

namespace Babel.Core.Presets;

/// <summary>طلب استبدال ثوابت الشركة.</summary>
/// <param name="Company">المنشأة.</param>
/// <param name="Actor">الفاعل.</param>
/// <param name="Values">المفاتيح وقيمها كما وصلت — قيمةٌ فارغة تزيل الثابت.</param>
public sealed record CompanyPresetReplacement(
    TenantId Company,
    UserId Actor,
    IReadOnlyList<KeyValuePair<string, string>> Values);

/// <summary>
/// خدمة ثوابت الشركة وترقيم مستنداتها.
/// <para>
/// <b>الترقيم هنا تخصيصٌ لا فرض:</b> الرقم المخصَّص يُعاد إلى المتصفّح، والمتصفّح هو الذي
/// يرسله في المستند كما كان دائماً (ADR-0054 §7)، وفرادته تُفرض عند المستند لا هنا. فما
/// يتغيّر هو أن المستخدم لم يعد يخترع الرقم بيده — لا أن الخادم صار يخترعه خلسةً.
/// ورقمٌ خُصّص ولم يُستعمل يترك فجوة، وهذا مقبول ومُعلَن: العدّاد عدّادُ تخصيص لا عدّادُ ترحيل.
/// </para>
/// </summary>
public sealed class CompanyPresetService : IApplicationService
{
    private readonly ICompanyPresetStore _store;
    private readonly IEntitlementEnforcer _enforcer;
    private readonly IAuditLog _audit;
    private readonly TimeProvider _clock;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="store">مخزن الثوابت.</param>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="audit">سجل التدقيق.</param>
    /// <param name="clock">مصدر الوقت.</param>
    public CompanyPresetService(
        ICompanyPresetStore store,
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

    /// <summary>يقرأ ثوابت المنشأة. منشأةٌ لم تضبط شيئاً تُقرأ فارغة.</summary>
    /// <param name="company">المنشأة.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Read)]
    public async ValueTask<Result<CompanyPresets>> GetAsync(
        TenantId company,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(company, actor, BabelModule.Core, EntitlementAccess.Read, "Core.CompanyPresets.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<CompanyPresets>.Failure(gate.Errors);
        }

        CompanyPresets presets = await _store.ReadAsync(company, cancellationToken).ConfigureAwait(false);
        return Result<CompanyPresets>.Success(presets);
    }

    /// <summary>يستبدل ثوابت المنشأة كلّها. الأخطاء تُعاد دفعةً واحدة ولا يُكتب شيء معها.</summary>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Write)]
    public async ValueTask<Result<CompanyPresets>> ReplaceAsync(
        CompanyPresetReplacement request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result gate = await _enforcer
            .EnsureAsync(request.Company, request.Actor, BabelModule.Core, EntitlementAccess.Write, "Core.CompanyPresets.Replace", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<CompanyPresets>.Failure(gate.Errors);
        }

        Result<CompanyPresets> validated = CompanyPresets.Validate(request.Values);

        if (validated.IsFailure)
        {
            return validated;
        }

        await _store.ReplaceAsync(request.Company, validated.Value, cancellationToken).ConfigureAwait(false);

        await _audit
            .RecordAsync(
                new AuditEntry(
                    request.Company,
                    request.Actor,
                    _clock.GetUtcNow(),
                    "company_presets.replaced",
                    "company_presets",
                    string.Join(" · ", validated.Value.Values.Select(static pair => pair.Key + "=" + pair.Value))),
                cancellationToken)
            .ConfigureAwait(false);

        return validated;
    }

    /// <summary>يخصّص الرقم التالي في سلسلةٍ، داخل سنة التاريخ المعطى.</summary>
    /// <param name="company">المنشأة.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="series">رمز السلسلة.</param>
    /// <param name="on">تاريخ المستند — سنتُه هي سنة العدّاد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Write)]
    public async ValueTask<Result<AllocatedNumber>> AllocateNumberAsync(
        TenantId company,
        UserId actor,
        string series,
        DateOnly on,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(company, actor, BabelModule.Core, EntitlementAccess.Write, "Core.CompanyPresets.AllocateNumber", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<AllocatedNumber>.Failure(gate.Errors);
        }

        if (!PresetCatalogue.TryFindSeries(series ?? string.Empty, out DocumentSeries found))
        {
            return Result<AllocatedNumber>.Failure(CompanyPresetErrors.UnknownSeries(series ?? string.Empty));
        }

        CompanyPresets presets = await _store.ReadAsync(company, cancellationToken).ConfigureAwait(false);
        int sequence = await _store.AllocateAsync(company, found.Code, on.Year, cancellationToken).ConfigureAwait(false);

        return Result<AllocatedNumber>.Success(presets.Compose(found, on.Year, sequence));
    }
}
