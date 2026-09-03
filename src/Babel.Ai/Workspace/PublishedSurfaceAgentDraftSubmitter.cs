using System.Globalization;
using System.Text.Json;
using Babel.Ai.Agent;
using Babel.Core.Audit;
using Babel.SharedKernel;

namespace Babel.Ai.Workspace;

/// <summary>
/// <b>منفّذُ المسوّدات الحقيقي — يُنشئ المسوّدة بالباب المنشور نفسه الذي يفتحه المتصفّح.</b>
/// <para>
/// <b>ولا مسارَ جانبي:</b> لا يعرف هذا النوع خدمةَ وحدةٍ واحدة، ولا يستطيع أن يعرفها —
/// القاعدة 3 تمنع <c>Babel.Ai</c> من الإشارة إلى وحدةٍ أفقية. فما يملكه هو
/// <see cref="IAgentPublishedSurface"/>: فعلٌ ومسارٌ وجسم، تُنفَّذ على جدول المسارات
/// المنشور. وأي تحقّقٍ في الباب — شكلُ الجسم، ونطاقُ الشركة، ودورُ العضوية، والاستحقاق،
/// وقواعدُ الوحدة — يقع كما يقع للمتصفّح حرفاً بحرف، <b>لأنه الباب نفسه</b>.
/// </para>
/// <para>
/// <b>وعلى هويّة مَن؟</b> على <b>إنسان الجلسة</b>. الوكيل ليس فاعلاً في السجلّ ولا
/// يملك اعتماداً: <c>AgentCaller.User</c> هو مستخدم الجلسة كما فُتحت من اعتماده، وهو
/// الذي يقرؤه الباب <c>Actor</c>. <b>ويبقى الأثر أنّها نشأت باقتراح وكيل</b> في سجلّ
/// التدقيق بفعلٍ اسمه <c>ai.agent.draft_created</c> — فالنسبة إلى الإنسان لا تُخفي
/// أنّ الوكيل هو من اقترح.
/// </para>
/// <para>
/// <b>وسقوطُ الإنشاء يُسمّى ولا يُعاد صامتاً:</b> رفضُ الخادم يُقرأ من
/// <c>application/problem+json</c> بأسمائه ورموزه ويعود <see cref="Result"/> ساقطاً،
/// فتُغلق الخطوة في اللوح <c>refused</c> ومعها السبب بالعربية. <b>ولا إعادةَ محاولةٍ
/// واحدة</b>: نداءٌ يُعاد على بابٍ يُنشئ مستنداً هو الطريق إلى مستندين.
/// </para>
/// <para>
/// <b>وحتى بعد كل ذلك: مسوّدة.</b> هذا النوع لا يملك ما يُرحّل به — لا يقبل إلا ما
/// أذنت به <see cref="AgentDraftConfirmationGate.Refuse"/>، ويعيد
/// <see cref="AgentDraftLanding"/> بمسار شاشةٍ ولا شيء غيره.
/// </para>
/// </summary>
public sealed class PublishedSurfaceAgentDraftSubmitter : IAgentDraftSubmitter
{
    /// <summary>رمز فعل التدقيق حين تهبط مسوّدةٌ باقتراح وكيل.</summary>
    public const string AuditAction = "ai.agent.draft_created";

    private readonly IAgentPublishedSurface _surface;
    private readonly IAuditLog? _audit;
    private readonly TimeProvider _clock;

    /// <summary>يركّب المنفّذ.</summary>
    /// <param name="surface">السطح المنشور.</param>
    /// <param name="audit">سجلّ التدقيق، أو <c>null</c> على تركيبٍ بلا سجلّ.</param>
    /// <param name="clock">مصدر الوقت.</param>
    public PublishedSurfaceAgentDraftSubmitter(
        IAgentPublishedSurface surface,
        IAuditLog? audit,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(clock);

        _surface = surface;
        _audit = audit;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Result<AgentDraftLanding>> SubmitAsync(
        AgentDispatch dispatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dispatch);

        // ── ١ · الحارس البنيوي مرّةً أخرى، هنا ────────────────────────────────
        // ‏**وتكرارُه مقصود لا سهو**: هذا النوع هو ما يملك النداء فعلاً، ومن ركّبه غداً
        // بلا الباب الملفوف يجد الرفض نفسه. وحارسٌ يعتمد على مستدعٍ واحد ليس حارساً.
        Error? refusal = AgentDraftConfirmationGate.Refuse(dispatch.Tool.OperationId, dispatch.Tool.Path);
        if (refusal is not null)
        {
            return Result<AgentDraftLanding>.Failure(refusal);
        }

        string operationId = dispatch.Tool.OperationId!;

        string? screen = AgentDraftScreens.RouteFor(operationId);
        if (screen is null)
        {
            return Result<AgentDraftLanding>.Failure(
                AgentWorkspaceErrors.DraftHasNoScreenToLandOn(operationId));
        }

        Result<string> address = Address(operationId, dispatch.Tool.Path!, dispatch.Caller.CompanyId);
        if (address.IsFailure)
        {
            return Result<AgentDraftLanding>.Failure(address.Errors);
        }

        // ── ٢ · النداء — مرّةً واحدة، ولا إعادة ───────────────────────────────
        Result<AgentSurfaceAnswer> called;
        try
        {
            called = await _surface
                .CallAsync(
                    new AgentSurfaceCall(
                        operationId,
                        dispatch.Tool.Method ?? "POST",
                        dispatch.Tool.Path!,
                        address.Value,
                        dispatch.Body,
                        dispatch.Caller),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            return Result<AgentDraftLanding>.Failure(
                AgentWorkspaceErrors.DraftCallBroke(operationId, fault.GetType().Name));
        }

        if (called.IsFailure)
        {
            return Result<AgentDraftLanding>.Failure(called.Errors);
        }

        AgentSurfaceAnswer answer = called.Value;

        if (answer.Status is < 200 or > 299)
        {
            return Result<AgentDraftLanding>.Failure(Refusals(operationId, answer));
        }

        // ── ٣ · الأثر: نُسبت إلى إنسان، ونشأت باقتراح وكيل ────────────────────
        if (_audit is not null)
        {
            await _audit
                .RecordAsync(
                    new AuditEntry(
                        dispatch.Caller.Tenant,
                        dispatch.Caller.User,
                        _clock.GetUtcNow(),
                        AuditAction,
                        operationId,
                        Trace(dispatch)),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<AgentDraftLanding>.Success(new AgentDraftLanding(screen));
    }

    /// <summary>
    /// يملأ وسائط المسار. <b>و<c>companyId</c> وحده يُملأ</b> — من نطاق المتكلّم لا من
    /// جسمٍ كتبه نموذج. وأي وسيطٍ آخر يبقى فارغاً فيُرفض النداء باسمه: عمليةٌ تحت أبٍ
    /// (‏<c>{propertyId}</c> وأخواته) تحتاج اختياراً لم يقع، والوكيل لا يخترع معرّفاً.
    /// </summary>
    /// <param name="operationId">معرّف العملية.</param>
    /// <param name="template">قالب المسار المنشور.</param>
    /// <param name="companyId">الشركة المفتوحة.</param>
    internal static Result<string> Address(string operationId, string template, Guid companyId)
    {
        ArgumentNullException.ThrowIfNull(template);

        string filled = template.Replace(
            "{companyId}",
            companyId.ToString("D", CultureInfo.InvariantCulture),
            StringComparison.Ordinal);

        int opened = filled.IndexOf('{', StringComparison.Ordinal);
        if (opened < 0)
        {
            return Result<string>.Success(filled);
        }

        int closed = filled.IndexOf('}', opened);
        string name = closed > opened ? filled[(opened + 1)..closed] : filled[(opened + 1)..];

        return Result<string>.Failure(
            AgentWorkspaceErrors.DraftPathParameterIsUnfilled(operationId, template, name));
    }

    /// <summary>
    /// يقرأ رفضَ الباب بأسمائه. <b>ولا رسالةَ عامّة تحلّ محلّ سببٍ مُسمّى</b>: «تعذّر
    /// إنشاء المسوّدة» تجعل الإنسان يعيد المحاولة على عطلٍ لن يزول بالإعادة، بينما
    /// «رقم التسجيل الضريبي ناقص» يجعله يصلحه.
    /// </summary>
    /// <param name="operationId">معرّف العملية.</param>
    /// <param name="answer">جواب الباب.</param>
    internal static IReadOnlyList<Error> Refusals(string operationId, AgentSurfaceAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        List<Error> found = [];

        try
        {
            using JsonDocument document = JsonDocument.Parse(answer.Body);

            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("errors", out JsonElement errors)
                && errors.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement entry in errors.EnumerateArray())
                {
                    Error? read = Read(entry);
                    if (read is not null)
                    {
                        found.Add(read);
                    }
                }
            }

            if (found.Count == 0 && document.RootElement.ValueKind == JsonValueKind.Object)
            {
                Error? single = Read(document.RootElement);
                if (single is not null)
                {
                    found.Add(single);
                }
            }
        }
        catch (JsonException)
        {
            // جسمٌ لا يُقرأ ليس سبباً — ويُقال إنه لا يُقرأ، ولا يُخترع له معنى.
        }

        return found.Count > 0
            ? found
            : [AgentWorkspaceErrors.DraftRefusalIsUnreadable(operationId, answer.Status)];
    }

    private static Error? Read(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object
            || !entry.TryGetProperty("code", out JsonElement code)
            || code.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string ar = entry.TryGetProperty("messageAr", out JsonElement arabic)
            && arabic.ValueKind == JsonValueKind.String
                ? arabic.GetString()!
                : entry.TryGetProperty("detailAr", out JsonElement detailAr)
                    && detailAr.ValueKind == JsonValueKind.String
                    ? detailAr.GetString()!
                    : string.Empty;

        string en = entry.TryGetProperty("messageEn", out JsonElement english)
            && english.ValueKind == JsonValueKind.String
                ? english.GetString()!
                : entry.TryGetProperty("detail", out JsonElement detail)
                    && detail.ValueKind == JsonValueKind.String
                    ? detail.GetString()!
                    : string.Empty;

        if (ar.Length == 0 && en.Length == 0)
        {
            return null;
        }

        // ‏**ونصفٌ واحد لا يُسقط السبب**: بابٌ يردّ بالعربية وحدها — أو بالإنجليزية
        // وحدها — يبقى سبباً مُسمّى يُقرأ على اللوح، لا رفضاً «لا يُقرأ».
        return new Error(code.GetString()!, ar.Length == 0 ? en : ar, en.Length == 0 ? ar : en);
    }

    /// <summary>
    /// سطرُ الأثر. <b>ولا معرّف صفٍّ فيه ولا قيمةَ حقل</b>: هو يقول «مِن أين جاءت»، لا
    /// «ما فيها» — وما فيها في المستند نفسه.
    /// </summary>
    /// <param name="dispatch">الأمر.</param>
    private static string Trace(AgentDispatch dispatch) =>
        "نشأت باقتراح وكيل · جلسة "
        + dispatch.Caller.SessionId.ToString("D", CultureInfo.InvariantCulture)
        + " · هويّة المسوّدة " + AgentDraftIdentity.Of(dispatch)
        + " / created on an agent's suggestion; the draft was accepted in shape by the human it is attributed to, "
        + "and posting remains a manual act on its screen.";
}
