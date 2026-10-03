using System.Net;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>سطرُ القيد اليدوي يسمّي حساباً على السلك</b> (ADR-0096).
/// <para>
/// السطر يحمل إمّا <c>role</c> وإمّا <c>accountCode</c> — واحداً بالضبط — والطلب من نوعٍ
/// واحد؛ والسطح يقرأ الشكل ويرفض ما خالفه بـ400 برمزٍ ثابت، ولا يقرّر في معنى الحساب شيئاً:
/// الدفتر يفحصه بالدليل ويكتبه. وحمولاتُ الدور في <see cref="Payloads"/> تمرّ كما كانت.
/// </para>
/// </summary>
public sealed class ManualVoucherNamesAnAccountOnTheWireTests
{
    [Fact]
    public async Task سطر_يحمل_الدور_ورمز_الحساب_معاً_يُرفض_400_برمز_ثابت()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA,
            AccountEntry(Payloads.Key("both"), debitExtra: "\"role\": \"Settlement\", \"qualifier\": \"bank\",")));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("wire.line.role_or_account", Http.CodeOf(problem));
    }

    [Fact]
    public async Task سطر_بلا_دور_وبلا_رمز_حساب_يُرفض_400_بالرمز_نفسه()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA,
            AccountEntry(Payloads.Key("neither"), creditAccount: null)));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("wire.line.role_or_account", Http.CodeOf(problem));
    }

    [Fact]
    public async Task خلطُ_سطر_دور_مع_سطر_حساب_في_طلب_واحد_يُرفض_400_برمز_ثابت()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA,
            AccountEntry(Payloads.Key("mixed"), creditAccount: null, creditExtra: "\"role\": \"NetAmount\",")));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("wire.lines.mixed_kinds", Http.CodeOf(problem));
    }

    [Fact]
    public async Task سطور_الحساب_على_حدث_القيد_اليدوي_تُرحَّل_201_والسطور_المكتوبة_تحمل_رموزها()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA,
            AccountEntry(Payloads.Key("account-post"))));

        (string text, JsonElement receipt) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(receipt.GetProperty("alreadyPosted").GetBoolean());
        Assert.Equal(2, receipt.GetProperty("lineCount").GetInt32());

        // القراءة من الدفتر نفسه: السطران كُتبا بالحسابين المسمَّيَين ودورٍ فارغ — لا دورٌ مُخترَع.
        Guid entryId = Guid.Parse(receipt.GetProperty("entryId").GetString()!);
        List<(string Account, string Role)> lines = await LinesOfAsync(entryId);

        Assert.Equal([(DebitAccount, string.Empty), (CreditAccount, string.Empty)], lines);
    }

    [Fact]
    public async Task سطور_الحساب_على_حدث_لا_تُعلن_المصفوفة_سطوره_يدوية_تُرفض_422_برمز_الدفتر()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA,
            AccountEntry(Payloads.Key("account-import"), @event: "ledger.opening_balance.posted")));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ledger.posting.event_takes_no_manual_lines", Http.CodeOf(problem));
    }

    /// <summary>نقد في الطريق — تفصيلي بلا بُعد إلزامي وبلا دفتر مساعد.</summary>
    private const string DebitAccount = "1204";

    /// <summary>حساب معلَّق لأرصدة الافتتاح — تفصيلي بلا شروط.</summary>
    private const string CreditAccount = "3901";

    /// <summary>قيد يدوي بسطرَي حساب — نصّاً خامّاً كسائر حمولات هذه المجموعة.</summary>
    private static string AccountEntry(
        string idempotencyKey,
        string? debitAccount = DebitAccount,
        string? creditAccount = CreditAccount,
        string debitExtra = "",
        string creditExtra = "",
        string @event = Payloads.ManualVoucherEvent) => $$"""
        {
          "event": {{Payloads.Quote(@event)}},
          "idempotencyKey": {{Payloads.Quote(idempotencyKey)}},
          "source": { "module": "Ledger", "documentType": "ManualJournal", "documentId": {{Payloads.Quote(idempotencyKey)}} },
          "trigger": "OnApproval",
          "documentDate": "2026-08-15",
          "narration": { "ar": "قيد يدوي بحسابات مسمّاة", "en": "Manual journal naming its accounts" },
          "lines": [
            {
              {{debitExtra}}
              {{(debitAccount is null ? string.Empty : "\"accountCode\": " + Payloads.Quote(debitAccount) + ",")}}
              "side": "Debit", "amount": "250.0000",
              "scope": { "branchId": "BR-01" },
              "narration": { "ar": "مدين", "en": "Debit" }
            },
            {
              {{creditExtra}}
              {{(creditAccount is null ? string.Empty : "\"accountCode\": " + Payloads.Quote(creditAccount) + ",")}}
              "side": "Credit", "amount": "250.0000",
              "scope": { "branchId": "BR-01" },
              "narration": { "ar": "دائن", "en": "Credit" }
            }
          ]
        }
        """;

    private static async Task<List<(string Account, string Role)>> LinesOfAsync(Guid entryId)
    {
        await using NpgsqlConnection connection = new(ApiTestDatabase.Options.AppConnectionString);
        await connection.OpenAsync(ApiFixture.Token);

        await using NpgsqlCommand command = new(
            "select account_code, role_code from ledger.journal_line where entry_id = $1 order by line_no", connection);
        command.Parameters.AddWithValue(entryId);

        List<(string, string)> lines = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(ApiFixture.Token);
        while (await reader.ReadAsync(ApiFixture.Token))
        {
            lines.Add((reader.GetString(0), reader.GetString(1)));
        }

        return lines;
    }
}
