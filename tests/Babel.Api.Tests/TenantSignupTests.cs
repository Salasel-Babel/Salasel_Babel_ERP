using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>التسجيل الأول — من الشبكة، بعميل لا يملك اعتماداً ولا يعرف إلا العنوان.</b>
/// <para>
/// وقبل هذا الباب كان المنتَج يُباع بالاشتراك ولا يستطيع أحد أن يشترك: مستوى التحكّم
/// فيه التزويد والاستحقاق والأرشفة، و<b>صفر نقطة نهاية HTTP</b>؛ والعقد المنشور فيه
/// ثماني وثلاثون عملية، ولا واحدة منها تُنشئ مستأجراً.
/// </para>
/// </summary>
public sealed class TenantSignupTests
{
    private const string Tenants = "/api/v1/tenants";

    private const string Sessions = "/api/v1/access/sessions";

    [Fact]
    public async Task التسجيل_يفتح_مستأجراً_واشتراكاً_وأول_مالك_ثم_تُفتح_به_جلسة()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        (JsonElement registered, HttpStatusCode status) = await RegisterAsync(api, NewKey());

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.False(registered.GetProperty("alreadyRegistered").GetBoolean());

        // المستأجر والمنشأة والمالك — ثلاثة معرّفات مشتقّة، ولا واحد منها يأتي من الجسم.
        Assert.True(Guid.TryParseExact(registered.GetProperty("tenantId").GetString(), "D", out Guid tenantId));
        Assert.True(Guid.TryParseExact(registered.GetProperty("companyId").GetString(), "D", out _));
        Assert.Equal("Owner", registered.GetProperty("owner").GetProperty("role").GetString());
        Assert.Equal("مالكة أولى", registered.GetProperty("owner").GetProperty("displayNameAr").GetString());

        // الاشتراك مفتوحٌ على **خطّة الدخول** — ولم يُطلب في الجسم ولا يمكن أن يُطلب.
        JsonElement subscription = registered.GetProperty("subscription");
        Assert.Equal("ESSENTIAL", subscription.GetProperty("planCode").GetString());
        Assert.Equal("Active", subscription.GetProperty("state").GetString());
        Assert.Equal(tenantId.ToString("D", CultureInfo.InvariantCulture), subscription.GetProperty("tenantId").GetString());

        // والوحدات مُستحَقّة بترجمة الكتالوجين: الأستاذ العام والمبيعات والمشتريات.
        Dictionary<string, string> modules = ModulesOf(subscription);
        Assert.Equal("Entitled", modules["CORE"]);
        Assert.Equal("Entitled", modules["AR"]);
        Assert.Equal("Entitled", modules["AP"]);
        Assert.Equal("NotEntitled", modules["POS"]);

        // والمبلغ نصّ لا رمز رقمي — على الطرفين.
        Assert.Equal(JsonValueKind.String, subscription.GetProperty("monthlyPrice").ValueKind);

        // ثم: اعتماد الانتساب يُبدَّل بجلسة كاملة، كأي دعوة أخرى ومن الباب نفسه.
        string enrolment = registered.GetProperty("enrolmentCredential").GetString()!;

        using HttpResponseMessage opened = await api.Call(Http.Request(
            HttpMethod.Post, Sessions, credential: null,
            $$"""{"enrolmentCredential":"{{enrolment}}"}"""));

        (string text, JsonElement session) = await Http.BodyAsync(opened);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        Assert.Equal(
            registered.GetProperty("owner").GetProperty("userId").GetString(),
            session.GetProperty("userId").GetString());
        Assert.Equal(tenantId.ToString("D", CultureInfo.InvariantCulture), session.GetProperty("tenantId").GetString());
    }

    [Fact]
    public async Task إعادة_الإرسال_بالمفتاح_نفسه_تردّ_المستأجر_نفسه_ولا_تُنشئ_ثانياً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        string key = NewKey();

        (JsonElement first, HttpStatusCode firstStatus) = await RegisterAsync(api, key);
        Assert.Equal(HttpStatusCode.Created, firstStatus);

        // والاسم مختلفٌ عمداً في الإعادة: الهوية من المفتاح لا من الحمولة، فلو كانت
        // من الحمولة لأنتجت هذه المحاولة مستأجراً ثانياً.
        (JsonElement second, HttpStatusCode secondStatus) = await RegisterAsync(api, key, companyAr: "اسمٌ آخر تماماً");

        Assert.Equal(HttpStatusCode.OK, secondStatus);
        Assert.True(second.GetProperty("alreadyRegistered").GetBoolean());

        Assert.Equal(first.GetProperty("tenantId").GetString(), second.GetProperty("tenantId").GetString());
        Assert.Equal(first.GetProperty("companyId").GetString(), second.GetProperty("companyId").GetString());
        Assert.Equal(
            first.GetProperty("owner").GetProperty("userId").GetString(),
            second.GetProperty("owner").GetProperty("userId").GetString());

        // **ولا يُسكّ سرٌّ ثانٍ**: السرّ يُسلَّم مرّة، والمُودَع بصمته. وباب مفتوح يُصدر
        // اعتماداً في كل إعادة إرسال هو مصنع اعتمادات لا حصانة ضد التكرار.
        Assert.Equal(JsonValueKind.Null, second.GetProperty("enrolmentCredential").ValueKind);
        Assert.Equal(JsonValueKind.Null, second.GetProperty("enrolmentExpiresAt").ValueKind);
    }

    [Fact]
    public async Task مفتاحان_مختلفان_مستأجران_مختلفان_ولا_يكشف_أحدهما_وجود_الآخر()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // والاسم نفسه في المحاولتين: الأسماء ليست هوية ولا تُفحص فرادتها، فلا يوجد
        // في هذا الباب سؤالٌ يُجاب بـ«هذا الاسم مأخوذ» — وهو الجواب الذي يجعل بابَ
        // تسجيلٍ عدّادَ وجودٍ لمنشآت الآخرين.
        (JsonElement one, HttpStatusCode first) = await RegisterAsync(api, NewKey(), companyAr: "شركة الاسم المكرَّر");
        (JsonElement two, HttpStatusCode second) = await RegisterAsync(api, NewKey(), companyAr: "شركة الاسم المكرَّر");

        Assert.Equal(HttpStatusCode.Created, first);
        Assert.Equal(HttpStatusCode.Created, second);
        Assert.NotEqual(one.GetProperty("tenantId").GetString(), two.GetProperty("tenantId").GetString());
        Assert.NotEqual(one.GetProperty("companyId").GetString(), two.GetProperty("companyId").GetString());
    }

    [Fact]
    public async Task مفتاحٌ_أقصر_من_الحدّ_يُرفض_برمزه_وبلغتين()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Tenants, credential: null,
            """{"requestKey":"short","companyNameAr":"منشأة","ownerNameAr":"مالك"}"""));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("signup.request_key_invalid", Http.CodeOf(problem));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detailAr").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task اسمٌ_ناقص_يُرفض_ولا_يُنشأ_مستأجر()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Tenants, credential: null,
            $$"""{"requestKey":"{{NewKey()}}","companyNameAr":"","ownerNameAr":"مالك"}"""));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal("signup.name_missing", Http.CodeOf(problem));
    }

    /// <summary>مفتاح طلب عشوائي — كما يجب أن يولّده كل عميل.</summary>
    internal static string NewKey() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    /// <summary>حالات الوحدات مقروءةً من اشتراكٍ في قاموس، لتُسأل بالرمز لا بالترتيب.</summary>
    internal static Dictionary<string, string> ModulesOf(JsonElement subscription)
    {
        Dictionary<string, string> states = new(StringComparer.Ordinal);

        foreach (JsonElement module in subscription.GetProperty("modules").EnumerateArray())
        {
            states[module.GetProperty("code").GetString()!] = module.GetProperty("state").GetString()!;
        }

        return states;
    }

    /// <summary>يسجّل مستأجراً من الباب المفتوح ويُرجع الجسم ورمز الحالة.</summary>
    internal static async Task<(JsonElement Body, HttpStatusCode Status)> RegisterAsync(
        ApiProcess api,
        string key,
        string companyAr = "منشأة التسجيل الأول",
        string ownerAr = "مالكة أولى")
    {
        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post,
            Tenants,
            credential: null,
            $$"""
            {"requestKey":"{{key}}","companyNameAr":"{{companyAr}}","ownerNameAr":"{{ownerAr}}","nameTranslations":[{"name":"en","value":"Signup Co"}]}
            """));

        (string text, JsonElement body) = await Http.BodyAsync(response);
        Console.WriteLine(text);
        return (body, response.StatusCode);
    }
}
