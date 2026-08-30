using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>الأبواب المفتوحة محروسة بحدّ معدّل — ‏429 وترويسة <c>Retry-After</c> وجسمٌ بلغتين.</b>
/// <para>
/// وكان هذا دَيناً <b>مُعلَناً ولم يُبنَ</b>: ‏ADR-0045 §7 بند ٤ يقول «لا حدّ على معدّل
/// المحاولات على بابَي الجلسة… وحدّ المعدّل يبقى ناقصاً ويُقال». وقد صار مبنيّاً، وصار
/// معه بابٌ ثالث يُطرَق: التسجيل الأول.
/// </para>
/// <para>
/// <b>وما لا يشتريه هذا الحارس مكتوبٌ كي لا يُفترَض:</b> الاعتماد 256 بتاً فالتخمين غير
/// عملي أصلاً، وليس هذا ما يُشترى. المُشترى أن لا يستطيع طارقٌ واحد أن يفتح ألف مستأجر
/// في دقيقة، ولا أن يستهلك مجمّع الاتصالات بمحاولات دخول متتابعة فيُسقط الخدمة عن أصحابها.
/// </para>
/// </summary>
public sealed class OpenDoorRateLimitTests
{
    private const string Sessions = "/api/v1/access/sessions";

    private const string Tenants = "/api/v1/tenants";

    /// <summary>الحدّ المُهيَّأ لخادم هذه المجموعة — صغيرٌ كي يُبلَغ في طلبات معدودة.</summary>
    private const int PerMinute = 3;

    [Fact]
    public async Task تجاوز_الحدّ_على_باب_الجلسة_يردّ_429_بترويسة_مهلة_وجسمٍ_بلغتين()
    {
        ApiProcess api = await ApiFixture.WithRateLimitAsync(PerMinute);

        HttpStatusCode last = HttpStatusCode.OK;
        JsonElement problem = default;
        string body = string.Empty;

        // المحاولات كلّها باعتماد مختلَق: ما يُقاس هو **عدد الطلبات** لا صحّتها.
        // وقبل التجاوز يردّ الباب 401 — أي أنه يخدم فعلاً — وبعده 429.
        for (int attempt = 0; attempt <= PerMinute + 1; attempt++)
        {
            using HttpResponseMessage response = await api.Call(Http.Request(
                HttpMethod.Post, Sessions, credential: null,
                """{"enrolmentCredential":"0000000000000000000000000000000000000000000"}"""));

            last = response.StatusCode;
            (body, problem) = await Http.BodyAsync(response);

            if (last == HttpStatusCode.TooManyRequests)
            {
                Assert.True(
                    response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? values),
                    "‏429 بلا Retry-After تترك العميل يخمّن متى يعود، فيعود فوراً — أي أن الحدّ يزيد الطرق.");

                string retryAfter = values!.First();
                Assert.True(
                    int.TryParse(retryAfter, out int seconds) && seconds >= 1,
                    "‏Retry-After يجب أن يكون عدد ثوانٍ موجباً: " + retryAfter);

                break;
            }

            Assert.Equal(HttpStatusCode.Unauthorized, last);
        }

        Console.WriteLine(body);

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
        Assert.Equal("rate.too_many_requests", Http.CodeOf(problem));

        // والجسم مشكلةٌ بلغتين، لا نصّاً واحداً ولا صفحةَ خطأ من الخادم.
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detailAr").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
        Assert.Equal(429, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task باب_التسجيل_محروس_بحدّه_هو_ولا_يستهلك_حصّة_باب_الجلسة()
    {
        ApiProcess api = await ApiFixture.WithRateLimitAsync(PerMinute);

        // ١ · تُستهلك حصّة باب الجلسة كاملةً.
        for (int attempt = 0; attempt <= PerMinute; attempt++)
        {
            using HttpResponseMessage burnt = await api.Call(Http.Request(
                HttpMethod.Post, Sessions, credential: null,
                """{"enrolmentCredential":"1111111111111111111111111111111111111111111"}"""));

            Console.WriteLine(attempt.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " · " + (int)burnt.StatusCode);
        }

        // ٢ · وباب التسجيل ما يزال يخدم: المفتاح يحمل المسار، فلا يستهلك بابٌ حصّة آخر.
        (JsonElement registered, HttpStatusCode status) =
            await TenantSignupTests.RegisterAsync(api, TenantSignupTests.NewKey());

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.False(registered.GetProperty("alreadyRegistered").GetBoolean());

        // ٣ · ثم يُبلغ حدُّه هو بطرقٍ متتابع عليه.
        HttpStatusCode last = HttpStatusCode.OK;

        for (int attempt = 0; attempt <= PerMinute + 1; attempt++)
        {
            using HttpResponseMessage response = await api.Call(Http.Request(
                HttpMethod.Post, Tenants, credential: null,
                $$"""
                {"requestKey":"{{TenantSignupTests.NewKey()}}","companyNameAr":"منشأة","ownerNameAr":"مالك"}
                """));

            last = response.StatusCode;

            if (last == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }
}
