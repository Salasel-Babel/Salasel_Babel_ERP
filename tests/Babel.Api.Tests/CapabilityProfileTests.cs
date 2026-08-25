using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>ملفّ القدرات عند حدّ HTTP — من خارج العملية.</b>
/// <para>
/// كل اختبار هنا يبذر ملفّه بنفسه قبل أن يقرأ: لا اختبار يعتمد على ملفّ كتبه اختبار آخر،
/// ولا على ترتيب تنفيذ. وهذا شرط لا تحسين — «‏0 فشل» في تشغيل كامل ليست جملةً عن صحّة
/// المجموعة بل عن صحّتها بترتيب واحد.
/// </para>
/// </summary>
public sealed class CapabilityProfileTests
{
    private const string Buffet = """
        {"documents":[{"documentType":"sales.invoice",
          "capabilities":[{"capability":"advance","enabled":false},{"capability":"cost_of_sales","enabled":false}],
          "defaults":[{"name":"paymentMethod","value":"cash"}]}]}
        """;

    private const string Contractor = """
        {"documents":[
          {"documentType":"projects.client_certificate",
           "capabilities":[{"capability":"advance","enabled":true},{"capability":"retention","enabled":true}]},
          {"documentType":"sales.invoice",
           "capabilities":[{"capability":"advance","enabled":true},{"capability":"cost_of_sales","enabled":true}],
           "defaults":[{"name":"paymentMethod","value":"bank"},{"name":"warehouse","value":"W1"}]}]}
        """;

    [Fact]
    public async Task ملفّ_البوفيه_يُحفظ_ويُقرأ_بشكل_ثلاثة_حقول_ولا_قدرة()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        await SaveAsync(api, Buffet);

        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Get, Profile(ApiTestDatabase.CompanyA), ApiFixture.TokenA));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        (_, JsonElement body) = await Http.BodyAsync(response);

        JsonElement invoice = Assert.Single(body.GetProperty("documents").EnumerateArray());
        Assert.Equal("sales.invoice", invoice.GetProperty("documentType").GetString());
        Assert.Equal(["customer", "lines", "paymentMethod"], Strings(invoice, "fields"));
        Assert.Empty(invoice.GetProperty("enabledCapabilities").EnumerateArray());

        // والقدرات المتاحة معروضة رغم إطفائها: الشاشة تعرف ما يمكن تشغيله، لا ما شُغِّل فقط.
        Assert.Equal(["advance", "cost_of_sales"], Strings(invoice, "availableCapabilities"));
    }

    [Fact]
    public async Task ملفّ_المقاول_يُنتج_شكلين_أوسع_من_الكتالوج_نفسه()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        await SaveAsync(api, Contractor);

        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Get, Shape(ApiTestDatabase.CompanyA, "sales.invoice"), ApiFixture.TokenA));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        (_, JsonElement invoice) = await Http.BodyAsync(response);

        Assert.Equal(["advanceApplied", "customer", "lines", "paymentMethod", "warehouse"], Strings(invoice, "fields"));
        Assert.Equal(["advance", "cost_of_sales"], Strings(invoice, "enabledCapabilities"));

        using HttpResponseMessage certificate = await api.Call(
            Http.Request(HttpMethod.Get, Shape(ApiTestDatabase.CompanyA, "projects.client_certificate"), ApiFixture.TokenA));

        (_, JsonElement shape) = await Http.BodyAsync(certificate);
        Assert.Equal(["advanceRecovery", "contract", "retention", "workValue"], Strings(shape, "fields"));
        Assert.Equal("Projects", shape.GetProperty("module").GetString());

        // الاسم المعروض ليس ثنائية لغتين: عربيةٌ إلزامية هي الارتداد، ومفتاح ترجمة إلى أيّ لغة.
        Assert.Equal("مستخلص عميل", shape.GetProperty("nameAr").GetString());
        Assert.Equal("document_type.projects.client_certificate", shape.GetProperty("nameKey").GetString());
        Assert.False(shape.TryGetProperty("nameEn", out _), "حقل لغة ثانية في السلك — تعدّد اللغات عرضٌ لا عمود.");
    }

    [Fact]
    public async Task البوفيه_يرفض_مستنداً_بدفعة_مقدمة_والمقاول_يقبل_المستند_نفسه()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        const string document = """{"fields":["customer","lines","paymentMethod","advanceApplied"]}""";

        await SaveAsync(api, Buffet);

        using HttpResponseMessage refused = await api.Call(Http.Request(
            HttpMethod.Post, Admission(ApiTestDatabase.CompanyA, "sales.invoice"), ApiFixture.TokenA, document));

        Assert.Equal(HttpStatusCode.UnprocessableContent, refused.StatusCode);
        (_, JsonElement problem) = await Http.BodyAsync(refused);
        Assert.Equal("document_admission.capability_not_enabled", Http.CodeOf(problem));
        Assert.Contains("advanceApplied", problem.GetProperty("detailAr").GetString()!, StringComparison.Ordinal);

        await SaveAsync(api, Contractor, reason: null);

        using HttpResponseMessage admitted = await api.Call(Http.Request(
            HttpMethod.Post, Admission(ApiTestDatabase.CompanyA, "sales.invoice"), ApiFixture.TokenA, document));

        Assert.Equal(HttpStatusCode.OK, admitted.StatusCode);
        (_, JsonElement verdict) = await Http.BodyAsync(admitted);
        Assert.True(verdict.GetProperty("admitted").GetBoolean());
        Assert.Equal(["advanceApplied", "customer", "lines", "paymentMethod"], Strings(verdict, "fields"));
    }

    [Fact]
    public async Task سحب_قدرة_يُرفض_بلا_سبب_مكتوب_ويمرّ_به()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        await SaveAsync(api, Contractor);

        using HttpResponseMessage refused = await api.Call(
            Http.Request(HttpMethod.Put, Profile(ApiTestDatabase.CompanyA), ApiFixture.TokenA, Buffet));

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        (_, JsonElement problem) = await Http.BodyAsync(refused);
        Assert.Equal("capability_profile.capability_withdrawal_requires_acknowledgement", Http.CodeOf(problem));

        // والملفّ لم يتغيّر: الرفض رفضٌ لا نصف حفظ.
        using HttpResponseMessage unchanged = await api.Call(
            Http.Request(HttpMethod.Get, Shape(ApiTestDatabase.CompanyA, "sales.invoice"), ApiFixture.TokenA));
        (_, JsonElement shape) = await Http.BodyAsync(unchanged);
        Assert.Equal(["advance", "cost_of_sales"], Strings(shape, "enabledCapabilities"));

        await SaveAsync(api, Buffet, "أُقفلت الدفعات المقدمة المفتوحة كلها ورصيد الحساب صفر");
    }

    [Fact]
    public async Task قدرة_غير_معروفة_ونوع_مستند_غير_معروف_يُرفضان_بأسمائهما()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage unknownCapability = await api.Call(Http.Request(
            HttpMethod.Put,
            Profile(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            """{"documents":[{"documentType":"sales.invoice","capabilities":[{"capability":"installments","enabled":true}]}]}"""));

        Assert.Equal(HttpStatusCode.UnprocessableContent, unknownCapability.StatusCode);
        (_, JsonElement first) = await Http.BodyAsync(unknownCapability);
        Assert.Equal("capability_profile.capability_unknown", Http.CodeOf(first));
        Assert.Contains("installments", first.GetProperty("detailAr").GetString()!, StringComparison.Ordinal);

        using HttpResponseMessage unknownType = await api.Call(Http.Request(
            HttpMethod.Put,
            Profile(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            """{"documents":[{"documentType":"sales.quotation","capabilities":[]}]}"""));

        (_, JsonElement second) = await Http.BodyAsync(unknownType);
        Assert.Equal("capability_profile.document_type_unknown", Http.CodeOf(second));
    }

    [Fact]
    public async Task حقل_غير_معروف_في_الجسم_يُرفض_الطلب_بسببه_ولا_يُتجاهل()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Put,
            Profile(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            """{"documents":[{"documentType":"sales.invoice","capabilities":[],"delivery":"staged"}]}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        (_, JsonElement problem) = await Http.BodyAsync(response);
        Assert.Equal("wire.body.malformed", Http.CodeOf(problem));
    }

    [Fact]
    public async Task اعتماد_لا_يبلغ_الشركة_لا_يقرأ_ملفّها_ولا_يكتبه()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage read = await api.Call(
            Http.Request(HttpMethod.Get, Profile(ApiTestDatabase.CompanyA), ApiFixture.TokenB));
        using HttpResponseMessage write = await api.Call(
            Http.Request(HttpMethod.Put, Profile(ApiTestDatabase.CompanyA), ApiFixture.TokenB, Buffet));

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);

        (_, JsonElement problem) = await Http.BodyAsync(write);
        Assert.Equal("tenancy.company_out_of_scope", Http.CodeOf(problem));
    }

    [Fact]
    public async Task رمز_نوع_مستند_مشوَّه_في_المسار_يُرفض_شكلاً_قبل_أي_عمل()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get,
            Shape(ApiTestDatabase.CompanyA, "SALES.Invoice"),
            ApiFixture.TokenA));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        (_, JsonElement problem) = await Http.BodyAsync(response);
        Assert.Equal("wire.path.malformed", Http.CodeOf(problem));
    }

    /// <summary>
    /// يبذر الملفّ الذي يحتاجه الاختبار — <b>ومعه سبب سحب دائماً</b>.
    /// <para>
    /// وليس ذلك تراخياً: الخادم مشترك بين اختبارات المجموعة، فقد يكون ملفّ الشركة أوسع
    /// مما يريده هذا الاختبار. والبذر الذي يسقط لأن اختباراً آخر سبقه هو بعينه العطل
    /// الذي يقيسه مسح العزل. والسبب هنا لا يُفحص إلا حين يقع سحب فعلاً — واختبار السحب
    /// أدناه يُرسل طلبه <b>بلا</b> سبب عمداً.
    /// </para>
    /// </summary>
    private static async Task SaveAsync(ApiProcess api, string body, string? reason = null)
    {
        string payload = body[..body.LastIndexOf('}')]
            + ",\"withdrawalReason\":\"" + (reason ?? "بذر حالة اختبار — الملفّ يُستبدل بالكامل") + "\"}";

        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Put, Profile(ApiTestDatabase.CompanyA), ApiFixture.TokenA, payload));

        (string text, _) = await Http.BodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"تعذّر حفظ الملفّ: {response.StatusCode}\n{text}");
    }

    private static string[] Strings(JsonElement element, string property) =>
        [.. element.GetProperty(property).EnumerateArray().Select(static value => value.GetString()!)];

    private static string Profile(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/capability-profile");

    private static string Shape(Guid company, string documentType) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/document-shapes/{documentType}");

    private static string Admission(Guid company, string documentType) =>
        Shape(company, documentType) + "/admissions";
}
