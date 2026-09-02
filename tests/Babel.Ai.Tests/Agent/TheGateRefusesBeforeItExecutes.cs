using System.Text.Json;
using System.Text.Json.Nodes;
using Babel.Ai.Agent;
using Babel.Ai.Lookup;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Agent;

/// <summary>
/// <b>البوّابة ترفض <u>قبل</u> التنفيذ لا بعده — سبعُ خطواتٍ بترتيبٍ كلّيّ.</b>
/// <para>
/// و«قبل لا بعد» ليست تفصيلاً: رفضٌ بعد التنفيذ يعني أن المسوّدة كُتبت، وأن المِقبض
/// فُكّ، وأن الصفّ قُرئ. والفرق كلّه في أنّ <see cref="AgentDispatch"/> لا يوجد قبل
/// أن تمرّ الخطوات السبع.
/// </para>
/// </summary>
public sealed class TheGateRefusesBeforeItExecutes
{
    private static readonly TenantId Here = new(new Guid("a9e70000-0000-4000-8000-000000000001"));
    private static readonly TenantId Elsewhere = new(new Guid("a9e70000-0000-4000-8000-000000000002"));
    private static readonly Guid Company = new("a9e70000-0000-4000-8000-0000000000c1");
    private static readonly Guid OtherCompany = new("a9e70000-0000-4000-8000-0000000000c2");
    private static readonly Guid Session = new("a9e70000-0000-4000-8000-0000000000f1");
    private static readonly Guid OtherSession = new("a9e70000-0000-4000-8000-0000000000f2");
    private static readonly Guid CustomerRow = new("a9e70000-0000-4000-8000-0000000000d1");

    private static readonly MovableClock Clock = new();
    private static readonly SignedLookupHandles Handles = AgentHarness.Handles(Clock);
    private static readonly AgentToolCatalogue Catalogue = AgentToolCatalogue.Embedded;

    private static AgentCaller Caller(params string[] permitted) =>
        AgentHarness.Caller(Here, Company, Session, permitted);

    private static AgentTurnState State() => new(4);

    private static string EntityHandle(TenantId tenant, Guid company, Guid session, Guid subject) =>
        Handles.Issue(LookupHandlePurpose.Entity, tenant, company, session, subject, TimeSpan.FromMinutes(10)).Value;

    private static AgentToolCall Draft(object arguments) =>
        new("tu_1", "draftSalesInvoice", JsonSerializer.Serialize(arguments));

    private static object InvoiceWith(string customerId, string branchId) => new
    {
        branchId,
        customerId,
        issuedOn = "2026-03-01",
        lines = new[] { new { description = new { ar = "بند", en = "line" }, discount = "0", itemGroup = "GOODS", quantity = "1", taxClassification = "standard", taxRate = "0.15", unitPrice = "100" } },
        number = "INV-1",
    };

    // ── ١ · الكتالوج مغلق ─────────────────────────────────────────────────────

    /// <summary>اسمٌ ليس في الكتالوج لا يُنفَّذ ولا يُقارَب بأقرب شبيه.</summary>
    [Theory]
    [InlineData("postSalesInvoice")]
    [InlineData("reverseJournalEntry")]
    [InlineData("terminateEmployee")]
    [InlineData("readSalesInvoice")]
    [InlineData("bash")]
    public void اسمٌ_خارج_الكتالوج_يُرفض(string name)
    {
        Result<AgentDispatch> gated = AgentToolGate.Authorise(
            new AgentToolCall("tu_1", name, "{}"), Caller(name), State(), Catalogue, Handles);

        Assert.True(gated.IsFailure);
        Assert.Equal("ai.agent.tool_unknown", Assert.Single(gated.Errors).Code);
    }

    // ── ٥ · المِقبض لا المعرّف ────────────────────────────────────────────────

    /// <summary>
    /// <b>معرّفٌ خام يكتبه النموذج من عنده يُرفض</b> — ولا يُسأل عنه السجلّ «فلعلّه موجود»،
    /// فسؤالٌ كهذا هو بعينه تسريبُ وجودٍ من عدم.
    /// </summary>
    [Fact]
    public void معرّفٌ_خام_في_موضع_مِقبضٍ_يُرفض()
    {
        Result<AgentDispatch> gated = AgentToolGate.Authorise(
            Draft(InvoiceWith(CustomerRow.ToString(), "BR-1")),
            Caller("draftSalesInvoice"), State(), Catalogue, Handles);

        Assert.True(gated.IsFailure);
        Assert.Contains(gated.Errors, static error => error.Code == "ai.agent.handle_required");
    }

    /// <summary>ونصٌّ ليس مِقبضاً ولا معرّفاً يسقط عند التوقيع.</summary>
    [Fact]
    public void نصٌّ_مخترَع_في_موضع_مِقبضٍ_يسقط_عند_التوقيع()
    {
        Result<AgentDispatch> gated = AgentToolGate.Authorise(
            Draft(InvoiceWith("شركة المسار الامثل", "BR-1")),
            Caller("draftSalesInvoice"), State(), Catalogue, Handles);

        Assert.True(gated.IsFailure);
        Assert.Contains(gated.Errors, static error => error.Code == "ai.lookup.handle_not_signed");
    }

    /// <summary>مِقبضٌ من منشأةٍ أخرى لا يُفكّ هنا — الطبقة الأولى من طبقتَي المنع.</summary>
    [Fact]
    public void مِقبضٌ_من_منشأةٍ_أخرى_لا_يُفكّ()
    {
        string foreign = EntityHandle(Elsewhere, Company, Session, CustomerRow);

        Result<AgentDispatch> gated = AgentToolGate.Authorise(
            Draft(InvoiceWith(foreign, "BR-1")),
            Caller("draftSalesInvoice"), State(), Catalogue, Handles);

        Assert.True(gated.IsFailure);
        Assert.Contains(gated.Errors, static error => error.Code == "ai.lookup.handle_out_of_scope");
    }

    /// <summary>ومن شركةٍ أخرى، ومن جلسةٍ أخرى — والرسالة واحدة في الثلاث.</summary>
    [Theory]
    [InlineData("company")]
    [InlineData("session")]
    public void مِقبضٌ_من_شركةٍ_أو_جلسةٍ_أخرى_لا_يُفكّ(string which)
    {
        string foreign = which == "company"
            ? EntityHandle(Here, OtherCompany, Session, CustomerRow)
            : EntityHandle(Here, Company, OtherSession, CustomerRow);

        Result<AgentDispatch> gated = AgentToolGate.Authorise(
            Draft(InvoiceWith(foreign, "BR-1")),
            Caller("draftSalesInvoice"), State(), Catalogue, Handles);

        Assert.True(gated.IsFailure);
        Assert.Contains(gated.Errors, static error => error.Code == "ai.lookup.handle_out_of_scope");
    }

    /// <summary>
    /// <b>ومعرّفُ ورقةٍ لا يُفكّ كِياناً.</b> الغرض داخل البايتات الموقَّعة، فلا يُبدَّل
    /// بلا أن يبطل التوقيع.
    /// </summary>
    [Fact]
    public void معرّفُ_ورقةٍ_في_موضع_كِيانٍ_يُرفض()
    {
        string question = Handles
            .Issue(LookupHandlePurpose.Question, Here, Company, Session, Guid.NewGuid(), TimeSpan.FromMinutes(10))
            .Value;

        Result<AgentDispatch> gated = AgentToolGate.Authorise(
            Draft(InvoiceWith(question, "BR-1")),
            Caller("draftSalesInvoice"), State(), Catalogue, Handles);

        Assert.True(gated.IsFailure);
        Assert.Contains(gated.Errors, static error => error.Code == "ai.lookup.handle_purpose_mismatch");
    }

    // ── ٤ · المِصفاة على الوسائط ──────────────────────────────────────────────

    /// <summary>
    /// ما شكلُه معرّفٌ في وسيطٍ نصّي يُرفض — <b>والاتجاه لا يهمّ</b>: القيمة ستُكتب في
    /// جسم مسوّدة وتُقرأ في صدىً يعود إلى النموذج.
    /// </summary>
    [Theory]
    [InlineData("1012345678", "ai.agent.identifier_refused.national_id")]
    [InlineData("SA03 8000 0000 6080 1016 7519", "ai.agent.identifier_refused.iban")]
    [InlineData("300123456789003", "ai.agent.identifier_refused.vat")]
    public void شكلُ_معرّفٍ_في_وسيطٍ_نصّي_يُرفض(string value, string code)
    {
        object arguments = new
        {
            branchId = "BR-1",
            customerId = "H",
            issuedOn = "2026-03-01",
            lines = Array.Empty<object>(),
            number = value,
        };

        Result<AgentDispatch> gated = AgentToolGate.Authorise(
            Draft(arguments), Caller("draftSalesInvoice"), State(), Catalogue, Handles);

        Assert.True(gated.IsFailure);
        Assert.Contains(gated.Errors, error => error.Code == code);
    }

    // ── ٧ · الاستحقاق ─────────────────────────────────────────────────────────

    /// <summary>
    /// الاستحقاق يُفحص <b>هنا</b> لا في بناء الكتالوج: الكتالوج واحدٌ للجميع كي تُقرأ
    /// الذاكرة، والتصفية تقع بعد أن ينطق النموذج وقبل أن يُنفَّذ شيء.
    /// </summary>
    [Fact]
    public void عمليةٌ_خارج_استحقاق_المتكلّم_تُرفض()
    {
        Result<AgentDispatch> gated = AgentToolGate.Authorise(
            Draft(InvoiceWith("H", "BR-1")),
            AgentHarness.Caller(Here, Company, Session, "draftCreditNote"), State(), Catalogue, Handles);

        Assert.True(gated.IsFailure);
        Assert.Contains(gated.Errors, static error => error.Code == "ai.agent.not_entitled");
    }

    // ── الشاهد الموجب ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>وشاهدٌ موجب: نداءٌ سليم يمرّ فعلاً، والمِقبض يُستبدل بما دلّ عليه.</b> بلا هذا
    /// السطر يكون «لا شيء يمرّ» ادّعاءً يُوفى به بمنع كل شيء.
    /// </summary>
    [Fact]
    public void نداءٌ_سليمٌ_يمرّ_ويحلّ_المِقبضُ_محلَّه()
    {
        Guid branchRow = new("a9e70000-0000-4000-8000-0000000000b1");

        string customer = EntityHandle(Here, Company, Session, CustomerRow);
        string branch = EntityHandle(Here, Company, Session, branchRow);

        Result<AgentDispatch> gated = AgentToolGate.Authorise(
            Draft(InvoiceWith(customer, branch)),
            Caller("draftSalesInvoice"), State(), Catalogue, Handles);

        Assert.True(gated.IsSuccess, string.Join(" · ", gated.Errors.Select(static e => e.Code)));

        JsonObject body = (JsonObject)JsonNode.Parse(gated.Value.Body)!;

        Assert.Equal(CustomerRow.ToString(), body["customerId"]!.GetValue<string>());
        Assert.Equal(branchRow.ToString(), body["branchId"]!.GetValue<string>());

        // وما لم يكن مِقبضاً بقي كما كتبه النموذج حرفاً بحرف.
        Assert.Equal("INV-1", body["number"]!.GetValue<string>());
        Assert.Equal("2026-03-01", body["issuedOn"]!.GetValue<string>());

        Assert.Equal(
            [new AgentRedeemedField("branchId", branchRow), new AgentRedeemedField("customerId", CustomerRow)],
            [.. gated.Value.Redeemed.OrderBy(static field => field.Field, StringComparer.Ordinal)]);

        // ‏**وحتى بعد كل ذلك: مسوّدة.**
        Assert.True(AgentDispatch.ProducesADraftOnly);
    }

    /// <summary>ومِقبضٌ داخل مصفوفةِ سطورٍ يُفكّ في موضعه من الشجرة.</summary>
    [Fact]
    public void مِقبضٌ_داخل_سطرٍ_يُفكّ_في_موضعه()
    {
        Guid itemRow = new("a9e70000-0000-4000-8000-0000000000e1");
        string item = EntityHandle(Here, Company, Session, itemRow);

        AgentToolCall call = new("tu_1", "draftStockMovement", JsonSerializer.Serialize(new
        {
            cost = "10",
            direction = "IN",
            itemGroup = "GOODS",
            itemId = item,
            locationId = item,
            number = "MV-1",
            occurredOn = "2026-03-01",
            quantity = new { magnitude = "3", unit = "EA" },
            warehouseId = item,
        }));

        Result<AgentDispatch> gated = AgentToolGate.Authorise(
            call, Caller("draftStockMovement"), State(), Catalogue, Handles);

        Assert.True(gated.IsSuccess, string.Join(" · ", gated.Errors.Select(static e => e.Code)));

        JsonObject body = (JsonObject)JsonNode.Parse(gated.Value.Body)!;
        Assert.Equal(itemRow.ToString(), body["itemId"]!.GetValue<string>());
        Assert.Equal(3, gated.Value.Redeemed.Count);
    }

    /// <summary>وسائطٌ ليست كائن JSON تُرفض ولا تُصلَح بالتخمين.</summary>
    [Theory]
    [InlineData("[1,2,3]")]
    [InlineData("\"draftSalesInvoice\"")]
    [InlineData("{ ناقص")]
    public void وسائطٌ_ليست_كائناً_تُرفض(string arguments)
    {
        Result<AgentDispatch> gated = AgentToolGate.Authorise(
            new AgentToolCall("tu_1", "draftSalesInvoice", arguments),
            Caller("draftSalesInvoice"), State(), Catalogue, Handles);

        Assert.True(gated.IsFailure);
        Assert.Equal("ai.agent.tool_arguments_malformed", Assert.Single(gated.Errors).Code);
    }

    /// <summary>
    /// وسجلٌّ خارج المفردة المغلقة يُرفض ولا يُبحَث في سجلٍّ غيره — <b>ويسقط عند
    /// المخطّط المنشور قبل أن يبلغ حارس السجلّ</b>، لأن المفردة مكتوبةٌ في المخطّط
    /// كذلك. والحارسان يقولان الشيء نفسه، ويُقاس اتّفاقُهما أدناه.
    /// </summary>
    [Fact]
    public void سجلٌّ_خارج_المفردة_يُرفض()
    {
        Result<AgentDispatch> gated = AgentToolGate.Authorise(
            new AgentToolCall("tu_1", AgentProtocolTools.LookupEntity,
                JsonSerializer.Serialize(new { kind = "bank_account", text = "الراجحي" })),
            Caller("draftSalesInvoice"), State(), Catalogue, Handles);

        Assert.True(gated.IsFailure);
        Assert.Equal("ai.agent.argument_shape_mismatch", Assert.Single(gated.Errors).Code);
    }

    /// <summary>
    /// <b>ومفردةُ السجلّات واحدةٌ في الموضعين</b>: ما يعلنه مخطّط <c>lookup_entity</c>
    /// هو <c>RegisterKeys</c> نفسه. ولو انحرفا لصار أحد الحارسين يقبل ما يرفضه الآخر.
    /// </summary>
    [Fact]
    public void مفردةُ_السجلّات_في_المخطّط_هي_مفردةُ_الكتالوج()
    {
        AgentTool lookup = Catalogue.Resolve(AgentProtocolTools.LookupEntity)!;

        using JsonDocument schema = JsonDocument.Parse(lookup.InputSchemaJson);

        IEnumerable<string> declared = schema.RootElement
            .GetProperty("properties").GetProperty("kind").GetProperty("enum")
            .EnumerateArray().Select(static value => value.GetString()!);

        Assert.Equal(Catalogue.RegisterKeys.Order(StringComparer.Ordinal), declared.Order(StringComparer.Ordinal));
    }
}
