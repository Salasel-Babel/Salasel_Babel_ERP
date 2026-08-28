using Babel.Api.Errors;
using Babel.Api.Security;
using Babel.Api.Wire;
using Babel.Core.CompanySetup;

namespace Babel.Api.Endpoints;

/// <summary>
/// قراءة النطاق من المسار وترجمة الرفض الشكلي — <b>موضع واحد لكل نقاط النهاية</b>.
/// <para>
/// ولماذا موضع واحد: فحصُ نطاقٍ منسوخ في ملفّين ينحرف في أحدهما عند أول تعديل، فيصير
/// أحد السطحين يميّز بين «شركة غير موجودة» و«شركة لا تبلغها» والآخر لا — وهو فرقٌ يُقرأ
/// عدّادَ وجودٍ لشركات مستأجرين آخرين. وهذا صنف العطل الذي يتكرّر في هذا المستودع:
/// <b>فحصٌ يؤدّيه مستدعٍ واحد</b>.
/// </para>
/// </summary>
internal static class Scope
{
    /// <summary>أقصى طول لرمز نوع مستند في المسار.</summary>
    public const int MaximumDocumentTypeLength = 64;

    /// <summary>
    /// يقرأ نطاق الشركة من المسار ويتحقق أن الاعتماد يبلغه — <b>قبل أي عمل</b>.
    /// <para>
    /// الترتيب هو المهمّ: الرفض يقع قبل قراءة الجسم وقبل أي اتصال بقاعدة بيانات، فلا
    /// يوجد مسار يلمس فيه طلبُ مستأجرٍ بياناتِ مستأجر آخر ولو للحظة، ولا فرق زمني
    /// يُقاس بين «شركة غير موجودة» و«شركة موجودة لا تبلغها».
    /// </para>
    /// </summary>
    /// <param name="context">سياق الطلب.</param>
    /// <param name="companyId">معرّف الشركة عند النجاح.</param>
    /// <param name="denied">استجابة الرفض عند الفشل.</param>
    public static bool TryCompany(HttpContext context, out Guid companyId, out IResult? denied)
    {
        ArgumentNullException.ThrowIfNull(context);

        companyId = Guid.Empty;
        denied = null;

        string raw = context.Request.RouteValues.TryGetValue("companyId", out object? value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;

        if (!Guid.TryParseExact(raw, "D", out companyId) || companyId == Guid.Empty)
        {
            denied = HttpProblemResults.Code(
                context,
                "tenancy.company_id_malformed",
                "معرّف الشركة في المسار ليس معرّفاً صالحاً بصيغة 8-4-4-4-12.",
                "The company identifier in the path is not a valid 8-4-4-4-12 identifier.",
                "companyId",
                StatusCodes.Status400BadRequest);
            return false;
        }

        if (!RequestPrincipal.Of(context).Reaches(companyId))
        {
            // رسالة واحدة لحالتين — «غير موجودة» و«لا تبلغها» — عمداً: التمييز بينهما
            // يجعل السطح عدّاد وجود لشركات مستأجرين آخرين.
            denied = HttpProblemResults.Code(
                context,
                "tenancy.company_out_of_scope",
                "هذا الاعتماد لا يبلغ الشركة المطلوبة.",
                "This credential does not reach the requested company.",
                "companyId");
            return false;
        }

        return true;
    }

    /// <summary>
    /// يقرأ رمز نوع المستند من المسار بفحص شكلي وحده.
    /// <para>
    /// وأي رمز غير معروف يمرّ من هنا ليُرفض في النواة برسالته التي <b>تسمّي المعروف</b> —
    /// لا هنا برسالة «مسار غير صالح»: السطح لا يملك الكتالوج ولا يجوز أن يملكه.
    /// </para>
    /// </summary>
    /// <param name="context">سياق الطلب.</param>
    /// <param name="documentType">الرمز عند النجاح.</param>
    /// <param name="malformed">استجابة الرفض عند الفشل.</param>
    public static bool TryDocumentType(HttpContext context, out string documentType, out IResult? malformed)
    {
        ArgumentNullException.ThrowIfNull(context);

        malformed = null;
        documentType = context.Request.RouteValues.TryGetValue("documentType", out object? value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;

        bool acceptable = documentType.Length is > 0 and <= MaximumDocumentTypeLength
            && documentType.All(static c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '.' or '_');

        if (!acceptable)
        {
            malformed = HttpProblemResults.Code(
                context,
                "wire.path.malformed",
                "رمز نوع المستند في المسار يُكتب بأحرف لاتينية صغيرة وأرقام ونقطة وشرطة سفلية، "
                + "وطوله بين محرف و64 محرفاً.",
                "The document type in the path is written with lower-case Latin letters, digits, dots, and "
                + "underscores, between one and 64 characters long.",
                "documentType",
                StatusCodes.Status400BadRequest);
            return false;
        }

        return true;
    }

    /// <summary>
    /// يقرأ رمز مركز التكلفة من المسار بفحص شكلي وحده.
    /// <para>
    /// وأي رمز غير موجود يمرّ من هنا ليُرفض في النواة بـ<c>cost_center.not_found</c> —
    /// لا هنا برسالة «مسار غير صالح»: السطح لا يملك سجلّ المنشأة ولا يجوز أن يملكه.
    /// </para>
    /// </summary>
    /// <param name="context">سياق الطلب.</param>
    /// <param name="code">الرمز عند النجاح.</param>
    /// <param name="malformed">استجابة الرفض عند الفشل.</param>
    public static bool TryCostCenterCode(HttpContext context, out string code, out IResult? malformed)
    {
        ArgumentNullException.ThrowIfNull(context);

        malformed = null;
        code = context.Request.RouteValues.TryGetValue("costCenterCode", out object? value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;

        if (!CostCenterCode.IsWellFormed(code))
        {
            malformed = HttpProblemResults.Code(
                context,
                "wire.path.malformed",
                "رمز مركز التكلفة في المسار يُكتب بأحرف لاتينية صغيرة وأرقام ونقطة وشرطة سفلية، "
                + "وطوله بين محرف و32 محرفاً.",
                "The cost centre code in the path is written with lower-case Latin letters, digits, dots, and "
                + "underscores, between one and 32 characters long.",
                "costCenterCode",
                StatusCodes.Status400BadRequest);
            return false;
        }

        return true;
    }

    /// <summary>
    /// يقرأ معرّفاً من المسار بفحص شكلي وحده، <b>باسم الوسيط في الرسالة</b>.
    /// <para>
    /// وموضعٌ واحد لكل معرّفات المسار: نسخةٌ لكل مورد كانت ستُنتج رسائل تختلف في
    /// صياغتها ورموزها بين بابٍ وباب على العطل نفسه — وهو ما يجعل عميلاً يعالج
    /// «معرّف مشوّه» في مسار ويتجاهله في آخر.
    /// </para>
    /// <para>
    /// <b>والمعرّف الذي لا يوجد يمرّ من هنا</b> ليُرفض في الوحدة بـ<c>not_found</c>:
    /// السطح لا يملك جداول الوحدة ولا يجوز أن يسأل عنها.
    /// </para>
    /// </summary>
    /// <param name="context">سياق الطلب.</param>
    /// <param name="name">اسم وسيط المسار.</param>
    /// <param name="id">المعرّف عند النجاح.</param>
    /// <param name="malformed">استجابة الرفض عند الفشل.</param>
    public static bool TryRouteId(HttpContext context, string name, out Guid id, out IResult? malformed)
    {
        ArgumentNullException.ThrowIfNull(context);

        malformed = null;
        string raw = context.Request.RouteValues.TryGetValue(name, out object? value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;

        if (!Guid.TryParseExact(raw, "D", out id) || id == Guid.Empty)
        {
            malformed = HttpProblemResults.Code(
                context,
                "wire.guid.malformed",
                "المعرّف في المسار ليس معرّفاً صالحاً بصيغة 8-4-4-4-12.",
                "The identifier in the path is not a valid 8-4-4-4-12 identifier.",
                name,
                StatusCodes.Status400BadRequest);
            return false;
        }

        return true;
    }

    /// <summary>
    /// يقرأ وسيط استعلام بفحص شكلي واحد لكل السطح.
    /// <para>
    /// <b>والوسيط المكرَّر يُرفض</b> ولا يُختار أوّله صامتاً: الاختيار الصامت هو ما
    /// يجعل تسميم الوسائط ممكناً — وسيطٌ ثانٍ يضيفه وسيطٌ في الطريق فيُقرأ بدل الأول
    /// أو يُهمل، وكلاهما بلا إعلان.
    /// </para>
    /// </summary>
    /// <param name="context">سياق الطلب.</param>
    /// <param name="name">اسم الوسيط.</param>
    /// <param name="required">هل هو إلزامي؟</param>
    /// <param name="maxLength">أقصى طول مقبول.</param>
    /// <exception cref="WireFormatException">الوسيط مفقود أو مكرَّر أو خارج الطول.</exception>
    public static string? Query(HttpContext context, string name, bool required, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(context);

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

    /// <summary>يترجم فشل التسلسل إلى رفض بحقله ورمزه، ولا يسرّب شيئاً عن الخادم.</summary>
    /// <param name="context">سياق الطلب.</param>
    /// <param name="exception">استثناء التسلسل.</param>
    public static IResult BadJson(HttpContext context, System.Text.Json.JsonException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        WireFormatException? wire = WireFormatException.Unwrap(exception);
        if (wire is not null)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        // رسالة System.Text.Json تسمّي المسار داخل الحمولة (‏$.lines[0].amount) وهي معلومة
        // العميل نفسه، لا معلومة خادم. أما ما عداها فلا يعبر: نص الاستثناء نفسه لا يُرسَل.
        string path = exception.Path ?? "$";
        return HttpProblemResults.Code(
            context,
            "wire.body.malformed",
            $"جسم الطلب لا يطابق العقد عند «{path}». والحقل غير المعروف يُرفض الطلب بسببه: "
            + "التجاهل الصامت يجعل العميل يظنّ أنه أرسل ما لم يصل.",
            $"The request body does not match the contract at '{path}'. An unknown field fails the whole request: "
            + "silently ignoring it makes the client believe it sent something that never arrived.",
            path);
    }
}
