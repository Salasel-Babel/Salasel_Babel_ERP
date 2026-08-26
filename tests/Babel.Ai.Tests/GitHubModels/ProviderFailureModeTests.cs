using Babel.Ai.Extraction;
using Babel.Ai.Extraction.GitHubModels;
using Babel.Ai.Suggestions;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.GitHubModels;

/// <summary>
/// <b>كل صنف عطل، وما يفعله المزوّد عنده.</b>
/// <para>
/// وهذه المجموعة هي الجواب على السؤال الوحيد الذي يهمّ في عرض حيّ: <b>ماذا يحدث حين
/// لا يعمل؟</b> ولذلك تُصنَّف الأعطال قيمةً مُعادة برمز عربي، لا استثناءً يصعد إلى
/// شاشة بيضاء.
/// </para>
/// <para>
/// ⚠ ولا نداء حقيقياً واحداً في هذه المجموعة: مضيف الخدمة محجوب على مخدّم البيئة
/// الوسيط. والسلك واجهة لهذا السبب بالذات — وأصناف العطل التي تُقتل عرضاً (تجاوز
/// الحدّ، والامتناع، والقطع) لا تُطلَب من خدمة حقيقية عند الحاجة أصلاً.
/// </para>
/// </summary>
public sealed class ProviderFailureModeTests
{
    private static GitHubModelsExtractionProvider Provider(ScriptedWire wire, string? secret = ModelFixtures.TestToken) =>
        new(ModelFixtures.Options(), wire, new InMemorySecrets(secret), MatrixPostingVocabulary.Default);

    private static async Task<Result<ExtractionOutput>> RunAsync(ScriptedWire wire, string? secret = ModelFixtures.TestToken) =>
        await Provider(wire, secret).ExtractAsync(ModelFixtures.Request());

    private static void HasCode(Result<ExtractionOutput> result, string code)
    {
        Assert.True(result.IsFailure, "نجح نداء كان يجب أن يفشل.");
        Assert.Contains(result.Errors, error => error.Code == code);
    }

    // ── 1 · لا رمز ────────────────────────────────────────────────────────

    [Fact]
    public async Task بلا_رمز_في_البيئة_يُسمّى_المتغيّر_ولا_يخرج_طلب()
    {
        ScriptedWire wire = ScriptedWire.Answering(200, ModelFixtures.Envelope("success.json", ModelFixtures.ValidExtraction()));

        Result<ExtractionOutput> result = await RunAsync(wire, secret: null);

        HasCode(result, "ai.provider.token_not_in_environment");
        Assert.Contains(ModelFixtures.TokenVariable, result.Errors[0].MessageAr, StringComparison.Ordinal);

        // ولا يخرج طلب أصلاً: نداءٌ بلا مصادقة يعود بردٍّ غامض يُقرأ عطلاً آخر.
        Assert.Equal(0, wire.Calls);
    }

    // ── 2 · رمز مرفوض ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task الرمز_المرفوض_يُسمّى_ولا_تُذكر_قيمته(int status)
    {
        Result<ExtractionOutput> result = await RunAsync(
            ScriptedWire.Answering(status, ModelFixtures.Envelope("unauthorized.json")));

        HasCode(result, "ai.provider.token_rejected");

        // الرسالة تُكتب في سجل: يجب ألا تحمل السرّ نفسه بحال.
        foreach (Error error in result.Errors)
        {
            Assert.DoesNotContain(ModelFixtures.TestToken, error.MessageAr, StringComparison.Ordinal);
            Assert.DoesNotContain(ModelFixtures.TestToken, error.MessageEn, StringComparison.Ordinal);
        }
    }

    // ── 3 · تجاوز الحدّ ───────────────────────────────────────────────────

    [Fact]
    public async Task تجاوز_الحدّ_يُعلن_مدّة_الانتظار_ولا_يُعاد_الطلب_تلقائياً()
    {
        ScriptedWire wire = ScriptedWire.Answering(429, ModelFixtures.Envelope("rate-limited.json"), retryAfter: 43);

        Result<ExtractionOutput> result = await RunAsync(wire);

        HasCode(result, "ai.provider.rate_limited");
        Assert.Contains("43", result.Errors[0].MessageAr, StringComparison.Ordinal);

        // «لا إعادة تلقائية» ادّعاء يُقاس لا يُقال: نداء واحد بالضبط.
        Assert.Equal(1, wire.Calls);
    }

    [Fact]
    public async Task تجاوز_الحدّ_بلا_مدّة_معلنة_يقول_ذلك_ولا_يخترع_رقماً()
    {
        Result<ExtractionOutput> result = await RunAsync(
            ScriptedWire.Answering(429, ModelFixtures.Envelope("rate-limited.json")));

        HasCode(result, "ai.provider.rate_limited");
        Assert.Contains("لم تُعلن", result.Errors[0].MessageAr, StringComparison.Ordinal);
    }

    // ── 4 · المهلة ────────────────────────────────────────────────────────

    [Fact]
    public async Task المهلة_تُسمّى_ويُقال_إن_إعادة_المحاولة_آمنة()
    {
        Result<ExtractionOutput> result = await RunAsync(
            ScriptedWire.Throwing(new TaskCanceledException("deadline")));

        HasCode(result, "ai.provider.timed_out");

        // الفرق الجوهري عن حدّ الالتزام: الاستخراج قراءة، فالمهلة ليست «لا أدري».
        Assert.Contains("آمنة", result.Errors[0].MessageAr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task إلغاء_المستدعي_ليس_مهلة_ويصعد_استثناءً()
    {
        using CancellationTokenSource source = new();
        await source.CancelAsync();

        ScriptedWire wire = ScriptedWire.Throwing(new OperationCanceledException(source.Token));

        // إلغاءُ المستدعي قرارُه هو، ولا يُترجَم عطلَ خدمة — وإلا بدا إلغاءٌ متعمَّد عطلاً.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await Provider(wire).ExtractAsync(ModelFixtures.Request(), source.Token));
    }

    // ── 5 · نموذج غير متاح ────────────────────────────────────────────────

    [Fact]
    public async Task النموذج_غير_الموجود_يُسمّى_باسمه()
    {
        Result<ExtractionOutput> result = await RunAsync(
            ScriptedWire.Answering(404, ModelFixtures.Envelope("model-not-found.json")));

        HasCode(result, "ai.provider.model_unavailable");
        Assert.Contains(ModelFixtures.ModelId, result.Errors[0].MessageAr, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    public async Task عطل_الخدمة_يُصنَّف_نموذجاً_غير_متاح(int status) =>
        HasCode(await RunAsync(ScriptedWire.Answering(status, "{}")), "ai.provider.model_unavailable");

    // ── 6 · تعذّر الوصول — وهو ما يقع فعلاً في بيئة هذا البناء ─────────────

    [Fact]
    public async Task تعذّر_الوصول_يُسمّي_المضيف_ويقترح_فحص_الشبكة()
    {
        Result<ExtractionOutput> result = await RunAsync(ScriptedWire.Throwing(ModelFixtures.ConnectionRefused()));

        HasCode(result, "ai.provider.unreachable");
        Assert.Contains("models.example.invalid", result.Errors[0].MessageAr, StringComparison.Ordinal);
    }

    // ── 7 · الغلاف ────────────────────────────────────────────────────────

    [Fact]
    public async Task غلاف_ليس_JSON_يُسمّى_عطلَ_وسيط_لا_عطلَ_نموذج() =>
        HasCode(await RunAsync(ScriptedWire.Answering(200, "<html>502 Bad Gateway</html>")), "ai.provider.envelope_unreadable");

    [Fact]
    public async Task ردّ_بلا_خيارات_يُعلن_الفراغ_ولا_يملؤه() =>
        HasCode(await RunAsync(ScriptedWire.Answering(200, ModelFixtures.Envelope("no-choices.json"))), "ai.provider.no_content");

    [Fact]
    public async Task امتناع_النموذج_عطلٌ_قائم_بذاته_لا_مُخرَجٌ_مشوَّه()
    {
        Result<ExtractionOutput> result = await RunAsync(
            ScriptedWire.Answering(200, ModelFixtures.Envelope("refusal.json")));

        HasCode(result, "ai.provider.model_refused");
        Assert.Contains("لا أستطيع", result.Errors[0].MessageAr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task مرشّح_المحتوى_يُقرأ_امتناعاً_لا_خللاً() =>
        HasCode(await RunAsync(ScriptedWire.Answering(200, ModelFixtures.Envelope("content-filter.json"))), "ai.provider.model_refused");

    [Fact]
    public async Task المُخرَج_المقطوع_يُسمّى_قطعاً_لا_ينسب_إلى_JSON()
    {
        Result<ExtractionOutput> result = await RunAsync(
            ScriptedWire.Answering(200, ModelFixtures.Envelope("truncated.json")));

        HasCode(result, "ai.provider.output_truncated");

        // ولو مرّ لَوصل الحدَّ «ليس JSON صالحاً» — رسالة صحيحة عن عَرَض لا عن سبب.
        Assert.DoesNotContain(result.Errors, error => error.Code == "ai.capture.payload_not_json");
    }

    // ── 8 · النجاح، وما يليه ───────────────────────────────────────────────

    [Fact]
    public async Task المُخرَج_السليم_يعبر_خاماً_ثم_يقبله_المخطط()
    {
        string content = ModelFixtures.ValidExtraction();
        Result<ExtractionOutput> result = await RunAsync(
            ScriptedWire.Answering(200, ModelFixtures.Envelope("success.json", content)));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Errors[0].MessageAr : string.Empty);
        Assert.Equal(GitHubModelsExtractionProvider.Id, result.Value.ProviderId);

        Result<ExtractedInvoice> validated = ExtractionSchema.Validate(result.Value.Json);
        Assert.True(validated.IsSuccess);
        Assert.Equal(1150.00m, validated.Value.GrossTotal.Value);
    }

    [Fact]
    public async Task سياج_الشيفرة_وحده_يُقشَر_وما_بداخله_يمرّ_على_المخطط_كاملاً()
    {
        string content = ModelFixtures.ValidExtraction();
        Result<ExtractionOutput> result = await RunAsync(
            ScriptedWire.Answering(200, ModelFixtures.Envelope("fenced.json", content)));

        Assert.True(result.IsSuccess);
        Assert.StartsWith("{", result.Value.Json, StringComparison.Ordinal);
        Assert.True(ExtractionSchema.Validate(result.Value.Json).IsSuccess);
    }

    /// <summary>
    /// <b>الشاهد الموجب على أن التحقّق غير فارغ:</b> مُخرَج مشوَّه يمرّ من المزوّد
    /// ويسقط عند المخطط برسالة تسمّي الحقل — لا «فشل الاستخراج».
    /// </summary>
    [Fact]
    public async Task مُخرَج_مشوَّه_يفشل_بصوت_عالٍ_ويسمّي_ما_فيه()
    {
        const string Malformed = """
            {"schema_version":"ai.capture.extraction.v1","document":{"seller_name":{"value":"م","confidence":0.9},
            "seller_vat_number":{"value":"3","confidence":0.9},"invoice_number":{"value":"i","confidence":0.9},
            "issued_on":{"value":"25/08/2026","confidence":0.9},"net":{"value":1000,"confidence":0.9},
            "tax_total":{"value":"150.00","confidence":0.9},"gross_total":{"value":"1150.00","confidence":0.9}},
            "lines":[{"description":{"value":"د","confidence":0.9},"quantity":{"value":"1","confidence":0.9},
            "unit_price":{"value":"1000.00","confidence":0.9},"net":{"value":"1000.00","confidence":0.9}}]}
            """;

        Result<ExtractionOutput> result = await RunAsync(
            ScriptedWire.Answering(200, ModelFixtures.Envelope("success.json", Malformed)));

        Assert.True(result.IsSuccess, "المزوّد يعيد الخام؛ التحقّق عند الحدّ لا فيه.");

        Result<ExtractedInvoice> validated = ExtractionSchema.Validate(result.Value.Json);
        Assert.True(validated.IsFailure);
        Assert.Contains(validated.Errors, error => error.Code == "ai.capture.date_not_iso");
        Assert.Contains(validated.Errors, error => error.Code == "ai.capture.field_wrong_json_kind");
        Assert.Contains(validated.Errors, error => error.MessageAr.Contains("issued_on", StringComparison.Ordinal));
    }

    /// <summary>مُخرَجٌ يسمّي حساباً — الباب الأول من أبواب رمز الحساب الثلاثة.</summary>
    [Fact]
    public async Task مُخرَج_يسمّي_حساباً_يُرفض_برمز_مستقلّ()
    {
        const string NamesAccount = """
            {"schema_version":"ai.capture.extraction.v1","document":{"seller_name":{"value":"م","confidence":0.9},
            "seller_vat_number":{"value":"3","confidence":0.9},"invoice_number":{"value":"i","confidence":0.9},
            "issued_on":{"value":"2026-08-25","confidence":0.9},"net":{"value":"1000.00","confidence":0.9},
            "tax_total":{"value":"150.00","confidence":0.9},"gross_total":{"value":"1150.00","confidence":0.9},
            "account_code":{"value":"1210","confidence":0.9}},
            "lines":[{"description":{"value":"د","confidence":0.9},"quantity":{"value":"1","confidence":0.9},
            "unit_price":{"value":"1000.00","confidence":0.9},"net":{"value":"1000.00","confidence":0.9}}]}
            """;

        Result<ExtractionOutput> result = await RunAsync(
            ScriptedWire.Answering(200, ModelFixtures.Envelope("success.json", NamesAccount)));

        Assert.True(result.IsSuccess);
        Result<ExtractedInvoice> validated = ExtractionSchema.Validate(result.Value.Json);

        Assert.True(validated.IsFailure);
        Assert.Contains(validated.Errors, error => error.Code == "ai.capture.field_names_a_ledger_code");
    }
}
