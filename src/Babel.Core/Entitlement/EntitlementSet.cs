using System.Collections.ObjectModel;
using Babel.SharedKernel;

namespace Babel.Core.Entitlement;

/// <summary>
/// مجموعة استحقاق مستأجر: حالة لكل وحدة. غير قابلة للتغيير بعد الإنشاء،
/// وكل مجموعة تُنشأ عبر <see cref="Create"/> الذي يرفض ما هو غير متسق.
/// </summary>
public sealed class EntitlementSet
{
    private readonly ReadOnlyDictionary<BabelModule, EntitlementState> _states;

    private EntitlementSet(TenantId tenant, ReadOnlyDictionary<BabelModule, EntitlementState> states)
    {
        Tenant = tenant;
        _states = states;
    }

    /// <summary>المستأجر.</summary>
    public TenantId Tenant { get; }

    /// <summary>حالة كل وحدة.</summary>
    public IReadOnlyDictionary<BabelModule, EntitlementState> States => _states;

    /// <summary>حالة وحدة بعينها.</summary>
    public EntitlementState StateOf(BabelModule module) =>
        _states.TryGetValue(module, out EntitlementState state) ? state : EntitlementState.NotEntitled;

    /// <summary>
    /// هل يسمح الاستحقاق بهذا الوصول على هذه الوحدة؟
    /// <para>القرار من <see cref="EntitlementRules.Allows"/> وحده: هذه الدالّة
    /// <b>تجد الحالة</b> ولا تقرّر ما تسمح به.</para>
    /// </summary>
    /// <param name="module">الوحدة.</param>
    /// <param name="access">الوصول المطلوب.</param>
    /// <returns><c>true</c> إن كان الوصول مسموحاً.</returns>
    public bool Allows(BabelModule module, EntitlementAccess access) =>
        EntitlementRules.Allows(StateOf(module), access);

    /// <summary>مجموعة الحد الأدنى: الوحدات الإلزامية فاعلة وما عداها غير مشترى.</summary>
    public static EntitlementSet Baseline(TenantId tenant)
    {
        Dictionary<BabelModule, EntitlementState> states = [];
        foreach (BabelModule module in ModuleDependencyGraph.All)
        {
            states[module] = ModuleDependencyGraph.IsMandatory(module) ? EntitlementState.Entitled : EntitlementState.NotEntitled;
        }

        return new EntitlementSet(tenant, new ReadOnlyDictionary<BabelModule, EntitlementState>(states));
    }

    /// <summary>
    /// ينشئ مجموعة بعد التحقق من اتساقها. المجموعة غير المتسقة تُرفض ولا تُصحَّح ضمنياً:
    /// التصحيح الضمني يعني أن العميل يظن أنه اشترى ما لم يشتره.
    /// </summary>
    public static Result<EntitlementSet> Create(TenantId tenant, IReadOnlyDictionary<BabelModule, EntitlementState> desired)
    {
        ArgumentNullException.ThrowIfNull(desired);

        List<Error> errors = [];
        Dictionary<BabelModule, EntitlementState> states = [];

        foreach (BabelModule module in ModuleDependencyGraph.All)
        {
            if (!desired.TryGetValue(module, out EntitlementState state))
            {
                errors.Add(EntitlementErrors.IncompleteSet(module));
                continue;
            }

            states[module] = state;
        }

        if (errors.Count > 0)
        {
            return Result<EntitlementSet>.Failure(errors);
        }

        errors.AddRange(Validate(states));

        return errors.Count > 0
            ? Result<EntitlementSet>.Failure(errors)
            : Result<EntitlementSet>.Success(new EntitlementSet(tenant, new ReadOnlyDictionary<BabelModule, EntitlementState>(states)));
    }

    /// <summary>نسخة معدّلة، بعد التحقق من اتساق الناتج.</summary>
    public Result<EntitlementSet> With(IReadOnlyDictionary<BabelModule, EntitlementState> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        Dictionary<BabelModule, EntitlementState> next = new(_states);
        foreach ((BabelModule module, EntitlementState state) in changes)
        {
            next[module] = state;
        }

        // الأرضية تُقاس على **الانتقال** لا على المجموعة: «‏Assets = NotEntitled»
        // جملةٌ مشروعة عن مستأجر لم يشترِها قط، وقطعٌ لسجلّ أصولٍ عن مستأجر اشتراها.
        // والفرق ليس في المطلوب بل فيما سبقه.
        List<Error> floorErrors = [];
        foreach (BabelModule module in ModuleDependencyGraph.All)
        {
            EntitlementState was = StateOf(module);
            if (was == EntitlementState.NotEntitled)
            {
                continue;
            }

            EntitlementState to = next.TryGetValue(module, out EntitlementState found)
                ? found
                : EntitlementState.NotEntitled;

            EntitlementState floor = (EntitlementState)Math.Min(
                (int)was, (int)ModuleDependencyGraph.FloorOf(module));

            if (to < floor)
            {
                floorErrors.Add(EntitlementErrors.RecordBearingModuleRevoked(module, was));
            }
        }

        Result<EntitlementSet> created = Create(Tenant, next);
        if (floorErrors.Count == 0)
        {
            return created;
        }

        return Result<EntitlementSet>.Failure(
            created.IsFailure ? [.. created.Errors, .. floorErrors] : floorErrors);
    }

    /// <summary>الوحدات التي اختلفت حالتها بين هذه المجموعة والأخرى.</summary>
    public IReadOnlyList<(BabelModule Module, EntitlementState From, EntitlementState To)> DiffTo(EntitlementSet other)
    {
        ArgumentNullException.ThrowIfNull(other);

        List<(BabelModule, EntitlementState, EntitlementState)> changes = [];
        foreach (BabelModule module in ModuleDependencyGraph.All)
        {
            EntitlementState from = StateOf(module);
            EntitlementState to = other.StateOf(module);
            if (from != to)
            {
                changes.Add((module, from, to));
            }
        }

        return changes;
    }

    private static List<Error> Validate(Dictionary<BabelModule, EntitlementState> states)
    {
        List<Error> errors = [];

        foreach ((BabelModule module, EntitlementState state) in states)
        {
            // «إلزامية» تعني **لا تُطفأ**، لا «يجب أن تبقى قابلة للكتابة». الفرق هو
            // كل الفرق: عميلٌ انقطع سداده يبقى قادراً على قراءة دفتره وطباعة تقاريره
            // وتقديم إقراره — والحالة التي تصف ذلك هي ReadOnly. منعُها كان يجعل
            // الاشتراك المنقطع غير قابل للتمثيل على أهمّ الوحدات
            // (docs/evidence/traps.md#fakh-mandatory-module-cannot-be-read-only).
            if (ModuleDependencyGraph.IsMandatory(module) && state == EntitlementState.NotEntitled)
            {
                errors.Add(EntitlementErrors.MandatoryModuleDisabled(module));
            }

            if (state == EntitlementState.NotEntitled)
            {
                continue;
            }

            // قدرة الوحدة لا تتجاوز قدرة ما تعتمد عليه: نقاط بيع فاعلة فوق مخزون للقراءة فقط
            // تعني بيعاً بلا حركة مخزون — وهي مجموعة غير متسقة تُرفض، لا تُصحَّح.
            foreach (BabelModule requirement in ModuleDependencyGraph.RequirementsOf(module))
            {
                EntitlementState requirementState = states.TryGetValue(requirement, out EntitlementState found)
                    ? found
                    : EntitlementState.NotEntitled;

                if (requirementState < state)
                {
                    errors.Add(EntitlementErrors.UnsatisfiedRequirement(module, state, requirement, requirementState));
                }
            }
        }

        return errors;
    }
}
