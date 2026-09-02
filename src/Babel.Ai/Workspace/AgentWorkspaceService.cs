using System.Globalization;
using Babel.Ai.Agent;
using Babel.Ai.Lookup;
using Babel.SharedKernel;

namespace Babel.Ai.Workspace;

/// <summary>
/// <b>مساحة العمل الجانبية — واجهةُ الوكيل الوحيدة، ولا ميزةَ مبعثرة على الشاشات.</b>
/// <para>
/// وهي التي تحوّل حلقة الوكيل — دورٌ يبثّ أحداثه ثم ينتهي — إلى شيءٍ يستطيع متصفّحٌ أن
/// يمسكه: <b>جلسةٌ تُفتح، ورسالةٌ تُرسَل، وأحداثٌ تُقرأ بمؤشّر، وخطّةٌ تُقرأ، وخطوةٌ
/// تُؤكَّد، وورقةٌ تُجاب، وإنفاقٌ يُقرأ</b>.
/// </para>
/// <para>
/// <b>والدور يجري خلف الطلب لا داخله.</b> ولذلك سببان: دورٌ يحمل نداءَي نموذجٍ وسؤالاً
/// لإنسان يتجاوز كل مهلة HTTP معقولة؛ <b>وأخطر من ذلك</b> أنّ إغلاق المتصفّح لنافذته
/// كان سيقتل دوراً في منتصفه — بين تأكيدٍ ومسوّدة — فتبقى الحال بلا صاحب. فالطلب يبدأ
/// الدور ويعود بمعرّفه، والدور يمضي، واللوحة تقرأ بمؤشّرها وتستأنف من حيث وقفت.
/// </para>
/// <para>
/// <b>ولا تُرحّل هذه المساحة شيئاً، ولا تملك ما تُرحّل به:</b> غايةُ ما تبلغه
/// <see cref="AgentDraftConfirmationGate"/>، وهو يرفض بنيوياً كلَّ عمليةٍ فعلُها ليس
/// <c>draft</c> وكلَّ مسارٍ فيه مقطعٌ لا يُعكَس.
/// </para>
/// </summary>
public sealed class AgentWorkspaceService
{
    private readonly IAgentWorkspaceStore _store;
    private readonly Func<AgentTurnService> _turns;
    private readonly ILookupHandles _handles;
    private readonly IAgentSpendLedger _spend;
    private readonly IAgentTenantBillingSource _billing;
    private readonly AgentOptions _agent;
    private readonly AgentWorkspaceOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>يركّب المساحة.</summary>
    /// <param name="store">مخزن الجلسات.</param>
    /// <param name="turns">مصنع حلقة الدور — تُحلّ لكل دورٍ على حدة.</param>
    /// <param name="handles">مُصدِر المقابض ومُستردّها.</param>
    /// <param name="spend">دفتر الإنفاق.</param>
    /// <param name="billing">إعداد إنفاق المنشأة.</param>
    /// <param name="agent">إعدادات الحلقة.</param>
    /// <param name="options">إعدادات المساحة.</param>
    /// <param name="clock">مصدر الوقت.</param>
    public AgentWorkspaceService(
        IAgentWorkspaceStore store,
        Func<AgentTurnService> turns,
        ILookupHandles handles,
        IAgentSpendLedger spend,
        IAgentTenantBillingSource billing,
        AgentOptions agent,
        AgentWorkspaceOptions options,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(turns);
        ArgumentNullException.ThrowIfNull(handles);
        ArgumentNullException.ThrowIfNull(spend);
        ArgumentNullException.ThrowIfNull(billing);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        _store = store;
        _turns = turns;
        _handles = handles;
        _spend = spend;
        _billing = billing;
        _agent = agent;
        _options = options;
        _clock = clock;
    }

    /// <summary>يفتح جلسة مساحةٍ لهذا المستخدم في هذه الشركة.</summary>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="companyId">الشركة.</param>
    /// <param name="user">المستخدم.</param>
    /// <param name="companyNameAr">اسم الشركة بالعربية.</param>
    public AgentWorkspaceSession Open(TenantId tenant, Guid companyId, UserId user, string companyNameAr) =>
        _store.Open(tenant, companyId, user, companyNameAr);

    /// <summary>يجد جلسةً بنطاق طالبها.</summary>
    /// <param name="sessionId">معرّف الجلسة.</param>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="companyId">الشركة.</param>
    /// <param name="user">المستخدم.</param>
    public Result<AgentWorkspaceSession> Find(Guid sessionId, TenantId tenant, Guid companyId, UserId user) =>
        _store.Find(sessionId, tenant, companyId, user);

    /// <summary>
    /// يبدأ دوراً على رسالةٍ من المستخدم ويعود بمعرّف الدور فوراً. <b>ولا ينتظر انتهاءه.</b>
    /// </summary>
    /// <param name="session">الجلسة.</param>
    /// <param name="utterance">كلام المستخدم بأسمائه.</param>
    /// <param name="permittedOperationIds">العمليات التي يبلغها استحقاق هذا المتكلّم.</param>
    /// <param name="todayIso">تاريخ اليوم <c>yyyy-MM-dd</c>.</param>
    public Result<Guid> Send(
        AgentWorkspaceSession session,
        string utterance,
        IReadOnlySet<string> permittedOperationIds,
        string todayIso)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(utterance);
        ArgumentNullException.ThrowIfNull(permittedOperationIds);
        ArgumentNullException.ThrowIfNull(todayIso);

        Result<Guid> begun = session.BeginTurn(_clock.GetUtcNow());
        if (begun.IsFailure)
        {
            return begun;
        }

        AgentCaller caller = new(
            session.Tenant,
            session.CompanyId,
            session.SessionId,
            session.CompanyNameAr,
            permittedOperationIds);

        Guid turnId = begun.Value;

        // ‏**مُهمَل عمداً وبانضباط**: `PumpAsync` لا ترمي — كل ما يقع داخلها يصير حدثاً
        // في السجلّ، والطور ينتهي دائماً. ومهمّةٌ تُنتظَر هنا كانت ستعيد الدور إلى داخل
        // الطلب وتُبطل كل ما وُصف أعلاه.
        _ = Task.Run(() => PumpAsync(session, caller, turnId, utterance, todayIso), CancellationToken.None);

        return Result<Guid>.Success(turnId);
    }

    /// <summary>يقرأ الأحداث بعد مؤشّر، وينتظر إن لم يكن هناك جديد.</summary>
    /// <param name="session">الجلسة.</param>
    /// <param name="after">آخر رقمٍ قرأته اللوحة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public Task<IReadOnlyList<AgentWorkspaceEvent>> ReadAsync(
        AgentWorkspaceSession session,
        long after,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.ReadAsync(after, _options.EventWait, cancellationToken);
    }

    /// <summary>يُسلّم تأكيد شكل البيانات — أو رفضه.</summary>
    /// <param name="session">الجلسة.</param>
    /// <param name="stepId">الخطوة المنتظِرة.</param>
    /// <param name="accepted">هل قَبِل الإنسان شكل البيانات؟</param>
    public static Result Confirm(AgentWorkspaceSession session, Guid stepId, bool accepted)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.SettleConfirmation(stepId, accepted);
    }

    /// <summary>يُسلّم اختيار الإنسان على ورقة السؤال.</summary>
    /// <param name="session">الجلسة.</param>
    /// <param name="questionId">معرّف الورقة المعتِم.</param>
    /// <param name="optionToken">رمز الخيار.</param>
    public static Result Answer(AgentWorkspaceSession session, string questionId, string optionToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.SettleAnswer(questionId, optionToken);
    }

    /// <summary>يقرأ إنفاق المنشأة في نافذتها الجارية.</summary>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async Task<AgentWorkspaceSpend> SpendAsync(TenantId tenant, CancellationToken cancellationToken)
    {
        AgentTenantBilling billing = await _billing.ReadAsync(tenant, cancellationToken).ConfigureAwait(false);

        long? ceiling = billing.BringsItsOwnKey
            ? billing.TokenCeiling
            : billing.TokenCeiling ?? _agent.DefaultTenantTokenCeiling;

        AgentTenantSpend measured = await _spend
            .ReadAsync(tenant, _agent.SpendWindow, cancellationToken)
            .ConfigureAwait(false);

        return new AgentWorkspaceSpend(
            measured.Usage.Billable,
            ceiling,
            measured.Turns,
            (long)_agent.SpendWindow.TotalSeconds,
            billing.BringsItsOwnKey);
    }

    /// <summary>
    /// <b>يضخّ أحداث الدور إلى سجلّ الجلسة</b> — ولا يرمي: كل ما يقع يصير حدثاً يُقرأ.
    /// <para>
    /// <b>ولماذا لا يرمي:</b> هذه المهمّة بلا مُنتظِر. استثناءٌ يخرج منها يختفي في مهمّةٍ
    /// مُهملة، فتبقى اللوحة تنتظر حدثاً لن يأتي وتبدو معلّقة — وهي أسوأ حالٍ يمكن أن
    /// تُترك فيها واجهة. فما يقع يُكتب حدثَ رفضٍ بنصّه، والطور ينتهي دائماً.
    /// </para>
    /// </summary>
    private async Task PumpAsync(
        AgentWorkspaceSession session,
        AgentCaller caller,
        Guid turnId,
        string utterance,
        string todayIso)
    {
        AgentTurnPhase ending = AgentTurnPhase.Completed;

        try
        {
            AgentTurnService loop = _turns();

            await foreach (AgentTurnEvent moment in loop
                .RunAsync(new AgentTurnRequest(caller, utterance, todayIso))
                .ConfigureAwait(false))
            {
                Guid? stepId = null;

                switch (moment.Kind)
                {
                    case AgentTurnEventKind.PlanProposed:
                        session.ReplacePlan(moment.Steps);
                        break;

                    case AgentTurnEventKind.ToolStarted:
                        // ‏**وأداة الخطّة ليست خطوة**: إعلانُ الخطّة ليس عملاً فيها.
                        if (!string.Equals(moment.ToolName, AgentProtocolTools.ProposePlan, StringComparison.Ordinal))
                        {
                            stepId = session.OpenStep(moment.ToolName!).StepId;
                        }

                        break;

                    case AgentTurnEventKind.QuestionRaised:
                        stepId = Note(session, caller, moment);
                        break;

                    case AgentTurnEventKind.DraftLanded:
                        stepId = session.CloseStep(AgentStepState.Landed, moment.ScreenRoute)?.StepId;
                        break;

                    case AgentTurnEventKind.ToolRefused:
                        stepId = session.CloseStep(AgentStepState.Refused, null, moment.Errors)?.StepId;
                        break;

                    case AgentTurnEventKind.Refused:
                        ending = AgentTurnPhase.Refused;
                        break;

                    default:
                        break;
                }

                session.Append(new AgentWorkspaceEvent(
                    0,
                    turnId,
                    moment.Kind,
                    moment.Text,
                    moment.ToolName,
                    moment.QuestionId,
                    moment.RegisterKey,
                    moment.ScreenRoute,
                    stepId,
                    moment.Errors,
                    moment.Steps));
            }
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            ending = AgentTurnPhase.Refused;

            session.Append(new AgentWorkspaceEvent(
                0,
                turnId,
                AgentTurnEventKind.Refused,
                null,
                null,
                null,
                null,
                null,
                null,
                [Broke(fault)],
                []));
        }
        finally
        {
            session.Touch(_clock.GetUtcNow());
            session.EndTurn(ending);
        }
    }

    /// <summary>
    /// يقيّد ورقةً رُفعت. <b>ويفكّ مِقبضها هنا</b> لأن البوّابة ستُسلّم إلى راسم الأوراق
    /// موضوعَ المِقبض مفكوكاً لا نصَّه، فلا بدّ من مفتاحٍ يربط الاثنين.
    /// </summary>
    private Guid? Note(AgentWorkspaceSession session, AgentCaller caller, AgentTurnEvent moment)
    {
        Guid? stepId = session.CloseStep(AgentStepState.AwaitingAnswer)?.StepId;

        if (moment.QuestionId is null || moment.RegisterKey is null)
        {
            return stepId;
        }

        Result<RedeemedLookupHandle> redeemed = _handles.Redeem(
            moment.QuestionId, LookupHandlePurpose.Question, caller.Tenant, caller.CompanyId, caller.SessionId);

        if (redeemed.IsSuccess)
        {
            session.NoteRaisedQuestion(
                redeemed.Value.Subject, moment.QuestionId, moment.RegisterKey, moment.Text ?? string.Empty);
        }

        return stepId;
    }

    private static Error Broke(Exception fault) => new(
        AgentWorkspaceErrors.CodePrefix + "turn_broke",
        "انقطع الدور بعطلٍ في الخادم: " + fault.GetType().Name
        + ". ولم يهبط شيء. أعِد المحاولة، وإن تكرّر فالعطل ليس في طلبك.",
        "the turn broke with a server fault: " + fault.GetType().Name + "; nothing landed.");

    /// <summary>عددٌ للرسائل بثقافةٍ ثابتة.</summary>
    internal static string Count(long value) => value.ToString(CultureInfo.InvariantCulture);
}
