using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>ثوابت الشركة على السلك: تُقرأ فارغةً لا 404، وتُستبدل كلّها، والرقم يُخصَّص من عدّاد.</b>
/// </summary>
public sealed class CompanyPresetTests
{
    [Fact]
    public async Task الثوابت_تُقرأ_فارغة_ثم_تُستبدل_وتُرفض_بمفتاح_خارج_الكتالوج()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiFixture.SetupCompanies[14];

        using HttpResponseMessage empty = await api.Call(Http.Request(HttpMethod.Get, Presets(company), ApiFixture.TokenS));
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        (_, JsonElement before) = await Http.BodyAsync(empty);
        Assert.Empty(before.GetProperty("values").EnumerateArray());
        Assert.Contains(before.GetProperty("catalogue").EnumerateArray(), key => key.GetProperty("key").GetString() == "tax.rate");
        Assert.Contains(before.GetProperty("series").EnumerateArray(), series => series.GetProperty("code").GetString() == "sales_invoice");

        using HttpResponseMessage replaced = await api.Call(Http.Request(
            HttpMethod.Put, Presets(company), ApiFixture.TokenS,
            """{"values":[{"name":"tax.rate","value":"0.15"},{"name":"branch.default","value":"BR-01"},{"name":"warehouse.default","value":""}]}"""));
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        (_, JsonElement after) = await Http.BodyAsync(replaced);
        Assert.Equal(2, after.GetProperty("values").GetArrayLength());

        using HttpResponseMessage refused = await api.Call(Http.Request(
            HttpMethod.Put, Presets(company), ApiFixture.TokenS,
            """{"values":[{"name":"coffee.default","value":"latte"}]}"""));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        (_, JsonElement problem) = await Http.BodyAsync(refused);
        Assert.Equal("company_presets.unknown_key", Http.CodeOf(problem));

        // الرفض لم يكتب شيئاً: القراءة بعده ترى الاستبدال الناجح كما هو.
        using HttpResponseMessage read = await api.Call(Http.Request(HttpMethod.Get, Presets(company), ApiFixture.TokenS));
        (_, JsonElement kept) = await Http.BodyAsync(read);
        Assert.Equal(2, kept.GetProperty("values").GetArrayLength());
    }

    [Fact]
    public async Task الرقم_يُخصَّص_متتابعاً_بالبادئة_المضبوطة_وسلسلة_مجهولة_404()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiFixture.SetupCompanies[15];

        using HttpResponseMessage first = await api.Call(Http.Request(
            HttpMethod.Post, Numbers(company, "sales_invoice"), ApiFixture.TokenS, """{"on":"2026-10-02"}"""));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        (_, JsonElement one) = await Http.BodyAsync(first);
        Assert.Equal("INV-2026-0001", one.GetProperty("number").GetString());

        using HttpResponseMessage prefixed = await api.Call(Http.Request(
            HttpMethod.Put, Presets(company), ApiFixture.TokenS, """{"values":[{"name":"number.sales_invoice","value":"SB"}]}"""));
        Assert.Equal(HttpStatusCode.OK, prefixed.StatusCode);

        using HttpResponseMessage second = await api.Call(Http.Request(
            HttpMethod.Post, Numbers(company, "sales_invoice"), ApiFixture.TokenS, """{"on":"2026-12-31"}"""));
        (_, JsonElement two) = await Http.BodyAsync(second);
        Assert.Equal("SB-2026-0002", two.GetProperty("number").GetString());
        Assert.Equal(2, two.GetProperty("sequence").GetInt32());

        using HttpResponseMessage unknown = await api.Call(Http.Request(
            HttpMethod.Post, Numbers(company, "lottery"), ApiFixture.TokenS, """{"on":"2026-10-02"}"""));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        (_, JsonElement problem) = await Http.BodyAsync(unknown);
        Assert.Equal("company_presets.unknown_series", Http.CodeOf(problem));
    }

    private static string Presets(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/presets");

    private static string Numbers(Guid company, string series) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/presets/numbers/{series}");
}
