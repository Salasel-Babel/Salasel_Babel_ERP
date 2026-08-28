using Babel.Api.Errors;

namespace Babel.Api.Security;

/// <summary>
/// وسيط المصادقة: يحلّ الاعتماد إلى هوية ويثبّتها على الطلب، أو يُغلق الباب.
/// <para>
/// <b>ولا يوجد مسار «ضيف».</b> كل شيء تحت <c>/api/</c> يحتاج اعتماداً؛ ونقطة الصحّة
/// وحدها خارج ذلك — ولا تقرأ بيانات مستأجر ولا تكتبها.
/// </para>
/// </summary>
internal static class RequestPrincipal
{
    private const string ItemKey = "babel.principal";

    /// <summary>مخطّط الاعتماد المقبول الوحيد.</summary>
    private const string Scheme = "Bearer ";

    /// <summary>هوية هذا الطلب. قراءتها قبل نجاح الوسيط خطأ برمجي لا حالة تشغيل.</summary>
    /// <param name="context">سياق الطلب.</param>
    public static ApiPrincipal Of(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Items.TryGetValue(ItemKey, out object? value) && value is ApiPrincipal principal
            ? principal
            : throw new InvalidOperationException(
                "قراءة هوية الطلب قبل المصادقة. / Reading the request principal before authentication.");
    }

    /// <summary>يضيف وسيط المصادقة إلى خط المعالجة.</summary>
    /// <param name="app">التطبيق.</param>
    public static void UseBabelAuthentication(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Use(static async (context, next) =>
        {
            if (IsAnonymous(context.Request.Path))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            IApiPrincipalResolver resolver = context.RequestServices.GetRequiredService<IApiPrincipalResolver>();

            string header = context.Request.Headers.Authorization.ToString();

            if (header.Length == 0)
            {
                await DenyAsync(
                    context,
                    "auth.credential_missing",
                    "لا اعتماد على هذا الطلب. الهوية تأتي من الاعتماد وحده — لا من ترويسة يكتبها العميل ولا من جسم الطلب.",
                    "The request carries no credential. Identity comes from the credential alone — never from a client-written header or the request body.")
                    .ConfigureAwait(false);
                return;
            }

            if (!header.StartsWith(Scheme, StringComparison.Ordinal))
            {
                await DenyAsync(
                    context,
                    "auth.scheme_unsupported",
                    "مخطّط الاعتماد غير مدعوم. المقبول: Bearer.",
                    "The credential scheme is unsupported. Accepted: Bearer.")
                    .ConfigureAwait(false);
                return;
            }

            ApiPrincipal? principal = resolver.Resolve(header[Scheme.Length..].Trim());

            if (principal is null)
            {
                await DenyAsync(
                    context,
                    "auth.credential_rejected",
                    "الاعتماد غير مقبول.",
                    "The credential was rejected.")
                    .ConfigureAwait(false);
                return;
            }

            // ── الانقضاء يقع **بعد** التحقّق من الاعتماد وقبل أي عمل ────────────────
            // ورمزه مستقلّ عمداً: من انقضت جلسته يحتاج أن يعرف أنه يدخل من جديد، لا أن
            // يظنّ أن اعتماده سُحب. والوقت يأتي من TimeProvider المحقون لا من
            // DateTimeOffset.UtcNow — ساعةٌ مقروءة من ثابتة ساكنة لا يمكن تحريكها في
            // اختبار، فتبقى هذه الحافة غير مبرهنة إلى أن يقع العطل عند عميل.
            DateTimeOffset now = context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();

            if (principal.HasExpiredAt(now))
            {
                await DenyAsync(
                    context,
                    "auth.credential_expired",
                    "انقضى هذا الاعتماد. ادخل من جديد — ولم يُسحب منك شيء ولم يتغيّر شيء في البيانات.",
                    "This credential has expired. Sign in again — nothing was revoked and nothing in the data changed.")
                    .ConfigureAwait(false);
                return;
            }

            context.Items["babel.principal"] = principal;
            context.RequestServices.GetRequiredService<RequestTenantContext>().Bind(principal);

            await next(context).ConfigureAwait(false);
        });
    }

    private static bool IsAnonymous(PathString path) =>
        path.Equals(Endpoints.ApiRoutes.Health, StringComparison.Ordinal);

    private static async Task DenyAsync(HttpContext context, string code, string ar, string en)
    {
        // ‏WWW-Authenticate إلزامية مع 401 بحكم RFC 9110 §11.6.1 — وغيابها يجعل العميل
        // يخلط «لم تُصادِق» بـ«صادقتَ ومُنعت»، وهما إجراءان مختلفان تماماً عنده.
        context.Response.Headers.WWWAuthenticate = "Bearer realm=\"babel\"";
        await HttpProblemResults
            .Code(context, code, ar, en, status: StatusCodes.Status401Unauthorized)
            .ExecuteAsync(context)
            .ConfigureAwait(false);
    }
}
