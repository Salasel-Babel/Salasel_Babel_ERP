using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Babel.Ai.Lookup;
using Babel.SharedKernel;

namespace Babel.Ai.Agent;

/// <summary>طلب دورٍ واحد.</summary>
/// <param name="Caller">المتكلّم ونطاقه وصلاحياته — لا شيء منه من النموذج.</param>
/// <param name="Utterance">كلام المستخدم بأسمائه.</param>
/// <param name="TodayIso">
/// تاريخ اليوم <c>yyyy-MM-dd</c>. <b>ويُحقن في رسالة نظامٍ وسط الرسائل لا في نصّ النظام
/// العلوي</b> — وحقنُه هناك هو أشهر مُبطِلٍ صامت لذاكرة البادئة. و<c>VoiceCapture</c>
/// يُمرّر «اليوم» خاصّيةً للسبب المجاور: الحتميّة.
/// </param>
/// <param name="Prior">نسخة المحادثة السابقة — فارغةٌ في أول دور.</param>
public sealed record AgentTurnRequest(
    AgentCaller Caller,
    string Utterance,
    string TodayIso,
    IReadOnlyList<AgentTranscriptEntry>? Prior = null);

/// <summary>
/// <b>حلقة الوكيل — نداءُ نموذجٍ، ثم بوّابة، ثم تنفيذ، ثم نداءٌ ثانٍ.</b>
/// <para>
/// <b>وترتيب العرض أدوات ← نظام ← رسائل، وهو ترتيب الذاكرة نفسه.</b> الأدوات تُبنى مرّةً
/// عند الإقلاع فلا تتغيّر؛ ونصّ النظام كتلةٌ مُجمَّدة عليها نقطة الذاكرة الوحيدة؛ وما
/// يتقلّب — تاريخ اليوم واسم الشركة المفتوحة — يذهب رسالةَ نظامٍ <b>في وسط الرسائل</b>،
/// أي بعد آخر نقطة. فبادئةُ الطلب واحدةٌ بايتاً ببايت بين نداءٍ ونداء وبين مستخدمٍ ومستخدم.
/// </para>
/// <para>
/// <b>ولا شيء يُرسَل قبل المِصفاة:</b> الطلب لا يُبنى إلا من ظرفٍ مختوم، والظرف لا يُنشَأ
/// إلا خلف <c>AgentOutboundBoundary.Seal</c>. ورفضٌ عند الحدّ يعني أن الدور <b>لم يُرسَل</b>،
/// ويُقال للمستخدم ما وُجد بجملةٍ تسمّي الشكل.
/// </para>
/// <para>
/// <b>ولا شيء يُنفَّذ قبل البوّابة:</b> <see cref="AgentToolGate.Authorise"/> هي موضع إنشاء
/// <see cref="AgentDispatch"/> الوحيد، والمنفّذ لا يقبل غيره.
/// </para>
/// </summary>
public sealed class AgentTurnService
{
    private readonly IAgentModelGateway _gateway;
    private readonly AgentToolCatalogue _catalogue;
    private readonly AgentOptions _options;
    private readonly NameRegisterLookup _lookup;
    private readonly ILookupHandles _handles;
    private readonly IAgentQuestionSheets _questions;
    private readonly IAgentDraftSubmitter _drafts;
    private readonly IAgentSpendLedger _spend;
    private readonly IAgentTenantBillingSource _billing;

    /// <summary>يركّب الحلقة.</summary>
    /// <param name="gateway">الباب إلى المزوّد.</param>
    /// <param name="catalogue">الكتالوج المغلق.</param>
    /// <param name="options">الإعدادات — تُفحص هنا، ويسقط التركيب إن اعتلّت.</param>
    /// <param name="lookup">البحث المحلّي عن الأسماء.</param>
    /// <param name="handles">مُصدِر المقابض ومُستردّها.</param>
    /// <param name="questions">أوراق السؤال.</param>
    /// <param name="drafts">منفّذ المسوّدات.</param>
    /// <param name="spend">دفتر الإنفاق.</param>
    /// <param name="billing">إعداد إنفاق المنشأة.</param>
    /// <exception cref="ArgumentException">إن اعتلّت الإعدادات.</exception>
    public AgentTurnService(
        IAgentModelGateway gateway,
        AgentToolCatalogue catalogue,
        AgentOptions options,
        NameRegisterLookup lookup,
        ILookupHandles handles,
        IAgentQuestionSheets questions,
        IAgentDraftSubmitter drafts,
        IAgentSpendLedger spend,
        IAgentTenantBillingSource billing)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(handles);
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(drafts);
        ArgumentNullException.ThrowIfNull(spend);
        ArgumentNullException.ThrowIfNull(billing);

        // ‏**إعدادٌ معتلّ لا يُركَّب.** والفحص هنا يلتقط المفتاح المكتوب في حقل «اسم
        // المتغيّر» قبل أن يبلغ سطر سجلّ — سابقة GitHubModelsOptions نفسها.
        Result validation = options.Validate();
        if (validation.IsFailure)
        {
            throw new ArgumentException(
                "إعدادات حلقة الوكيل معتلّة فلا تُركَّب: "
                + string.Join(" · ", validation.Errors.Select(static error => error.MessageAr)),
                nameof(options));
        }

        _gateway = gateway;
        _catalogue = catalogue;
        _options = options;
        _lookup = lookup;
        _handles = handles;
        _questions = questions;
        _drafts = drafts;
        _spend = spend;
        _billing = billing;
    }

    /// <summary>
    /// يُجري دوراً ويبثّ ما يجري. <b>ولا يرمي على رفض</b>: الرفض حدثٌ يُقرأ في اللوحة.
    /// </summary>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async IAsyncEnumerable<AgentTurnEvent> RunAsync(
        AgentTurnRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        AgentCaller caller = request.Caller;

        // ── الإنفاق أوّلاً: لا يُرسَل دورٌ لمنشأةٍ بلغت سقفها ──────────────────
        AgentTenantBilling billing = await _billing.ReadAsync(caller.Tenant, cancellationToken).ConfigureAwait(false);
        long? ceiling = billing.BringsItsOwnKey
            ? billing.TokenCeiling
            : billing.TokenCeiling ?? _options.DefaultTenantTokenCeiling;

        Result admission = await _spend
            .AdmitAsync(caller.Tenant, ceiling, _options.SpendWindow, cancellationToken)
            .ConfigureAwait(false);

        if (admission.IsFailure)
        {
            yield return AgentTurnEvent.Refused(admission.Errors);
            yield break;
        }

        string apiKeyVariable = billing.ApiKeyVariable ?? _options.ApiKeyVariable;

        // ── نسخة المحادثة ────────────────────────────────────────────────────
        List<AgentTranscriptEntry> entries = [.. request.Prior ?? []];

        entries.Add(new AgentTranscriptEntry(AgentWireRole.User, AgentWireBlockKind.Text, request.Utterance));

        // ‏**ما يتقلّب يذهب هنا — لا في نصّ النظام العلوي**: اسم الشركة المفتوحة وتاريخ
        // اليوم. وموضعُه **بعد** دور المستخدم لا قبله، لأن رسالة النظام في وسط المحادثة
        // يجب أن تتلو دور مستخدم وأن تكون آخر الرسائل أو يتلوها دور مساعد — ولا تكون
        // أوّلها. فالترتيب هنا قيدُ بروتوكول لا ذوق.
        entries.Add(new AgentTranscriptEntry(
            AgentWireRole.System,
            AgentWireBlockKind.Text,
            "المنشأة المفتوحة: " + caller.CompanyNameAr + " · تاريخ اليوم: " + request.TodayIso));

        AgentTurnState state = new(_options.LookupBudgetPerTurn);
        RememberEarlierLookups(request.Prior, state);

        AgentTurnMetrics metrics = new();

        for (int iteration = 0; iteration < _options.MaxToolIterations; iteration++)
        {
            Result<AgentModelRequest> sealing =
                AgentTranscript.Seal(entries, _catalogue, _options, apiKeyVariable);

            if (sealing.IsFailure)
            {
                yield return AgentTurnEvent.Refused([AgentErrors.TurnRefusedAtTheBoundary, .. sealing.Errors]);
                yield break;
            }

            state.RecordModelCall();

            List<AgentTranscriptEntry> assistant = [];
            List<AgentToolCall> calls = [];
            string stopReason = "end_turn";

            await foreach (AgentModelEvent modelEvent in _gateway
                .StreamAsync(sealing.Value, cancellationToken)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                switch (modelEvent.Kind)
                {
                    case AgentModelEventKind.ThinkingDelta:
                        yield return AgentTurnEvent.Thinking(modelEvent.Text!);
                        break;

                    case AgentModelEventKind.TextDelta:
                        yield return AgentTurnEvent.Said(modelEvent.Text!);
                        break;

                    case AgentModelEventKind.ThinkingBlock:
                        // ‏**التوقيع يُعاد كما ورد حرفاً بحرف.** كتلةُ تفكيرٍ بتوقيعٍ
                        // معدَّل تُرفض عند المزوّد، وحذفُها يقطع سلسلة التفكير.
                        assistant.Add(new AgentTranscriptEntry(
                            AgentWireRole.Assistant,
                            AgentWireBlockKind.Thinking,
                            modelEvent.Text!,
                            Signature: modelEvent.Signature));
                        break;

                    case AgentModelEventKind.TextBlock:
                        assistant.Add(new AgentTranscriptEntry(
                            AgentWireRole.Assistant, AgentWireBlockKind.Text, modelEvent.Text!));
                        break;

                    case AgentModelEventKind.ToolCall:
                        assistant.Add(new AgentTranscriptEntry(
                            AgentWireRole.Assistant,
                            AgentWireBlockKind.ToolUse,
                            modelEvent.Call!.ArgumentsJson,
                            ToolUseId: modelEvent.Call.Id,
                            ToolName: modelEvent.Call.Name));
                        calls.Add(modelEvent.Call);
                        break;

                    case AgentModelEventKind.Completed:
                        stopReason = modelEvent.StopReason ?? "end_turn";
                        metrics.Record(modelEvent.Usage ?? AgentModelUsage.Zero);
                        await _spend
                            .RecordAsync(caller.Tenant, modelEvent.Usage ?? AgentModelUsage.Zero,
                                _options.SpendWindow, cancellationToken)
                            .ConfigureAwait(false);
                        break;

                    default:
                        break;
                }
            }

            entries.AddRange(assistant);

            if (calls.Count == 0)
            {
                yield return AgentTurnEvent.Completed(metrics);
                yield break;
            }

            // ── البوّابة ثم التنفيذ، نداءً نداءً ─────────────────────────────
            foreach (AgentToolCall call in calls)
            {
                yield return AgentTurnEvent.ToolStarted(call.Name);

                Result<AgentDispatch> authorised = Authorise(call, caller, state);

                if (authorised.IsFailure)
                {
                    // ‏**يعود إلى النموذج رفضاً يُقرأ لا استثناءً يقتل الدور.**
                    entries.Add(Refusal(call, authorised.Errors));
                    yield return AgentTurnEvent.ToolRefused(call.Name, authorised.Errors);
                    continue;
                }

                (AgentTranscriptEntry result, AgentTurnEvent? panel) = await ExecuteAsync(
                    authorised.Value, caller, state, cancellationToken).ConfigureAwait(false);

                entries.Add(result);

                if (panel is not null)
                {
                    yield return panel;
                }
            }

            _ = stopReason;
        }

        yield return AgentTurnEvent.Refused([AgentErrors.ToolIterationsExhausted(_options.MaxToolIterations)]);
    }

    /// <summary>
    /// <b>يبذر ذاكرة السبر من نسخة المحادثة — فالتضييق يعبر الأدوار وإن لم تعبرها الحالة.</b>
    /// <para>
    /// كانت <see cref="AgentTurnState"/> تُبنى جديدةً في كل دور، فكان «عبدالرحمن» ثم
    /// «عبدالرحمن الش» ثم «عبدالرحمن الشم» في ثلاثة أدوارٍ متتالية يمرّ بلا حارس واحد —
    /// وهو نصّاً «الخطر الحقيقيّ» الذي يسمّيه قرار هذا المسار. وكتلُ <c>tool_use</c>
    /// السابقة تحمل نصّ كل بحثٍ نطق به النموذج، فتُقرأ منها.
    /// </para>
    /// <para>
    /// <b>ووسائطٌ لا تُقرأ لا تُوقف الدور:</b> نسخةٌ محفوظة قد تحمل كتلةً مشوَّهة، وحارسٌ
    /// يسقط بخطأ برمجي عند نصٍّ مشوَّه يصير باباً لا حارساً — فتُتخطّى الكتلة وحدها.
    /// </para>
    /// </summary>
    /// <param name="prior">نسخة المحادثة السابقة، أو <c>null</c>.</param>
    /// <param name="state">حالة الدور الجديدة.</param>
    private static void RememberEarlierLookups(
        IReadOnlyList<AgentTranscriptEntry>? prior,
        AgentTurnState state)
    {
        if (prior is null)
        {
            return;
        }

        foreach (AgentTranscriptEntry entry in prior)
        {
            if (entry.Kind != AgentWireBlockKind.ToolUse
                || !string.Equals(entry.ToolName, AgentProtocolTools.LookupEntity, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                if (JsonNode.Parse(entry.Text) is JsonObject arguments
                    && arguments["text"] is { } text
                    && text.GetValueKind() == JsonValueKind.String)
                {
                    state.RememberEarlierLookup(text.GetValue<string>());
                }
            }
            catch (JsonException)
            {
                // كتلةٌ لا تُقرأ لا تُذكَر، ولا تُسقط الدور.
            }
        }
    }

    /// <summary>ينفّذ أمراً اجتاز البوّابة، ويعيد نتيجته إلى النموذج وحدثَه إلى اللوحة.</summary>
    private async Task<(AgentTranscriptEntry Result, AgentTurnEvent? Panel)> ExecuteAsync(
        AgentDispatch dispatch,
        AgentCaller caller,
        AgentTurnState state,
        CancellationToken cancellationToken)
    {
        // ── الخطّة: إعلانٌ يُعرض على الإنسان، ولا يُنفَّذ منه شيء ──────────────
        //
        // ‏**ولماذا لا تنتظر هذه الأداة إنساناً:** انتظارُها كان سيجعل «أوافق على
        // الخطّة» تأكيداً ثانياً فوق تأكيد كل خطوة، فيتعلّم المستخدم أن يضغط «موافق»
        // مرّتين على كل شيء — وهو بعينه ما يُبطل التأكيد الذي يهمّ. والتأكيد الذي يهمّ
        // واحد: <b>شكلُ بيانات المسوّدة قبل أن تهبط</b>، وهو عند منفّذ المسوّدات.
        if (string.Equals(dispatch.Tool.Name, AgentProtocolTools.ProposePlan, StringComparison.Ordinal))
        {
            JsonObject planned = (JsonObject)JsonNode.Parse(dispatch.Body)!;

            List<string> steps = [];
            if (planned["steps"] is JsonArray declared)
            {
                foreach (JsonNode? step in declared)
                {
                    if (step is not null && step.GetValueKind() == JsonValueKind.String)
                    {
                        steps.Add(step.GetValue<string>());
                    }
                }
            }

            return (new AgentTranscriptEntry(
                AgentWireRole.User,
                AgentWireBlockKind.ToolResult,
                "{\"plan\":\"recorded\"}",
                ToolUseId: dispatch.CallId,
                ToolName: dispatch.Tool.Name), AgentTurnEvent.PlanProposed(steps));
        }

        if (string.Equals(dispatch.Tool.Name, AgentProtocolTools.LookupEntity, StringComparison.Ordinal))
        {
            JsonObject arguments = (JsonObject)JsonNode.Parse(dispatch.Body)!;
            string registerKey = arguments["kind"]!.GetValue<string>();
            string text = arguments["text"]!.GetValue<string>();

            Result<NameLookupResult> found = await _lookup
                .ResolveAsync(registerKey, text, new LookupSession(caller.Tenant, caller.CompanyId, caller.SessionId),
                    cancellationToken)
                .ConfigureAwait(false);

            if (found.IsFailure)
            {
                return (Refusal(dispatch, found.Errors), AgentTurnEvent.ToolRefused(dispatch.Tool.Name, found.Errors));
            }

            NameLookupResult answer = found.Value;
            state.RecordLookup(registerKey, text, answer.Outcome == NameLookupOutcome.NeedsQuestion);

            // ‏**الشكل السلكيّ يُكتب في وحدة البحث لا هنا** — ثلاثة مفاتيح لا رابع لها،
            // ومجموعتها واحدة في الحالات الثلاث، فلا يُقاس من الشكل شيء.
            AgentTranscriptEntry result = new(
                AgentWireRole.User,
                AgentWireBlockKind.ToolResult,
                NameLookupWire.Write(answer),
                ToolUseId: dispatch.CallId,
                ToolName: dispatch.Tool.Name);

            AgentTurnEvent? panel = answer.Outcome == NameLookupOutcome.NeedsQuestion
                ? AgentTurnEvent.QuestionRaised(answer.QuestionId!, registerKey, text)
                : null;

            return (result, panel);
        }

        if (string.Equals(dispatch.Tool.Name, AgentProtocolTools.AskQuestion, StringComparison.Ordinal))
        {
            Guid questionId = dispatch.Redeemed[0].Subject;

            Result<string> handle = await _questions
                .AwaitAnswerAsync(questionId, caller, cancellationToken)
                .ConfigureAwait(false);

            if (handle.IsFailure)
            {
                return (Refusal(dispatch, handle.Errors), AgentTurnEvent.ToolRefused(dispatch.Tool.Name, handle.Errors));
            }

            // كل سجلٍّ غامضٍ يُرفع حجرُه بعد أن سُئل عنه فعلاً.
            foreach (string registerKey in state.RegistersAwaitingQuestion.ToArray())
            {
                state.RecordQuestionAnswered(registerKey);
            }

            // ‏**حقلٌ واحد، وشكلٌ واحد سواء اختار قائماً أو أنشأ جديداً** — فلا يتعلّم
            // النموذج حتى أنّ طرفاً أُنشئ.
            return (new AgentTranscriptEntry(
                AgentWireRole.User,
                AgentWireBlockKind.ToolResult,
                "{\"handle\":\"" + handle.Value + "\"}",
                ToolUseId: dispatch.CallId,
                ToolName: dispatch.Tool.Name), null);
        }

        Result<AgentDraftLanding> landed = await _drafts
            .SubmitAsync(dispatch, cancellationToken)
            .ConfigureAwait(false);

        if (landed.IsFailure)
        {
            return (Refusal(dispatch, landed.Errors), AgentTurnEvent.ToolRefused(dispatch.Tool.Name, landed.Errors));
        }

        // ‏**ما يعود إلى النموذج «مسوّدة» وحدها** — لا معرّف ولا رقم ولا مسار. والمسار
        // يذهب إلى الشاشة، وهناك يقرؤه إنسان ويرحّل بيده إن شاء.
        return (new AgentTranscriptEntry(
            AgentWireRole.User,
            AgentWireBlockKind.ToolResult,
            "{\"state\":\"draft\"}",
            ToolUseId: dispatch.CallId,
            ToolName: dispatch.Tool.Name), AgentTurnEvent.DraftLanded(landed.Value.ScreenRoute));
    }

    /// <summary>
    /// <b>البوّابة، وشبكةٌ تحتها تحوّل أي عطلٍ برمجي إلى رفضٍ يُقرأ.</b>
    /// <para>
    /// قاعدة هذه الوحدة مكتوبة: «الرفض يعود إلى النموذج <c>tool_result</c> بنصّه العربي
    /// ولا يُرمى استثناءً يقتل الدور». وكانت جملةً بلا شبكة: وسائطٌ بمفتاحٍ مكرَّر كانت
    /// تُخرج <c>ArgumentException</c> من <c>Authorise</c> إلى <c>IAsyncEnumerable</c>
    /// فتقتل الدور كلّه — مقيس. والسبب المباشر أُغلق في البوّابة نفسها، <b>وهذه الشبكة
    /// تُغلق الصنف</b>: نداءٌ يكتبه نموذجٌ احتماليّ لا يجوز أن يُسقط الحلقة بأي شكل.
    /// </para>
    /// <para>
    /// ولا تلتقط ما ليس من هذا الصنف: <see cref="OperationCanceledException"/> إلغاءٌ
    /// مطلوب، و<see cref="OutOfMemoryException"/> ليست حالة تشغيلٍ تُصحَّح برسالة.
    /// </para>
    /// </summary>
    private Result<AgentDispatch> Authorise(AgentToolCall call, AgentCaller caller, AgentTurnState state)
    {
        try
        {
            return AgentToolGate.Authorise(call, caller, state, _catalogue, _handles);
        }
        catch (Exception fault) when (fault is ArgumentException or FormatException
            or InvalidOperationException or JsonException or NotSupportedException)
        {
            return Result<AgentDispatch>.Failure(AgentErrors.ToolArgumentsNotAnObject(call.Name));
        }
    }

    private static AgentTranscriptEntry Refusal(AgentToolCall call, IReadOnlyList<Error> errors) =>
        new(AgentWireRole.User,
            AgentWireBlockKind.ToolResult,
            AgentDispatchResults.RefusalTextAr(errors),
            ToolUseId: call.Id,
            ToolName: call.Name,
            IsError: true);

    private static AgentTranscriptEntry Refusal(AgentDispatch dispatch, IReadOnlyList<Error> errors) =>
        new(AgentWireRole.User,
            AgentWireBlockKind.ToolResult,
            AgentDispatchResults.RefusalTextAr(errors),
            ToolUseId: dispatch.CallId,
            ToolName: dispatch.Tool.Name,
            IsError: true);

    /// <summary>عددٌ للرسائل — بثقافةٍ ثابتة، فلا تُكتب أرقامٌ عربية-هندية في نصّ بروتوكول.</summary>
    internal static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
