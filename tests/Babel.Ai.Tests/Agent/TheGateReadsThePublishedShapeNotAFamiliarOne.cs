using System.Text.Json;
using System.Text.Json.Nodes;
using Babel.Ai.Agent;
using Babel.Ai.Lookup;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Agent;

/// <summary>
/// <b>الخطوة الخامسة تفكّ المقابض بمسارها المنشور — فإن لم يكن الجسم على شكله المنشور
/// لم تجد شيئاً، ولم تُفكّ شيئاً، ولم تَرفض شيئاً.</b>
/// <para>
/// وهذا الملفّ هو الشاهد الموجب لأربع صورٍ <b>مقيسة</b> وصل فيها معرّفٌ خام إلى جسم
/// المسوّدة سالماً: مصفوفةٌ كُتبت كائناً، ومصفوفةُ مصفوفات، وأخٌ بحرفٍ كبير
/// (‏<c>CustomerId</c>)، وعشٌّ باسمٍ لا يعلنه المخطّط (‏<c>meta</c>). وكلّها اليوم
/// تسقط عند مطابقة المخطّط <b>قبل</b> أن تبلغ الفكّ.
/// </para>
/// <para>
/// ومعها ثلاثة أبوابٍ أخرى كانت مفتوحة: مفتاحٌ مكرَّر كان <b>يقتل الدور باستثناء</b>،
/// وأداتا البروتوكول كانتا خارج فحص الاستحقاق كلّه، ومعرّفٌ في <b>اسم</b> مفتاح لم يكن
/// يُفحص أصلاً.
/// </para>
/// </summary>
public sealed class TheGateReadsThePublishedShapeNotAFamiliarOne
{
    private static readonly TenantId Here = new(new Guid("100c0a5e-0000-4000-8000-0000000000aa"));
    private static readonly Guid Company = new("c0000000-0000-4000-8000-0000000000bb");
    private static readonly Guid Session = new("5e551000-0000-4000-8000-0000000000cc");
    private static readonly Guid CustomerRow = new("c5700000-0000-4000-8000-0000000000dd");

    private static readonly SignedLookupHandles Handles = AgentHarness.Handles(new MovableClock());
    private static readonly AgentToolCatalogue Catalogue = AgentToolCatalogue.Embedded;

    private static AgentCaller Caller(params string[] permitted) =>
        AgentHarness.Caller(Here, Company, Session, permitted);

    private static string Handle() => Handles
        .Issue(LookupHandlePurpose.Entity, Here, Company, Session, CustomerRow, TimeSpan.FromMinutes(10))
        .Value;

    private static Result<AgentDispatch> Gate(string toolName, string argumentsJson) =>
        AgentToolGate.Authorise(
            new AgentToolCall("tu_1", toolName, argumentsJson),
            Caller("draftSalesInvoice", "draftCreditNote"),
            new AgentTurnState(4),
            Catalogue,
            Handles);

    private static string SoundInvoice(string customerId) => JsonSerializer.Serialize(new
    {
        branchId = Handle(),
        customerId,
        issuedOn = "2026-03-01",
        lines = new[]
        {
            new
            {
                description = new { ar = "بند", en = "line" },
                discount = "0",
                itemGroup = "GOODS",
                quantity = "1",
                taxClassification = "standard",
                taxRate = "0.15",
                unitPrice = "100",
            },
        },
        number = "INV-1",
    });

    /// <summary>الشاهد السالب: الجسم على شكله المنشور يمرّ ويحلّ المِقبضُ محلَّه.</summary>
    [Fact]
    public void TheBodyOnItsPublishedShapeStillPasses()
    {
        Result<AgentDispatch> gated = Gate("draftSalesInvoice", SoundInvoice(Handle()));

        Assert.True(gated.IsSuccess, string.Join(" · ", gated.Errors.Select(static e => e.Code)));

        JsonObject body = (JsonObject)JsonNode.Parse(gated.Value.Body)!;
        Assert.Equal(CustomerRow.ToString(), body["customerId"]!.GetValue<string>());
    }

    /// <summary>
    /// <b>الصور الأربع المقيسة</b> — وفي كلٍّ منها معرّفٌ خام كان يبلغ جسم المسوّدة.
    /// </summary>
    public static TheoryData<string, string> ShapesThatUsedToSmuggleARawIdentifier() => new()
    {
        // مصفوفةٌ كُتبت كائناً: «lines.[].originalInvoiceLineId» لا يوجد فيه مسارٌ يُحدَّد.
        {
            "lines كائن لا مصفوفة",
            """{"invoiceId":"__H__","lines":{"originalInvoiceLineId":"9f2b0000-0000-4000-8000-0000000000ff","quantity":"1","reason":"ر"}}"""
        },
        // مصفوفةُ مصفوفات: العنصر ليس كائناً فلا يُقرأ منه اسم حقل.
        {
            "lines مصفوفةُ مصفوفات",
            """{"invoiceId":"__H__","lines":[[{"originalInvoiceLineId":"9f2b0000-0000-4000-8000-0000000000ff"}]]}"""
        },
        // أخٌ بحرفٍ كبير: المسار المنشور «customerId» لا «CustomerId».
        {
            "CustomerId بحرفٍ كبير",
            """{"branchId":"__H__","CustomerId":"9f2b0000-0000-4000-8000-0000000000ff","customerId":"__H__","issuedOn":"2026-03-01","lines":[],"number":"INV-1"}"""
        },
        // عشٌّ باسمٍ لا يعلنه المخطّط.
        {
            "meta عشٌّ غير معلَن",
            """{"branchId":"__H__","customerId":"__H__","issuedOn":"2026-03-01","lines":[],"meta":{"customerId":"9f2b0000-0000-4000-8000-0000000000ff"},"number":"INV-1"}"""
        },
    };

    /// <summary>ولا واحدةٌ منها تبلغ التنفيذ اليوم.</summary>
    /// <param name="which">وصف الصورة.</param>
    /// <param name="template">الجسم، و<c>__H__</c> موضع مِقبضٍ سليم.</param>
    [Theory]
    [MemberData(nameof(ShapesThatUsedToSmuggleARawIdentifier))]
    public void ARawIdentifierInAnUnpublishedShapeNeverReachesTheBody(string which, string template)
    {
        string tool = template.Contains("invoiceId", StringComparison.Ordinal)
            ? "draftCreditNote"
            : "draftSalesInvoice";

        Result<AgentDispatch> gated = Gate(
            tool, template.Replace("__H__", Handle(), StringComparison.Ordinal));

        Assert.True(gated.IsFailure, which);
        Assert.All(
            gated.Errors,
            error => Assert.StartsWith("ai.agent.argument_", error.Code, StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>المفتاح المكرَّر رفضٌ يُقرأ لا استثناءٌ يقتل الدور.</b> كان
    /// <c>JsonObject.TryGetPropertyValue</c> يرمي <c>ArgumentException</c> — ولا يلتقطها
    /// أحد، فتخرج من <c>Authorise</c> إلى <c>IAsyncEnumerable</c>.
    /// </summary>
    [Theory]
    [InlineData("""{"customerId":"__H__","customerId":"__H__"}""")]
    [InlineData("""{"number":"INV-1","number":"INV-2"}""")]
    [InlineData("""{"lines":[{"quantity":"1","quantity":"2"}]}""")]
    public void ADuplicatedKeyIsARefusalNotAnException(string template)
    {
        Result<AgentDispatch> gated = Gate(
            "draftSalesInvoice", template.Replace("__H__", Handle(), StringComparison.Ordinal));

        Assert.True(gated.IsFailure);
        Assert.Equal("ai.agent.argument_key_duplicated", Assert.Single(gated.Errors).Code);
    }

    /// <summary>
    /// <b>و«إلزاميّ» يعني أنّ المفتاح موجود، لا أنّ قيمته ليست فارغة.</b> العقد ينشر
    /// ثلاثة حقولٍ إلزامية نوعُها <c>["string","null"]</c>، فربطُ الإلزام بعدم الفراغ
    /// كان سيرفض نداءً سليماً — والحاضرُ الفارغُ في حقلٍ لا يقبل الفراغ يسقط بفحص الشكل.
    /// </summary>
    [Fact]
    public void ARequiredFieldThatThePublishedSchemaAllowsToBeNullIsAccepted()
    {
        AgentTool tool = Catalogue.Resolve("draftSubcontractorAdvance")!;

        using JsonDocument schema = JsonDocument.Parse(tool.InputSchemaJson);

        JsonElement declared = schema.RootElement
            .GetProperty("properties").GetProperty("guaranteeId").GetProperty("type");

        Assert.Contains(
            "null",
            declared.EnumerateArray().Select(static value => value.GetString()));

        Assert.Contains(
            "guaranteeId",
            schema.RootElement.GetProperty("required")
                .EnumerateArray().Select(static value => value.GetString()));

        // فارغاً صراحةً: لا يُبلَّغ عنه غائباً ولا مخالفاً للشكل — والمخطّط يعلنه كذلك.
        // (وبقيّةُ الإلزاميات تبقى مُبلَّغاً عنها، وهي ليست موضوع هذا القياس.)
        Assert.DoesNotContain(
            AgentArgumentSchema.Violations(
                (JsonObject)JsonNode.Parse("""{"guaranteeId":null}""")!, tool),
            static error => error.MessageAr.Contains("guaranteeId", StringComparison.Ordinal));

        // وغائباً: يُرفض باسمه.
        Assert.Contains(
            AgentArgumentSchema.Violations((JsonObject)JsonNode.Parse("{}")!, tool),
            static error => string.Equals(
                error.Code, "ai.agent.argument_required_missing", StringComparison.Ordinal)
                && error.MessageAr.Contains("guaranteeId", StringComparison.Ordinal));
    }

    /// <summary>وسائطٌ فارغة تُرفض بأسماء الحقول الإلزامية لا تمرّ.</summary>
    [Fact]
    public void AnEmptyBodyIsRefusedByNameNotAccepted()
    {
        Result<AgentDispatch> gated = Gate("draftSalesInvoice", "{}");

        Assert.True(gated.IsFailure);
        Assert.Equal(5, gated.Errors.Count);
        Assert.All(
            gated.Errors,
            static error => Assert.Equal("ai.agent.argument_required_missing", error.Code));
    }

    /// <summary>
    /// <b>وأداتا البروتوكول داخل فحص الاستحقاق لا خارجه.</b> مقيس: متكلّمٌ بمجموعة
    /// صلاحيات <b>فارغة</b> كان يسبر كل سجلّات الأسماء ويسأل كل ورقة.
    /// </summary>
    [Theory]
    [InlineData(AgentProtocolTools.LookupEntity, """{"kind":"customer","text":"محمد"}""")]
    [InlineData(AgentProtocolTools.AskQuestion, """{"questionId":"AAAA"}""")]
    public void ACallerWhoCanConsumeNoHandleReachesNeitherProtocolTool(string tool, string arguments)
    {
        Result<AgentDispatch> gated = AgentToolGate.Authorise(
            new AgentToolCall("tu_1", tool, arguments),
            Caller(),
            new AgentTurnState(4),
            Catalogue,
            Handles);

        Assert.True(gated.IsFailure);
        Assert.Equal("ai.agent.not_entitled_to_probe", Assert.Single(gated.Errors).Code);
    }

    /// <summary>والشاهد السالب: من يستهلك مِقبضاً يبلغهما.</summary>
    [Fact]
    public void ACallerEntitledToAHandleConsumerStillReachesTheProbe()
    {
        Result<AgentDispatch> gated = AgentToolGate.Authorise(
            new AgentToolCall("tu_1", AgentProtocolTools.LookupEntity, """{"kind":"customer","text":"محمد"}"""),
            Caller("draftSalesInvoice"),
            new AgentTurnState(4),
            Catalogue,
            Handles);

        Assert.True(gated.IsSuccess, string.Join(" · ", gated.Errors.Select(static e => e.Code)));
    }

    /// <summary>
    /// <b>ومعرّفٌ في اسم مفتاح</b> — <c>{"1092837465":"x"}</c> — كان يبلغ جسم المسوّدة
    /// لأن الفحص كان على القيم وحدها. اليوم يسقط بحارسَين مستقلَّين: المخطّط لا يعلنه،
    /// والمِصفاة تقرأ الاسم كما تقرأ القيمة.
    /// </summary>
    [Fact]
    public void AnIdentifierWrittenAsAPropertyNameDoesNotReachTheBody()
    {
        Result<AgentDispatch> gated = Gate(
            "draftSalesInvoice",
            """{"customerId":"__H__","1092837465":"x"}""".Replace("__H__", Handle(), StringComparison.Ordinal));

        Assert.True(gated.IsFailure);
        Assert.Contains(
            gated.Errors,
            static error => string.Equals(error.Code, "ai.agent.argument_not_in_schema", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>والمعرّف المقطوع بقطع سطرٍ أو جدولةٍ في حقلٍ نصّي يُرفض عند الحدّ.</b> مقيس
    /// أنه كان يمرّ ويبلغ <c>AgentDispatch.Body</c> حرفياً.
    /// </summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\t")]
    [InlineData("­")]
    [InlineData("⁠")]
    public void AnIdentifierSplitByALineBreakOrASoftHyphenIsRefusedAtTheGate(string joiner)
    {
        string body = SoundInvoice(Handle()).Replace(
            JsonSerializer.Serialize("INV-1"),
            JsonSerializer.Serialize("هويته 1092" + joiner + "837465"),
            StringComparison.Ordinal);

        Result<AgentDispatch> gated = Gate("draftSalesInvoice", body);

        Assert.True(gated.IsFailure);
        Assert.Contains(
            gated.Errors,
            static error => error.Code.StartsWith("ai.agent.identifier_refused.", StringComparison.Ordinal));
    }
}
