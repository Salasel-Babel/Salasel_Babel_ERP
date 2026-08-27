using System.Globalization;
using Babel.Api.Errors;
using Babel.Api.Hosting;
using Babel.Api.Ports;
using Babel.Api.Security;
using Babel.Api.Wire;
using Babel.Contracts.Posting;
using Babel.Core.CompanySetup;
using Babel.Ledger.Audit;
using Babel.SharedKernel;

namespace Babel.Api.Endpoints;

/// <summary>
/// سطح HTTP فوق محرّك الترحيل.
/// <para>
/// <b>ما يفعله كل معالج هنا، بالترتيب، ولا شيء غيره:</b> يقرأ نطاق الشركة من المسار
/// ويتحقق أن الاعتماد يبلغه · يقرأ الجسم بالقواعد الصارمة · ينقل الحقول إلى العقد ·
/// ينادي الخدمة · يترجم النتيجة. لا قرار محاسبي واحد يقع في هذا الملف: لا اختيار دور،
/// ولا اختيار حدث، ولا حساب مبلغ، ولا قاعدة توازن. الدفتر يقرّر، والسطح ينقل.
/// </para>
/// <para>
/// <b>والاستحقاق ليس هنا أيضاً — عمداً.</b> <c>PostingService</c> و<c>LedgerAuditService</c>
/// يستدعيان <c>IEntitlementEnforcer</c> بأنفسهما قبل أي عمل، والقاعدة 6 تفرض ذلك على كل
/// نقطة دخول. فحصٌ ثانٍ هنا كان سيكون آليةَ تصريحٍ موازية — وهي أخطر من غيابها: تُصان
/// إحداهما وتُنسى الأخرى، والفارق لا يظهر إلا يوم يتجاوزه أحدهم.
/// </para>
/// </summary>
internal static class LedgerEndpoints
{
    /// <summary>يسجّل كل نقاط النهاية المُصدَّرة.</summary>
    /// <param name="app">مُنشئ المسارات.</param>
    public static void MapLedgerApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(ApiRoutes.Health, Health);
        app.MapPost(ApiRoutes.PostJournalEntry, PostJournalEntryAsync);
        app.MapPost(ApiRoutes.ReverseJournalEntry, ReverseJournalEntryAsync);
        app.MapGet(ApiRoutes.ReadJournalEntry, ReadJournalEntryAsync);
        app.MapGet(ApiRoutes.TrialBalance, TrialBalanceAsync);
        app.MapGet(ApiRoutes.ChainVerification, VerifyChainAsync);
    }

    /// <summary>
    /// حالة الخدمة — ومعها ثقافة العملية وتقويمها.
    /// <para>
    /// إعلان الثقافة ليس زينة تشخيصية: خادم يعمل تحت <c>ar-SA</c> تقويمه الافتراضي أم
    /// القرى، وأي تنسيق تاريخ ضمني عليه يكتب <c>1448-03</c> مكان <c>2026-08</c> فيُفسد رمز
    /// الفترة المالية بلا استثناء ولا سطر سجل (فخ-38). ومن يشغّل النظام يحتاج أن يعرف من
    /// الخارج بأي ثقافة يعمل خادمه، لا أن يخمّنها.
    /// </para>
    /// </summary>
    private static IResult Health() => Results.Json(
        new HealthDto(
            "ok",
            CultureInfo.CurrentCulture.Name,
            CultureInfo.CurrentCulture.Calendar.GetType().Name,
            ApiRoutes.Version),
        ApiJson.Options);

    private static async Task<IResult> PostJournalEntryAsync(
        HttpContext context,
        IPostingService posting,
        ICostCenterResolver costCenters,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        ApiPrincipal principal = RequestPrincipal.Of(context);

        PostJournalEntryRequestDto? dto;
        try
        {
            dto = await context.Request
                .ReadFromJsonAsync<PostJournalEntryRequestDto>(ApiJson.Options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException exception)
        {
            return Scope.BadJson(context, exception);
        }

        if (dto is null)
        {
            return HttpProblemResults.Code(
                context, "wire.body.missing", "جسم الطلب مفقود.", "The request body is missing.");
        }

        // ── مركز التكلفة يُحلّ **قبل** بناء الطلب ─────────────────────────────
        // ‏ADR-0026: المذكور إن كان عاملاً، والافتراضي إن لم يُذكر شيء. والحلّ سؤالٌ عن
        // سجلّ المنشأة — وهو في النواة — فالسطح يسأل ولا يقرّر، ثم يُسلّم الجواب إلى
        // النقل. وبهذا **لا يستطيع أحد أن يبني PostingScope بلا مركز**: النوع نفسه
        // يرفض ذلك، والقيمة الوحيدة التي تبلغه جاءت من هنا.
        List<string?> candidates;
        try
        {
            candidates = WireMapping.CostCenterCandidates(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Dictionary<string, string> resolvedCostCenters = new(StringComparer.Ordinal);

        foreach (string? candidate in candidates)
        {
            Result<string> resolution = await costCenters
                .ResolveAsync(new TenantId(companyId), candidate, cancellationToken)
                .ConfigureAwait(false);

            if (resolution.IsFailure)
            {
                return HttpProblemResults.Domain(context, resolution.Errors);
            }

            resolvedCostCenters[candidate ?? string.Empty] = resolution.Value;
        }

        PostingRequest request;
        try
        {
            request = WireMapping.ToPostingRequest(dto, companyId, principal.User, resolvedCostCenters);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }
        catch (ArgumentException exception)
        {
            // آخر شبكة: نوع قيمة في النواة المشتركة رفض المدخل بعد أن مرّ من ماسحنا.
            // يُرجَع رفضاً شكلياً لا عطلاً — ومع ذلك يُسجَّل، لأن وصوله هنا يعني ثغرة
            // في التحقق عند الحدّ يجب أن تُسدّ.
            return HttpProblemResults.Code(
                context,
                "wire.value.rejected_by_domain_type",
                "قيمة رفضها نوع مجالي بعد اجتيازها فحص الحدّ: " + exception.Message,
                "A domain value type refused the input after it passed boundary validation: " + exception.Message);
        }

        Result<PostingReceipt> result = await posting.PostAsync(request, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return HttpProblemResults.Domain(context, result.Errors);
        }

        PostingReceipt receipt = result.Value;

        // ‏200 للوصول الثاني بالمفتاح نفسه و201 للأول. الفارق **مُعلن في الجسم أيضاً**
        // بـ alreadyPosted: عميلٌ يعيد المحاولة بعد انقطاع شبكة يحتاج أن يعرف أيّهما
        // وقع، ورمز الحالة وحده يضيع خلف أي وسيط يعيد التوجيه.
        string location = ApiRoutes.ReadJournalEntry
            .Replace("{companyId}", companyId.ToString("D", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{entryId}", receipt.JournalEntryId.ToString("D", CultureInfo.InvariantCulture), StringComparison.Ordinal);

        context.Response.Headers.Location = location;

        return Results.Json(
            WireMapping.ToDto(receipt),
            ApiJson.Options,
            statusCode: receipt.WasAlreadyPosted ? StatusCodes.Status200OK : StatusCodes.Status201Created);
    }

    private static async Task<IResult> ReverseJournalEntryAsync(
        HttpContext context,
        IPostingService posting,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!TryEntryId(context, out Guid entryId, out IResult? malformed))
        {
            return malformed!;
        }

        ApiPrincipal principal = RequestPrincipal.Of(context);

        ReverseJournalEntryRequestDto? dto;
        try
        {
            dto = await context.Request
                .ReadFromJsonAsync<ReverseJournalEntryRequestDto>(ApiJson.Options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException exception)
        {
            return Scope.BadJson(context, exception);
        }

        if (dto is null)
        {
            return HttpProblemResults.Code(
                context, "wire.body.missing", "جسم الطلب مفقود.", "The request body is missing.");
        }

        ReversalRequest request;
        try
        {
            request = WireMapping.ToReversalRequest(dto, companyId, entryId, principal.User);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<PostingReceipt> result = await posting.ReverseAsync(request, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(WireMapping.ToDto(result.Value), ApiJson.Options, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> ReadJournalEntryAsync(
        HttpContext context,
        IJournalEntryReader reader,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!TryEntryId(context, out Guid entryId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<JournalEntryDto> result = await reader
            .ReadAsync(new TenantId(companyId), entryId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(result.Value, ApiJson.Options);
    }

    private static async Task<IResult> TrialBalanceAsync(
        HttpContext context,
        LedgerAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        string book;
        string? period;
        try
        {
            book = ReadQuery(context, "book", required: true, maxLength: 32)!;
            period = ReadQuery(context, "period", required: false, maxLength: 7);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        // الفاعل من الاعتماد يعبر إلى الدفتر: القراءة تُقاس على محور «المستخدم الفاعل»
        // كما تُقاس الكتابة، ولا تُنسب إلى فاعل نظام.
        Result<TrialBalanceReport> result = await audit
            .TrialBalanceFromLinesAsync(
                new TenantId(companyId), RequestPrincipal.Of(context).User, book, period, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(WireMapping.ToDto(book, period, result.Value), ApiJson.Options);
    }

    private static async Task<IResult> VerifyChainAsync(
        HttpContext context,
        LedgerAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        string book;
        int fiscalYear;
        try
        {
            book = ReadQuery(context, "book", required: true, maxLength: 32)!;
            fiscalYear = ReadFiscalYear(context);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<LedgerChainReport> result = await audit
            .VerifyChainAsync(
                new TenantId(companyId), RequestPrincipal.Of(context).User, book, fiscalYear, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(WireMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static bool TryEntryId(HttpContext context, out Guid entryId, out IResult? malformed)
    {
        malformed = null;
        string raw = context.Request.RouteValues.TryGetValue("entryId", out object? value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;

        if (!Guid.TryParseExact(raw, "D", out entryId) || entryId == Guid.Empty)
        {
            malformed = HttpProblemResults.Code(
                context,
                "wire.guid.malformed",
                "معرّف القيد في المسار ليس معرّفاً صالحاً بصيغة 8-4-4-4-12.",
                "The entry identifier in the path is not a valid 8-4-4-4-12 identifier.",
                "entryId",
                StatusCodes.Status400BadRequest);
            return false;
        }

        return true;
    }

    private static string? ReadQuery(HttpContext context, string name, bool required, int maxLength)
    {
        if (!context.Request.Query.TryGetValue(name, out Microsoft.Extensions.Primitives.StringValues values)
            || values.Count == 0)
        {
            if (!required)
            {
                return null;
            }

            throw WireNumbers.Reject(
                "wire.query.missing", name, "وسيط استعلام إلزامي مفقود.", "A required query parameter is missing.");
        }

        if (values.Count > 1)
        {
            // تكرار الوسيط يجعل «أي القيمتين» سؤالاً بلا جواب معلن — والاختيار الصامت
            // لأولاهما هو ما يجعل تسميم الوسائط ممكناً.
            throw WireNumbers.Reject(
                "wire.query.repeated", name, "وسيط الاستعلام مكرَّر.", "The query parameter is repeated.");
        }

        string value = values[0] ?? string.Empty;

        if (value.Length == 0 || value.Length > maxLength)
        {
            throw WireNumbers.Reject(
                "wire.query.malformed",
                name,
                FormattableString.Invariant($"قيمة الوسيط فارغة أو أطول من {maxLength} محرفاً."),
                FormattableString.Invariant($"The parameter value is empty or longer than {maxLength} characters."));
        }

        return value;
    }

    private static int ReadFiscalYear(HttpContext context)
    {
        string raw = ReadQuery(context, "fiscalYear", required: true, maxLength: 4)!;

        foreach (char c in raw)
        {
            if (c is < '0' or > '9')
            {
                throw WireNumbers.Reject(
                    "wire.query.malformed",
                    "fiscalYear",
                    "السنة المالية أربعة أرقام لاتينية ميلادية.",
                    "The fiscal year is four Latin Gregorian digits.");
            }
        }

        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int year) || year is < 1900 or > 2999)
        {
            throw WireNumbers.Reject(
                "wire.query.malformed",
                "fiscalYear",
                "السنة المالية خارج المدى المقبول.",
                "The fiscal year is outside the accepted range.");
        }

        return year;
    }

}
