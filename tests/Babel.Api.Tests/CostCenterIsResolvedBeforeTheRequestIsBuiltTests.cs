using System.Globalization;
using System.Net;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>مركز التكلفة يُحلّ عند البوّابة، قبل أن يُبنى الطلب.</b>
/// <para>
/// ‏ADR-0026: لكل منشأة مركز تكلفة واحد على الأقل، والمذكور يُقبل إن كان عاملاً، وإن لم
/// يُذكر شيء فالمركز الافتراضي. وكانت الثابتة مفروضة عند حدٍّ واحد — التأسيس — ومنقوضة
/// عند حدَّين: <c>PostingScope.CostCenterId</c> كان <c>string?</c>، والعقد كان يسمح
/// بغياب الحقل وبقيمة <c>null</c>. وهذه المجموعة تفحص الحدّ الذي أُغلق: <b>السطح يسأل
/// النواة ثم يبني</b>، فلا يبلغ الدفتر سطرٌ بلا مركز.
/// </para>
/// <para>
/// <b>والقراءة من قاعدة البيانات لا من الاستجابة</b> — عمداً: العقد لا ينشر النطاق
/// التحليلي على قراءة القيد، والمقصود إثبات <b>ما كُتب</b> لا ما يُعرَض.
/// </para>
/// </summary>
public sealed class CostCenterIsResolvedBeforeTheRequestIsBuiltTests
{
    /// <summary>رمز أول مركز في أي منشأة — ما يُسكّه التأسيس.</summary>
    private const string Default = "cc.001";

    [Fact]
    public async Task طلبٌ_بلا_مركز_تكلفة_يُرحَّل_على_المركز_الافتراضي_ولا_يكتب_فراغاً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        string key = Payloads.Key("cc-default");

        using HttpResponseMessage posted = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            Payloads.BalancedEntry(key, documentDate: "2026-11-05")));

        (string text, JsonElement receipt) = await Http.BodyAsync(posted);
        Console.WriteLine(text);
        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);

        Guid entryId = Guid.Parse(receipt.GetProperty("entryId").GetString()!, CultureInfo.InvariantCulture);
        string[] centres = await CostCentersOfAsync(entryId);

        Console.WriteLine("مراكز سطور القيد: " + string.Join(" · ", centres));

        // سطران: أحدهما لا يحمل scope إطلاقاً، والآخر يحمل فرعاً بلا مركز. وكلاهما
        // يُرحَّل على الافتراضي — لا على null، ولا على نصّ فارغ.
        Assert.Equal(2, centres.Length);
        Assert.All(centres, centre => Assert.Equal(Default, centre));
    }

    [Fact]
    public async Task المركز_المُسمّى_يُحترَم_ولا_يرتدّ_إلى_الافتراضي()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // مركز ثانٍ يُضاف إلى المنشأة — فالاختبار يبني الحالة التي يفحصها.
        using HttpResponseMessage added = await api.Call(Http.Request(
            HttpMethod.Post,
            CostCenters(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            """{"nameAr":"فرع الرياض"}"""));

        (string addedText, _) = await Http.BodyAsync(added);
        Console.WriteLine(addedText);
        Assert.True(
            added.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
            "تعذّرت إضافة مركز ثانٍ: " + addedText);

        string key = Payloads.Key("cc-named");
        using HttpResponseMessage posted = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            Payloads.BalancedEntry(key, documentDate: "2026-11-06", costCenterId: "cc.002")));

        (string text, JsonElement receipt) = await Http.BodyAsync(posted);
        Console.WriteLine(text);
        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);

        Guid entryId = Guid.Parse(receipt.GetProperty("entryId").GetString()!, CultureInfo.InvariantCulture);
        string[] centres = await CostCentersOfAsync(entryId);

        Console.WriteLine("مراكز سطور القيد: " + string.Join(" · ", centres));

        // والحلّ **لكل سطر على حدة**، لا للطلب كلّه: السطر الأول لا يسمّي مركزاً فيأخذ
        // الافتراضي، والثاني يسمّي cc.002 فيأخذه. وحلٌّ على مستوى الطلب كان سيبتلع
        // أحدهما بصمت — وهو نفس صنف العطب الذي أُغلق هنا.
        Assert.Equal([Default, "cc.002"], centres);
    }

    [Fact]
    public async Task مركزٌ_مُسمّى_لا_وجود_له_يُرفض_باسمه_ولا_يرتدّ_إلى_الافتراضي_بصمت()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            Payloads.BalancedEntry(Payloads.Key("cc-ghost"), costCenterId: "cc.999")));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        // والارتداد الصامت هو العطل: قيدٌ يُرحَّل على مركز غير الذي طُلب لا يُبلَّغ عنه أبداً.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("cost_center.not_found", Http.CodeOf(problem));
    }

    [Fact]
    public async Task منشأةٌ_لم_تُؤسَّس_لا_تُرحّل_لأن_لا_مركز_لها_أصلاً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // منشأة يبلغها اعتماد التأسيس ولم تُؤسَّس بعد — والحالة يبنيها الاختبار.
        Guid unfounded = ApiFixture.SetupCompanies[^1];

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(unfounded),
            ApiFixture.TokenS,
            Payloads.BalancedEntry(Payloads.Key("cc-unfounded"))));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("company_setup.not_found", Http.CodeOf(problem));
    }

    /// <summary>
    /// <b>ولا سطر واحد في الدفتر كلّه بلا مركز</b> — بعد كل ما رحّلته هذه المجموعة وغيرها.
    /// <para>
    /// وهذا الفحص هو الذي يجعل ما سبقه غير شكليّ: حالةٌ واحدة تُثبت مساراً، ومسحُ الجدول
    /// كلّه يُثبت أن لا مساراً آخر يفلت. والقيد في قاعدة البيانات يمنع ذلك بنيوياً؛ وهذا
    /// يقرؤه من الجدول لا من تعريف القيد.
    /// </para>
    /// </summary>
    [Fact]
    public async Task لا_سطر_واحد_في_الدفتر_كلّه_بلا_مركز_تكلفة()
    {
        // الترحيل أولاً كي لا يمرّ الفحص على جدول فارغ — مسحٌ لا يقرأ شيئاً يمرّ دائماً.
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage posted = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            Payloads.BalancedEntry(Payloads.Key("cc-sweep"), documentDate: "2026-11-07")));

        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);

        await using NpgsqlConnection connection = new(ApiTestDatabase.Options.AppConnectionString);
        await connection.OpenAsync(ApiFixture.Token);

        await using NpgsqlCommand command = new(
            """
            select count(*) filter (where cost_center_id is null or length(btrim(cost_center_id)) = 0),
                   count(*)
              from ledger.journal_line
            """, connection);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(ApiFixture.Token);
        Assert.True(await reader.ReadAsync(ApiFixture.Token));

        long blank = reader.GetInt64(0);
        long total = reader.GetInt64(1);

        Console.WriteLine(FormattableString.Invariant($"سطور الدفتر: {total} · منها بلا مركز: {blank}"));

        Assert.True(total > 0, "الجدول فارغ، فالمسح لا يفحص شيئاً.");
        Assert.Equal(0, blank);
    }

    private static async Task<string[]> CostCentersOfAsync(Guid entryId)
    {
        await using NpgsqlConnection connection = new(ApiTestDatabase.Options.AppConnectionString);
        await connection.OpenAsync(ApiFixture.Token);

        await using NpgsqlCommand command = new(
            "select cost_center_id from ledger.journal_line where entry_id = $1 order by line_no", connection);
        command.Parameters.AddWithValue(entryId);

        List<string> centres = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(ApiFixture.Token);

        while (await reader.ReadAsync(ApiFixture.Token))
        {
            centres.Add(reader.IsDBNull(0) ? "«فراغ»" : reader.GetString(0));
        }

        return [.. centres];
    }

    private static string CostCenters(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/cost-centers");
}
