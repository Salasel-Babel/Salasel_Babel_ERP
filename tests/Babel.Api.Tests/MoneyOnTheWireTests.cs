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
    /// <summary>
    /// الفترة المحجوزة لاختبار المقياس — <b>لا يُرحَّل إليها من مكان آخر</b>.
    /// <para>
    /// السنة المالية المبذورة تحمل اثنتي عشرة فترة، وكل اختبار يحتاج قيمةً معلومة يأخذ
    /// فترةً لنفسه. والحجز ليس اصطلاحاً مكتوباً فقط: كل اختبار هنا يفحص عدد صفوف فترته،
    /// فترحيلٌ غريب إليها يُسقطه <b>بصوت عالٍ</b> بدل أن يميّع القيمة المتوقّعة تحته.
    /// </para>
    /// </summary>
    private const string ScalePeriod = "2026-03";

    /// <summary>الفترة المحجوزة لاختبار المجموعين — بالشرط نفسه.</summary>
    private const string TotalsPeriod = "2026-10";

    /// <summary>مبلغ مكتوب على السلك بمقياس <b>واحد</b> — مادّة إثبات أن المخرج يُطبَّع إلى أربعة.</summary>
    private const string ShortScaleAmount = "7.5";

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

        // ‏**الحالة يبنيها الاختبار، لا جاره.** كان هذا الاختبار يقرأ ميزاناً بذره اختبار
        // آخر في صنف آخر، فيمرّ مع الجماعة ويسقط وحده على `Assert.NotEmpty` — أخضرُ
        // بترتيب التشغيل لا ببنائه. (‏docs/evidence/traps.md#fakh-green-by-ordering-not-by-construction)
        //
        // والمبلغ مكتوب على السلك **بمقياس أقصر من أربعة** عن قصد: «7.5» يدخل بمقياس
        // واحد، والخاصية المفحوصة هي أنه يخرج «7.5000» **دائماً**. مبلغٌ مكتوب أصلاً
        // بأربع خانات كان سيجعل الفحص يمرّ ولو لم يُطبَّع شيء.
        using HttpResponseMessage posted = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            Payloads.BalancedEntry(Payloads.Key("scale"), amount: ShortScaleAmount, documentDate: ScalePeriod + "-11")));

        (string postedText, _) = await Http.BodyAsync(posted);
        Console.WriteLine(postedText);
        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);

        // ‏(أ) فترة هذا الاختبار وحده: صفّان معلومان، فالفحص يجري على قيمة **معروفة**.
        using HttpResponseMessage mine = await api.Call(Http.Request(
            HttpMethod.Get,
            Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book, ScalePeriod),
            ApiFixture.TokenA));

        (string mineText, JsonElement mineTrial) = await Http.BodyAsync(mine);
        Console.WriteLine(mineText);
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);

        JsonElement[] mineRows = [.. mineTrial.GetProperty("rows").EnumerateArray()];

        // صفّان لا غير: الفترة محجوزة لهذا الاختبار، وأي ترحيل آخر إليها يُسقطه بصوت
        // عالٍ بدل أن يميّع القيمة المتوقّعة تحته.
        Assert.Equal(2, mineRows.Length);
        Assert.Equal(4, AssertEveryCellIsTextAtScaleFour(mineRows));

        // «7.5» دخل بمقياس واحد وخرج بمقياس أربعة — والهجاء الأصلي لا أثر له على السلك.
        Assert.Contains("\"7.5000\"", mineText, StringComparison.Ordinal);
        Assert.DoesNotContain("\"7.5\"", mineText, StringComparison.Ordinal);
        Assert.Equal("7.5000", mineTrial.GetProperty("totalDebit").GetString());
        Assert.Equal("7.5000", mineTrial.GetProperty("totalCredit").GetString());

        // ‏(ب) والدفتر كلّه: **كل** خانة مال فيه — لا خانات هذا الاختبار وحدها.
        using HttpResponseMessage balance = await api.Call(Http.Request(
            HttpMethod.Get,
            Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book),
            ApiFixture.TokenA));

        (_, JsonElement trial) = await Http.BodyAsync(balance);
        Assert.Equal(HttpStatusCode.OK, balance.StatusCode);

        JsonElement[] rows = [.. trial.GetProperty("rows").EnumerateArray()];
        Assert.NotEmpty(rows);

        int cells = AssertEveryCellIsTextAtScaleFour(rows);

        Console.WriteLine($"صفوف مفحوصة: {rows.Length} · خانات: {cells}");
    }

    [Fact]
    public async Task مجموعا_ميزان_المراجعة_يعبران_نصّاً_ويساويان_جمع_الصفوف_بالضبط()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // ‏**الحالة يبنيها الاختبار، لا جاره** — للسبب نفسه المشروح فوق
        // (‏docs/evidence/traps.md#fakh-green-by-ordering-not-by-construction). والمبلغ
        // المزروع هو القيمة التي يُتلفها double بعينها: مجموعٌ يُحسب أو يُنسَّق في فاصلة
        // عائمة ثنائية لا يستطيع أن يُخرجها سليمة، فالفحص أدناه ليس فحص شكل بل فحص قيمة.
        using HttpResponseMessage posted = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            Payloads.BalancedEntry(
                Payloads.Key("totals"),
                amount: Payloads.LossyUnderDouble,
                documentDate: TotalsPeriod + "-14")));

        (string postedText, _) = await Http.BodyAsync(posted);
        Console.WriteLine(postedText);
        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);

        // ‏(أ) فترة هذا الاختبار وحده: المجموعان **معلومان بالضبط**، فلا يكفي أن
        // يتّسقا مع الصفوف — يجب أن يكونا القيمة التي دخلت، خانةً بخانة.
        using HttpResponseMessage mine = await api.Call(Http.Request(
            HttpMethod.Get,
            Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book, TotalsPeriod),
            ApiFixture.TokenA));

        (string mineText, JsonElement mineTrial) = await Http.BodyAsync(mine);
        Console.WriteLine(mineText);
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);

        // صفّان لا غير: الفترة محجوزة لهذا الاختبار، وأي ترحيل آخر إليها يُسقطه بصوت عالٍ.
        Assert.Equal(2, mineTrial.GetProperty("rows").EnumerateArray().Count());

        // نوع الرمز أولاً ثم القيمة: مجموعٌ خرج رمزاً رقمياً يجب أن يُسمّى بما هو،
        // لا أن يظهر استثناء قراءة نصّ من رقم في منتصف مقارنة.
        JsonElement minedDebit = mineTrial.GetProperty("totalDebit");
        JsonElement minedCredit = mineTrial.GetProperty("totalCredit");
        Assert.Equal(JsonValueKind.String, minedDebit.ValueKind);
        Assert.Equal(JsonValueKind.String, minedCredit.ValueKind);
        Assert.Equal(Payloads.LossyUnderDouble, minedDebit.GetString());
        Assert.Equal(Payloads.LossyUnderDouble, minedCredit.GetString());

        // ‏1000000000000.4012 هو ما يُنتجه أي مسار يمرّ بـdouble. غيابه من الجسم كلّه
        // هو الشهادة، لا مساواةٌ على حقل واحد.
        Assert.DoesNotContain("1000000000000.4012", mineText, StringComparison.Ordinal);

        // ‏(ب) والدفتر كلّه: المجموعان يساويان جمع كل الصفوف بالضبط.
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

    /// <summary>
    /// يفحص أن كل خانة مال في الصفوف <b>نصّ</b> بمقياس أربعة، ويعيد عدد الخانات المفحوصة.
    /// <para>
    /// والعدد يُعاد لا زينةً: فحصٌ يدور على مجموعة فارغة يمرّ دائماً، والعدد هو ما يجعل
    /// المُنادي قادراً على إثبات أن الدوران وقع فعلاً.
    /// </para>
    /// </summary>
    /// <param name="rows">صفوف الميزان.</param>
    private static int AssertEveryCellIsTextAtScaleFour(JsonElement[] rows)
    {
        int cells = 0;

        foreach (JsonElement row in rows)
        {
            foreach (string column in new[] { "debit", "credit" })
            {
                JsonElement cell = row.GetProperty(column);
                Assert.Equal(JsonValueKind.String, cell.ValueKind);

                string value = cell.GetString()!;
                int dot = value.IndexOf('.', StringComparison.Ordinal);
                Assert.True(dot >= 0 && value.Length - dot - 1 == 4, $"مقياس غير قانوني على السلك: «{value}»");
                cells++;
            }
        }

        return cells;
    }
}
