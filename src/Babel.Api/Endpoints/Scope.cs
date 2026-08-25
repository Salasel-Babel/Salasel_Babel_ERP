using Babel.Api.Errors;
using Babel.Api.Security;
using Babel.Api.Wire;

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
