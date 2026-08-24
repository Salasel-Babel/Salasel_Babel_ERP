using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>المال على السلك — إثبات أن قناة فقدان الدقّة مُغلقة، لا أن المسار السعيد يعمل.</b>
/// <para>
/// كل اختبار هنا يُظهر <b>العطل وهو يُمنَع</b>. والاختبار الأول يقيس التلف نفسه قبل أن
/// يفحص المنع: لو لم يكن التلف واقعاً لكان المنع بلا معنى، ولكان الاختبار أخضر وفارغاً.
/// </para>
/// </summary>
public sealed class MoneyOnTheWireTests
{
    [Fact]
    public void التلف_واقع_فعلاً_قبل_أي_حديث_عن_منعه()
    {
        // القيمة نفسها، محلَّلة مرّتين: عشرياً وفاصلةً عائمة ثنائية.
        decimal exact = decimal.Parse(Payloads.LossyUnderDouble, CultureInfo.InvariantCulture);
        double throughDouble = double.Parse(Payloads.LossyUnderDouble, CultureInfo.InvariantCulture);
        decimal afterRoundTrip = (decimal)throughDouble;

        Console.WriteLine($"عشري                : {exact.ToString("0.0000", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"double منسَّقاً بأربع : {throughDouble.ToString("F4", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"double ثم عشري      : {afterRoundTrip.ToString("0.0000", CultureInfo.InvariantCulture)}");

        Assert.NotEqual(exact, afterRoundTrip);
        Assert.Equal("1000000000000.4013", exact.ToString("0.0000", CultureInfo.InvariantCulture));

        // ‏1000000000000.4013 لا يُمثَّل في IEEE-754 ثنائي 64 بت. أقرب double له يساوي
        // 1000000000000.4012451171875 — فالخانة الأخيرة تصير 2 بدل 3، وهو بالضبط الشكل
        // الذي يُنتجه أي عميل يمرّ برقمه على double قبل أن يرسله.
        Assert.Equal("1000000000000.4012", throughDouble.ToString("F4", CultureInfo.InvariantCulture));

        // والتحويل من double إلى decimal يفقد أكثر: يقرّب إلى 15 رقماً معنوياً (سلوك
        // موثَّق في .NET)، فتضيع أربع خانات دفعةً واحدة لا خانة واحدة.
        Assert.Equal("1000000000000.4000", afterRoundTrip.ToString("0.0000", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task مبلغ_وصل_رمزاً_رقمياً_في_JSON_يُرفض_الطلب_بسببه_ولا_يُقرأ()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // هذه هي الحمولة العدائية بعينها: العميل كتب الرقم رقماً، وأي عميل يمرّره على
        // double يكون قد أتلفه قبل أن يرسله. الخادم لا يقبل القناة أصلاً.
        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            Payloads.BalancedEntry(Payloads.Key("number-token"), rawAmountToken: Payloads.LossyUnderDouble)));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("wire.money.number_token", Http.CodeOf(problem));
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("1e2", "wire.number.malformed")]
    [InlineData("1E2", "wire.number.malformed")]
    [InlineData("1.5e-3", "wire.number.malformed")]
    [InlineData("+5.0000", "wire.number.malformed")]
    [InlineData(" 5.0000", "wire.number.malformed")]
    [InlineData("5.0000 ", "wire.number.malformed")]
    [InlineData("0005.0000", "wire.number.leading_zero")]
    [InlineData("5,0000", "wire.number.malformed")]
    [InlineData("٥.٠٠٠٠", "wire.number.non_latin_digits")]
    [InlineData("०.४०१३", "wire.number.non_latin_digits")]
    [InlineData("0.40135", "wire.number.scale_exceeded")]
    [InlineData("", "wire.number.empty")]
    [InlineData("NaN", "wire.number.malformed")]
    [InlineData("Infinity", "wire.number.malformed")]
    public async Task كل_هجاء_يفتح_باباً_لفقدان_الدقّة_يُرفض_برمزه(string spelling, string expectedCode)
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            Payloads.BalancedEntry(Payloads.Key("spelling"), amount: spelling)));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine($"«{spelling}» → {response.StatusCode} {Http.CodeOf(problem)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, Http.CodeOf(problem));

        // الرسالة تسمّي الحقل: مطوّر الواجهة يعرف أي سطر رُفض بلا تخمين.
        Assert.Contains("lines[0].amount", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task القيمة_التي_يُتلفها_double_تعبر_السلك_سليمة_نصّاً_وتعود_كما_دخلت()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        string key = Payloads.Key("lossless");

        using HttpResponseMessage posted = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            Payloads.BalancedEntry(key, amount: Payloads.LossyUnderDouble, documentDate: "2026-09-10")));

        (string postedText, JsonElement receipt) = await Http.BodyAsync(posted);
        Console.WriteLine(postedText);
        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);

        using HttpResponseMessage balance = await api.Call(Http.Request(
            HttpMethod.Get,
            Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book, "2026-09"),
            ApiFixture.TokenA));

        (string balanceText, JsonElement trial) = await Http.BodyAsync(balance);
        Console.WriteLine(balanceText);

        Assert.Equal(HttpStatusCode.OK, balance.StatusCode);

        List<string> amounts = [.. trial.GetProperty("rows").EnumerateArray()
            .SelectMany(static row => new[] { row.GetProperty("debit").GetString()!, row.GetProperty("credit").GetString()! })
            .Where(static value => value != "0.0000")];

        // كل خانة عبرت: 1000000000000.4013 لا 1000000000000.4012.
        Assert.Equal(2, amounts.Count);
        Assert.All(amounts, value => Assert.Equal("1000000000000.4013", value));
        Assert.DoesNotContain("1000000000000.4012", balanceText, StringComparison.Ordinal);

        // والإيصال نفسه لا يحمل رمزاً رقمياً لرقم قيد أو تسلسل: كلاهما نصّ.
        Assert.Equal(JsonValueKind.String, receipt.GetProperty("entryNumber").ValueKind);
        Assert.Equal(JsonValueKind.String, receipt.GetProperty("chainSequence").ValueKind);
    }

    [Fact]
    public async Task كل_مبلغ_يخرج_من_السطح_نصّاً_بمقياس_أربعة_دائماً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage balance = await api.Call(Http.Request(
            HttpMethod.Get,
            Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book),
            ApiFixture.TokenA));

        (string text, JsonElement trial) = await Http.BodyAsync(balance);
        Assert.Equal(HttpStatusCode.OK, balance.StatusCode);

        JsonElement[] rows = [.. trial.GetProperty("rows").EnumerateArray()];
        Assert.NotEmpty(rows);

        foreach (JsonElement row in rows)
        {
            foreach (string column in new[] { "debit", "credit" })
            {
                JsonElement cell = row.GetProperty(column);
                Assert.Equal(JsonValueKind.String, cell.ValueKind);

                string value = cell.GetString()!;
                int dot = value.IndexOf('.', StringComparison.Ordinal);
                Assert.True(dot >= 0 && value.Length - dot - 1 == 4, $"مقياس غير قانوني على السلك: «{value}»");
            }
        }

        Console.WriteLine($"صفوف مفحوصة: {rows.Length}");
    }

    [Fact]
    public async Task مجموعا_ميزان_المراجعة_يعبران_نصّاً_ويساويان_جمع_الصفوف_بالضبط()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage balance = await api.Call(Http.Request(
            HttpMethod.Get,
            Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book),
            ApiFixture.TokenA));

        (string text, JsonElement trial) = await Http.BodyAsync(balance);
        Assert.Equal(HttpStatusCode.OK, balance.StatusCode);

        // المجموعان نصّان لا رمزان رقميان: الفاصلة العائمة الثنائية عند العميل هي
        // الفخّ نفسه الذي بُني له شكل السلك.
        JsonElement totalDebit = trial.GetProperty("totalDebit");
        JsonElement totalCredit = trial.GetProperty("totalCredit");
        Assert.Equal(JsonValueKind.String, totalDebit.ValueKind);
        Assert.Equal(JsonValueKind.String, totalCredit.ValueKind);

        JsonElement[] rows = [.. trial.GetProperty("rows").EnumerateArray()];
        Assert.NotEmpty(rows);

        // الجمع في الاختبار يقع بـdecimal — وهو ما لا يجوز أن يقع في السطح ولا في المتصفّح.
        decimal sumDebit = rows.Sum(row => decimal.Parse(row.GetProperty("debit").GetString()!, CultureInfo.InvariantCulture));
        decimal sumCredit = rows.Sum(row => decimal.Parse(row.GetProperty("credit").GetString()!, CultureInfo.InvariantCulture));

        Assert.Equal(sumDebit.ToString("0.0000", CultureInfo.InvariantCulture), totalDebit.GetString());
        Assert.Equal(sumCredit.ToString("0.0000", CultureInfo.InvariantCulture), totalCredit.GetString());

        // وحكم التوازن يصل محسوماً — لا يُقارَن مبلغان في JavaScript.
        Assert.Equal(JsonValueKind.True, trial.GetProperty("balanced").ValueKind);
        Assert.Equal(sumDebit, sumCredit);

        Console.WriteLine($"مدين {totalDebit.GetString()} · دائن {totalCredit.GetString()} · صفوف {rows.Length}");
        Console.WriteLine(text.Length > 400 ? text[..400] + "…" : text);
    }
}
