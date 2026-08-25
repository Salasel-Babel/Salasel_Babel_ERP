using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>تأسيس المنشأة عند حدّ HTTP — من خارج العملية.</b>
/// <para>
/// ولكل اختبار هنا <b>منشأته الخاصة</b>: التأسيس يُقبل مرّة واحدة بحكم القرار نفسه،
/// فمنشأةٌ مشتركة بين اختبارين تجعل الثاني يمرّ أو يسقط بحسب من سبقه.
/// </para>
/// </summary>
public sealed class CompanySetupTests
{
    [Fact]
    public async Task المنشأة_البسيطة_تُؤسَّس_بجواب_واحد_ويصير_اسمها_مركز_تكلفتها()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = Company(0);

        using HttpResponseMessage created = await api.Call(Http.Request(
            HttpMethod.Put,
            Setup(company),
            ApiFixture.TokenS,
            """{"companyNameAr":"بوفيه الفرات","costCenters":"One","decimalPlaces":2}"""));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        (_, JsonElement body) = await Http.BodyAsync(created);

        Assert.Equal("بوفيه الفرات", body.GetProperty("nameAr").GetString());
        Assert.Equal(2, body.GetProperty("decimalPlaces").GetInt32());
        Assert.Equal("cc.001", body.GetProperty("defaultCostCenter").GetString());

        JsonElement centre = Assert.Single(body.GetProperty("costCenters").EnumerateArray());
        Assert.Equal("cc.001", centre.GetProperty("code").GetString());
        Assert.Equal("بوفيه الفرات", centre.GetProperty("nameAr").GetString());
        Assert.Equal("Active", centre.GetProperty("state").GetString());
        Assert.True(centre.GetProperty("isDefault").GetBoolean());
    }

    [Fact]
    public async Task التأسيس_الثاني_يُرفض_بـ409_ولا_يُغيّر_عدد_الخانات()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = Company(1);

        using HttpResponseMessage first = await api.Call(Http.Request(
            HttpMethod.Put, Setup(company), ApiFixture.TokenS,
            """{"companyNameAr":"مؤسسة الرافدين","costCenters":"One","decimalPlaces":2}"""));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using HttpResponseMessage second = await api.Call(Http.Request(
            HttpMethod.Put, Setup(company), ApiFixture.TokenS,
            """{"companyNameAr":"مؤسسة الرافدين","costCenters":"One","decimalPlaces":4}"""));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        (_, JsonElement problem) = await Http.BodyAsync(second);
        Assert.Equal("company_setup.already_initialised", Http.CodeOf(problem));

        // والمخزَّن لم يتحرّك خانةً واحدة.
        using HttpResponseMessage read = await api.Call(
            Http.Request(HttpMethod.Get, Setup(company), ApiFixture.TokenS));

        (_, JsonElement body) = await Http.BodyAsync(read);
        Assert.Equal(2, body.GetProperty("decimalPlaces").GetInt32());
    }

    [Fact]
    public async Task الجواب_متعدّد_بلا_اسم_أول_مركز_يُرفض_بـ422()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Put, Setup(Company(2)), ApiFixture.TokenS,
            """{"companyNameAr":"شركة المقاولات","costCenters":"Multiple","decimalPlaces":2}"""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        (_, JsonElement problem) = await Http.BodyAsync(response);
        Assert.Equal("company_setup.first_cost_center_name_required", Http.CodeOf(problem));
    }

    [Fact]
    public async Task عدد_خانات_خارج_المدى_يُرفض_ويُسمّي_المدى()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Put, Setup(Company(3)), ApiFixture.TokenS,
            """{"companyNameAr":"شركة ما","costCenters":"One","decimalPlaces":7}"""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Assert.Equal("company_setup.decimal_places_out_of_range", Http.CodeOf(problem));
        Assert.Contains("0–4", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task جواب_مراكز_التكلفة_مجموعة_مغلقة_ولا_يُقبل_غيرها()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Put, Setup(Company(4)), ApiFixture.TokenS,
            """{"companyNameAr":"شركة ما","costCenters":"maybe","decimalPlaces":2}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        (_, JsonElement problem) = await Http.BodyAsync(response);
        Assert.Equal("wire.body.malformed", Http.CodeOf(problem));
    }

    [Fact]
    public async Task المركز_الافتراضي_لا_يُوقَف_والموقوف_يبقى_في_القائمة()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = Company(5);

        await FoundAsync(api, company, """{"companyNameAr":"شركة الفروع","costCenters":"Multiple","firstCostCenterNameAr":"الإدارة العامة","decimalPlaces":2}""");

        using HttpResponseMessage added = await api.Call(Http.Request(
            HttpMethod.Post, CostCenters(company), ApiFixture.TokenS, """{"nameAr":"فرع جدة"}"""));

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        // ١ — الافتراضي مرفوض إيقافه.
        using HttpResponseMessage refused = await api.Call(Http.Request(
            HttpMethod.Post, Suspension(company, "cc.001"), ApiFixture.TokenS,
            """{"reason":"إعادة هيكلة الإدارة"}"""));

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        (_, JsonElement problem) = await Http.BodyAsync(refused);
        Assert.Equal("cost_center.default_cannot_be_suspended", Http.CodeOf(problem));

        // ٢ — وغير الافتراضي يُوقَف بسبب مكتوب، ويبقى في القائمة.
        using HttpResponseMessage suspended = await api.Call(Http.Request(
            HttpMethod.Post, Suspension(company, "cc.002"), ApiFixture.TokenS,
            """{"reason":"أُغلق الفرع نهائياً"}"""));

        Assert.Equal(HttpStatusCode.Created, suspended.StatusCode);
        (_, JsonElement body) = await Http.BodyAsync(suspended);

        JsonElement[] centres = [.. body.GetProperty("costCenters").EnumerateArray()];
        Assert.Equal(2, centres.Length);
        Assert.Equal("Suspended", centres[1].GetProperty("state").GetString());
        Assert.Equal("أُغلق الفرع نهائياً", centres[1].GetProperty("suspensionReason").GetString());
        Assert.Equal("cc.001", body.GetProperty("defaultCostCenter").GetString());
    }

    [Fact]
    public async Task الإيقاف_بلا_سبب_مكتوب_مرفوض()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = Company(6);

        await FoundAsync(api, company, """{"companyNameAr":"شركة الفروع","costCenters":"Multiple","firstCostCenterNameAr":"الإدارة","decimalPlaces":2}""");
        using (await api.Call(Http.Request(HttpMethod.Post, CostCenters(company), ApiFixture.TokenS, """{"nameAr":"فرع جدة"}""")))
        {
        }

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Suspension(company, "cc.002"), ApiFixture.TokenS, """{"reason":"قصير"}"""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        (_, JsonElement problem) = await Http.BodyAsync(response);
        Assert.Equal("cost_center.suspension_reason_required", Http.CodeOf(problem));
    }

    [Fact]
    public async Task إعادة_التسمية_لا_تمسّ_الرمز_ولا_صفة_الافتراضي()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = Company(7);

        await FoundAsync(api, company, """{"companyNameAr":"مؤسسة النخيل","costCenters":"One","decimalPlaces":3}""");

        using HttpResponseMessage renamed = await api.Call(Http.Request(
            HttpMethod.Put, CostCenter(company, "cc.001"), ApiFixture.TokenS,
            """{"nameAr":"الإدارة المالية","nameTranslations":[{"name":"en","value":"Finance"},{"name":"ur","value":"مالیات"}]}"""));

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        (_, JsonElement body) = await Http.BodyAsync(renamed);

        JsonElement centre = Assert.Single(body.GetProperty("costCenters").EnumerateArray());
        Assert.Equal("cc.001", centre.GetProperty("code").GetString());
        Assert.Equal("الإدارة المالية", centre.GetProperty("nameAr").GetString());
        Assert.True(centre.GetProperty("isDefault").GetBoolean());
        Assert.Equal(["en", "ur"], centre.GetProperty("nameTranslations").EnumerateArray()
            .Select(static entry => entry.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task مركز_تكلفة_غير_موجود_يُرفض_بـ404_ورمز_المسار_المعطوب_بـ400()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = Company(8);

        await FoundAsync(api, company, """{"companyNameAr":"مؤسسة ما","costCenters":"One","decimalPlaces":2}""");

        using HttpResponseMessage missing = await api.Call(Http.Request(
            HttpMethod.Post, Suspension(company, "cc.999"), ApiFixture.TokenS, """{"reason":"سبب مكتوب كافٍ"}"""));

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        (_, JsonElement notFound) = await Http.BodyAsync(missing);
        Assert.Equal("cost_center.not_found", Http.CodeOf(notFound));

        using HttpResponseMessage malformed = await api.Call(Http.Request(
            HttpMethod.Post, Suspension(company, "CC%20001"), ApiFixture.TokenS, """{"reason":"سبب مكتوب كافٍ"}"""));

        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        (_, JsonElement bad) = await Http.BodyAsync(malformed);
        Assert.Equal("wire.path.malformed", Http.CodeOf(bad));
    }

    [Fact]
    public async Task القراءة_قبل_التأسيس_تُرجع_404_ولا_تُنشئ_منشأة_ضمناً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = Company(9);

        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Get, Setup(company), ApiFixture.TokenS));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        (_, JsonElement problem) = await Http.BodyAsync(response);
        Assert.Equal("company_setup.not_found", Http.CodeOf(problem));

        // ولا يُنشئ الاستعلام شيئاً: الثاني يُرجع 404 أيضاً.
        using HttpResponseMessage again = await api.Call(
            Http.Request(HttpMethod.Get, Setup(company), ApiFixture.TokenS));

        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task تأسيس_منشأة_لا_يبلغها_الاعتماد_مرفوض_قبل_قراءة_الجسم()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Put, Setup(ApiTestDatabase.CompanyA), ApiFixture.TokenS,
            """{"companyNameAr":"اختطاف","costCenters":"One","decimalPlaces":2}"""));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        (_, JsonElement problem) = await Http.BodyAsync(response);
        Assert.Equal("tenancy.company_out_of_scope", Http.CodeOf(problem));
    }

    [Fact]
    public async Task عدد_الخانات_لا_يمسّ_مقياس_المال_على_السلك()
    {
        // منشأة بخانتين — والمال يبقى يصل ويخرج بمقياس Money لا بمقياس العرض.
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = Company(10);

        await FoundAsync(api, company, """{"companyNameAr":"مؤسسة الخانتين","costCenters":"One","decimalPlaces":2}""");

        using HttpResponseMessage read = await api.Call(
            Http.Request(HttpMethod.Get, Setup(company), ApiFixture.TokenS));

        (string text, JsonElement body) = await Http.BodyAsync(read);

        Assert.Equal(2, body.GetProperty("decimalPlaces").GetInt32());

        // ولا حقل مال واحد في هذا المورد: التأسيس يصف العرض، ولا يحمل مبلغاً.
        Assert.DoesNotContain("amount", text, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task FoundAsync(ApiProcess api, Guid company, string json)
    {
        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Put, Setup(company), ApiFixture.TokenS, json));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static Guid Company(int index) => ApiFixture.SetupCompanies[index];

    private static string Setup(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/setup");

    private static string CostCenters(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/cost-centers");

    private static string CostCenter(Guid company, string code) => CostCenters(company) + "/" + code;

    private static string Suspension(Guid company, string code) => CostCenter(company, code) + "/suspension";
}
