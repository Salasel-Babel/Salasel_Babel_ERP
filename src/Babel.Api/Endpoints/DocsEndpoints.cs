using Babel.Api.OpenApi;

namespace Babel.Api.Endpoints;

/// <summary>
/// بابان للتوثيق: العقد المنشور نفسه، وصفحةٌ تستعرضه.
/// <para>
/// <b>ولا شيء هنا يقرّر شيئاً.</b> كلا المعالجَين يكتب مصفوفة بايتات ثابتة قُرئت مرّة
/// واحدة من موارد التجميعة: لا قاعدة بيانات، ولا مستأجر، ولا مدخل من العميل يُقرأ أو
/// يُعكَس في المخرَج. وهذا هو **كامل** السطح الجديد الذي يُفتح بلا اعتماد.
/// </para>
/// <para>
/// <b>ولماذا بلا اعتماد — والحجّة تقنية قبل أن تكون سياسة:</b> المتصفّح <b>لا يستطيع</b>
/// أن يضع ترويسة <c>Authorization</c> على تنقّلٍ عُلوي، أي على عنوانٍ يُكتب في شريط
/// العنوان. فصفحةُ توثيق محميّة بـ<c>Bearer</c> غير قابلة للفتح من متصفّح أصلاً، ولا
/// علاج لذلك إلا ملفّ ارتباط أو جلسة — أي <b>آلية تصريح ثانية</b> بجانب القائمة، وهي
/// أخطر من غيابها: تُصان إحداهما وتُنسى الأخرى (‏ADR-0036 · فخ-81). ومحتوى الوثيقة ليس
/// سرّاً بحال — هو ملفٌّ مُودَع في المستودع — ولا يحمل بيانات مستأجر واحد.
/// </para>
/// <para>
/// <b>وما لا يفتحه هذا الباب:</b> الصفحة عميلٌ كأي عميل. زرّ «جرّب» يُصدر طلباً عادياً
/// يمرّ بوسيط المصادقة ثم <c>Scope.TryCompany</c> ثم الاستحقاق — فشركةٌ خارج نطاق
/// الاعتماد تُرفض بـ<c>403 tenancy.company_out_of_scope</c> من الصفحة كما تُرفض من
/// <c>curl</c>، ولا مسار التفاف واحد. <b>ولا رمز مطبوع في الصفحة</b>: تُخدَم بايتاتها
/// نفسها لكل طالب، فارغةً من كل اعتماد.
/// </para>
/// </summary>
internal static class DocsEndpoints
{
    /// <summary>يسجّل بابَي التوثيق.</summary>
    /// <param name="app">مُنشئ المسارات.</param>
    public static void MapDocsApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(ApiRoutes.OpenApiDocument, Document);
        app.MapGet(ApiRoutes.Docs, Page);
    }

    /// <summary>
    /// العقد المُودَع، بايتاً بايت.
    /// <para>
    /// <c>Results.Bytes</c> لا <c>Results.Json</c>: التسلسل من جديد يعيد ترميز النصّ
    /// ويعيد ترتيب المسافات، فيخرج شيءٌ <b>يكافئ</b> المُودَع ولا <b>يطابقه</b> — وحارس
    /// هذا المستودع يقارن بايتات لا معانيَ، ولسببٍ وجيه.
    /// </para>
    /// </summary>
    private static IResult Document() =>
        Results.Bytes(PublishedContract.Bytes, "application/json; charset=utf-8");

    private static IResult Page() =>
        Results.Bytes(DocsPage.Bytes, "text/html; charset=utf-8");
}
