using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>الاعتماد يُصدَر، ويدور، ويُبطَل — من الشبكة، بعميل لا يملك إلا عنواناً وجسم طلب.</b>
/// <para>
/// وقبل هذا السطح كان الاعتماد يُحقن عند الإقلاع من الإعداد: لا يُصدَر، ولا يدور، ولا
/// يُبطَل، ولا ينقضي إلا بلحظةٍ ساكنة تُكتب بيد. وهو شكلٌ يكفي عرضاً ولا يُباع —
/// خدمةٌ تُباع بالاشتراك لا يوجد فيها طريق لأن يُنشئ عميلٌ اعتماده ولا لأن يسحبه حين
/// يترك موظّفٌ عمله.
/// </para>
/// <para>
/// وكل ما هنا يُفحص من حيث يقع فعلاً: من HTTP، بالاستجابات نفسها التي تصل عميلاً حقيقياً.
/// </para>
/// </summary>
public sealed class AccessSurfaceTests
{
    /// <summary>مسار فتح الجلسة — بلا مصادقة، والاعتماد في الجسم.</summary>
    private const string Sessions = "/api/v1/access/sessions";

    /// <summary>مسار تجديد الجلسة.</summary>
    private const string Renewal = "/api/v1/access/sessions/renewal";

    /// <summary>مسار إبطال الجلسة.</summary>
    private const string Revocation = "/api/v1/access/sessions/revocation";

    /// <summary>مسار فتح الجلسة ببريدٍ وكلمة مرور — بلا مصادقة.</summary>
    private const string ByPassword = "/api/v1/access/sessions/password";

    /// <summary>مسار ضبط معرّف الدخول وكلمته — مصادَقٌ عليه، ولصاحب الجلسة وحده.</summary>
    private const string Password = "/api/v1/access/password";

    private static string Memberships(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/memberships");

    [Fact]
    public async Task دعوةُ_عضوٍ_تُنتج_اعتماد_انتساب_يُبدَّل_بجلسة_تفتح_مسارات_المنشأة()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // ١ · التسجيل: يُسكّ للمدعوّ معرّف، ويُمنح دوره، ويخرج اعتماد انتسابه مرّة واحدة.
        (JsonElement granted, HttpStatusCode grantStatus) =
            await InviteAsync(api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "منى القحطاني", "Contributor");

        Assert.Equal(HttpStatusCode.Created, grantStatus);
        string enrolment = granted.GetProperty("enrolmentCredential").GetString()!;
        string invitedUser = granted.GetProperty("member").GetProperty("userId").GetString()!;
        Assert.Equal("Contributor", granted.GetProperty("member").GetProperty("role").GetString());

        // ٢ · الإصدار: الانتساب يُبدَّل بجلسة كاملة.
        (JsonElement session, HttpStatusCode openStatus) = await OpenAsync(api, enrolment);
        Assert.Equal(HttpStatusCode.Created, openStatus);

        Assert.Equal(invitedUser, session.GetProperty("userId").GetString());
        Assert.Equal(1, session.GetProperty("generation").GetInt32());
        Assert.False(session.GetProperty("writeReachesNothing").GetBoolean());
        Assert.Equal(
            ApiTestDatabase.CompanyA.ToString("D", CultureInfo.InvariantCulture),
            session.GetProperty("memberships")[0].GetProperty("companyId").GetString());

        // ٣ · والاعتماد المُصدَر يعمل على مسارات المنشأة كأي اعتماد آخر — لا باب خاصّ به.
        using HttpResponseMessage read = await api.Call(Http.Request(
            HttpMethod.Get,
            Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book),
            Bearer(session, "accessCredential")));

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    [Fact]
    public async Task اعتماد_الانتساب_يُقبل_مرّة_واحدة_والثانية_تُسمّى_باسمها()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        (JsonElement granted, _) =
            await InviteAsync(api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "سعد الدوسري", "Contributor");
        string enrolment = granted.GetProperty("enrolmentCredential").GetString()!;

        (_, HttpStatusCode first) = await OpenAsync(api, enrolment);
        Assert.Equal(HttpStatusCode.Created, first);

        using HttpResponseMessage second = await api.Call(Http.Request(
            HttpMethod.Post, Sessions, credential: null, Body("enrolmentCredential", enrolment)));

        (string text, JsonElement problem) = await Http.BodyAsync(second);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);

        // ورمزٌ يفترق عن «اعتماد غير مقبول» عمداً: «استُعملت دعوتك» يُخبر صاحبها أن شيئاً
        // وقع فيسأل عنه، و«غير مقبول» لا يتعلّم منه مختلِقٌ شيئاً.
        Assert.Equal("access.enrolment_consumed", Http.CodeOf(problem));
    }

    [Fact]
    public async Task التجديد_يدوّر_الاعتمادين_ويُبطل_القديم_ويرفع_رقم_الدورة()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        JsonElement session = await SessionAsync(api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "ريم العتيبي");

        string firstRefresh = session.GetProperty("refreshCredential").GetString()!;
        string firstAccess = session.GetProperty("accessCredential").GetString()!;

        using HttpResponseMessage renewed = await api.Call(Http.Request(
            HttpMethod.Post, Renewal, credential: null, Body("refreshCredential", firstRefresh)));

        (_, JsonElement next) = await Http.BodyAsync(renewed);
        Assert.Equal(HttpStatusCode.Created, renewed.StatusCode);

        // العائلة واحدة، والدورة تزيد، والاعتمادان جديدان — ولا واحد منهما يساوي سلفه.
        Assert.Equal(session.GetProperty("sessionId").GetString(), next.GetProperty("sessionId").GetString());
        Assert.Equal(2, next.GetProperty("generation").GetInt32());
        Assert.NotEqual(firstAccess, next.GetProperty("accessCredential").GetString());
        Assert.NotEqual(firstRefresh, next.GetProperty("refreshCredential").GetString());

        // والاعتماد الفاعل الجديد يعمل.
        using HttpResponseMessage read = await api.Call(Http.Request(
            HttpMethod.Get, Http.Session, Bearer(next, "accessCredential")));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    [Fact]
    public async Task اعتماد_تجديد_يُقدَّم_مرّتين_يُسقط_العائلة_كلّها_لا_الطلب_الثاني_وحده()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        JsonElement session = await SessionAsync(api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "خالد الشمري");

        string stolen = session.GetProperty("refreshCredential").GetString()!;

        // الاستعمال الأول — المشروع — ينجح ويُنتج اعتماداً فاعلاً جديداً.
        using HttpResponseMessage honest = await api.Call(Http.Request(
            HttpMethod.Post, Renewal, credential: null, Body("refreshCredential", stolen)));
        (_, JsonElement rotated) = await Http.BodyAsync(honest);
        Assert.Equal(HttpStatusCode.Created, honest.StatusCode);

        // والاعتماد الفاعل الناتج حيّ **قبل** إعادة الاستعمال — وإلا لما أثبت الفحص شيئاً.
        using HttpResponseMessage before = await api.Call(Http.Request(
            HttpMethod.Get, Http.Session, Bearer(rotated, "accessCredential")));
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        // الاستعمال الثاني — بالاعتماد نفسه — سرقة.
        using HttpResponseMessage replay = await api.Call(Http.Request(
            HttpMethod.Post, Renewal, credential: null, Body("refreshCredential", stolen)));

        (string text, JsonElement problem) = await Http.BodyAsync(replay);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("access.refresh_replayed", Http.CodeOf(problem));

        // ‏**وهذا هو بيت القصيد**: لا يُرفض الطلب الثاني وحده — تسقط العائلة كلّها، فالاعتماد
        // الفاعل الذي كان حيّاً قبل سطرين يموت الآن. والبديل — رفضُ الثاني وحده — يترك
        // سارقاً بجلسة حيّة ولا يعلم بذلك أحد.
        using HttpResponseMessage after = await api.Call(Http.Request(
            HttpMethod.Get, Http.Session, Bearer(rotated, "accessCredential")));

        (_, JsonElement denied) = await Http.BodyAsync(after);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Equal("auth.credential_revoked", Http.CodeOf(denied));

        // والتجديد بالاعتماد الجديد ساقطٌ كذلك: العائلة كلّها، لا فرعٌ منها.
        using HttpResponseMessage renewAgain = await api.Call(Http.Request(
            HttpMethod.Post, Renewal, credential: null,
            Body("refreshCredential", rotated.GetProperty("refreshCredential").GetString()!)));

        (_, JsonElement renewProblem) = await Http.BodyAsync(renewAgain);
        Assert.Equal(HttpStatusCode.Unauthorized, renewAgain.StatusCode);
        Assert.Equal("access.session_revoked", Http.CodeOf(renewProblem));
    }

    [Fact]
    public async Task الإبطال_يقع_فوراً_ولا_يُنتظر_به_انقضاء()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        JsonElement session = await SessionAsync(api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "نورة الحربي");

        // الاعتماد حيّ الآن — والانقضاء بعد ربع ساعة، فما يلي ليس مرور وقت.
        using HttpResponseMessage alive = await api.Call(Http.Request(
            HttpMethod.Get, Http.Session, Bearer(session, "accessCredential")));
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);

        using HttpResponseMessage revoked = await api.Call(Http.Request(
            HttpMethod.Post, Revocation, Bearer(session, "accessCredential")));

        (string revokedText, JsonElement revocation) = await Http.BodyAsync(revoked);
        Console.WriteLine(revokedText);

        Assert.Equal(HttpStatusCode.Created, revoked.StatusCode);
        Assert.Equal("signed_out", revocation.GetProperty("reason").GetString());
        Assert.Equal(session.GetProperty("sessionId").GetString(), revocation.GetProperty("sessionId").GetString());

        // الطلب التالي مباشرة — لا بعد انقضاء — يُرفض برمزه المستقلّ.
        using HttpResponseMessage dead = await api.Call(Http.Request(
            HttpMethod.Get, Http.Session, Bearer(session, "accessCredential")));

        (_, JsonElement problem) = await Http.BodyAsync(dead);
        Assert.Equal(HttpStatusCode.Unauthorized, dead.StatusCode);
        Assert.Equal("auth.credential_revoked", Http.CodeOf(problem));

        // واعتماد التجديد لا يُحيي المُبطَل: الإبطال على العائلة لا على الاعتماد المفرد.
        using HttpResponseMessage renew = await api.Call(Http.Request(
            HttpMethod.Post, Renewal, credential: null,
            Body("refreshCredential", session.GetProperty("refreshCredential").GetString()!)));

        (_, JsonElement renewProblem) = await Http.BodyAsync(renew);
        Assert.Equal(HttpStatusCode.Unauthorized, renew.StatusCode);
        Assert.Equal("access.session_revoked", Http.CodeOf(renewProblem));
    }

    [Fact]
    public async Task اعتماد_مختلَق_على_بابَي_الجلسة_يُرفض_ولا_يُفرَّق_عن_غيره()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        foreach ((string path, string field) in new[] { (Sessions, "enrolmentCredential"), (Renewal, "refreshCredential") })
        {
            using HttpResponseMessage response = await api.Call(Http.Request(
                HttpMethod.Post, path, credential: null, Body(field, "ZmFrZS1jcmVkZW50aWFsLXRoYXQtd2FzLW5ldmVyLWlzc3VlZA")));

            (string text, JsonElement problem) = await Http.BodyAsync(response);
            Console.WriteLine($"{path} → {response.StatusCode} {Http.CodeOf(problem)}");

            // ‏401 لا 403: الفرق بينهما هو الفرق بين «لم تُصادِق» و«صادقتَ ومُنعت».
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("access.credential_rejected", Http.CodeOf(problem));

            // ولا يعبر من الرفض شيء عمّا في الخادم: لا معرّف جلسة، ولا مستأجر، ولا اعتماد.
            // (والمقارنة على الجسم بعد نزع instance — وهو **سطر الطلب الذي كتبه العميل
            // نفسه**، فوجودُ اسم المسار فيه ليس تسريباً بل صدىً لما أرسله.)
            string withoutInstance = WithoutInstance(text, problem);
            Assert.DoesNotContain("sessionId", withoutInstance, StringComparison.Ordinal);
            Assert.DoesNotContain("tenantId", withoutInstance, StringComparison.Ordinal);
            Assert.DoesNotContain("userId", withoutInstance, StringComparison.Ordinal);
            Assert.DoesNotContain("accessCredential", withoutInstance, StringComparison.Ordinal);
            Assert.DoesNotContain("refreshCredential", withoutInstance, StringComparison.Ordinal);

            // ولا يُردّ النصّ المُقدَّم إلى مُقدِّمه: صدىً كهذا يجعل السطح مرآةً تُستعمل
            // في تسميم سجلّات من يقرأ الاستجابة أو يخزّنها.
            Assert.DoesNotContain("ZmFrZS1jcmVk", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task دورُ_القارئ_يقرأ_ولا_يكتب_ورمزُه_يفترق_عن_رمز_الاستحقاق()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        JsonElement session = await SessionAsync(
            api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "هند الزهراني", role: "Reader");

        Assert.True(session.GetProperty("writeReachesNothing").GetBoolean());

        using HttpResponseMessage read = await api.Call(Http.Request(
            HttpMethod.Get,
            Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book),
            Bearer(session, "accessCredential")));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        using HttpResponseMessage write = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            Bearer(session, "accessCredential"),
            Payloads.BalancedEntry(Payloads.Key("reader-role"))));

        (string text, JsonElement problem) = await Http.BodyAsync(write);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);

        // ورمزه ليس entitlement.read_only: ذاك يقول «جدّد اشتراكك» وهذا يقول «اطلب صلاحية»،
        // وخلطهما يجعل قارئاً يتّصل بالمحاسبة بلا سبب.
        Assert.Equal("membership.read_only", Http.CodeOf(problem));
        Assert.NotEqual("entitlement.read_only", Http.CodeOf(problem));
    }

    [Fact]
    public async Task غيرُ_المالك_لا_يدعو_أحداً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        JsonElement session = await SessionAsync(
            api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "بدر السبيعي", role: "Contributor");

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post,
            Memberships(ApiTestDatabase.CompanyA),
            Bearer(session, "accessCredential"),
            """{"displayNameAr":"دعوةٌ لا تقع","role":"Owner"}"""));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("membership.inviter_is_not_an_owner", Http.CodeOf(problem));
    }

    [Fact]
    public async Task قائمةُ_الأعضاء_لا_تحمل_اعتماداً_واحداً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        (JsonElement granted, _) =
            await InviteAsync(api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "لطيفة العنزي", "Contributor");
        string enrolment = granted.GetProperty("enrolmentCredential").GetString()!;

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Memberships(ApiTestDatabase.CompanyA), ApiFixture.TokenA));

        (string text, JsonElement list) = await Http.BodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(list.GetProperty("memberCount").GetInt32() >= 1);

        // ‏**سالباً**: اعتماد الانتساب الذي أُصدر قبل سطور لا يظهر في القائمة، ولا أي نصّ
        // يصلح للاستعمال. المُودَع بصمةٌ، والنصّ خرج مرّة واحدة في استجابة الدعوة.
        Assert.DoesNotContain(enrolment, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Credential", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <b>البوّابة الأمامية كاملةً من الشبكة: أضبط كلمتي، ثم أدخل بها، ثم أعمل.</b>
    /// <para>
    /// وهذا هو الطريق الذي يسلكه محاسبٌ كلَّ صباح، ولم يكن موجوداً قبل ADR-0094:
    /// كان الدخول لصقَ اعتمادٍ مبهم — لغةُ من يجرّب بـcurl لا لغةُ من يعمل.
    /// </para>
    /// </summary>
    [Fact]
    public async Task أضبط_كلمتي_بجلستي_ثم_أدخل_بها_ثم_أبلغ_مسارات_منشأتي()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // ١ · جلسةٌ من الطريق القائم — الانتساب — هي إثباتُ من يضبط كلمته.
        JsonElement session = await SessionAsync(api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "سارة العتيبي");
        TestCredential mine = Bearer(session, "accessCredential");
        string handle = "sara." + Guid.NewGuid().ToString("N")[..8] + "@example.sa";

        // ٢ · الضبط. والمعرّف يُردّ **مُسوّى**، فيراه صاحبه كما سيكتبه.
        using (HttpResponseMessage set = await api.Call(Http.Request(
            HttpMethod.Put, Password, mine,
            $$"""{"handle":"  {{handle.ToUpperInvariant()}}  ","password":"a-long-enough-password"}""")))
        {
            (string text, JsonElement body) = await Http.BodyAsync(set);
            Assert.Equal(HttpStatusCode.OK, set.StatusCode);
            Assert.Equal(handle, body.GetProperty("handle").GetString());

            // ‏**سالباً: لا شيء عن كلمة المرور يعود** — لا نصّها ولا بصمتها ولا ملحها.
            Assert.DoesNotContain("a-long-enough-password", text, StringComparison.Ordinal);
            Assert.DoesNotContain("pbkdf2", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("proof", text, StringComparison.OrdinalIgnoreCase);
        }

        // ٣ · الدخول بها — **جلسةٌ كاملة كجلسة الانتساب سواءً بسواء**.
        JsonElement opened;
        using (HttpResponseMessage door = await api.Call(Http.Request(
            HttpMethod.Post, ByPassword, credential: null,
            $$"""{"handle":"{{handle}}","password":"a-long-enough-password"}""")))
        {
            (_, opened) = await Http.BodyAsync(door);
            Assert.Equal(HttpStatusCode.Created, door.StatusCode);
            Assert.Equal(session.GetProperty("userId").GetString(), opened.GetProperty("userId").GetString());
            Assert.Equal(1, opened.GetProperty("generation").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(opened.GetProperty("refreshCredential").GetString()));
        }

        // ٤ · والاعتمادُ المُصدَر يعمل على مسارات المنشأة — لا بابَ خاصّ به.
        using HttpResponseMessage read = await api.Call(Http.Request(
            HttpMethod.Get,
            Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book),
            Bearer(opened, "accessCredential")));

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    /// <summary>
    /// <b>ثلاثةُ رفضٍ برمزٍ واحد — وهو ما يمنع بابَ الدخول أن يصير كشّافَ عملاء.</b>
    /// <para>
    /// بريدٌ غير مسجَّل، وكلمةٌ خاطئة، وبريدٌ مشوّه: <c>access.credential_rejected</c>
    /// في ثلاثتها. ورمزٌ يقول «هذا البريد غير مسجَّل» يجعل التجريبَ يُخرج قائمةَ
    /// العملاء صفّاً صفّاً.
    /// </para>
    /// </summary>
    [Fact]
    public async Task الرفضُ_رمزٌ_واحد_لثلاثِ_حالاتٍ_فلا_يُعرَف_المسجَّل_من_غيره()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        JsonElement session = await SessionAsync(api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "خالد الدوسري");
        string handle = "khalid." + Guid.NewGuid().ToString("N")[..8] + "@example.sa";

        using (HttpResponseMessage set = await api.Call(Http.Request(
            HttpMethod.Put, Password, Bearer(session, "accessCredential"),
            $$"""{"handle":"{{handle}}","password":"a-long-enough-password"}""")))
        {
            Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        }

        foreach (string body in new[]
                 {
                     $$"""{"handle":"{{handle}}","password":"wrong-but-long-enough"}""",
                     """{"handle":"nobody-at-all@example.sa","password":"a-long-enough-password"}""",
                     """{"handle":"ليس بريداً","password":"a-long-enough-password"}""",
                 })
        {
            using HttpResponseMessage refused = await api.Call(
                Http.Request(HttpMethod.Post, ByPassword, credential: null, body));

            (_, JsonElement problem) = await Http.BodyAsync(refused);
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
            Assert.Equal("access.credential_rejected", problem.GetProperty("code").GetString());
        }
    }

    /// <summary>
    /// ومعرّفٌ مأخوذ يُقال لمن يضبط — <b>خلافاً لباب الدخول وعمداً</b>: من يضبط
    /// معرّفه يستحقّ أن يعرف ما يُصلحه، ومن يخمّن لا يبلغ هذا الباب بلا جلسة.
    /// </summary>
    [Fact]
    public async Task ومعرّفٌ_مأخوذٌ_يُقال_لمن_يضبط_وكلمةٌ_قصيرة_تُردّ_بطولها()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        string handle = "shared." + Guid.NewGuid().ToString("N")[..8] + "@example.sa";

        JsonElement first = await SessionAsync(api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "أوّلُ من سجّل");
        using (HttpResponseMessage set = await api.Call(Http.Request(
            HttpMethod.Put, Password, Bearer(first, "accessCredential"),
            $$"""{"handle":"{{handle}}","password":"a-long-enough-password"}""")))
        {
            Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        }

        JsonElement second = await SessionAsync(api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "ثاني من حاول");
        using (HttpResponseMessage taken = await api.Call(Http.Request(
            HttpMethod.Put, Password, Bearer(second, "accessCredential"),
            $$"""{"handle":"{{handle}}","password":"another-long-password"}""")))
        {
            (_, JsonElement problem) = await Http.BodyAsync(taken);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, taken.StatusCode);
            Assert.Equal("access.handle_taken", problem.GetProperty("code").GetString());
        }

        using (HttpResponseMessage tooShort = await api.Call(Http.Request(
            HttpMethod.Put, Password, Bearer(second, "accessCredential"),
            $$"""{"handle":"short.{{Guid.NewGuid():N}}@example.sa","password":"قصيرة"}""")))
        {
            (_, JsonElement problem) = await Http.BodyAsync(tooShort);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, tooShort.StatusCode);
            Assert.Equal("access.password_length_rejected", problem.GetProperty("code").GetString());
        }
    }

    /// <summary>
    /// <b>وضبطٌ ثانٍ ينقل المعرّف ولا يُضيفه</b>: البابُ القديم يُغلق في المعاملة نفسها.
    /// <para>
    /// ومعرّفان لمستخدمٍ واحد بابان لحسابٍ واحد، فسحبُ أحدهما يُقرأ «سُحب الوصول»
    /// وهو باقٍ على الآخر.
    /// </para>
    /// </summary>
    [Fact]
    public async Task وضبطٌ_ثانٍ_ينقل_المعرّف_ولا_يترك_البابَ_الأول_مفتوحاً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        JsonElement session = await SessionAsync(api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "من نقل بريده");
        TestCredential mine = Bearer(session, "accessCredential");

        string first = "old." + Guid.NewGuid().ToString("N")[..8] + "@example.sa";
        string second = "new." + Guid.NewGuid().ToString("N")[..8] + "@example.sa";

        foreach (string handle in new[] { first, second })
        {
            using HttpResponseMessage set = await api.Call(Http.Request(
                HttpMethod.Put, Password, mine,
                $$"""{"handle":"{{handle}}","password":"a-long-enough-password"}"""));
            Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        }

        using (HttpResponseMessage closed = await api.Call(Http.Request(
            HttpMethod.Post, ByPassword, credential: null,
            $$"""{"handle":"{{first}}","password":"a-long-enough-password"}""")))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, closed.StatusCode);
        }

        using HttpResponseMessage opens = await api.Call(Http.Request(
            HttpMethod.Post, ByPassword, credential: null,
            $$"""{"handle":"{{second}}","password":"a-long-enough-password"}"""));

        Assert.Equal(HttpStatusCode.Created, opens.StatusCode);
    }

    /// <summary>يدعو عضواً ويُعيد استجابة الدعوة.</summary>
    private static async Task<(JsonElement Body, HttpStatusCode Status)> InviteAsync(
        ApiProcess api, Guid company, TestCredential inviter, string nameAr, string role)
    {
        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post,
            Memberships(company),
            inviter,
            $$"""{"displayNameAr":"{{nameAr}}","role":"{{role}}"}"""));

        (_, JsonElement body) = await Http.BodyAsync(response);
        return (body, response.StatusCode);
    }

    private static async Task<(JsonElement Body, HttpStatusCode Status)> OpenAsync(ApiProcess api, string enrolment)
    {
        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Sessions, credential: null, Body("enrolmentCredential", enrolment)));

        (_, JsonElement body) = await Http.BodyAsync(response);
        return (body, response.StatusCode);
    }

    /// <summary>يدعو عضواً ثم يفتح له جلسة — الطريق الكامل الذي يسلكه عميل حقيقي.</summary>
    internal static async Task<JsonElement> SessionAsync(
        ApiProcess api, Guid company, TestCredential inviter, string nameAr, string role = "Contributor")
    {
        (JsonElement granted, HttpStatusCode grantStatus) = await InviteAsync(api, company, inviter, nameAr, role);
        Assert.Equal(HttpStatusCode.Created, grantStatus);

        (JsonElement session, HttpStatusCode openStatus) =
            await OpenAsync(api, granted.GetProperty("enrolmentCredential").GetString()!);
        Assert.Equal(HttpStatusCode.Created, openStatus);

        return session;
    }

    /// <summary>اعتمادٌ مُصدَر ملفوفاً في نوع الاعتماد الاختباري نفسه — فيُقدَّم كما يُقدَّمه عميل.</summary>
    internal static TestCredential Bearer(JsonElement session, string field) => new(
        session.GetProperty(field).GetString()!,
        Guid.Parse(session.GetProperty("tenantId").GetString()!),
        Guid.Parse(session.GetProperty("userId").GetString()!),
        []);

    private static string Body(string field, string value) =>
        $$"""{"{{field}}":"{{value}}"}""";

    /// <summary>
    /// الجسم بعد نزع قيمة <c>instance</c>.
    /// <para>
    /// و<c>instance</c> هو <b>مسار الطلب كما كتبه العميل</b>، فوجود اسم المسار فيه صدىً
    /// لما أرسله لا تسريباً من الخادم. ونزعُه يجعل الإثبات السالب يقول ما يقصده بالضبط:
    /// «لا شيء من حالة الخادم عبر».
    /// </para>
    /// </summary>
    internal static string WithoutInstance(string text, JsonElement problem) =>
        text.Replace(problem.GetProperty("instance").GetString() ?? string.Empty, "«المسار»", StringComparison.Ordinal);
}
