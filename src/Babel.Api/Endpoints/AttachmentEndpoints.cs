using System.Globalization;
using System.Text;
using Babel.Api.Errors;
using Babel.Api.Hosting;
using Babel.Api.Security;
using Babel.Api.Wire;
using Babel.Contracts.Storage;
using Babel.SharedKernel;
using Babel.Storage.Surface;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Babel.Api.Endpoints;

/// <summary>
/// سطح HTTP فوق المرفقات — <b>الباب الذي لم يكن للمرفقات باب</b>.
/// <para>
/// المنفذ والمحوّلان والانضباط في القاعدة بُنيت كلّها في ADR-0046 و<b>لم تُفتح لها نقطة
/// نهاية واحدة</b>، فكان النظام يحمل مخزن سندات إثبات لا يبلغه أحد من خارج العملية.
/// وهذا الملفّ يفتح ستّة أبواب بشكل ADR-0044 حرفياً: <b>مورد رئيسي وموارد فرعية</b>،
/// ولا <c>PUT</c> ولا <c>PATCH</c> ولا <c>DELETE</c> على مرفقٍ واحد.
/// </para>
/// <para>
/// <b>وما يفعله كل معالج هنا، بالترتيب، ولا شيء غيره:</b> يقرأ نطاق الشركة من المسار
/// ويتحقّق أن الاعتماد يبلغه · يقرأ الحمولة بالقواعد الصارمة · ينقل الحقول إلى السطح
/// المنشور · ينادي · يترجم النتيجة. <b>ولا قرار واحد عن المحتوى يقع في هذا الملفّ:</b>
/// لا شمّ، ولا تجزئة، ولا تطهير اسم، ولا سقف — كلّه في المنفذ ومحوّله، حيث يُختبَر على
/// PostgreSQL حقيقية ونظام ملفّات حقيقي.
/// </para>
/// <para>
/// <b>وثلاثة شروط من ADR-0046 §8 منفَّذةٌ هنا بأعيانها:</b> الرفع <c>multipart/form-data</c>
/// لا JSON — فجسم JSON يعني <c>base64</c> يعني انتفاخ الثلث وصورةً كاملة في سجلّ الطلب ·
/// والتنزيل يقرأ التذكرة ثم يقارن مستأجرها بمستأجر الجلسة ثم ينادي المخزن بمستأجر
/// <b>الجلسة</b> · وترويسة <c>Content-Type</c> من النوع <b>المشموم</b> وحده و
/// <c>Content-Disposition</c> بـ<c>attachment</c> لا <c>inline</c>.
/// </para>
/// </summary>
internal static class AttachmentEndpoints
{
    /// <summary>اسم جزء البايتات في الحمولة متعدّدة الأجزاء.</summary>
    public const string ContentPart = "content";

    /// <summary>اسم جزء نوع المستند المصدر.</summary>
    public const string SourceTypePart = "sourceDocumentType";

    /// <summary>اسم جزء معرّف المستند المصدر.</summary>
    public const string SourceIdPart = "sourceDocumentId";

    /// <summary>أقصى طول لقيمة جزء نصّي في الحمولة — الرموز والمعرّفات وحدها تمرّ منه.</summary>
    private const int MaximumTextPartLength = 128;

    /// <summary>أقصى طول لرمز التذكرة في سلسلة الاستعلام.</summary>
    private const int TicketQueryLength = 256;

    /// <summary>أقصى طول لوسيط عددي في سلسلة الاستعلام.</summary>
    private const int CountQueryLength = 9;

    /// <summary>
    /// فسحةٌ فوق سقف المرفق لترويسات الأجزاء وحدودها. الحدّ الذي يقرؤه Kestrel يجب أن
    /// يزيد على سقف البايتات، وإلا رمى الخادم قبل أن يبلغ الطلبُ فحصَنا فيصير الرفض
    /// استثناءً بلا جسم مشكلة — وهو بالضبط ما تمنعه هذه الفسحة.
    /// </summary>
    private const long MultipartOverheadBytes = 64 * 1024;

    /// <summary>يسجّل سطح المرفقات.</summary>
    /// <param name="app">مُنشئ المسارات.</param>
    public static void MapAttachmentApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(ApiRoutes.Attachments, DepositAsync);
        app.MapGet(ApiRoutes.Attachments, ListAsync);
        app.MapGet(ApiRoutes.Attachment, ReadAsync);
        app.MapPost(ApiRoutes.AttachmentRevisions, ReviseAsync);
        app.MapPost(ApiRoutes.AttachmentWithdrawal, WithdrawAsync);
        app.MapPost(ApiRoutes.AttachmentDownloadTickets, IssueTicketAsync);
        app.MapGet(ApiRoutes.AttachmentContent, DownloadAsync);
    }

    // ── الإيداع والتصحيح ─────────────────────────────────────────────────────

    private static async Task<IResult> DepositAsync(
        HttpContext context,
        AttachmentSurface attachments,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (AttachmentDeposit? deposit, IResult? refused) =
            await ReadDepositAsync(context, attachments, cancellationToken).ConfigureAwait(false);

        if (deposit is null)
        {
            return refused!;
        }

        Result<AttachmentRecord> result = await attachments
            .DepositAsync(new TenantId(companyId), Actor(context), deposit, cancellationToken)
            .ConfigureAwait(false);

        return Created(context, companyId, result);
    }

    private static async Task<IResult> ReviseAsync(
        HttpContext context,
        AttachmentSurface attachments,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "attachmentId", out Guid attachmentId, out IResult? malformed))
        {
            return malformed!;
        }

        (AttachmentDeposit? deposit, IResult? refused) =
            await ReadDepositAsync(context, attachments, cancellationToken).ConfigureAwait(false);

        if (deposit is null)
        {
            return refused!;
        }

        Result<AttachmentRecord> result = await attachments
            .ReviseAsync(new TenantId(companyId), Actor(context), attachmentId, deposit, cancellationToken)
            .ConfigureAwait(false);

        return Created(context, companyId, result);
    }

    // ── القراءة والجرد ───────────────────────────────────────────────────────

    private static async Task<IResult> ReadAsync(
        HttpContext context,
        AttachmentSurface attachments,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "attachmentId", out Guid attachmentId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<AttachmentRecord> result = await attachments
            .DescribeAsync(new TenantId(companyId), attachmentId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(AttachmentMapping.ToDto(result.Value, companyId), ApiJson.Options);
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        AttachmentSurface attachments,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        string? sourceType;
        Guid? sourceId;
        int skip;
        int take;

        try
        {
            sourceType = Scope.Query(context, SourceTypePart, required: false, MaximumTextPartLength);
            sourceId = ReadOptionalGuid(Scope.Query(context, SourceIdPart, required: false, MaximumTextPartLength), SourceIdPart);
            skip = ReadCount(Scope.Query(context, "skip", required: false, CountQueryLength), "skip", 0);
            take = ReadCount(Scope.Query(context, "take", required: false, CountQueryLength), "take", AttachmentQuery.DefaultPageSize);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<AttachmentInventory> result = await attachments
            .ListAsync(new TenantId(companyId), sourceType, sourceId, skip, take, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(AttachmentMapping.ToDto(result.Value, companyId), ApiJson.Options);
    }

    // ── السحب ────────────────────────────────────────────────────────────────

    private static async Task<IResult> WithdrawAsync(
        HttpContext context,
        AttachmentSurface attachments,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "attachmentId", out Guid attachmentId, out IResult? malformed))
        {
            return malformed!;
        }

        (WithdrawAttachmentRequestDto? dto, IResult? refused) =
            await BodyAsync<WithdrawAttachmentRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        if (!IsCode(dto.ReasonKey))
        {
            return HttpProblemResults.Code(
                context,
                "wire.field.malformed",
                "مفتاح سبب السحب يُكتب بأحرف لاتينية صغيرة وأرقام ونقطة وشرطة سفلية، وطوله بين محرف و64 محرفاً. "
                + "وهو مفتاحٌ يقرؤه برنامج لا نصٌّ يُعرض على شاشة.",
                "The withdrawal reason key is written with lower-case Latin letters, digits, dots, and underscores, "
                + "between one and 64 characters. It is a key a program reads, not text displayed on a screen.",
                "reasonKey",
                StatusCodes.Status400BadRequest);
        }

        Result<AttachmentRecord> result = await attachments
            .WithdrawAsync(new TenantId(companyId), Actor(context), attachmentId, dto.ReasonKey, cancellationToken)
            .ConfigureAwait(false);

        return Created(context, companyId, result);
    }

    // ── التذكرة والتنزيل ─────────────────────────────────────────────────────

    private static async Task<IResult> IssueTicketAsync(
        HttpContext context,
        AttachmentSurface attachments,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "attachmentId", out Guid attachmentId, out IResult? malformed))
        {
            return malformed!;
        }

        (IssueAttachmentTicketRequestDto? dto, IResult? refused) =
            await BodyAsync<IssueAttachmentTicketRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        Result<AttachmentAccessTicket> result = await attachments
            .IssueTicketAsync(
                new TenantId(companyId),
                Actor(context),
                attachmentId,
                TimeSpan.FromSeconds(dto.LifetimeSeconds),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(
                AttachmentMapping.ToDto(result.Value, companyId),
                ApiJson.Options,
                statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> DownloadAsync(
        HttpContext context,
        AttachmentSurface attachments,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "attachmentId", out Guid attachmentId, out IResult? malformed))
        {
            return malformed!;
        }

        string ticket;
        try
        {
            ticket = Scope.Query(context, "ticket", required: true, TicketQueryLength)!;
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<AttachmentBytes> result = await attachments
            .OpenAsync(new TenantId(companyId), ticket, attachmentId, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return HttpProblemResults.Domain(context, result.Errors);
        }

        AttachmentRecord descriptor = result.Value.Descriptor;

        // **الترويسة من المشموم وحده.** ما أعلنه العميل يوم الإيداع لم يُخزَّن أصلاً،
        // فلا سبيل إلى أن يعود إليه بترويسة يخترعها هو.
        context.Response.Headers.ContentDisposition = Disposition(descriptor.FileName);
        context.Response.Headers.CacheControl = "no-store";

        return Results.Bytes(
            result.Value.Content.ToArray(),
            descriptor.MediaType,
            fileDownloadName: null,
            enableRangeProcessing: false);
    }

    // ── قراءة الحمولة متعدّدة الأجزاء ────────────────────────────────────────

    /// <summary>
    /// يقرأ حمولة <c>multipart/form-data</c> إلى إيداع، أو يردّ رفضاً بجسم مشكلة.
    /// <para>
    /// <b>والسقف يُفحص ثلاث مرّات لا مرّة</b>، ولكلٍّ سببها: على <c>Content-Length</c>
    /// قبل قراءة بايتة (فيُرفض الطلب الضخم قبل أن يُقرأ)، وعلى ما يُقرأ فعلاً (فالترويسة
    /// تكذب أو تغيب في التقطيع)، وعند المخزن نفسه (فهو من يملك السقف). ومن ينزع أحدها
    /// يترك الآخرين — وذلك مقصود.
    /// </para>
    /// </summary>
    private static async Task<(AttachmentDeposit? Deposit, IResult? Refused)> ReadDepositAsync(
        HttpContext context,
        AttachmentSurface attachments,
        CancellationToken cancellationToken)
    {
        (AttachmentDeposit? deposit, IResult? refused) =
            await ReadDepositCoreAsync(context, attachments, cancellationToken).ConfigureAwait(false);

        // ── ورفضٌ يُكتب وجسمُ الطلب ما يزال يصل ⇒ **اتصالٌ يُقطع لا جواب يُقرأ** ──
        //
        // ‏**مقيس، وهذا بالضبط ما وقع:** الرفض بـ413 كان يُكتب بعد قراءة السقف وحده،
        // فيبقى الفائض في الطريق، فيُنهي الخادم الاتصال ويقرأ العميل
        // «‏Broken pipe» بدل جسم المشكلة. والاختبار كان يمرّ وحده ويسقط تحت الحِمل —
        // فرقُه الزمنُ لا الشيفرة، وهو شكل فخ-95 نفسه.
        //
        // فالتصريف هنا **جزء من الرفض لا كياسة**: بلا استهلاك ما تبقّى لا يصل الجواب
        // أصلاً. وهو **محدود** بحدّ Kestrel لهذا الطلب (السقف + فسحة الترويسات)، فلا
        // يفتح باباً لقراءة بلا حدّ: ما يتجاوزه يقطعه الخادم نفسه ويُبتلع هنا.
        if (deposit is null)
        {
            await DrainAsync(context, cancellationToken).ConfigureAwait(false);
        }

        return (deposit, refused);
    }

    /// <summary>يستهلك ما تبقّى من جسم الطلب كي يصل جسمُ الرفض إلى العميل.</summary>
    private static async Task DrainAsync(HttpContext context, CancellationToken cancellationToken)
    {
        try
        {
            byte[] sink = new byte[81920];
            while (await context.Request.Body.ReadAsync(sink, cancellationToken).ConfigureAwait(false) > 0)
            {
                // يُقرأ ويُهمَل: الغرض إفراغ الطريق لا الاحتفاظ بشيء.
            }
        }
        catch (Exception failure) when (failure is IOException or BadHttpRequestException or OperationCanceledException)
        {
            // انقطع الطرف الآخر أثناء التصريف: الرفض يبقى هو الجواب، ولا يتحوّل إلى عطل.
        }
    }

    private static async Task<(AttachmentDeposit? Deposit, IResult? Refused)> ReadDepositCoreAsync(
        HttpContext context,
        AttachmentSurface attachments,
        CancellationToken cancellationToken)
    {
        long cap = attachments.MaximumBytes;

        string? boundary = BoundaryOf(context.Request.ContentType);
        if (boundary is null)
        {
            return (null, HttpProblemResults.Code(
                context,
                "wire.body.unsupported_media_type",
                "إيداع المرفق يكون بحمولة multipart/form-data لا بجسم JSON: جسم JSON يعني ترميز البايتات نصّاً، "
                + "أي انتفاخ الثلث وصورةً كاملة في سجلّ الطلب.",
                "An attachment is deposited as multipart/form-data, not as a JSON body: a JSON body means the bytes are "
                + "text-encoded — a third larger, and a whole image in the request log.",
                "Content-Type",
                StatusCodes.Status415UnsupportedMediaType));
        }

        if (context.Request.ContentLength is { } declaredLength && declaredLength - MultipartOverheadBytes > cap)
        {
            return (null, HttpProblemResults.Domain(context, [AttachmentErrors.TooLarge(declaredLength, cap)]));
        }

        // حدّ Kestrel يُرفع لهذا الطلب وحده وبفسحة الترويسات: الحدّ العام للسطح ميغابايت
        // واحد، والمرفق حتى عشرين ميبي‌بايت. ورفعُه هنا لا يفتح السطح كلّه.
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = cap + MultipartOverheadBytes;
        }

        byte[]? content = null;
        string? declaredFileName = null;
        string? declaredMediaType = null;
        string? sourceType = null;
        string? sourceId = null;

        try
        {
            MultipartReader reader = new(boundary, context.Request.Body);
            MultipartSection? section = await reader.ReadNextSectionAsync(cancellationToken).ConfigureAwait(false);

            while (section is not null)
            {
                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out ContentDispositionHeaderValue? disposition))
                {
                    return (null, Malformed(context, "جزءٌ بلا ترويسة Content-Disposition.", "A part without a Content-Disposition header."));
                }

                string name = HeaderUtilities.RemoveQuotes(disposition.Name).Value ?? string.Empty;

                switch (name)
                {
                    case ContentPart:
                        if (content is not null)
                        {
                            return (null, Malformed(context, "جزء البايتات مكرَّر.", "The content part is repeated."));
                        }

                        declaredFileName = HeaderUtilities.RemoveQuotes(disposition.FileName).Value;
                        declaredMediaType = section.ContentType;

                        (byte[]? read, IResult? tooLarge) = await ReadCappedAsync(context, section.Body, cap, cancellationToken)
                            .ConfigureAwait(false);

                        if (read is null)
                        {
                            return (null, tooLarge!);
                        }

                        content = read;
                        break;

                    case SourceTypePart:
                        sourceType = await TextAsync(section, cancellationToken).ConfigureAwait(false);
                        break;

                    case SourceIdPart:
                        sourceId = await TextAsync(section, cancellationToken).ConfigureAwait(false);
                        break;

                    default:
                        // **جزءٌ لا نعرفه يُفشل الطلب كلّه** — كما يفعل حقلٌ غير معروف في
                        // جسم JSON. التجاهل الصامت يجعل المُرسِل يظنّ أنه أرسل ما لم يصل.
                        return (null, Malformed(
                            context,
                            "جزءٌ غير معروف في الحمولة: «" + name + "».",
                            "An unknown part in the payload: '" + name + "'."));
                }

                section = await reader.ReadNextSectionAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (BadHttpRequestException)
        {
            // ‏Kestrel رفض الحجم قبل أن نبلغ فحصنا: يُترجَم إلى الرفض نفسه بجسمه لا يُترك استثناءً.
            return (null, HttpProblemResults.Domain(context, [AttachmentErrors.TooLarge(cap + MultipartOverheadBytes, cap)]));
        }
        catch (IOException)
        {
            // ‏InvalidDataException من محلّل الأجزاء يرث IOException، فالمسك واحد.
            return (null, Malformed(context, "حمولة متعدّدة الأجزاء مقطوعة أو مشوّهة.", "A truncated or malformed multipart payload."));
        }

        if (content is null)
        {
            return (null, Malformed(
                context,
                "لا جزء اسمه «content» في الحمولة — والمرفق بايتاته.",
                "There is no part named 'content' in the payload, and an attachment is its bytes."));
        }

        Guid? parsedSource = null;
        if (sourceId is not null)
        {
            if (!Guid.TryParseExact(sourceId, "D", out Guid value) || value == Guid.Empty)
            {
                return (null, Malformed(
                    context,
                    "معرّف المستند المصدر ليس معرّفاً صالحاً بصيغة 8-4-4-4-12.",
                    "The source document identifier is not a valid 8-4-4-4-12 identifier."));
            }

            parsedSource = value;
        }

        return (new AttachmentDeposit
        {
            Content = content,
            DeclaredFileName = declaredFileName,
            DeclaredMediaType = declaredMediaType,
            SourceDocumentType = sourceType,
            SourceDocumentId = parsedSource,
        }, null);
    }

    /// <summary>
    /// يقرأ تدفّق جزءٍ بسقفٍ صارم: بايتةٌ واحدة فوق السقف تُنهي القراءة برفض 413.
    /// <b>ولا يُقرأ الفائض</b> — قراءةُ عشرين ميبي‌بايت لتُرفض بعدها هي ثمنٌ يدفعه الخادم.
    /// </summary>
    private static async Task<(byte[]? Bytes, IResult? Refused)> ReadCappedAsync(
        HttpContext context,
        Stream body,
        long cap,
        CancellationToken cancellationToken)
    {
        using MemoryStream buffer = new();
        byte[] chunk = new byte[81920];
        long total = 0;

        int read;
        while ((read = await body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;

            if (total > cap)
            {
                return (null, HttpProblemResults.Domain(context, [AttachmentErrors.TooLarge(total, cap)]));
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return (buffer.ToArray(), null);
    }

    private static async Task<string?> TextAsync(MultipartSection section, CancellationToken cancellationToken)
    {
        using StreamReader reader = new(section.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
        char[] chunk = new char[MaximumTextPartLength + 1];
        int read = await reader.ReadBlockAsync(chunk, cancellationToken).ConfigureAwait(false);
        return read == 0 ? null : new string(chunk, 0, Math.Min(read, MaximumTextPartLength));
    }

    // ── مشترك ────────────────────────────────────────────────────────────────

    /// <summary>الفاعل من الاعتماد وحده — لا من ترويسة ولا من حقل في الحمولة.</summary>
    private static UserId Actor(HttpContext context) => RequestPrincipal.Of(context).User;

    private static IResult Created(HttpContext context, Guid companyId, Result<AttachmentRecord> result)
    {
        if (result.IsFailure)
        {
            return HttpProblemResults.Domain(context, result.Errors);
        }

        AttachmentDto dto = AttachmentMapping.ToDto(result.Value, companyId);
        context.Response.Headers.Location = AttachmentMapping.SelfPath(companyId, result.Value.Id);
        return Results.Json(dto, ApiJson.Options, statusCode: StatusCodes.Status201Created);
    }

    private static IResult Malformed(HttpContext context, string ar, string en) =>
        HttpProblemResults.Code(
            context,
            "wire.multipart.malformed",
            ar,
            en,
            "content",
            StatusCodes.Status400BadRequest);

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

    private static string? BoundaryOf(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType)
            || !MediaTypeHeaderValue.TryParse(contentType, out MediaTypeHeaderValue? parsed)
            || !parsed.MediaType.HasValue
            || !parsed.MediaType.Value!.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? boundary = HeaderUtilities.RemoveQuotes(parsed.Boundary).Value;
        return string.IsNullOrEmpty(boundary) ? null : boundary;
    }

    /// <summary>
    /// ترويسة <c>Content-Disposition</c>: <c>attachment</c> دائماً لا <c>inline</c>،
    /// ومعها الاسم مرّتين — نسخة ASCII لمن لا يفهم الترميز، ونسخة <c>UTF-8</c> مُرمَّزة
    /// بـRFC 5987 كي يبقى الاسم العربي عربياً.
    /// </summary>
    private static string Disposition(string fileName)
    {
        StringBuilder ascii = new(fileName.Length);
        foreach (char character in fileName)
        {
            ascii.Append(char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '_');
        }

        return "attachment; filename=\"" + ascii + "\"; filename*=UTF-8''" + Uri.EscapeDataString(fileName);
    }

    private static bool IsCode(string value) =>
        value.Length is > 0 and <= 64
        && value.All(static c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '.' or '_');

    private static Guid? ReadOptionalGuid(string? raw, string field)
    {
        if (raw is null)
        {
            return null;
        }

        return Guid.TryParseExact(raw, "D", out Guid value) && value != Guid.Empty
            ? value
            : throw WireNumbers.Reject(
                "wire.guid.malformed",
                field,
                "القيمة ليست معرّفاً صالحاً بصيغة 8-4-4-4-12.",
                "The value is not a valid 8-4-4-4-12 identifier.");
    }

    private static int ReadCount(string? raw, string field, int fallback)
    {
        if (raw is null)
        {
            return fallback;
        }

        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw WireNumbers.Reject(
                "wire.query.malformed",
                field,
                "القيمة عددٌ صحيح غير سالب بأرقام لاتينية، بلا إشارة ولا فاصلة.",
                "The value is a non-negative integer in Latin digits, with no sign and no separator.");
    }
}
