using Babel.Contracts.Parameters;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.SharedKernel;

namespace Babel.Core.Parameters;

/// <summary>
/// <b>لوحةُ تحكّم المعامِلات — إيداعٌ وقراءةٌ وقائمةُ مراجعة.</b>
/// <para>
/// <b>‏<c>POST</c> لا <c>PUT</c>، والصفّ يُضاف ولا يُعدَّل:</b> نسبةُ فترةٍ ماضية لا
/// تُغيَّر، والتغيير إصدارٌ جديد بتاريخ سريانه ومعتمِده ومصدره. وهي القاعدة نفسها التي
/// يقوم عليها <c>PayrollSettingsService</c>، معمَّمةً على كلّ معامِلٍ في المنتج.
/// </para>
/// <para>
/// <b>وما لا يُودَع من هنا:</b> افتراضُ المنصّة. هو يُشحن مع المنتج في
/// <c>data/parameters/platform-defaults.json</c> ويدخل بخطوة النشر بدور المالك — ولو
/// أُودع من هنا لصار بوسع مستأجرٍ أن يكتب صفّاً <b>بلا معتمِد</b> ثم يُقرأ افتراضَ منصّة.
/// </para>
/// </summary>
public sealed class ParameterSettingsService : IApplicationService
{
    private readonly IEntitlementEnforcer _enforcer;
    private readonly IParameterStore _store;
    private readonly TimeProvider _clock;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="store">المخزن.</param>
    /// <param name="clock">مصدر الوقت — لا <c>DateTimeOffset.UtcNow</c> مباشرةً.</param>
    public ParameterSettingsService(IEntitlementEnforcer enforcer, IParameterStore store, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);

        _enforcer = enforcer;
        _store = store;
        _clock = clock;
    }

    /// <summary>يودِع إصداراً جديداً من مجموعةٍ على مستوى هذه المنشأة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Write)]
    public async ValueTask<Result<ParameterVersionView>> DepositAsync(
        TenantId tenant,
        UserId actor,
        ParameterVersionDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Core, EntitlementAccess.Write, "Core.Parameters.Deposit", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ParameterVersionView>.Failure(gate.Errors);
        }

        ParameterSetDefinition? definition = ParameterCatalogue.Find(draft.SetCode);

        if (definition is null)
        {
            return Result<ParameterVersionView>.Failure(ParameterErrors.SetUnknown(draft.SetCode ?? string.Empty));
        }

        if (!ParameterApprovalInfo.CarriesAHumanApprover(draft.Approval))
        {
            return Result<ParameterVersionView>.Failure(
                ParameterErrors.ApprovalNotDepositable(ParameterApprovalInfo.TokenOf(draft.Approval)));
        }

        if (string.IsNullOrWhiteSpace(draft.ApprovedBy))
        {
            return Result<ParameterVersionView>.Failure(ParameterErrors.ApproverIsNotAHuman());
        }

        if (string.IsNullOrWhiteSpace(draft.SourceRef))
        {
            return Result<ParameterVersionView>.Failure(ParameterErrors.SourceRefMissing());
        }

        // ── المجموعة تُودَع كاملةً: لا ناقصَ ولا زائد ─────────────────────────
        List<string> missing = [.. definition.Keys
            .Select(static key => key.Key)
            .Where(key => !draft.Values.ContainsKey(key))];

        List<string> extra = [.. draft.Values.Keys
            .Where(key => !definition.Keys.Any(known => string.Equals(known.Key, key, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)];

        if (missing.Count > 0 || extra.Count > 0)
        {
            return Result<ParameterVersionView>.Failure(
                ParameterErrors.KeysDoNotMatchTheSet(definition.Code, missing, extra));
        }

        List<ParameterValueView> values = [];

        foreach (ParameterKeyDefinition key in definition.Keys)
        {
            decimal value = draft.Values[key.Key];
            Error? refusal = ParameterGuards.Check(key.Key, key.Kind, value);

            if (refusal is not null)
            {
                return Result<ParameterVersionView>.Failure(refusal);
            }

            values.Add(new ParameterValueView(key.Key, key.Kind, value));
        }

        ParameterVersionView version = new(
            Guid.CreateVersion7(_clock.GetUtcNow()),
            definition.Code,
            ParameterScope.Tenant,
            draft.EffectiveFrom,
            draft.Approval,
            draft.ApprovedBy.Trim(),
            draft.ApprovedOn,
            draft.SourceRef.Trim(),
            values);

        bool written = await _store.TryDepositAsync(tenant, version, cancellationToken).ConfigureAwait(false);

        return written
            ? Result<ParameterVersionView>.Success(version)
            : Result<ParameterVersionView>.Failure(
                ParameterErrors.DuplicateVersion(definition.Code, draft.EffectiveFrom));
    }

    /// <summary>يقرأ كلَّ ما تراه هذه المنشأة: افتراضاتِ المنصّة وتجاوزاتِها هي وحدها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<ParameterVersionView>>> ListAsync(
        TenantId tenant, UserId actor, CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Core, EntitlementAccess.Read, "Core.Parameters.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ParameterVersionView>>.Failure(gate.Errors);
        }

        IReadOnlyList<ParameterVersionView> versions =
            await _store.ListAsync(tenant, cancellationToken).ConfigureAwait(false);

        return Result<IReadOnlyList<ParameterVersionView>>.Success(versions);
    }

    /// <summary>
    /// <b>قائمةُ مراجعة المحاسب القانوني</b>: كلُّ إصدارٍ غير موقَّع، ومعه كلُّ مستندٍ
    /// مُرحَّلٍ استعمله. استعلامٌ واحد، وبابُ قراءةٍ منشور — لا تقريرٌ تحسبه شاشة.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<ParameterReviewView>>> ReviewAsync(
        TenantId tenant, UserId actor, CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Core, EntitlementAccess.Read, "Core.Parameters.Review", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ParameterReviewView>>.Failure(gate.Errors);
        }

        IReadOnlyList<ParameterReviewRow> rows = await _store.ReviewAsync(tenant, cancellationToken).ConfigureAwait(false);

        List<ParameterReviewView> review = [.. rows
            .GroupBy(static row => row.Version.Id)
            .Select(static group => new ParameterReviewView(
                group.First().Version,
                [.. group.Where(static row => row.Usage is not null).Select(static row => row.Usage!)]))
            .OrderBy(static entry => entry.Version.SetCode, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Version.EffectiveFrom)];

        return Result<IReadOnlyList<ParameterReviewView>>.Success(review);
    }
}
