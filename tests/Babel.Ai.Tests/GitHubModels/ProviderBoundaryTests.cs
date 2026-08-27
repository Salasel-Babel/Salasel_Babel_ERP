using System.Text.RegularExpressions;
using Babel.Ai.Extraction;
using Babel.Ai.Extraction.GitHubModels;
using Babel.Ai.Suggestions;
using Babel.Ai.Tests.Support;
using Babel.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Babel.Ai.Tests.GitHubModels;

/// <summary>مفردات مبذورة — تُستعمل شاهداً موجباً على أن الحارس يعضّ فعلاً.</summary>
internal sealed class SeededVocabulary(IReadOnlyList<string> events, IReadOnlyList<string> roles) : IPostingVocabulary
{
    public int EventCount => events.Count;

    public int RoleCount => roles.Count;

    public IReadOnlyList<string> EventCodes => events;

    public IReadOnlyList<string> RoleCodes => roles;

    public bool KnowsEvent(string eventCode) => events.Contains(eventCode);

    public bool KnowsRole(string roleCode) => roles.Contains(roleCode);
}

/// <summary>
/// <b>حدّ المزوّد: ما يخرج، وما لا يخرج، وأي مزوّد يُركَّب أصلاً.</b>
/// </summary>
public sealed partial class ProviderBoundaryTests
{
    [GeneratedRegex(@"code:\s*""([a-z0-9_.]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex TypeScriptEventCode();

    // ── التوجيه: ما يراه النموذج ──────────────────────────────────────────

    [Fact]
    public void التوجيه_يحمل_المفردات_المغلقة_ولا_يحمل_اسم_حقل_يسمّي_حساباً()
    {
        Result<string> prompt = ExtractionPrompt.System(MatrixPostingVocabulary.Default);
        Assert.True(prompt.IsSuccess, prompt.IsFailure ? prompt.Errors[0].MessageAr : string.Empty);

        // المفردات محقونة فعلاً — وإلا كان «لا رمز حساب في التوجيه» صحيحاً وفارغاً.
        Assert.Contains("purchasing.invoice.expense.posted", prompt.Value, StringComparison.Ordinal);
        Assert.Contains("ap_supplier_control", prompt.Value, StringComparison.Ordinal);
        Assert.True(MatrixPostingVocabulary.Default.EventCount >= ExtractionPrompt.MinimumEvents);

        // ولا اسم حقل يسمّي حساباً، بالمطابقة على الرمز كاملاً.
        foreach (string name in SuggestionGuard.LedgerCodeFieldNames)
        {
            Assert.DoesNotContain(
                Regex.Matches(prompt.Value, @"[A-Za-z0-9_]+", RegexOptions.CultureInvariant).Select(match => match.Value),
                token => string.Equals(token, name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// <b>الشاهد الموجب:</b> مفرداتٌ مسمومة برمز مقطعُه رقم تُسقط التوجيه قبل إرساله.
    /// وبدون هذا الفحص يكون «لا رمز حساب في التوجيه» ادّعاءً لا يستطيع أن يفشل.
    /// </summary>
    [Fact]
    public void رمزٌ_مقطعُه_رقم_في_المفردات_يُسقط_التوجيه_قبل_إرساله()
    {
        SeededVocabulary poisoned = new(
            [.. MatrixPostingVocabulary.Default.EventCodes, "purchasing.1210"],
            MatrixPostingVocabulary.Default.RoleCodes);

        Result<string> prompt = ExtractionPrompt.System(poisoned);

        Assert.True(prompt.IsFailure);
        Assert.Equal("ai.provider.prompt_names_a_ledger_code", prompt.Errors[0].Code);
        Assert.Contains("purchasing.1210", prompt.Errors[0].MessageAr, StringComparison.Ordinal);
    }

    [Fact]
    public void مفرداتٌ_ضامرة_تُوقف_الإرسال_ولا_تُرسَل_قائمة_فارغة()
    {
        Result<string> prompt = ExtractionPrompt.System(new SeededVocabulary(["a.b.c"], ["r"]));

        Assert.True(prompt.IsFailure);
        Assert.Equal("ai.provider.vocabulary_too_small", prompt.Errors[0].Code);
    }

    [Fact]
    public void جسم_الطلب_يحمل_معرّف_النموذج_من_الإعداد_ولا_يحمل_الرمز()
    {
        GitHubModelsOptions options = ModelFixtures.Options();
        string prompt = ExtractionPrompt.System(MatrixPostingVocabulary.Default).Value;
        string body = ExtractionPrompt.Body(options, prompt, ModelFixtures.Request());

        Assert.Contains(ModelFixtures.ModelId, body, StringComparison.Ordinal);
        Assert.DoesNotContain(ModelFixtures.TestToken, body, StringComparison.Ordinal);
        Assert.DoesNotContain(ModelFixtures.TokenVariable, body, StringComparison.Ordinal);
    }

    // ── الإعداد ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ghp_this_is_not_a_token_only_the_prefix_matters")]
    [InlineData("github_pat_this_is_not_a_token_only_the_prefix")]
    [InlineData("Bearer abc")]
    /* ⚠ القيم أعلاه **ليست اعتمادات**: بادئةٌ معروفة وذيلٌ بشُرَط سفلية لا يطابق شكل
       الرمز الحقيقي. والحارس يفحص البادئة، فالاختبار غير فارغ ولا يُودَع سرّ. */
    public void رمزٌ_مكتوبٌ_في_حقل_اسم_المتغيّر_يُرفض_ولا_تُذكر_قيمته(string leaked)
    {
        GitHubModelsOptions options = ModelFixtures.Options();
        options.TokenVariable = leaked;

        Result validated = options.Validate();

        Assert.True(validated.IsFailure);
        Assert.Equal("ai.provider.token_variable_looks_like_a_token", validated.Errors[0].Code);

        // الرسالة تُكتب في سجل: يجب ألا تحمل ما التقطته.
        Assert.DoesNotContain(leaked, validated.Errors[0].MessageAr, StringComparison.Ordinal);
        Assert.DoesNotContain(leaked, validated.Errors[0].MessageEn, StringComparison.Ordinal);
    }

    [Fact]
    public void عنوانٌ_غير_مشفّر_يُرفض()
    {
        GitHubModelsOptions options = ModelFixtures.Options();
        options.Endpoint = new Uri("http://models.example.invalid/inference");

        Assert.Contains(options.Validate().Errors, error => error.Code == "ai.provider.endpoint_not_https");
    }

    [Fact]
    public void سقفُ_مُخرَجٍ_صغير_يُرفض_لأنه_يقطع_JSON_في_منتصفه()
    {
        GitHubModelsOptions options = ModelFixtures.Options();
        options.MaxOutputTokens = 32;

        Assert.Contains(options.Validate().Errors, error => error.Code == "ai.provider.output_budget_too_small");
    }

    [Fact]
    public void إعدادٌ_معطوب_يُسقط_البناء_لا_أول_نداء()
    {
        GitHubModelsOptions options = ModelFixtures.Options();
        options.ModelId = string.Empty;

        Assert.Throws<ArgumentException>(() => new GitHubModelsExtractionProvider(
            options,
            ScriptedWire.Answering(200, "{}"),
            new InMemorySecrets(ModelFixtures.TestToken),
            MatrixPostingVocabulary.Default));
    }

    // ── القدرات: تُقرأ وقت التركيب لا وقت النداء ──────────────────────────

    [Fact]
    public void القدرات_تُعلن_أن_البايتات_تغادر_المنشأة_وأن_المزوّد_ليس_حتمياً()
    {
        GitHubModelsExtractionProvider provider = new(
            ModelFixtures.Options(),
            ScriptedWire.Answering(200, "{}"),
            new InMemorySecrets(ModelFixtures.TestToken),
            MatrixPostingVocabulary.Default);

        Assert.Equal(ExtractionResidency.Offshore, provider.Capabilities.Residency);
        Assert.True(provider.Capabilities.DocumentBytesLeaveThePremises);
        Assert.False(provider.Capabilities.IsDeterministic);
        Assert.Equal(TimeSpan.FromSeconds(7), provider.Capabilities.Timeout);
    }

    // ── الارتداد إلى المزوّد الحتمي ───────────────────────────────────────

    [Fact]
    public void بلا_إعداد_يُركَّب_المزوّد_الحتمي_ويُعلَن_السبب()
    {
        ExtractorChoice choice = ExtractorSelection.Choose(new GitHubModelsOptions(), new InMemorySecrets(null));

        Assert.Equal(DeterministicExtractionProvider.Id, choice.ProviderId);
        Assert.Equal(ExtractorChoiceReason.NotConfigured, choice.Reason);
        Assert.False(choice.IsRemote);
    }

    [Fact]
    public void إعدادٌ_كاملٌ_بلا_سرّ_يرتدّ_ويُسمّي_السبب_سرّاً_غائباً()
    {
        ExtractorChoice choice = ExtractorSelection.Choose(ModelFixtures.Options(), new InMemorySecrets(null));

        Assert.Equal(ExtractorChoiceReason.SecretMissing, choice.Reason);
        Assert.Equal(DeterministicExtractionProvider.Id, choice.ProviderId);
    }

    [Fact]
    public void إعدادٌ_معطوب_يرتدّ_ولا_يُسقط_النظام()
    {
        GitHubModelsOptions options = ModelFixtures.Options();
        options.Endpoint = new Uri("http://models.example.invalid/inference");

        ExtractorChoice choice = ExtractorSelection.Choose(options, new InMemorySecrets(ModelFixtures.TestToken));

        Assert.Equal(ExtractorChoiceReason.ConfigurationInvalid, choice.Reason);
    }

    [Fact]
    public void إعدادٌ_كاملٌ_بسرّ_يُركِّب_المزوّد_البعيد()
    {
        ExtractorChoice choice = ExtractorSelection.Choose(ModelFixtures.Options(), new InMemorySecrets(ModelFixtures.TestToken));

        Assert.Equal(GitHubModelsExtractionProvider.Id, choice.ProviderId);
        Assert.True(choice.IsRemote);
    }

    [Fact]
    public void التركيب_يحلّ_المزوّد_فعلاً_من_الحاوية()
    {
        ServiceCollection services = new();
        services.AddSingleton<IPostingVocabulary>(MatrixPostingVocabulary.Default);
        services.AddGitHubModelsInvoiceExtractor(
            ModelFixtures.Options(),
            new DeterministicExtractionProvider(),
            new InMemorySecrets(ModelFixtures.TestToken));

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.IsType<GitHubModelsExtractionProvider>(provider.GetRequiredService<IInvoiceExtractionProvider>());
        Assert.True(provider.GetRequiredService<ExtractorChoice>().IsRemote);
    }

    [Fact]
    public void التركيب_بلا_سرّ_يحلّ_المزوّد_الحتمي_فيبقى_النظام_يعمل_بلا_شبكة()
    {
        ServiceCollection services = new();
        services.AddSingleton<IPostingVocabulary>(MatrixPostingVocabulary.Default);
        services.AddGitHubModelsInvoiceExtractor(
            ModelFixtures.Options(),
            new DeterministicExtractionProvider(),
            new InMemorySecrets(null));

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.IsType<DeterministicExtractionProvider>(provider.GetRequiredService<IInvoiceExtractionProvider>());
    }

    // ── حارس عبر الحدّ: رموز الأحداث المكتوبة في الواجهة ───────────────────

    /// <summary>
    /// <b>الواجهة تنطق برموز أحداث، والمصفوفة هي المرجع.</b> ورمزٌ مخترَع في ملفّ
    /// TypeScript لا يراه أي حارس في الخادم — إلا هذا. وقد قيس في هذا المستودع أن
    /// الرمز المخترَع يُنتج ترحيلاً مكرَّراً صامتاً (‏ADR-0016).
    /// </summary>
    [Fact]
    public void كل_رمز_حدث_تنطق_به_الواجهة_موجود_في_المصفوفة()
    {
        string source = File.ReadAllText(RepositoryRoot.At("web/src/voice/intent.ts"));
        string[] codes = [.. TypeScriptEventCode().Matches(source).Select(match => match.Groups[1].Value)];

        // حارس لا فراغ: تعبيرٌ نمطي توقّف عن المطابقة يجعل الفحص يمرّ على كل شيء.
        Assert.True(codes.Length >= 4, "رموز مُلتقَطة من الواجهة: " + codes.Length);

        foreach (string code in codes)
        {
            Assert.True(
                MatrixPostingVocabulary.Default.KnowsEvent(code),
                "رمز حدث في web/src/voice/intent.ts ليس في مصفوفة الترحيل: " + code);
        }
    }
}
