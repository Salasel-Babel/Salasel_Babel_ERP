using System.Globalization;
using Babel.Ai.Agent;
using Babel.Ai.Workspace;
using Babel.Api.Errors;
using Babel.Api.Hosting;
using Babel.Api.Security;
using Babel.Api.Wire;
using Babel.Core.CompanySetup;
using Babel.SharedKernel;

namespace Babel.Api.Endpoints;

/// <summary>
/// سطح HTTP فوق مساحة عمل الوكيل.
/// <para>
/// <b>ما يفعله كل معالج هنا، بالترتيب، ولا شيء غيره:</b> يقرأ نطاق الشركة من المسار
/// ويتحقق أن الاعتماد يبلغه · يقرأ الجسم · ينادي <c>AgentWorkspaceService</c> · يترجم
/// النتيجة. <b>ولا قرار واحد يقع في هذا الملفّ</b>: لا اختيار أداة، ولا فكّ مِقبض، ولا
/// حكمٌ على شكل بيانات — تلك كلّها في <c>Babel.Ai</c>، والسطح ينقل.
/// </para>
/// <para>
/// <b>والمفتاح لا يعبر هذا السطح في أي اتجاه.</b> لا في جسم طلب، ولا في جسم جواب، ولا
/// في ترويسة: المتصفّح يكلّم هذا الخادم، وهذا الخادم يكلّم النموذج بمفتاحٍ يقرؤه من
/// متغيّر بيئةٍ يُسمّى بالاسم في الإعدادات ولا تُقرأ قيمتُه في أي نوعٍ يُسلسَل.
/// </para>
/// <para>
/// <b>ولاحظ ما ليس في هذا الملفّ ولا يجوز أن يوجد: نداءُ ترحيلٍ واحد.</b> لا خدمة
/// ترحيل، ولا مقطعَ بابِ ترحيلٍ في أي مسار، ولا اسم عملية ترحيلٍ واحدة من العقد
/// المنشور. و<c>TheAgentSurfaceEndsAtTheDraft</c> يقرأ هذا الملفّ نفسه ويفرض ذلك —
/// <b>ويقرؤه حرفياً، فلا يُستثنى منه تعليقٌ يذكر ما يمنعه</b>: مقطعُ المنع مكتوبٌ
/// في موضعٍ واحد يعرّفه (<c>AgentDraftConfirmationGate</c>)، وكلُّ ما عداه يُمسح.
/// </para>
/// </summary>
internal static class AgentEndpoints
{
    /// <summary>أقصى طول لرسالة المستخدم في الطلب الواحد.</summary>
    private const int MaximumUtteranceLength = 4000;

    /// <summary>أقصى طول لرمزٍ معتِم يصل في جسم — مِقبضاً كان أو معرّف ورقة.</summary>
    private const int MaximumTokenLength = 512;

    /// <summary>يسجّل سطح مساحة العمل.</summary>
    /// <param name="app">مُنشئ المسارات.</param>
    public static void MapAgentApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(AgentRoutes.Sessions, OpenAsync);
        app.MapGet(AgentRoutes.Session, ReadAsync);
        app.MapPost(AgentRoutes.Messages, SendAsync);
        app.MapGet(AgentRoutes.Events, EventsAsync);
        app.MapPost(AgentRoutes.StepConfirmation, ConfirmAsync);
        app.MapPost(AgentRoutes.Answers, AnswerAsync);
        app.MapGet(AgentRoutes.Spend, SpendAsync);
    }

    // ── الجلسة ───────────────────────────────────────────────────────────────

    private static async Task<IResult> OpenAsync(
        HttpContext context,
        CompanySetupService setups,
        CancellationToken cancellationToken)
    {
        if (!Composed(context, out AgentWorkspaceService? workspace, out IResult? off))
        {
            return off!;
        }

        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        ApiPrincipal principal = RequestPrincipal.Of(context);

        // ‏**اسم الشركة يُقرأ من التأسيس لا من الطلب.** وهو يذهب رسالةَ نظامٍ في وسط
        // الرسائل — لا في نصّ النظام العلوي — كي تبقى بادئة الذاكرة واحدةً لكل منشأة.
        Result<FoundedCompany> founded = await setups
            .GetAsync(new TenantId(companyId), principal.User, cancellationToken)
            .ConfigureAwait(false);

        if (founded.IsFailure)
        {
            return HttpProblemResults.Domain(context, founded.Errors);
        }

        AgentWorkspaceSession session = workspace!.Open(
            principal.Tenant, companyId, principal.User, founded.Value.NameAr);

        context.Response.Headers.Location = AgentRoutes.Session
            .Replace("{companyId}", Text(companyId), StringComparison.Ordinal)
            .Replace("{agentSessionId}", Text(session.SessionId), StringComparison.Ordinal);

        return Results.Json(Dto(session), ApiJson.Options, statusCode: StatusCodes.Status201Created);
    }

    private static IResult ReadAsync(HttpContext context)
    {
        if (!Composed(context, out AgentWorkspaceService? workspace, out IResult? off))
        {
            return off!;
        }

        (AgentWorkspaceSession? session, IResult? refused) = Session(context, workspace!);
        return session is null ? refused! : Results.Json(Dto(session), ApiJson.Options);
    }

    // ── الرسالة والأحداث ─────────────────────────────────────────────────────

    private static async Task<IResult> SendAsync(
        HttpContext context,
        AgentToolCatalogue catalogue,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!Composed(context, out AgentWorkspaceService? workspace, out IResult? off))
        {
            return off!;
        }

        (AgentWorkspaceSession? session, IResult? refused) = Session(context, workspace!);
        if (session is null)
        {
            return refused!;
        }

        (AgentMessageRequestDto? dto, IResult? malformed) =
            await BodyAsync<AgentMessageRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return malformed!;
        }

        if (string.IsNullOrWhiteSpace(dto.Text) || dto.Text.Length > MaximumUtteranceLength)
        {
            return HttpProblemResults.Code(
                context,
                "agent.message_text_invalid",
                "نصّ الرسالة فارغ أو أطول من الحدّ المنشور.",
                "The message text is empty or longer than the published limit.",
                "text",
                StatusCodes.Status400BadRequest);
        }

        // ── هويّة الإنسان تُحفظ **قبل** أن يبدأ الدور ────────────────────────────
        //
        // والدور يمضي خلف الطلب، فلا اعتماد ولا سياق في اللحظة التي تُنشأ فيها
        // المسوّدة. <b>والمسوّدة تُنسب إلى إنسان</b> — إلى صاحب هذه الجلسة بهويّته
        // المحلولة من اعتماده الآن، لا إلى وكيل ولا إلى فاعل نظام.
        //
        // ‏**ولا تُبنى هذه الهوية من معرّفين**: الهوية المحلولة تحمل «قراءةٌ فقط في هذه
        // المنشأة»، وبناؤها من جديد كان سيُسقط ذلك فيصير مسار الوكيل أوسع من الباب الذي
        // يفتحه المتصفّح للإنسان نفسه.
        context.RequestServices.GetService<Babel.Api.Agent.AgentSessionHumans>()
            ?.Hold(session.SessionId, RequestPrincipal.Of(context));

        Result<Guid> begun = workspace!.Send(
            session,
            dto.Text,
            PermittedOperations(catalogue),
            clock.GetUtcNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        return begun.IsFailure
            ? HttpProblemResults.Domain(context, begun.Errors)
            : Results.Json(
                new AgentTurnDto(Text(begun.Value), (int)session.LastSequence),
                ApiJson.Options,
                statusCode: StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> EventsAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (!Composed(context, out AgentWorkspaceService? workspace, out IResult? off))
        {
            return off!;
        }

        (AgentWorkspaceSession? session, IResult? refused) = Session(context, workspace!);
        if (session is null)
        {
            return refused!;
        }

        string raw = context.Request.Query["after"].ToString();

        if (raw.Length > 0 && (!long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed)
            || parsed < 0))
        {
            return HttpProblemResults.Code(
                context,
                "agent.after_cursor_invalid",
                "المؤشّر «after» ليس عدداً صحيحاً غير سالب.",
                "The 'after' cursor is not a non-negative integer.",
                "after",
                StatusCodes.Status400BadRequest);
        }

        long after = raw.Length == 0 ? 0 : long.Parse(raw, NumberStyles.None, CultureInfo.InvariantCulture);

        IReadOnlyList<AgentWorkspaceEvent> found = await workspace!
            .ReadAsync(session, after, cancellationToken)
            .ConfigureAwait(false);

        return Results.Json(
            new AgentTurnEventPageDto(
                [.. found.Select(Dto)],
                (int)(found.Count == 0 ? after : found[^1].Sequence),
                Phase(session.Phase)),
            ApiJson.Options);
    }

    // ── التأكيد والجواب ──────────────────────────────────────────────────────

    private static async Task<IResult> ConfirmAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (!Composed(context, out AgentWorkspaceService? workspace, out IResult? off))
        {
            return off!;
        }

        (AgentWorkspaceSession? session, IResult? refused) = Session(context, workspace!);
        if (session is null)
        {
            return refused!;
        }

        string raw = context.Request.RouteValues.TryGetValue("stepId", out object? value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;

        if (!Guid.TryParseExact(raw, "D", out Guid stepId) || stepId == Guid.Empty)
        {
            return HttpProblemResults.Code(
                context,
                "agent.step_id_malformed",
                "معرّف الخطوة في المسار ليس معرّفاً صالحاً بصيغة 8-4-4-4-12.",
                "The step identifier in the path is not a valid 8-4-4-4-12 identifier.",
                "stepId",
                StatusCodes.Status400BadRequest);
        }

        (AgentStepConfirmationRequestDto? dto, IResult? malformed) =
            await BodyAsync<AgentStepConfirmationRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return malformed!;
        }

        if (dto.Accepted is null)
        {
            return HttpProblemResults.Code(
                context,
                "agent.confirmation_verdict_missing",
                "حقل «accepted» غائب — ولا يُفترَض القبول عند غيابه. والتأكيد فعلٌ يُقال لا يُستنتَج.",
                "The 'accepted' field is missing and acceptance is never assumed in its absence.",
                "accepted",
                StatusCodes.Status400BadRequest);
        }

        Result settled = AgentWorkspaceService.Confirm(session, stepId, dto.Accepted.Value);

        return settled.IsFailure
            ? HttpProblemResults.Domain(context, settled.Errors)
            : Results.Json(Dto(session), ApiJson.Options);
    }

    private static async Task<IResult> AnswerAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (!Composed(context, out AgentWorkspaceService? workspace, out IResult? off))
        {
            return off!;
        }

        (AgentWorkspaceSession? session, IResult? refused) = Session(context, workspace!);
        if (session is null)
        {
            return refused!;
        }

        (AgentAnswerRequestDto? dto, IResult? malformed) =
            await BodyAsync<AgentAnswerRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return malformed!;
        }

        if (string.IsNullOrEmpty(dto.QuestionId) || dto.QuestionId.Length > MaximumTokenLength
            || string.IsNullOrEmpty(dto.OptionToken) || dto.OptionToken.Length > MaximumTokenLength)
        {
            return HttpProblemResults.Code(
                context,
                "agent.answer_malformed",
                "جواب الورقة يحتاج «questionId» و«optionToken» كليهما، ولا ثالث لهما.",
                "A sheet answer needs both 'questionId' and 'optionToken', and nothing else.",
                "optionToken",
                StatusCodes.Status400BadRequest);
        }

        Result settled = AgentWorkspaceService.Answer(session, dto.QuestionId, dto.OptionToken);

        return settled.IsFailure
            ? HttpProblemResults.Domain(context, settled.Errors)
            : Results.Json(Dto(session), ApiJson.Options);
    }

    // ── الإنفاق ──────────────────────────────────────────────────────────────

    private static async Task<IResult> SpendAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (!Composed(context, out AgentWorkspaceService? workspace, out IResult? off))
        {
            return off!;
        }

        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        AgentWorkspaceSpend spend = await workspace!
            .SpendAsync(RequestPrincipal.Of(context).Tenant, cancellationToken)
            .ConfigureAwait(false);

        return Results.Json(
            new AgentSpendDto(
                spend.Billable.ToString(CultureInfo.InvariantCulture),
                spend.Ceiling?.ToString(CultureInfo.InvariantCulture),
                spend.Turns,
                (int)spend.WindowSeconds,
                spend.BringsItsOwnKey),
            ApiJson.Options);
    }

    // ── ترجمةٌ لا قرار ───────────────────────────────────────────────────────

    /// <summary>
    /// <b>ما يبلغه هذا المتكلّم من العمليات.</b>
    /// <para>
    /// <b>وهو مُعلَن ضيّقاً لا واسعاً:</b> عمليات المسوّدات في الكتالوج المغلق وحدها —
    /// وهي وحدها ما فيه أصلاً. <b>والاستحقاق لكل وحدة يبقى مفروضاً عند الوحدة المالكة</b>
    /// بـ<c>[RequiresEntitlement]</c>، والقاعدة 6 تفرض ذلك على IL. فمساحةُ الوكيل ليست
    /// سلطة الاستحقاق ولا تدّعيها؛ وفحصٌ ثانٍ هنا كان يصير آليةَ تصريحٍ موازية تُصان
    /// إحداهما وتُنسى الأخرى — وهي حجّة <c>DocumentEndpoints</c> نفسها بنصّها.
    /// </para>
    /// </summary>
    private static HashSet<string> PermittedOperations(AgentToolCatalogue catalogue) =>
        catalogue.Tools
            .Where(static tool => tool.IsDraftOperation)
            .Select(static tool => tool.OperationId!)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// <b>هل رُكِّبت مساحة الوكيل على هذا الخادم؟</b> — والجواب <c>503</c> برمزٍ يسمّي
    /// السبب، لا <c>500</c> ولا صمت.
    /// <para>
    /// <b>ولماذا تُقرأ من الحاوية لا تُحقن في المعالج:</b> الوكيل تركيبٌ اختياري —
    /// يحتاج مفتاح نموذجٍ ومفتاح توقيع مقابض، ولا يملكهما كل ناشر. ومعالجٌ يحقنها حقناً
    /// إلزامياً كان سيردّ <c>500</c> على كل خادمٍ لم يركّبها، وهو رمزٌ يُقرأ «الخادم
    /// معطوب» بينما الحقيقة «هذه الميزة غير مُفعَّلة هنا». واللوحة تعرض ذلك حالاً من
    /// حالاتها بدل أن تبقى تدور.
    /// </para>
    /// </summary>
    private static bool Composed(
        HttpContext context,
        out AgentWorkspaceService? workspace,
        out IResult? unavailable)
    {
        workspace = context.RequestServices.GetService<AgentWorkspaceService>();

        unavailable = workspace is not null
            ? null
            : HttpProblemResults.Code(
                context,
                AgentWorkspaceErrors.AgentDisabled.Code,
                AgentWorkspaceErrors.AgentDisabled.MessageAr,
                AgentWorkspaceErrors.AgentDisabled.MessageEn,
                status: StatusCodes.Status503ServiceUnavailable);

        return workspace is not null;
    }

    private static (AgentWorkspaceSession? Session, IResult? Refused) Session(
        HttpContext context,
        AgentWorkspaceService workspace)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return (null, denied!);
        }

        string raw = context.Request.RouteValues.TryGetValue("agentSessionId", out object? value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;

        if (!Guid.TryParseExact(raw, "D", out Guid sessionId) || sessionId == Guid.Empty)
        {
            return (null, HttpProblemResults.Code(
                context,
                "agent.session_id_malformed",
                "معرّف جلسة الوكيل في المسار ليس معرّفاً صالحاً بصيغة 8-4-4-4-12.",
                "The agent session identifier in the path is not a valid 8-4-4-4-12 identifier.",
                "agentSessionId",
                StatusCodes.Status400BadRequest));
        }

        ApiPrincipal principal = RequestPrincipal.Of(context);

        Result<AgentWorkspaceSession> found = workspace.Find(
            sessionId, principal.Tenant, companyId, principal.User);

        return found.IsFailure
            ? (null, HttpProblemResults.Domain(context, found.Errors))
            : (found.Value, null);
    }

    private static AgentSessionDto Dto(AgentWorkspaceSession session) => new(
        Text(session.SessionId),
        Phase(session.Phase),
        session.CurrentTurnId == Guid.Empty ? null : Text(session.CurrentTurnId),
        (int)session.LastSequence,
        [.. session.Steps.Select(Dto)],
        session.PendingConfirmation is { } waiting
            ? new AgentConfirmationDto(
                Text(waiting.StepId),
                waiting.ToolName,
                waiting.ScreenRoute,
                [.. waiting.Fields.Select(static field =>
                    new AgentDraftFieldDto(field.Path, field.Value, field.Masked))])
            : null,
        session.PendingQuestion is { } asked
            ? new AgentQuestionSheetDto(
                asked.QuestionId,
                asked.RegisterKey,
                asked.SubjectText,
                [.. asked.Options.Select(static option =>
                    new AgentQuestionOptionDto(option.OptionToken, option.LabelAr, option.SubtitleAr))],
                asked.AllowsCreate)
            : null);

    private static AgentPlanStepDto Dto(AgentWorkspaceStep step) => new(
        Text(step.StepId),
        step.Order,
        step.TitleAr,
        State(step.State),
        step.ToolName,
        step.ScreenRoute,
        [.. step.Errors.Select(Dto)]);

    private static AgentTurnEventDto Dto(AgentWorkspaceEvent moment) => new(
        (int)moment.Sequence,
        Text(moment.TurnId),
        Kind(moment.Kind),
        moment.Text,
        moment.ToolName,
        moment.QuestionId,
        moment.RegisterKey,
        moment.ScreenRoute,
        moment.StepId is { } step ? Text(step) : null,
        moment.Steps,
        [.. moment.Errors.Select(Dto)]);

    private static ApiErrorDto Dto(Error error) => new(error.Code, error.MessageAr, error.MessageEn);

    private static string Text(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);

    /// <summary>
    /// أسماء الأطوار على السلك. <b>ولا اسم اسمه <c>posted</c></b> — الوكيل لا يبلغه.
    /// </summary>
    private static string Phase(AgentTurnPhase phase) => phase switch
    {
        AgentTurnPhase.Running => "running",
        AgentTurnPhase.AwaitingHuman => "awaitingHuman",
        AgentTurnPhase.Refused => "refused",
        _ => "completed",
    };

    private static string State(AgentStepState state) => state switch
    {
        AgentStepState.Planned => "planned",
        AgentStepState.Running => "running",
        AgentStepState.AwaitingConfirmation => "awaitingConfirmation",
        AgentStepState.AwaitingAnswer => "awaitingAnswer",
        AgentStepState.Landed => "landed",
        _ => "refused",
    };

    private static string Kind(AgentTurnEventKind kind) => kind switch
    {
        AgentTurnEventKind.Thinking => "thinking",
        AgentTurnEventKind.Text => "text",
        AgentTurnEventKind.ToolStarted => "toolStarted",
        AgentTurnEventKind.ToolRefused => "toolRefused",
        AgentTurnEventKind.QuestionRaised => "questionRaised",
        AgentTurnEventKind.PlanProposed => "planProposed",
        AgentTurnEventKind.DraftLanded => "draftLanded",
        AgentTurnEventKind.Refused => "refused",
        _ => "completed",
    };

    private static async Task<(T? Dto, IResult? Refused)> BodyAsync<T>(
        HttpContext context,
        CancellationToken cancellationToken)
        where T : class
    {
        T? dto;
        try
        {
            dto = await context.Request
                .ReadFromJsonAsync<T>(ApiJson.Options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException exception)
        {
            return (null, Scope.BadJson(context, exception));
        }

        return dto is null
            ? (null, HttpProblemResults.Code(
                context, "wire.body.missing", "جسم الطلب مفقود.", "The request body is missing."))
            : (dto, null);
    }
}
