using System.Globalization;
using Babel.Api.Wire;
using Babel.SharedKernel;

namespace Babel.Api.Errors;

/// <summary>
/// ترجمة الأخطاء المجالية إلى <c>RFC 9457</c>.
/// <para>
/// <b>القاعدة الحاكمة:</b> العميل يقرأ <b>الرمز</b>، ولا يقرأ نصّ الرسالة أبداً ليتخذ
/// قراراً. ولذلك كل خطأ يخرج من هنا برمزٍ ثابت واحد على الأقل، ورسالتين للعرض. وتصنيف
/// الخطأ إلى رمز حالة HTTP يقع هنا وحده — لا في نقطة النهاية — كي لا يختلف تصنيف
/// «فترة مقفلة» بين مسار وآخر.
/// </para>
/// <para>
/// <b>وما لا يخرج من هنا أبداً:</b> نصّ خطأ قاعدة بيانات، أو أثر مكدّس، أو شذرة SQL.
/// محرّك الترحيل يُرجع اليوم <c>ledger.posting.database.&lt;SQLSTATE&gt;</c> ومعه
/// <c>MessageText</c> الخام من PostgreSQL — وهو نصّ يحمل أسماء جداول وقيود. الرمز يعبر
/// (فهو معلومة تصنيفية بلا تسريب)، <b>والنصّ يُحجب ويُستبدل</b>. وحارس ذلك اختبار عند
/// حدّ HTTP لا مراجعة بشرية.
/// </para>
/// </summary>
internal static class ApiProblems
{
    /// <summary>البادئة التي يحملها كل مرجع نوع مشكلة في هذا السطح.</summary>
    public const string TypeBase = "https://salasel-babel.example/problems/";

    /// <summary>بادئة أخطاء قاعدة البيانات كما يصدرها محرّك الترحيل.</summary>
    public const string DatabaseErrorPrefix = "ledger.posting.database.";

    /// <summary>الرمز الذي يخرج به أي عطل غير متوقّع — بلا تفصيل، ومع معرّف تتبّع.</summary>
    public const string InternalErrorCode = "api.internal_error";

    /// <summary>الرمز الذي يخرج به رفض قاعدة البيانات بعد حجب نصّه.</summary>
    public const string DatabaseRefusedCode = "ledger.posting.database_refused";

    /// <summary>رمز الحالة الذي يقابل خطأً مجالياً بعينه.</summary>
    /// <param name="code">رمز الخطأ المجالي.</param>
    public static int StatusOf(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        if (code.StartsWith(DatabaseErrorPrefix, StringComparison.Ordinal))
        {
            // ‏23514 (check_violation) و‏23505 (unique_violation) رفضٌ مجالي كتبه المشغّل
            // المؤجَّل أو قيد فريد: 409. وما عداها — ومنها 42501 صلاحيات — عطل تشغيلي
            // لا يُصلحه العميل بإعادة صياغة الطلب.
            string sqlState = code[DatabaseErrorPrefix.Length..];
            return sqlState is "23514" or "23505" or "23503" ? 409 : 500;
        }

        return code switch
        {
            "entitlement.not_entitled" or "entitlement.read_only" => 403,
            "entitlement.mandatory_disabled" or "entitlement.unsatisfied_requirement" or "entitlement.incomplete_set" => 409,

            "ledger.posting.entry_not_found" => 404,
            "ledger.posting.already_reversed" or "ledger.posting.cannot_reverse_a_reversal" => 409,
            "ledger.posting.closed_period" or "ledger.posting.permanently_closed_period" => 409,

            "ledger.posting.missing_tenant" or "ledger.posting.missing_idempotency_key" => 400,

            "capability_profile.not_found" => 404,
            "capability_profile.capability_withdrawal_requires_acknowledgement" => 409,

            _ when code.StartsWith("capability_profile.", StringComparison.Ordinal) => 422,
            _ when code.StartsWith("document_admission.", StringComparison.Ordinal) => 422,

            _ when code.StartsWith("wire.", StringComparison.Ordinal) => 400,
            _ when code.StartsWith("auth.", StringComparison.Ordinal) => 401,
            _ when code.StartsWith("tenancy.", StringComparison.Ordinal) => 403,
            _ when code.StartsWith("ledger.posting.guard.", StringComparison.Ordinal) => 422,
            _ when code.StartsWith("ledger.posting.", StringComparison.Ordinal) => 422,
            _ when code.StartsWith("ledger.read.", StringComparison.Ordinal) => 501,

            _ => 500,
        };
    }

    /// <summary>يبني تفاصيل مشكلة من قائمة أخطاء مجالية بعد تعقيمها.</summary>
    /// <param name="errors">الأخطاء كما جاءت من الخدمة.</param>
    /// <param name="instance">مسار الطلب.</param>
    /// <param name="traceId">معرّف التتبّع.</param>
    public static ProblemDto FromDomain(IReadOnlyList<Error> errors, string instance, string traceId)
    {
        ArgumentNullException.ThrowIfNull(errors);

        List<ApiErrorDto> sanitised = [.. errors.Select(Sanitise)];
        ApiErrorDto first = sanitised.Count > 0
            ? sanitised[0]
            : new ApiErrorDto(InternalErrorCode, "عطل غير محدّد.", "An unspecified failure.");

        int status = StatusOf(first.Code);
        return Build(status, first, sanitised, instance, traceId);
    }

    /// <summary>يبني تفاصيل مشكلة من خطأ واحد يخصّ الحدّ نفسه (مصادقة، نطاق، شكل).</summary>
    /// <param name="code">الرمز الثابت.</param>
    /// <param name="messageAr">الرسالة العربية.</param>
    /// <param name="messageEn">الرسالة الإنجليزية.</param>
    /// <param name="instance">مسار الطلب.</param>
    /// <param name="traceId">معرّف التتبّع.</param>
    /// <param name="field">الحقل المعنيّ، إن وُجد.</param>
    /// <param name="status">رمز حالة صريح يتجاوز التصنيف الافتراضي.</param>
    public static ProblemDto FromCode(
        string code,
        string messageAr,
        string messageEn,
        string instance,
        string traceId,
        string? field = null,
        int? status = null)
    {
        ApiErrorDto error = new(code, messageAr, messageEn, field);
        return Build(status ?? StatusOf(code), error, [error], instance, traceId);
    }

    /// <summary>
    /// يحجب ما لا يجوز أن يعبر: نصّ قاعدة البيانات يُستبدل، والرمز يبقى.
    /// </summary>
    /// <param name="error">الخطأ المجالي.</param>
    private static ApiErrorDto Sanitise(Error error)
    {
        if (error.Code.StartsWith(DatabaseErrorPrefix, StringComparison.Ordinal))
        {
            string sqlState = error.Code[DatabaseErrorPrefix.Length..];
            return new ApiErrorDto(
                DatabaseRefusedCode + "." + sqlState,
                "رفضت طبقة التخزين هذه العملية. التصنيف في الرمز، والتفصيل في سجلّ الخادم "
                + "تحت معرّف التتبّع — ولا يعبر نصّ قاعدة البيانات إلى العميل.",
                "The storage layer refused this operation. The classification is in the code and the detail is in "
                + "the server log under the trace id; database text never crosses to the client.");
        }

        return new ApiErrorDto(error.Code, error.MessageAr, error.MessageEn);
    }

    private static ProblemDto Build(
        int status,
        ApiErrorDto first,
        IReadOnlyList<ApiErrorDto> errors,
        string instance,
        string traceId) => new(
            TypeBase + first.Code,
            TitleEn(status),
            TitleAr(status),
            status,
            first.MessageEn,
            first.MessageAr,
            instance,
            first.Code,
            traceId,
            errors);

    private static string TitleAr(int status) => status switch
    {
        400 => "طلب غير صالح",
        401 => "اعتماد مفقود أو غير مقبول",
        403 => "ممنوع",
        404 => "غير موجود",
        405 => "فعل غير مسموح",
        409 => "تعارض مع حالة قائمة",
        413 => "الحمولة أكبر من الحدّ",
        415 => "نوع محتوى غير مدعوم",
        422 => "الطلب مفهوم ومرفوض محاسبياً",
        501 => "غير منفَّذ بعد",
        _ => "عطل في الخادم",
    };

    private static string TitleEn(int status) => status switch
    {
        400 => "Bad request",
        401 => "Missing or unacceptable credential",
        403 => "Forbidden",
        404 => "Not found",
        405 => "Method not allowed",
        409 => "Conflict with existing state",
        413 => "Payload too large",
        415 => "Unsupported media type",
        422 => "Understood and refused on accounting grounds",
        501 => "Not implemented yet",
        _ => "Server failure",
    };

    /// <summary>معرّف تتبّع جديد بصيغة ثابتة لا تقرأ ثقافة.</summary>
    public static string NewTraceId() => Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture);
}
