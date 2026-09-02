using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>السطح يتصرّف تصرّفاً واحداً تحت أي ثقافة نظام.</b>
/// <para>
/// والثقافة تُضبط كما تُضبط على خادم إنتاج: بمتغيّري <c>LANG</c> و<c>LC_ALL</c> على العملية،
/// لا بسطر داخلها. ثم <b>يُتحقَّق أنها ضُبطت فعلاً</b> من نقطة الصحّة — وهذا هو ما يمنع هذه
/// المجموعة من أن تمرّ فراغاً: لو لم يكن للمتغيّر أثر، لكان كل ما تحته يفحص <c>en-US</c>
/// أربع مرّات ويُسمّي نفسه اختبار ثقافة.
/// </para>
/// <para>
/// و<c>ar-SA</c> بالتحديد هي الحالة الخطرة: تقويمها الافتراضي أم القرى، فأي تنسيق تاريخ
/// ضمني يكتب <c>1448-03</c> مكان <c>2026-08</c> فيُفسد رمز الفترة المالية بلا استثناء ولا
/// سطر سجل. و<c>tr-TR</c> خطرة لسبب آخر: الحرف <c>I</c> لا يُصغَّر إلى <c>i</c> فيها، فأي
/// مطابقة تمرّ على تحويل حالة الأحرف تصير حجّة لغوية في مسار محاسبي.
/// </para>
/// </summary>
public sealed partial class CultureTests
{
    [Theory]
    [InlineData("ar_SA.UTF-8", "ar-SA", "UmAlQuraCalendar")]
    [InlineData("en_US.UTF-8", "en-US", "GregorianCalendar")]
    [InlineData("hi_IN.UTF-8", "hi-IN", "GregorianCalendar")]
    [InlineData("tr_TR.UTF-8", "tr-TR", "GregorianCalendar")]
    public async Task ثقافة_العملية_تُضبط_فعلاً_وتُعلَن_فلا_يمرّ_ما_تحتها_فراغاً(
        string locale, string expectedCulture, string expectedCalendar)
    {
        ApiProcess api = await ApiFixture.WithCultureAsync(locale);

        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Get, "/health", credential: null));

        (string text, JsonElement health) = await Http.BodyAsync(response);
        Console.WriteLine($"{locale} → {text}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedCulture, health.GetProperty("culture").GetString());
        Assert.Equal(expectedCalendar, health.GetProperty("calendar").GetString());
    }

    [Theory]
    [InlineData("ar_SA.UTF-8")]
    [InlineData("en_US.UTF-8")]
    [InlineData("hi_IN.UTF-8")]
    [InlineData("tr_TR.UTF-8")]
    public async Task رمز_الفترة_المالية_ميلادي_تحت_كل_ثقافة_ولا_يصير_هجرياً_تحت_العربية(string locale)
    {
        ApiProcess api = await ApiFixture.WithCultureAsync(locale);

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            Payloads.BalancedEntry(Payloads.Key("period"), documentDate: "2026-08-15")));

        (string text, JsonElement receipt) = await Http.BodyAsync(response);
        Console.WriteLine($"{locale} → {text}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // ‏1448-03 هو ما كان سيُكتب لو قرأ أي تنسيق تاريخ ثقافةَ العملية تحت ar-SA.
        Assert.Equal("2026-08", receipt.GetProperty("periodCode").GetString());

        // ‏**والمسح يبحث عن شكلِ تاريخٍ هجريّ لا عن أربعة أرقام.** كان
        // ‏`DoesNotContain("1448", text)` يمسح **جسم الاستجابة كلَّه** وفيه بصمةٌ
        // سداسية عشرية عشوائية؛ فحمرَّ مرّةً على `…c37d891448066e…` — أربعةُ أرقامٍ
        // وقعت مصادفةً داخل بصمة، والحارس يقول «تسرّب تاريخٌ هجريّ». والشرطةُ وحدها
        // لا تكفي علاجاً: المعرّف الكوني سداسيٌّ **بشرطات**، فـ`1448-` تقع فيه أيضاً.
        // فالشرط أن يكون المطابَق **خارج** جريان سداسيٍّ أو معرّف: ما قبله وما بعده
        // ليسا من `[0-9a-fA-F-]`. وبذلك يُلتقط `"1448-03"` في حقلٍ أو في نصّ رسالة،
        // ولا يُلتقط شيءٌ داخل بصمةٍ ولا داخل GUID.
        Assert.DoesNotMatch(HijriPeriodShape(), text);
    }

    // فترة مستقلّة لكل ثقافة: حالات النظرية تشترك في قاعدة بيانات واحدة، والمجاميع
    // تتراكم — واختبار يقرأ مجموعاً متراكماً يفشل لسبب لا علاقة له بما يدّعي فحصه.
    [Theory]
    [InlineData("ar_SA.UTF-8", "2026-04")]
    [InlineData("en_US.UTF-8", "2026-05")]
    [InlineData("hi_IN.UTF-8", "2026-06")]
    [InlineData("tr_TR.UTF-8", "2026-07")]
    public async Task المبلغ_يُقرأ_بنقطة_عشرية_واحدة_تحت_كل_ثقافة(string locale, string period)
    {
        ApiProcess api = await ApiFixture.WithCultureAsync(locale);
        string key = Payloads.Key("amount-culture");

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            Payloads.BalancedEntry(key, amount: "1234.5678", documentDate: period + "-05")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using HttpResponseMessage balance = await api.Call(Http.Request(
            HttpMethod.Get,
            Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book, period),
            ApiFixture.TokenA));

        (string text, _) = await Http.BodyAsync(balance);
        Console.WriteLine($"{locale} → {text}");

        // لا فاصلة عشرية أوروبية، ولا فاصل آلاف، ولا رقم عربي-هندي في المُخرَج.
        Assert.Contains("1234.5678", text, StringComparison.Ordinal);
        Assert.DoesNotContain("1234,5678", text, StringComparison.Ordinal);
        Assert.DoesNotContain("١٢٣٤", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ar_SA.UTF-8")]
    [InlineData("en_US.UTF-8")]
    [InlineData("hi_IN.UTF-8")]
    [InlineData("tr_TR.UTF-8")]
    public async Task اسم_الدور_يُطابَق_حرفياً_تحت_كل_ثقافة_ولا_يمرّ_عبر_تحويل_حالة_أحرف(string locale)
    {
        ApiProcess api = await ApiFixture.WithCultureAsync(locale);

        // الهجاء الصحيح يعبر طبقة السلك — والدليل أن ما يرجع ليس رمز سلك.
        using HttpResponseMessage accepted = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            Payloads.EntryWithRoleSpelling(Payloads.Key("role-exact"), "InputTax")));

        (string acceptedText, JsonElement acceptedBody) = await Http.BodyAsync(accepted);
        Console.WriteLine($"{locale} InputTax → {accepted.StatusCode} {acceptedText[..Math.Min(200, acceptedText.Length)]}");

        if (accepted.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.OK))
        {
            Assert.DoesNotContain("wire.enum", Http.CodeOf(acceptedBody), StringComparison.Ordinal);
        }

        // وكل هجاء آخر يُرفض — بما فيها ما تنتجه tr-TR من تحويل حالة أحرف على I/ı.
        foreach (string spelling in new[] { "inputTax", "INPUTTAX", "inputtax", "ınputTax", "İnputTax", "3" })
        {
            using HttpResponseMessage rejected = await api.Call(Http.Request(
                HttpMethod.Post,
                Http.PostEntry(ApiTestDatabase.CompanyA),
                ApiFixture.TokenA,
                Payloads.EntryWithRoleSpelling(Payloads.Key("role-bad"), spelling)));

            (_, JsonElement problem) = await Http.BodyAsync(rejected);
            Console.WriteLine($"{locale} «{spelling}» → {rejected.StatusCode} {Http.CodeOf(problem)}");

            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            Assert.StartsWith("wire.enum.", Http.CodeOf(problem), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("ar_SA.UTF-8")]
    [InlineData("tr_TR.UTF-8")]
    public async Task التاريخ_يُقبل_ميلادياً_بأرقام_لاتينية_ولا_يُقبل_بغيرها(string locale)
    {
        ApiProcess api = await ApiFixture.WithCultureAsync(locale);

        foreach (string date in new[] { "١٤٤٨-٠٣-٠١", "1448-03-01", "15/08/2026", "2026-8-15" })
        {
            using HttpResponseMessage response = await api.Call(Http.Request(
                HttpMethod.Post,
                Http.PostEntry(ApiTestDatabase.CompanyA),
                ApiFixture.TokenA,
                Payloads.BalancedEntry(Payloads.Key("date"), documentDate: date)));

            (_, JsonElement problem) = await Http.BodyAsync(response);
            string code = Http.CodeOf(problem);
            Console.WriteLine($"{locale} «{date}» → {(int)response.StatusCode} {code}");

            // ‏1448-03-01 تاريخ هجري صالح **شكلاً** بصيغة yyyy-MM-dd، فيعبر الماسح ويُقرأ
            // ميلادياً — ثم يُرفض في الدفتر لأن لا فترة مالية تحويه. وهذا مقصود: السطح
            // لا يقرأ تقويماً ثانياً ولا يخمّن أي تقويم قصد العميل، فالتخمين هنا يعني
            // ترحيلاً في فترة أخرى بصمت.
            Assert.True(
                code is "wire.date.malformed" or "wire.date.non_latin_digits" or "ledger.posting.no_fiscal_period",
                $"رمز غير متوقّع لتاريخ «{date}»: {code}");

            Assert.Equal(
                code == "ledger.posting.no_fiscal_period" ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.BadRequest,
                response.StatusCode);
        }
    }

    /// <summary>
    /// شكلُ الفترة الهجرية <c>NNNN-NN</c> بسنةٍ في المدى 1300–1499، <b>خارج أي جريان
    /// سداسيّ أو معرّف كوني</b>. النظرتان الخلفية والأمامية هما العلاج: البصمة لا شرطة
    /// فيها فلا تُطابق أصلاً، والمعرّف الكوني تسبقه أو تتبعه شرطةٌ أو محرفٌ سداسيّ فيُرفض.
    /// </summary>
    [GeneratedRegex(@"(?<![0-9a-fA-F-])1[34][0-9]{2}-[0-9]{2}(?![0-9a-fA-F-])", RegexOptions.CultureInvariant)]
    private static partial Regex HijriPeriodShape();

    /// <summary>
    /// <b>حارس لافراغ للنمط نفسه.</b> نمطٌ توقّف عن المطابقة يجعل الفحص أعلاه يمرّ على
    /// كل شيء — وهو بالضبط شكلُ الحارس الذي لا يحرس. والشواهد السالبة هي الغرض: بصمةٌ
    /// حقيقية حمّرت الحارس القديم، ومعرّفٌ كونيّ يحمل <c>1448</c> في مقطعٍ منه.
    /// </summary>
    [Fact]
    public void شكل_الفترة_الهجرية_يُطابق_التسرّب_ولا_يُطابق_البصمة_ولا_المعرّف()
    {
        // موجب: الشكل في حقلٍ، وفي نصّ رسالة.
        Assert.Matches(HijriPeriodShape(), ("{\"periodCode\":\"1448-03\"}"));
        Assert.Matches(HijriPeriodShape(), ("الفترة 1448-03 مقفلة"));
        Assert.Matches(HijriPeriodShape(), ("1447-12"));

        // سالب: البصمة التي حمّرت الحارس القديم فعلاً — لا شرطة فيها.
        Assert.DoesNotMatch(HijriPeriodShape(), ("71122523b78715b2cc37d891448066e93ab73302e3901940"));

        // سالب: معرّف كوني يحمل 1448 مقطعاً كاملاً — الشرطة قبله وبعده تمنعه.
        Assert.DoesNotMatch(HijriPeriodShape(), ("d3305e1e-1448-4000-8000-000000000001"));
        Assert.DoesNotMatch(HijriPeriodShape(), ("00001448-01ab-4000-8000-000000000001"));

        // سالب: التاريخ الميلادي لا يُطابَق — المدى 13xx/14xx وحده.
        Assert.DoesNotMatch(HijriPeriodShape(), ("2026-08"));
    }
}
