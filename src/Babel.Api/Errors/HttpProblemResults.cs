using Babel.Api.Hosting;
using Babel.Api.Wire;
using Babel.SharedKernel;

namespace Babel.Api.Errors;

/// <summary>كتابة تفاصيل المشكلة على الاستجابة بنوع محتوى <c>RFC 9457</c>.</summary>
internal static class HttpProblemResults
{
    /// <summary>نوع محتوى تفاصيل المشكلة كما تعرّفه المواصفة.</summary>
    public const string ContentType = "application/problem+json";

    /// <summary>ترويسة معرّف التتبّع — الرابط الوحيد بين استجابة العميل وسجلّ الخادم.</summary>
    public const string TraceHeader = "X-Babel-Trace-Id";

    /// <summary>يكتب مشكلة من خطأ حدّ برمزه.</summary>
    /// <param name="context">سياق الطلب.</param>
    /// <param name="code">الرمز الثابت.</param>
    /// <param name="messageAr">الرسالة العربية.</param>
    /// <param name="messageEn">الرسالة الإنجليزية.</param>
    /// <param name="field">الحقل، إن وُجد.</param>
    /// <param name="status">حالة صريحة تتجاوز التصنيف الافتراضي.</param>
    public static IResult Code(
        HttpContext context,
        string code,
        string messageAr,
        string messageEn,
        string? field = null,
        int? status = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        string traceId = TraceIdOf(context);
        ProblemDto problem = ApiProblems.FromCode(code, messageAr, messageEn, InstanceOf(context), traceId, field, status);
        return Write(context, problem);
    }

    /// <summary>يكتب مشكلة من أخطاء خدمة مجالية.</summary>
    /// <param name="context">سياق الطلب.</param>
    /// <param name="errors">الأخطاء.</param>
    public static IResult Domain(HttpContext context, IReadOnlyList<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(context);
        string traceId = TraceIdOf(context);
        ProblemDto problem = ApiProblems.FromDomain(errors, InstanceOf(context), traceId);
        return Write(context, problem);
    }

    /// <summary>يكتب مشكلة من رفض شكلي عند حدّ التسلسل.</summary>
    /// <param name="context">سياق الطلب.</param>
    /// <param name="wire">الرفض.</param>
    public static IResult Wire(HttpContext context, WireFormatException wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        return Code(context, wire.Code, wire.MessageAr, wire.MessageEn, wire.Field);
    }

    /// <summary>معرّف تتبّع هذا الطلب، يُنشأ مرّة ويُكتب في الترويسة.</summary>
    /// <param name="context">سياق الطلب.</param>
    public static string TraceIdOf(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items.TryGetValue(TraceHeader, out object? existing) && existing is string value)
        {
            return value;
        }

        string traceId = ApiProblems.NewTraceId();
        context.Items[TraceHeader] = traceId;
        return traceId;
    }

    private static string InstanceOf(HttpContext context) => context.Request.Path.Value ?? "/";

    private static IResult Write(HttpContext context, ProblemDto problem)
    {
        context.Response.Headers[TraceHeader] = problem.TraceId;
        return Results.Json(problem, ApiJson.Options, ContentType, problem.Status);
    }
}
