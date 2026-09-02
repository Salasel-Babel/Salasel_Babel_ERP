using System.Text;
using Babel.Ai.Workspace;
using Babel.Api.Security;
using Babel.SharedKernel;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing.Template;

namespace Babel.Api.Agent;

/// <summary>
/// <b>تنفيذُ منفذ السطح المنشور: الباب نفسه، لا نسخةٌ منه.</b>
/// <para>
/// وما يفعله هذا النوع أن يجد في <b>جدول مسارات هذا الخادم</b> الطرفَ الذي سجّله
/// <c>MapDocumentApi</c> وأخواتُه — بقالب مساره وفعله — ثمّ ينفّذ
/// <c>RequestDelegate</c> الخاصّ به على سياقٍ يحمل هوية إنسان الجلسة. <b>فالمعالج
/// والتحقّقات والترجمة والرفض كلُّها هي هي</b>: النطاق يُقرأ من المسار، ودورُ العضوية
/// يُفحص في <c>Scope</c>، والاستحقاق يُفرض عند الوحدة المالكة، والجسم يُقرأ بالعقد.
/// </para>
/// <para>
/// <b>ولماذا لا HTTP على مقبس:</b> نداءٌ على الشبكة يحتاج عنواناً ومنفذاً — أي مضيفاً
/// يُكتب في ملفّ أو يُخمَّن — ويحتاج اعتماداً يُصدَّر ويُحمل، فيصير في العملية طريقٌ
/// ثانٍ يحمل رمزاً. <b>ولا رمز هنا ولا مضيف</b>: الهوية تُثبَّت على السياق مباشرةً،
/// وهي الهوية التي حلّها وسيطُ المصادقة من اعتماد الإنسان في الطلب الذي بدأ الدور.
/// </para>
/// <para>
/// <b>وما لا يفعله:</b> لا يسمّي عمليةً ولا مورداً ولا خدمةَ وحدة. ما يُنادى يأتي كلُّه
/// من <c>AgentDispatch</c> — أي من الكتالوج المُولَّد من العقد المنشور — وقد اجتاز
/// <c>AgentDraftConfirmationGate.Refuse</c> مرّتين قبل أن يصل. و<c>TheAgentSurfaceEndsAtTheDraft</c>
/// يقرأ هذا الملفّ ويفرض ألّا يحمل اسم عملية ترحيلٍ واحدة ولا مقطعَ بابِ الترحيل —
/// <b>ولا يستثني تعليقاً يذكره</b>، فالمقطع مكتوبٌ في موضعٍ واحد يعرّفه وحده.
/// </para>
/// </summary>
internal sealed class PublishedEndpointAgentSurface : IAgentPublishedSurface
{
    private readonly IServiceProvider _root;
    private readonly AgentSessionHumans _humans;
    private IReadOnlyList<EndpointDataSource> _sources = [];

    /// <summary>يركّب السطح.</summary>
    /// <param name="root">جذر الخدمات — يُفتح منه نطاقٌ لكل نداء.</param>
    /// <param name="humans">هويّات أصحاب الجلسات.</param>
    public PublishedEndpointAgentSurface(IServiceProvider root, AgentSessionHumans humans)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(humans);

        _root = root;
        _humans = humans;
    }

    /// <summary>
    /// <b>يُسلَّم جدولَ المسارات بعد أن تُسجَّل كلُّها</b> — لا يقرؤه من الحاوية.
    /// <para>
    /// والفرق ليس ذوقاً: جدولُ المسارات يكتمل <b>بعد</b> بناء الحاوية، وقراءةٌ منها
    /// كانت ستربط الصحّة بترتيب تسجيلٍ لا يقوله شيء. والتسليم الصريح يجعل الجذر
    /// التركيبي — وهو الذي يعرف اللحظة — هو من يقولها.
    /// </para>
    /// </summary>
    /// <param name="routes">بانِي المسارات بعد تسجيلها كلّها.</param>
    public void Attach(IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        _sources = [.. routes.DataSources];
    }

    /// <inheritdoc />
    public async Task<Result<AgentSurfaceAnswer>> CallAsync(
        AgentSurfaceCall call,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);

        // ── ١ · الإنسان أوّلاً — ولا مسوّدة بلا فاعلٍ يُنسب إليه ────────────────
        ApiPrincipal? human = _humans.Of(call.Caller.SessionId);
        if (human is null)
        {
            return Result<AgentSurfaceAnswer>.Failure(AgentWorkspaceErrors.DraftHasNoHumanToAttributeTo);
        }

        // ── ٢ · البابُ من جدول المسارات نفسه ─────────────────────────────────
        RouteEndpoint? door = Door(call.Template, call.Method);
        if (door?.RequestDelegate is null)
        {
            return Result<AgentSurfaceAnswer>.Failure(
                AgentWorkspaceErrors.DraftDoorIsNotOnThisServer(call.OperationId, call.Template));
        }

        // ── ٣ · سياقٌ كسياق المتصفّح، بهويّة إنسان الجلسة ─────────────────────
        using IServiceScope scope = _root.CreateScope();

        byte[] body = Encoding.UTF8.GetBytes(call.Body);
        using MemoryStream sent = new(body, writable: false);
        using MemoryStream received = new();

        DefaultHttpContext context = new() { RequestServices = scope.ServiceProvider };

        context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(received));
        context.RequestAborted = cancellationToken;

        context.Request.Method = call.Method.ToUpperInvariant();
        context.Request.Path = new PathString(call.Path);
        context.Request.ContentType = "application/json; charset=utf-8";
        context.Request.ContentLength = body.LongLength;
        context.Request.Body = sent;

        // وسائط المسار تُستخرج بمُطابِق القوالب نفسه الذي يستعمله التوجيه، لا بتقطيع نصّ.
        RouteValueDictionary values = [];
        new TemplateMatcher(TemplateParser.Parse(door.RoutePattern.RawText!), [])
            .TryMatch(context.Request.Path, values);

        foreach (KeyValuePair<string, object?> value in values)
        {
            context.Request.RouteValues[value.Key] = value.Value;
        }

        context.SetEndpoint(door);
        RequestPrincipal.Bind(context, human);

        await door.RequestDelegate(context).ConfigureAwait(false);
        await received.FlushAsync(cancellationToken).ConfigureAwait(false);

        return Result<AgentSurfaceAnswer>.Success(new AgentSurfaceAnswer(
            context.Response.StatusCode,
            Encoding.UTF8.GetString(received.ToArray())));
    }

    /// <summary>
    /// يجد الطرف المسجَّل بهذا القالب وهذا الفعل. <b>ومطابقةٌ حرفية على القالب</b>:
    /// قالبٌ يختلف عن العقد بحرف يعني أن العقد والخادم افترقا، وذلك رفضٌ يُقال لا
    /// بابٌ «قريب» يُخمَّن.
    /// </summary>
    /// <param name="template">قالب المسار كما ينشره العقد.</param>
    /// <param name="method">الفعل.</param>
    private RouteEndpoint? Door(string template, string method)
    {
        foreach (EndpointDataSource source in _sources)
        {
            foreach (Endpoint endpoint in source.Endpoints)
            {
                if (endpoint is not RouteEndpoint route
                    || !string.Equals(route.RoutePattern.RawText, template, StringComparison.Ordinal))
                {
                    continue;
                }

                HttpMethodMetadata? verbs = endpoint.Metadata.GetMetadata<HttpMethodMetadata>();

                if (verbs is not null && verbs.HttpMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
                {
                    return route;
                }
            }
        }

        return null;
    }
}
