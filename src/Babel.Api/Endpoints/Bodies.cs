using System.Text.Json;
using Babel.Api.Errors;
using Babel.Api.Hosting;

namespace Babel.Api.Endpoints;

/// <summary>
/// قراءة جسم الطلب وترجمة رفضه الشكلي — <b>موضع واحد لكل نقاط النهاية</b>.
/// <para>
/// وهو موضعٌ واحد للسبب الذي كُتب من أجله <see cref="Scope"/> نفسه: قارئُ جسمٍ منسوخ
/// في ملفّين ينحرف في أحدهما عند أول تعديل، فيصير جسمٌ مفقود يُردّ عليه بـ<c>400</c>
/// هنا وبـ<c>500</c> هناك — والعميل يقرأ الفرق سلوكاً لا سهواً.
/// </para>
/// </summary>
internal static class Bodies
{
    /// <summary>
    /// يقرأ جسم الطلب مستنداً من نوع معلوم، أو يُرجع رفضاً شكلياً جاهزاً.
    /// </summary>
    /// <typeparam name="TBody">نوع الجسم.</typeparam>
    /// <param name="context">سياق الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<(TBody? Body, IResult? Refused)> ReadAsync<TBody>(
        HttpContext context,
        CancellationToken cancellationToken)
        where TBody : class
    {
        ArgumentNullException.ThrowIfNull(context);

        TBody? dto;
        try
        {
            dto = await context.Request
                .ReadFromJsonAsync<TBody>(ApiJson.Options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            return (null, Scope.BadJson(context, exception));
        }

        return dto is null
            ? (null, HttpProblemResults.Code(
                context, "wire.body.missing", "جسم الطلب مفقود.", "The request body is missing."))
            : (dto, null);
    }
}
