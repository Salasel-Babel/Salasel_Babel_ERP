using System.Globalization;
using Babel.Ai.Suggestions;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Capture;

/// <summary>
/// <b>النموذج يسمّي حدثاً أو دوراً من مفردات مغلقة، ولا يسمّي حساباً بحال.</b>
/// </summary>
public sealed class SuggestionGuardTests(ITestOutputHelper output)
{
    private static readonly MatrixPostingVocabulary Vocabulary = MatrixPostingVocabulary.Default;

    private static PostingSuggestion Suggest(string eventCode, string roleCode = "", decimal confidence = 0.8m) =>
        new() { EventCode = eventCode, RoleCode = roleCode, Confidence = confidence };

    [Fact]
    public void An_event_code_that_exists_in_the_matrix_is_accepted()
    {
        Result result = SuggestionGuard.Validate(Suggest("purchasing.invoice.expense.posted", "ap_supplier_control"), Vocabulary);

        output.WriteLine(Report(result));
        Assert.True(result.IsSuccess);
    }

    /// <summary>
    /// رمز مخترَع: قيس في هذا المستودع وهو يُنتج <b>ترحيلاً مكرَّراً صامتاً</b>،
    /// فالرفض هنا يقع قبل أن يصل إلى أحد.
    /// </summary>
    [Fact]
    public void An_invented_event_code_is_refused_before_anyone_sees_it()
    {
        Result result = SuggestionGuard.Validate(Suggest("purchasing.invoice.magic.posted"), Vocabulary);

        output.WriteLine(Report(result));
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors, e => e.Code == "ai.capture.event_code_not_in_matrix");
    }

    /// <summary>رمز حساب في موضع رمز الحدث: يُرفض برمز مستقلّ يسمّي المخالفة لا الشكل.</summary>
    [Theory]
    [InlineData("1210")]
    [InlineData("purchasing.1210")]
    [InlineData("purchasing.invoice.1210.posted")]
    public void A_suggestion_carrying_a_ledger_code_is_refused(string code)
    {
        Result result = SuggestionGuard.Validate(Suggest(code), Vocabulary);

        output.WriteLine(code + " ⇒ " + Report(result));
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors, e => e.Code == "ai.capture.suggestion_names_a_ledger_code");
    }

    [Fact]
    public void A_role_code_outside_the_matrix_is_refused()
    {
        Result result = SuggestionGuard.Validate(Suggest("purchasing.invoice.expense.posted", "ap_imaginary_control"), Vocabulary);

        output.WriteLine(Report(result));
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors, e => e.Code == "ai.capture.role_code_not_in_matrix");
    }

    [Fact]
    public void A_malformed_event_code_is_refused()
    {
        Result result = SuggestionGuard.Validate(Suggest("PurchasingInvoiceExpense"), Vocabulary);

        output.WriteLine(Report(result));
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors, e => e.Code == "ai.capture.event_code_malformed");
    }

    [Fact]
    public void A_confidence_outside_zero_to_one_is_refused()
    {
        Result result = SuggestionGuard.Validate(Suggest("purchasing.invoice.expense.posted", confidence: 1.2m), Vocabulary);

        output.WriteLine(Report(result));
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors, e => e.Code == "ai.capture.confidence_out_of_range");
    }

    /// <summary>
    /// <b>المفردات مقروءة من المصفوفة نفسها لا مكتوبة بيد.</b> والحارس هنا يمنع أن تمرّ
    /// كل الاختبارات أعلاه على مفردات فارغة — وهي حالة كانت لتقبل «كل شيء مرفوض» خضراء.
    /// </summary>
    [Fact]
    public void The_vocabulary_is_read_from_the_matrix_and_is_not_empty()
    {
        output.WriteLine("رموز الأحداث: " + Vocabulary.EventCount.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("رموز الأدوار: " + Vocabulary.RoleCount.ToString(CultureInfo.InvariantCulture));

        Assert.True(Vocabulary.EventCount >= 80, "عدد رموز الأحداث المقروءة أقلّ من المتوقّع — المفردات ضامرة");
        Assert.True(Vocabulary.RoleCount >= 70, "عدد رموز الأدوار المقروءة أقلّ من المتوقّع — المفردات ضامرة");
        Assert.True(Vocabulary.KnowsEvent("purchasing.invoice.expense.posted"));
        Assert.True(Vocabulary.KnowsRole("ap_supplier_control"));
        Assert.False(Vocabulary.KnowsEvent("purchasing.invoice.magic.posted"));
    }

    /// <summary>
    /// <b>ولا رقم حساب واحد داخل التجميعة.</b> المفردات تُقرأ من رموز الأحداث والأدوار،
    /// ولا تُضمَّن خريطة المستأجر — وهي وحدها التي تحمل أرقام الحسابات.
    /// </summary>
    [Fact]
    public void The_module_assembly_embeds_no_tenant_role_map_and_therefore_no_ledger_code()
    {
        string[] resources = typeof(MatrixPostingVocabulary).Assembly.GetManifestResourceNames();

        foreach (string resource in resources)
        {
            output.WriteLine("مورد مضمَّن: " + resource);
        }

        Assert.NotEmpty(resources);
        Assert.DoesNotContain(resources, r => r.Contains("role-map", StringComparison.OrdinalIgnoreCase));

        // ونصّ الأدوار المضمَّن لا يحمل عمود رمز الحساب أصلاً.
        using Stream stream = typeof(MatrixPostingVocabulary).Assembly
            .GetManifestResourceStream("Babel.Ai.Matrix.roles.csv")!;
        using StreamReader reader = new(stream);
        string header = reader.ReadLine()!;

        output.WriteLine("ترويسة ملف الأدوار: " + header);
        Assert.DoesNotContain("account_code", header, StringComparison.Ordinal);
    }

    private static string Report(Result result) =>
        result.IsSuccess ? "نجح" : string.Join('\n', result.Errors.Select(static e => e.ToString()));
}
