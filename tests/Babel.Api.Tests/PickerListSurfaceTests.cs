using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// قوائم الاختيار الثلاث — العملاء والموردون والموظفون — من خارج العملية.
/// <para>
/// <b>ولكل إثباتٍ منشأتُه</b> (<see cref="ApiFixture.ListCompanies"/>): القائمة مسحٌ يشمل كل
/// ما في المنشأة، فطرفٌ يسجّله إثباتٌ آخر يدخل قائمة جاره ويغيّر عدّادها. والمنشأة تُؤسَّس
/// هنا لا في الخادم، كمنشآت التأسيس.
/// </para>
/// <para>
/// وما يُفحص واحدٌ في الثلاث: <b>غلافٌ بعدّاد</b> لا مصفوفة عارية، و<b>الترتيب بالرمز</b>،
/// و<b>الشكل نفسه</b> الذي يُقرأ به المورد الواحد — فالعنصر في القائمة هو بايتاتُ القراءة
/// المفردة بعينها، لا نسخةً ثانية تنحرف عنها.
/// </para>
/// </summary>
public sealed class PickerListSurfaceTests
{
    [Fact]
    public async Task قائمة_العملاء_غلافٌ_بعدّاد_مرتَّبٌ_بالرمز_بشكل_العميل_الواحد()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = await FoundAsync(api, ApiFixture.ListCompanies[0]);

        // ── ١ · منشأة بلا عملاء: غلافٌ فارغ لا 404 ────────────────────────────
        (JsonElement empty, _) = await ListAsync(api, Documents.Customers(company));
        Assert.Equal(0, empty.GetProperty("partyCount").GetInt32());
        Assert.Empty(empty.GetProperty("parties").EnumerateArray());

        // ── ٢ · عميلان بعكس ترتيب رمزيهما ────────────────────────────────────
        string second = await AddAsync(api, Documents.Customers(company), Documents.Customer("PICK-B"));
        string first = await AddAsync(api, Documents.Customers(company), Documents.Customer("PICK-A"));

        (JsonElement list, string listText) = await ListAsync(api, Documents.Customers(company));
        Console.WriteLine(listText);

        List<JsonElement> parties = [.. list.GetProperty("parties").EnumerateArray()];
        Assert.Equal(2, list.GetProperty("partyCount").GetInt32());
        Assert.Equal(["PICK-A", "PICK-B"], parties.Select(static p => p.GetProperty("code").GetString()));
        Assert.Equal([first, second], parties.Select(static p => p.GetProperty("id").GetString()));

        // ── ٣ · العنصر هو القراءة المفردة بعينها ─────────────────────────────
        Assert.Equal(await ReadAsync(api, Documents.Customer(company, first)), parties[0].GetRawText());

        // ‏null على العميل لا فراغ: الحقل لا يوجد عليه أصلاً.
        Assert.Equal(JsonValueKind.Null, parties[0].GetProperty("vatNumber").ValueKind);

        // ── ٤ · واعتمادٌ لا يبلغ المنشأة لا يرى قائمتها ───────────────────────
        await AssertOutOfScopeAsync(api, Documents.Customers(company));
    }

    [Fact]
    public async Task قائمة_الموردين_غلافٌ_بعدّاد_مرتَّبٌ_بالرمز_بشكل_المورد_الواحد()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = await FoundAsync(api, ApiFixture.ListCompanies[1]);

        (JsonElement empty, _) = await ListAsync(api, Documents.Suppliers(company));
        Assert.Equal(0, empty.GetProperty("partyCount").GetInt32());
        Assert.Empty(empty.GetProperty("parties").EnumerateArray());

        string second = await AddAsync(api, Documents.Suppliers(company), Documents.Supplier("PICK-B"));
        string first = await AddAsync(api, Documents.Suppliers(company), Documents.Supplier("PICK-A"));

        (JsonElement list, string listText) = await ListAsync(api, Documents.Suppliers(company));
        Console.WriteLine(listText);

        List<JsonElement> parties = [.. list.GetProperty("parties").EnumerateArray()];
        Assert.Equal(2, list.GetProperty("partyCount").GetInt32());
        Assert.Equal(["PICK-A", "PICK-B"], parties.Select(static p => p.GetProperty("code").GetString()));
        Assert.Equal([first, second], parties.Select(static p => p.GetProperty("id").GetString()));

        Assert.Equal(await ReadAsync(api, Documents.Supplier(company, first)), parties[0].GetRawText());

        // والمورد يحمل رقمه الضريبي كما سُجّل.
        Assert.Equal("300000000000003", parties[0].GetProperty("vatNumber").GetString());

        await AssertOutOfScopeAsync(api, Documents.Suppliers(company));
    }

    [Fact]
    public async Task قائمة_الموظفين_غلافٌ_بعدّاد_مرتَّبٌ_بالرمز_والهوية_مقنَّعة_كالموظف_الواحد()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = await FoundAsync(api, ApiFixture.ListCompanies[2]);
        string costCenter = await Documents.DefaultCostCenterAsync(api, company, ApiFixture.TokenL);

        (JsonElement empty, _) = await ListAsync(api, Employees(company));
        Assert.Equal(0, empty.GetProperty("itemCount").GetInt32());
        Assert.Empty(empty.GetProperty("items").EnumerateArray());

        // الرمز يسكّه الخادم معتماً، فلا يُفترض ترتيبُ تسجيلٍ — بل يُفحص أن القائمة مرتَّبة به.
        string one = await AddAsync(api, Employees(company), Employee(costCenter, "1012345678", "SA0380000000608010167519"));
        string two = await AddAsync(api, Employees(company), Employee(costCenter, "2098765432", "SA4420000001234567891234"));

        (JsonElement list, string listText) = await ListAsync(api, Employees(company));
        Console.WriteLine(listText);

        List<JsonElement> items = [.. list.GetProperty("items").EnumerateArray()];
        Assert.Equal(2, list.GetProperty("itemCount").GetInt32());

        List<string> codes = [.. items.Select(static i => i.GetProperty("code").GetString()!)];
        Assert.Equal(codes.Order(StringComparer.Ordinal), codes);
        Assert.Equal(
            new[] { one, two }.Order(StringComparer.Ordinal),
            items.Select(static i => i.GetProperty("id").GetString()).Order(StringComparer.Ordinal));

        // ── العنصر هو القراءة المفردة بعينها — والقناع نفسه ───────────────────
        JsonElement firstItem = items[0];
        Assert.Equal(await ReadAsync(api, Employees(company) + "/" + firstItem.GetProperty("id").GetString()), firstItem.GetRawText());

        // ولا رقمٌ شخصي كامل في القائمة كلّها: آخر أربعة محارف وحدها، وما قبلها نجوم.
        Assert.DoesNotContain("1012345678", listText, StringComparison.Ordinal);
        Assert.DoesNotContain("2098765432", listText, StringComparison.Ordinal);
        Assert.DoesNotContain("SA0380000000608010167519", listText, StringComparison.Ordinal);
        Assert.All(items, static item =>
        {
            JsonElement identity = item.GetProperty("identity");
            Assert.StartsWith("••••", identity.GetProperty("nationalIdMask").GetString(), StringComparison.Ordinal);
            Assert.StartsWith("••••", identity.GetProperty("ibanMask").GetString(), StringComparison.Ordinal);
        });

        // والحالة كما تُكتب على السلك — رمزٌ كبير الحروف يقرؤه برنامج، لا نصٌّ يُعرض.
        Assert.All(items, static item => Assert.Equal("ACTIVE", item.GetProperty("state").GetString()));

        await AssertOutOfScopeAsync(api, Employees(company));
    }

    // ── أدوات ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// يؤسّس منشأة الإثبات. ‏201 أول مرّة، و409 إن كان خادمٌ آخر على القاعدة نفسها أسّسها —
    /// وكلاهما مقبول، كما في تأسيس منشآت الخادم.
    /// </summary>
    private static async Task<Guid> FoundAsync(ApiProcess api, Guid company)
    {
        using HttpResponseMessage founded = await api.Call(Http.Request(
            HttpMethod.Put,
            string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/setup"),
            ApiFixture.TokenL,
            """{"companyNameAr":"منشأة قوائم الاختيار","costCenters":"One","decimalPlaces":2,"currencyCode":"SAR"}"""));

        (string text, _) = await Http.BodyAsync(founded);
        Assert.True(
            founded.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
            "تأسيس منشأة القائمة: " + text);

        return company;
    }

    private static async Task<string> AddAsync(ApiProcess api, string path, string body)
    {
        using HttpResponseMessage response = await api.Call(Http.Request(HttpMethod.Post, path, ApiFixture.TokenL, body));

        (string text, JsonElement created) = await Http.BodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.Created, "التسجيل: " + text);
        return created.GetProperty("id").GetString()!;
    }

    private static async Task<(JsonElement List, string Text)> ListAsync(ApiProcess api, string path)
    {
        using HttpResponseMessage response = await api.Call(Http.Request(HttpMethod.Get, path, ApiFixture.TokenL));

        (string text, JsonElement list) = await Http.BodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.OK, "القائمة: " + text);
        return (list, text);
    }

    /// <summary>القراءة المفردة <b>نصّاً خاماً</b> — لتُقارَن بايتاتها بعنصر القائمة.</summary>
    private static async Task<string> ReadAsync(ApiProcess api, string path)
    {
        using HttpResponseMessage response = await api.Call(Http.Request(HttpMethod.Get, path, ApiFixture.TokenL));

        (string text, JsonElement read) = await Http.BodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.OK, "القراءة المفردة: " + text);
        return read.GetRawText();
    }

    private static async Task AssertOutOfScopeAsync(ApiProcess api, string path)
    {
        using HttpResponseMessage response = await api.Call(Http.Request(HttpMethod.Get, path, ApiFixture.TokenB));

        (_, JsonElement problem) = await Http.BodyAsync(response);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("tenancy.company_out_of_scope", Http.CodeOf(problem));
    }

    private static string Employees(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/employees");

    /// <summary>حمولة موظف — <b>ولا رمز فيها</b>: الخادم يسكّه.</summary>
    private static string Employee(string costCenter, string nationalId, string iban) => $$$"""
        {"nameAr":"موظف قائمة الاختيار","nameTranslations":[{"name":"en","value":"Picker employee"}],
         "classCode":"SAUDI","costCenterId":"{{{costCenter}}}","hiredOn":"2026-01-01",
         "identity":{"nationalId":"{{{nationalId}}}","iban":"{{{iban}}}","birthDate":"1990-01-01"}}
        """;
}
