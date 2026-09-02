using System.Text.Json;
using System.Text.Json.Nodes;
using Babel.Ai.Agent;
using Babel.SharedKernel;

namespace Babel.Ai.Workspace;

/// <summary>
/// <b>آخر بابٍ قبل أن تهبط مسوّدة — وفيه حارسان لا واحد.</b>
/// <list type="number">
///   <item><b>حارسُ الترحيل، ويسبق التأكيد:</b> عمليةٌ فعلُها ليس <c>draft</c>، أو مسارٌ
///         فيه مقطعٌ لا يُعكَس (<c>…/posting</c> وأخواتُه)، <b>لا تُسلَّم إلى المنفّذ
///         ولو أذنت البوّابة ولو أكّد الإنسان</b>. وهو الطبقة الرابعة تحت ثلاثٍ قائمة —
///         الكتالوج المُرشَّح عند التركيب، و<c>AgentToolGate</c>، وغيابُ عمليات الترحيل
///         من الكتالوج أصلاً — <b>وموضعُه هنا مقصود</b>: الثلاث تحرس ما <b>يُعرَض</b>
///         على النموذج وما <b>يُؤذَن</b> له، وهذا يحرس ما <b>يُنفَّذ</b> فعلاً. فمن ركّب
///         منفّذاً يقبل غير المسوّدات لا يجد ما يمرّره إليه.</item>
///   <item><b>ثمّ التأكيد، ومعناه واحد:</b> «أقبل شكل هذه البيانات». <b>ولا يعني
///         الترحيل</b> — والناتج بعده مسوّدةٌ كما كان قبله، والترحيل فعلٌ بصريّ يدويّ
///         على شاشة المستند.</item>
/// </list>
/// <para>
/// <b>وبطاقة التأكيد لا تعرض معرّفاً واحداً:</b> ما فُكّ من مِقبضٍ صار معرّف صفٍّ في
/// الجسم، وعرضُه على الشاشة يجعل الحدَّ الذي حُفظ أمام النموذج مكسوراً أمام الكتف الذي
/// يقف خلف المستخدم. فالحقل يُقنَّع ويُقال إنه مُقنَّع.
/// </para>
/// </summary>
public sealed class AgentDraftConfirmationGate : IAgentDraftSubmitter
{
    private readonly IAgentDraftSubmitter _destination;
    private readonly IAgentWorkspaceStore _store;
    private readonly AgentWorkspaceOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>يركّب الباب.</summary>
    /// <param name="destination">المنفّذ الحقيقي — ولا يُنادى إلا بعد الحارسَين.</param>
    /// <param name="store">مخزن الجلسات.</param>
    /// <param name="options">الإعدادات.</param>
    /// <param name="clock">مصدر الوقت.</param>
    public AgentDraftConfirmationGate(
        IAgentDraftSubmitter destination,
        IAgentWorkspaceStore store,
        AgentWorkspaceOptions options,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        _destination = destination;
        _store = store;
        _options = options;
        _clock = clock;
    }

    /// <summary>
    /// <b>الفعل المسموح وحده.</b> مقروءٌ من معرّف العملية نفسه، ومكتوبٌ مرّةً واحدة
    /// كي يقرأه حارسٌ معماري بدل أن يُكرَّر في ملفَّين فينحرف أحدهما.
    /// </summary>
    public const string PermittedVerb = "draft";

    /// <summary>
    /// <b>هل تبلغ هذه العملية ما لا يُعكَس؟</b> يعيد سبب الرفض أو <c>null</c>.
    /// <para>
    /// ويُقرأ من <b>المسار</b> كما تُقرأ في <c>AgentToolCatalogue.IrreversibleSegmentIn</c>
    /// وفي البوّابة: عمليةٌ تُسمّى غداً بأي اسمٍ ومسارُها يمرّ بـ<c>posting</c> تبقى ممنوعة.
    /// </para>
    /// </summary>
    /// <param name="operationId">معرّف العملية، أو <c>null</c> لأداة بروتوكول.</param>
    /// <param name="path">المسار المنشور.</param>
    public static Error? Refuse(string? operationId, string? path)
    {
        if (operationId is null)
        {
            return AgentWorkspaceErrors.StepIsNotADraftOperation(string.Empty);
        }

        if (!operationId.StartsWith(PermittedVerb, StringComparison.Ordinal))
        {
            return AgentWorkspaceErrors.StepIsNotADraftOperation(operationId);
        }

        string published = path ?? string.Empty;
        string? irreversible = AgentToolCatalogue.IrreversibleSegmentIn(published);

        return irreversible is null
            ? null
            : AgentWorkspaceErrors.StepReachesAnIrreversibleDoor(operationId, published, irreversible);
    }

    /// <inheritdoc />
    public async Task<Result<AgentDraftLanding>> SubmitAsync(
        AgentDispatch dispatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dispatch);

        // ── ١ · الحارس البنيوي، قبل كل شيء وقبل أن يُسأل إنسان ────────────────
        Error? refusal = Refuse(dispatch.Tool.OperationId, dispatch.Tool.Path);
        if (refusal is not null)
        {
            return Result<AgentDraftLanding>.Failure(refusal);
        }

        AgentWorkspaceSession? session = _store.FindForLoop(dispatch.Caller.SessionId);
        if (session is null)
        {
            return Result<AgentDraftLanding>.Failure(AgentWorkspaceErrors.SessionNotFound);
        }

        // ── ٢ · التأكيد: «أقبل شكل هذه البيانات» ─────────────────────────────
        AgentWorkspaceStep step = session.OpenStep(dispatch.Tool.Name);

        AgentWorkspaceConfirmation card = new(
            step.StepId,
            dispatch.Tool.Name,
            dispatch.Tool.Path ?? string.Empty,
            Fields(dispatch.Body));

        using CancellationTokenSource waiting =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        waiting.CancelAfter(_options.HumanWait);

        Result accepted = await session
            .AwaitConfirmationAsync(card, waiting.Token)
            .ConfigureAwait(false);

        session.Touch(_clock.GetUtcNow());

        if (accepted.IsFailure)
        {
            return Result<AgentDraftLanding>.Failure(accepted.Errors);
        }

        // ── ٣ · وبعد كل ذلك: مسوّدة ──────────────────────────────────────────
        return await _destination.SubmitAsync(dispatch, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// يسطّح الجسم حقولاً معروضة. <b>وكل قيمةٍ شكلُها معرّف تُقنَّع</b> — بالشكل لا
    /// بقائمة أسماء حقول: قائمةٌ تُكتب بيدٍ تنسى الحقل الذي يُضاف غداً.
    /// </summary>
    /// <param name="body">جسم المسوّدة كما اجتاز البوّابة.</param>
    internal static IReadOnlyList<AgentDraftField> Fields(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        List<AgentDraftField> fields = [];

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return fields;
        }

        Walk(root, string.Empty, fields, 0);
        return fields;
    }

    private static void Walk(JsonNode? node, string path, List<AgentDraftField> fields, int depth)
    {
        if (depth > 12 || node is null)
        {
            return;
        }

        switch (node)
        {
            case JsonObject entry:
                foreach (KeyValuePair<string, JsonNode?> property in entry)
                {
                    Walk(
                        property.Value,
                        path.Length == 0 ? property.Key : path + "." + property.Key,
                        fields,
                        depth + 1);
                }

                break;

            case JsonArray array:
                for (int index = 0; index < array.Count; index++)
                {
                    Walk(array[index], path + "[" + index.ToString("D", System.Globalization.CultureInfo.InvariantCulture) + "]",
                        fields, depth + 1);
                }

                break;

            case JsonValue value:
                string? text = value.GetValueKind() == JsonValueKind.String
                    ? value.GetValue<string>()
                    : value.ToJsonString();

                bool masked = text is not null && Guid.TryParse(text, out _);
                fields.Add(new AgentDraftField(path, masked ? null : text, masked));
                break;

            default:
                break;
        }
    }
}

/// <summary>
/// منفّذُ مسوّداتٍ <b>غير مركَّب</b> — يرفض بجملةٍ تسمّي ما ينقص.
/// <para>
/// وهو سابقة <c>UnavailableJournalEntryReader</c> و<c>UnavailableFleetDirectory</c> في
/// هذا المستودع نفسها: <b>نقصُ تركيبٍ مُعلَن</b> لا فراغٌ يُقرأ نجاحاً. ووصلُ كلٍّ من
/// عمليات المسوّدات الثلاث والعشرين بوحدتها المالكة سطحٌ آخر، وإلى أن ينزل تقول اللوحة
/// أيّ خطوةٍ وقفت ولماذا بدل أن تُعلّق بلا جواب.
/// </para>
/// </summary>
public sealed class UnavailableAgentDraftSubmitter : IAgentDraftSubmitter
{
    /// <inheritdoc />
    public Task<Result<AgentDraftLanding>> SubmitAsync(
        AgentDispatch dispatch,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result<AgentDraftLanding>.Failure(
            AgentWorkspaceErrors.DraftDestinationUnavailable));
}
