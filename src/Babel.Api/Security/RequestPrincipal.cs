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

            CredentialVerdict verdict = await resolver
                .ResolveAsync(header[Scheme.Length..].Trim(), context.RequestAborted)
                .ConfigureAwait(false);

            if (verdict.Outcome == CredentialOutcome.Rejected)
            {
                await DenyAsync(
                    context,
                    "auth.credential_rejected",
                    "الاعتماد غير مقبول.",
                    "The credential was rejected.")
                    .ConfigureAwait(false);
                return;
            }

            // ── الإبطال: رمزٌ ثالث مستقلّ عن الرفض وعن الانقضاء ──────────────────────
            // ولماذا يفترق: من أُبطلت جلسته يحتاج أن يعرف أن **شيئاً وقع** — ربما لم يقع
            // منه — فيغيّر سلوكه؛ ومن انقضت جلسته يدخل من جديد ولا شيء غير ذلك. ورمزٌ
            // واحد للاثنين يجعل الأول يظنّ أنه مجرّد وقت مضى، وهو أخطر ما يُقال له.
            if (verdict.Outcome == CredentialOutcome.Revoked)
            {
                await DenyAsync(
                    context,
                    "auth.credential_revoked",
                    "أُبطلت هذه الجلسة. ادخل من جديد — والإبطال يقع فوراً ولا يُنتظر به انقضاء. "
                    + "وإن لم تكن أنت من أبطلها فأخبر صاحب المنشأة الآن.",
                    "This session has been revoked. Sign in again — revocation takes effect immediately and never "
                    + "waits for an expiry. If it was not you who revoked it, tell the company's owner now.")
                    .ConfigureAwait(false);
                return;
            }

            if (verdict.Outcome == CredentialOutcome.Expired)
            {
                await DenyAsync(
                    context,
                    "auth.credential_expired",
                    "انقضى هذا الاعتماد. ادخل من جديد — ولم يُسحب منك شيء ولم يتغيّر شيء في البيانات.",
                    "This credential has expired. Sign in again — nothing was revoked and nothing in the data changed.")
                    .ConfigureAwait(false);
                return;
            }

            ApiPrincipal principal = verdict.Principal!;

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

    /// <summary>
    /// الأبواب الثلاثة التي تُفتح بلا اعتماد — <b>مسمّاةً واحداً واحداً، لا بنمط</b>.
    /// <para>
    /// نمطٌ فضفاض («ما تحت <c>/docs</c>» أو «ما ليس تحت <c>/api/</c>») كان سيقبل أي
    /// مسارٍ جديد يقع خارج <c>/api/</c> دون أن ينتبه أحد — وهو بالضبط ما يجعل حارساً
    /// كهذا يمرّ على العطل الذي وُجد لأجله.
    /// </para>
    /// <list type="bullet">
    ///   <item><c>/health</c> — لا يقرأ بيانات مستأجر ولا يكتبها.</item>
    ///   <item><c>/openapi/v1.json</c> — بايتات ملفٍّ مُودَع في المستودع. لا سرّ فيه،
    ///         ولا بيانات مستأجر واحد.</item>
    ///   <item><c>/docs</c> — صفحةٌ ساكنة تقرأ ذلك الملفّ. <b>والمتصفّح لا يستطيع أن
    ///         يضع ترويسة <c>Authorization</c> على تنقّلٍ عُلوي</b>، فصفحةُ توثيق محميّة
    ///         بـ<c>Bearer</c> غير قابلة للفتح أصلاً؛ وعلاجُها الوحيد ملفّ ارتباط أو
    ///         جلسة — أي آلية تصريح ثانية، وهي أخطر من غيابها.</item>
    ///   <item><c>/api/v1/access/sessions</c> و<c>/api/v1/access/sessions/renewal</c> —
    ///         <b>وغيابُ الاعتماد عنهما بنيوي:</b> من يطلب اعتماداً لا يملك اعتماداً،
    ///         وبابٌ يُصدر جلسةً ويشترط جلسةً بابٌ لا يُفتح أبداً. والاعتماد ليس غائباً
    ///         عنهما بل <b>منقولاً من الترويسة إلى الجسم</b>: اعتماد انتساب على الأول
    ///         واعتماد تجديد على الثاني، وكلاهما يُبصَم ويُطابَق بالبصمة كأي اعتماد،
    ///         والرفض واحدٌ لا يُفرَّق فيه المختلَق عن غيره. <b>وما لا يُفتح بفتحهما:</b>
    ///         لا يقرأ أيٌّ منهما بيانات مستأجرٍ ولا يكتبها إلا بعد أن يُثبت مُقدِّمُه
    ///         اعتماداً، ولا يُرجع أيٌّ منهما شيئاً لمن لم يُثبت.</item>
    /// </list>
    /// <para>
    /// <b>وما لا يُفتح بفتحها:</b> الثلاثة تكتب بايتات ثابتة أو حالةَ عملية. ولا واحد
    /// منها يلمس مستأجراً، ولا يعكس مدخلاً من العميل، ولا يمنح الصفحة امتيازاً: زرّ
    /// «جرّب» فيها عميلٌ يمرّ بهذا الوسيط نفسه سطراً بسطر.
    /// </para>
    /// </summary>
    private static bool IsAnonymous(PathString path) =>
        path.Equals(Endpoints.ApiRoutes.Health, StringComparison.Ordinal)
        || path.Equals(Endpoints.ApiRoutes.OpenApiDocument, StringComparison.Ordinal)
        || path.Equals(Endpoints.ApiRoutes.Docs, StringComparison.Ordinal)
        || path.Equals(Endpoints.AccessRoutes.Sessions, StringComparison.Ordinal)
        || path.Equals(Endpoints.AccessRoutes.SessionRenewal, StringComparison.Ordinal);

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
