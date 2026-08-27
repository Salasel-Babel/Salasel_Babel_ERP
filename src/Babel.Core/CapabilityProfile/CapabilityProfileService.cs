using System.Collections.Immutable;
using Babel.Core.Application;
using Babel.Core.Audit;
using Babel.Core.Entitlement;
using Babel.SharedKernel;

namespace Babel.Core.CapabilityProfile;

/// <summary>طلب حفظ ملفّ قدرات.</summary>
/// <param name="Tenant">المستأجر.</param>
/// <param name="Actor">الفاعل.</param>
/// <param name="Draft">المسودّة الواصلة.</param>
/// <param name="WithdrawalReason">
/// سبب سحب قدرة، إن كان التغيير يسحب قدرة. غيابه يجعل السحب مرفوضاً — والاتجاه الآخر
/// (التشغيل) لا يحتاج شيئاً: توسيعُ ما يجوز حمله لا يُبطل مستنداً قائماً.
/// </param>
public sealed record CapabilityProfileSaveRequest(
    TenantId Tenant,
    UserId Actor,
    CapabilityProfileDraft Draft,
    string? WithdrawalReason);

/// <summary>
/// خدمة ملفّ القدرات: القراءة، والاشتقاق، والقبول، والحفظ.
/// <para>
/// كل نقطة دخول تمرّ بالاستحقاق أولاً (القاعدة 6)، والحفظ يمرّ بالتحقّق مقابل مصفوفة
/// الترحيل قبل أن يلمس المخزن.
/// </para>
/// </summary>
public sealed class CapabilityProfileService : IApplicationService
{
    private readonly ICapabilityProfileStore _store;
    private readonly IPostingEventDirectory _directory;
    private readonly IEntitlementEnforcer _enforcer;
    private readonly IAuditLog _audit;
    private readonly TimeProvider _clock;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="store">مخزن الملفّات.</param>
    /// <param name="directory">فهرس أحداث المصفوفة.</param>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="audit">سجل التدقيق.</param>
    /// <param name="clock">مصدر الوقت.</param>
    public CapabilityProfileService(
        ICapabilityProfileStore store,
        IPostingEventDirectory directory,
        IEntitlementEnforcer enforcer,
        IAuditLog audit,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);

        _store = store;
        _directory = directory;
        _enforcer = enforcer;
        _audit = audit;
        _clock = clock;
    }

    /// <summary>يقرأ ملفّ المستأجر بأشكاله المشتقّة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Read)]
    public async ValueTask<Result<ValidatedCapabilityProfile>> GetAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Core, EntitlementAccess.Read, "Core.CapabilityProfile.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ValidatedCapabilityProfile>.Failure(gate.Errors);
        }

        ValidatedCapabilityProfile? profile = await _store.FindAsync(tenant, cancellationToken).ConfigureAwait(false);

        return profile is null
            ? Result<ValidatedCapabilityProfile>.Failure(CapabilityProfileErrors.ProfileNotFound)
            : Result<ValidatedCapabilityProfile>.Success(profile);
    }

    /// <summary>يشتقّ شكل مستند واحد لهذا المستأجر.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="documentType">نوع المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Read)]
    public async ValueTask<Result<DocumentShape>> GetShapeAsync(
        TenantId tenant,
        UserId actor,
        DocumentTypeCode documentType,
        CancellationToken cancellationToken = default)
    {
        Result<ValidatedCapabilityProfile> profile = await GetAsync(tenant, actor, cancellationToken).ConfigureAwait(false);

        if (profile.IsFailure)
        {
            return Result<DocumentShape>.Failure(profile.Errors);
        }

        DocumentShape? shape = profile.Value.ShapeOf(documentType);

        return shape is null
            ? Result<DocumentShape>.Failure(
                CapabilityCatalogue.Find(documentType) is null
                    ? CapabilityProfileErrors.UnknownDocumentType(documentType.Value ?? string.Empty)
                    : CapabilityProfileErrors.DocumentTypeNotInProfile(documentType.Value ?? string.Empty))
            : Result<DocumentShape>.Success(shape);
    }

    /// <summary>يعرض مستنداً على ملفّ المستأجر فيقبله أو يرفضه.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="submission">المستند المقدَّم.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Read)]
    public async ValueTask<Result<AdmittedDocument>> AdmitAsync(
        TenantId tenant,
        UserId actor,
        DocumentSubmission submission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        Result<ValidatedCapabilityProfile> profile = await GetAsync(tenant, actor, cancellationToken).ConfigureAwait(false);

        return profile.IsFailure
            ? Result<AdmittedDocument>.Failure(profile.Errors)
            : profile.Value.Admit(submission);
    }

    /// <summary>
    /// يحفظ ملفّاً بعد مطابقته بالمصفوفة، ويرفض سحب قدرة بلا إقرار مسبَّب.
    /// </summary>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Write)]
    public async ValueTask<Result<ValidatedCapabilityProfile>> SaveAsync(
        CapabilityProfileSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result gate = await _enforcer
            .EnsureAsync(request.Tenant, request.Actor, BabelModule.Core, EntitlementAccess.Write, "Core.CapabilityProfile.Save", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ValidatedCapabilityProfile>.Failure(gate.Errors);
        }

        Result<ValidatedCapabilityProfile> created = ValidatedCapabilityProfile.Create(request.Draft, _directory);

        if (created.IsFailure)
        {
            return created;
        }

        ValidatedCapabilityProfile next = created.Value;
        ValidatedCapabilityProfile? previous = await _store.FindAsync(request.Tenant, cancellationToken).ConfigureAwait(false);

        ImmutableArray<CapabilityWithdrawal> withdrawals = previous is null
            ? []
            : next.WithdrawalsAgainst(previous);

        if (withdrawals.Length > 0 && !IsAcknowledged(request.WithdrawalReason))
        {
            return Result<ValidatedCapabilityProfile>.Failure(
                [.. withdrawals.Select(static withdrawal => CapabilityProfileErrors.WithdrawalRequiresAcknowledgement(
                    withdrawal.DocumentType.Value ?? string.Empty,
                    [.. withdrawal.Capabilities.Select(static code => code.Value)]))]);
        }

        await _store.SaveAsync(request.Tenant, next, cancellationToken).ConfigureAwait(false);

        await _audit
            .RecordAsync(
                new AuditEntry(
                    request.Tenant,
                    request.Actor,
                    _clock.GetUtcNow(),
                    "capability_profile.saved",
                    string.Join(" · ", next.DocumentTypes.Select(static code => code.Value)),
                    Describe(withdrawals, request.WithdrawalReason)),
                cancellationToken)
            .ConfigureAwait(false);

        return Result<ValidatedCapabilityProfile>.Success(next);
    }

    private static bool IsAcknowledged(string? reason)
        => reason is not null
            && reason.Trim().Length >= ProfileLimits.MinimumReasonLength
            && reason.Length <= ProfileLimits.MaximumReasonLength;

    private static string? Describe(ImmutableArray<CapabilityWithdrawal> withdrawals, string? reason)
    {
        if (withdrawals.Length == 0)
        {
            return null;
        }

        string sold = string.Join(
            " · ",
            withdrawals.Select(static withdrawal =>
                withdrawal.DocumentType.Value + ": " + string.Join(
                    "، ",
                    withdrawal.Capabilities.Select(static code => code.Value))));

        return "سحب قدرات — " + sold + " — السبب: " + (reason ?? string.Empty);
    }
}
