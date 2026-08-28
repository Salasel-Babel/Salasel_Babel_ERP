using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>‏ADR-0034 من طرفٍ إلى طرف، على السلك: من انقطع اشتراكه يدخل ويقرأ، ويُردّ عند
/// أول كتابة.</b>
/// <para>
/// وكان هذا القرار مُنفَّذاً في النواة ومُثبَتاً على مستأجرٍ <b>مبذور من الإعداد</b>؛
/// وما يُثبته هذا الملفّ أنه صحيح عن <b>الطريق كاملاً</b>: تسجيلٌ من الباب المفتوح، ثم
/// جلسة، ثم كتابةٌ ناجحة، ثم انقطاعٌ يُطلَب من سطح الاشتراك، ثم <b>قراءةٌ ناجحة بعده</b>،
/// ثم أول كتابة تُردّ برسالةٍ تُسمّي السبب بالعربية والإنجليزية.
/// </para>
/// <para>
/// <b>وأهمّ خطوة فيه هي الخامسة</b> — القراءة بعد الانقطاع. فالخطأ الذي يقع بسهولة هو
/// أن يُجعل الدخول نفسه مشروطاً بالاستحقاق، فيصير «التخفيض إلى القراءة» حجباً باسم آخر
/// ويسقط القرار من بابه الخلفي. ولذلك يقرأ هذا الاختبار <b>ميزان المراجعة</b> بعد
/// الانقطاع ويشترط 200، لا مجرّد أن الجلسة تُفتح.
/// </para>
/// </summary>
public sealed class LapsedSubscriptionKeepsReadingTests
{
    [Fact]
    public async Task مستأجرٌ_انقطع_اشتراكه_يدخل_ويقرأ_ميزان_المراجعة_ويُردّ_عند_أول_كتابة()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // ١ · التسجيل الأول: مستأجر جديد، وجلسة مالكه.
        (Guid tenantId, TestCredential owner, Guid companyId) = await SubscriptionSurfaceTests.SignUpAsync(api);

        // ٢ · التأسيس — وهو كتابة، وتنجح لأن الاشتراك فعّال.
        using (HttpResponseMessage founded = await api.Call(Http.Request(
            HttpMethod.Put,
            string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{companyId:D}/setup"),
            owner,
            """{"companyNameAr":"منشأة الاشتراك المنقطع","costCenters":"One","decimalPlaces":2}""")))
        {
            (string founding, _) = await Http.BodyAsync(founded);
            Console.WriteLine(founding);
            Assert.Equal(HttpStatusCode.Created, founded.StatusCode);
        }

        // ٣ · وقراءةٌ قبل الانقطاع: ميزان المراجعة يُخدَم.
        using (HttpResponseMessage before = await api.Call(Http.Request(
            HttpMethod.Get, Http.TrialBalance(companyId, ApiTestDatabase.Book), owner)))
        {
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        }

        // ٤ · الانقطاع — فعلُ مشغِّل بسندٍ مكتوب.
        using (HttpResponseMessage lapsed = await api.Call(Http.Request(
            HttpMethod.Post,
            SubscriptionSurfaceTests.Lapse(tenantId),
            ApiFixture.TokenA,
            """{"authority":"حدث-سداد-٤٤١","reasonAr":"انقطع السداد في اختبار من طرف إلى طرف"}""")))
        {
            (string text, JsonElement subscription) = await Http.BodyAsync(lapsed);
            Console.WriteLine(text);

            Assert.Equal(HttpStatusCode.Created, lapsed.StatusCode);
            Assert.Equal("Lapsed", subscription.GetProperty("state").GetString());

            // ولا تجديد على اشتراك ليس فعّالاً: تاريخٌ يُعرض هنا يُقرأ وعداً بعودةٍ لا تقع.
            Assert.Equal(JsonValueKind.Null, subscription.GetProperty("renewsOn").ValueKind);

            // والوحدات هبطت إلى **أرضيتها** لا إلى العدم.
            Dictionary<string, string> modules = TenantSignupTests.ModulesOf(subscription);
            Assert.Equal("ReadOnly", modules["CORE"]);
            Assert.Equal("ReadOnly", modules["AR"]);
            Assert.Equal("ReadOnly", modules["AP"]);
        }

        // ٥ · **والجلسة ما تزال تُفتح والقراءة ما تزال تُخدَم** — وهذا هو بيت القصيد.
        using (HttpResponseMessage after = await api.Call(Http.Request(
            HttpMethod.Get, Http.TrialBalance(companyId, ApiTestDatabase.Book), owner)))
        {
            (string text, _) = await Http.BodyAsync(after);
            Console.WriteLine(text);
            Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        }

        // ٦ · وأول كتابة تُردّ — برمزٍ ثابت ورسالةٍ **تُسمّي السبب** بلغتين.
        using (HttpResponseMessage refused = await api.Call(Http.Request(
            HttpMethod.Post,
            string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{companyId:D}/cost-centers"),
            owner,
            """{"nameAr":"الفرع الغربي"}""")))
        {
            (string text, JsonElement problem) = await Http.BodyAsync(refused);
            Console.WriteLine(text);

            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            Assert.Equal("entitlement.read_only", Http.CodeOf(problem));

            string ar = problem.GetProperty("detailAr").GetString()!;
            string en = problem.GetProperty("detail").GetString()!;

            // والرسالة تُسمّي السبب فلا تُقرأ عطلاً تقنياً، وتقول ما الذي يبقى متاحاً.
            Assert.Contains("الاشتراك", ar, StringComparison.Ordinal);
            Assert.Contains("subscription", en, StringComparison.OrdinalIgnoreCase);
        }

        // ٧ · ثم الاستئناف يُعيد الكتابة كما كانت، ولم تُفقد بيانة واحدة في الأثناء.
        using (HttpResponseMessage resumed = await api.Call(Http.Request(
            HttpMethod.Post,
            SubscriptionSurfaceTests.Resumption(tenantId),
            ApiFixture.TokenA,
            """{"authority":"حدث-سداد-٤٤٢","reasonAr":"استُؤنف السداد"}""")))
        {
            (string text, JsonElement subscription) = await Http.BodyAsync(resumed);
            Console.WriteLine(text);

            Assert.Equal(HttpStatusCode.Created, resumed.StatusCode);
            Assert.Equal("Active", subscription.GetProperty("state").GetString());
            Assert.Equal("Entitled", TenantSignupTests.ModulesOf(subscription)["CORE"]);
        }

        using (HttpResponseMessage written = await api.Call(Http.Request(
            HttpMethod.Post,
            string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{companyId:D}/cost-centers"),
            owner,
            """{"nameAr":"الفرع الغربي"}""")))
        {
            (string text, _) = await Http.BodyAsync(written);
            Console.WriteLine(text);
            Assert.Equal(HttpStatusCode.Created, written.StatusCode);
        }
    }
}
