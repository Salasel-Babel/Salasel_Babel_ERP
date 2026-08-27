using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>خادمٌ أُقلع للتوّ يعرف منشآته — ويكتب، لا يقرأ فقط.</b>
/// <para>
/// هذه هي حالة العرض بعينها: القاعدة مبنيّة ومبذورة في <b>عملية أخرى</b> (حاوية
/// الترحيل)، ثم يُقلع الخادم. وكان مخزن التأسيس <c>InMemoryCompanySetupStore</c> —
/// حالتُه عمرُ العملية — فكان الخادم يبدأ بسجلٍّ فارغ.
/// </para>
/// <para>
/// <b>والأثر لم يكن «شاشة إعدادات فارغة»</b>: كل مسار كتابة يسأل
/// <c>ICostCenterResolver</c> عن مركز التكلفة <b>قبل</b> أن يبني طلباً (ADR-0026 ·
/// ADR-0029 جعل <c>PostingScope.CostCenterId</c> غير فارغ)، فكان <b>كل</b> ترحيل من
/// السطح يرتدّ بـ<c>company_setup.not_found</c> بينما يعمل الميزان والأعمار وإعادة
/// التحقق من السلسلة — عرضٌ يقرأ ولا يكتب، ويسقط أمام الحضور عند أول فاتورة.
/// </para>
/// <para>
/// <b>ولذلك يُقلع هنا خادمٌ جديد لا يؤسّس شيئاً</b>: لا يستدعي
/// <c>ApiFixture.StartAndFoundAsync</c>، ولا يمرّ عليه <c>PUT …/setup</c> واحد في عمره.
/// كل ما يعرفه عن المنشأة يأتي من القاعدة. وعلى <c>develop</c> قبل هذا التسليم كانت
/// الحالتان أدناه تُرجعان 404 و404.
/// </para>
/// </summary>
public sealed class SetupSurvivesAServerRestartTests
{
    [Fact]
    public async Task خادمٌ_لم_يؤسّس_شيئاً_في_عمره_يقرأ_التأسيس_ويقبل_ترحيلاً()
    {
        await ApiTestDatabase.EnsureAsync(ApiFixture.Token);

        // المنشأة أُسّست في **عملية أخرى**: الخادم المشترك. وهذه عملية ثالثة لا تشترك
        // معه في بايت واحد من الحالة — وهو بالضبط ما تفعله إعادة الإقلاع.
        _ = await ApiFixture.DefaultAsync();

        await using ApiProcess restarted = await ApiProcess.StartAsync(
            ApiFixture.Environment(ApiTestDatabase.Options.AppConnectionString),
            "en_US.UTF-8",
            ApiFixture.Token);

        // ── ١ · التأسيس يُقرأ: 200 لا 404 ────────────────────────────────────
        using HttpResponseMessage setup = await restarted.Call(Http.Request(
            HttpMethod.Get,
            string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{ApiTestDatabase.CompanyA:D}/setup"),
            ApiFixture.TokenA));

        (string setupText, JsonElement setupJson) = await Http.BodyAsync(setup);

        Assert.True(
            setup.StatusCode == HttpStatusCode.OK,
            "‏GET …/setup على خادم أُقلع للتوّ: " + (int)setup.StatusCode + " — " + setupText);

        string defaultCostCenter = setupJson.GetProperty("defaultCostCenter").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(defaultCostCenter));
        Assert.Equal(2, setupJson.GetProperty("decimalPlaces").GetInt32());

        // ── ٢ · والكتابة تمرّ: 201 لا 404 — وهذا هو الاختبار الحقيقي ─────────
        // حمولةٌ بلا `costCenterId`: أي أن الحلّ يعتمد كلّياً على المركز الافتراضي
        // المقروء من القاعدة. فلو ضاع التأسيس لارتدّ الطلب قبل أن يبلغ المحرّك.
        string key = "RESTART-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];

        using HttpResponseMessage posted = await restarted.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            Payloads.BalancedEntry(key)));

        (string postText, JsonElement postJson) = await Http.BodyAsync(posted);

        Assert.True(
            posted.StatusCode == HttpStatusCode.Created,
            "ترحيلٌ من خادم أُقلع للتوّ: " + (int)posted.StatusCode + " — " + postText);

        Assert.False(postJson.GetProperty("alreadyPosted").GetBoolean());
        Assert.Equal(2, postJson.GetProperty("lineCount").GetInt32());

        // ── ٣ · وأن الرفض المفحوص ليس مستحيلاً أصلاً ────────────────────────
        // منشأةٌ لم تُؤسَّس قط ترتدّ بالرمز نفسه من الخادم نفسه. فلو كان السطح قد كفّ
        // عن سؤال مركز التكلفة لمرّت الحالتان أعلاه بلا أن تُثبتا شيئاً.
        Guid never = ApiFixture.SetupCompanies[^1];

        using HttpResponseMessage refused = await restarted.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(never),
            ApiFixture.TokenS,
            Payloads.BalancedEntry("NEVER-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12])));

        (_, JsonElement refusedJson) = await Http.BodyAsync(refused);

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal("company_setup.not_found", Http.CodeOf(refusedJson));
    }
}
