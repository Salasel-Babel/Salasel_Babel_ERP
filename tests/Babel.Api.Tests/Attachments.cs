using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Babel.Api.Tests;

/// <summary>
/// أدوات مخاطبة سطح المرفقات: المسارات، وبناء حمولة <c>multipart</c>، وبايتات نماذج.
/// <para>
/// <b>ولا مسار مكتوب بيد في اختبار.</b> مسارٌ يُكتب في عشرة اختبارات ينحرف في أحدها،
/// فيبقى اختبارٌ أخضر يخاطب باباً لا وجود له.
/// </para>
/// </summary>
internal static class Attachments
{
    /// <summary>بايتات JPEG صالحة الترويسة — <b>ثلاث بايتات سحرية فعلية</b> لا نصّ يُدّعى أنه صورة.</summary>
    public static byte[] Jpeg(int payload = 64) =>
        [0xFF, 0xD8, 0xFF, .. Enumerable.Range(0, payload).Select(static i => (byte)(i % 251))];

    /// <summary>بايتات PNG صالحة الترويسة.</summary>
    public static byte[] Png(int payload = 64) =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. Enumerable.Range(0, payload).Select(static i => (byte)(i % 241))];

    /// <summary>بايتات PDF صالحة الترويسة.</summary>
    public static byte[] Pdf(int payload = 64) =>
        [.. "%PDF-"u8.ToArray(), .. Enumerable.Range(0, payload).Select(static i => (byte)(i % 239))];

    /// <summary>مسار المورد الرئيسي.</summary>
    /// <param name="company">الشركة.</param>
    public static string Root(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/attachments");

    /// <summary>مسار مرفق واحد.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="attachment">المرفق.</param>
    public static string One(Guid company, string attachment) => Root(company) + "/" + attachment;

    /// <summary>مسار مورد الإصدارات.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="attachment">المرفق.</param>
    public static string Revisions(Guid company, string attachment) => One(company, attachment) + "/revisions";

    /// <summary>مسار مورد السحب.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="attachment">المرفق.</param>
    public static string Withdrawal(Guid company, string attachment) => One(company, attachment) + "/withdrawal";

    /// <summary>مسار سكّ التذاكر.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="attachment">المرفق.</param>
    public static string Tickets(Guid company, string attachment) => One(company, attachment) + "/download-tickets";

    /// <summary>مسار تنزيل البايتات بتذكرة.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="attachment">المرفق.</param>
    /// <param name="ticket">الرمز الموقّع.</param>
    public static string Content(Guid company, string attachment, string ticket) =>
        One(company, attachment) + "/content?ticket=" + Uri.EscapeDataString(ticket);

    /// <summary>
    /// يبني طلب إيداع بحمولة <c>multipart/form-data</c>.
    /// <para>
    /// <b>والاسم المُعلَن ونوع المحتوى المُعلَن ترويستا جزء البايتات</b> — لا حقلان في
    /// الجسم — وهو الشكل الطبيعي لـ<c>multipart</c> والشكل الذي ينشره العقد.
    /// </para>
    /// </summary>
    /// <param name="path">المسار.</param>
    /// <param name="credential">الاعتماد.</param>
    /// <param name="content">البايتات.</param>
    /// <param name="declaredFileName">الاسم المُعلَن، أو <c>null</c>.</param>
    /// <param name="declaredMediaType">النوع المُعلَن، أو <c>null</c>.</param>
    /// <param name="sourceDocumentType">رمز المستند المصدر، أو <c>null</c>.</param>
    /// <param name="sourceDocumentId">معرّف المستند المصدر، أو <c>null</c>.</param>
    /// <param name="partName">اسم جزء البايتات — يُغيَّر عمداً في اختبار الجزء المفقود.</param>
    public static HttpRequestMessage Deposit(
        string path,
        TestCredential credential,
        byte[] content,
        string? declaredFileName = "فاتورة.jpg",
        string? declaredMediaType = "image/jpeg",
        string? sourceDocumentType = null,
        string? sourceDocumentId = null,
        string partName = "content")
    {
        MultipartFormDataContent form = new("babel-test-boundary-" + Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture));

        ByteArrayContent bytes = new(content);
        if (declaredMediaType is not null)
        {
            bytes.Headers.ContentType = MediaTypeHeaderValue.Parse(declaredMediaType);
        }

        if (declaredFileName is null)
        {
            form.Add(bytes, partName);
        }
        else
        {
            form.Add(bytes, partName, declaredFileName);
        }

        if (sourceDocumentType is not null)
        {
            form.Add(new StringContent(sourceDocumentType, new UTF8Encoding(false)), "sourceDocumentType");
        }

        if (sourceDocumentId is not null)
        {
            form.Add(new StringContent(sourceDocumentId, new UTF8Encoding(false)), "sourceDocumentId");
        }

        HttpRequestMessage request = new(HttpMethod.Post, new Uri(path, UriKind.Relative)) { Content = form };
        request.Headers.TryAddWithoutValidation("Authorization", credential.Header);
        return request;
    }

    /// <summary>يودِع مرفقاً ويعيد معرّفه، ويرمي بنصّ الجسم إن لم يُقبل.</summary>
    /// <param name="api">الخادم.</param>
    /// <param name="company">الشركة.</param>
    /// <param name="credential">الاعتماد.</param>
    /// <param name="content">البايتات.</param>
    /// <param name="declaredFileName">الاسم المُعلَن.</param>
    /// <param name="declaredMediaType">النوع المُعلَن.</param>
    /// <param name="sourceDocumentType">رمز المستند المصدر.</param>
    /// <param name="sourceDocumentId">معرّف المستند المصدر.</param>
    public static async Task<JsonElement> DepositAsync(
        ApiProcess api,
        Guid company,
        TestCredential credential,
        byte[] content,
        string? declaredFileName = "فاتورة.jpg",
        string? declaredMediaType = "image/jpeg",
        string? sourceDocumentType = null,
        string? sourceDocumentId = null)
    {
        using HttpResponseMessage response = await api.Call(Deposit(
            Root(company), credential, content, declaredFileName, declaredMediaType, sourceDocumentType, sourceDocumentId));

        (string text, JsonElement body) = await Http.BodyAsync(response);

        return response.StatusCode == System.Net.HttpStatusCode.Created
            ? body
            : throw new InvalidOperationException("تعذّر إيداع المرفق: " + (int)response.StatusCode + " — " + text);
    }

    /// <summary>يسكّ تذكرة تنزيل ويعيد رمزها.</summary>
    /// <param name="api">الخادم.</param>
    /// <param name="company">الشركة.</param>
    /// <param name="credential">الاعتماد.</param>
    /// <param name="attachment">المرفق.</param>
    /// <param name="lifetimeSeconds">عمر التذكرة بالثواني.</param>
    public static async Task<string> TicketAsync(
        ApiProcess api,
        Guid company,
        TestCredential credential,
        string attachment,
        int lifetimeSeconds = 60)
    {
        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post,
            Tickets(company, attachment),
            credential,
            string.Create(CultureInfo.InvariantCulture, $$"""{"lifetimeSeconds":{{lifetimeSeconds}}}""")));

        (string text, JsonElement body) = await Http.BodyAsync(response);

        return response.StatusCode == System.Net.HttpStatusCode.Created
            ? body.GetProperty("token").GetString()!
            : throw new InvalidOperationException("تعذّر سكّ التذكرة: " + (int)response.StatusCode + " — " + text);
    }

    /// <summary>معرّف المرفق من جسم الاستجابة.</summary>
    /// <param name="body">الجسم.</param>
    public static string IdOf(JsonElement body) => body.GetProperty("id").GetString()!;
}
