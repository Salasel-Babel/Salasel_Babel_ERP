using Babel.Ai.Extraction;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Capture;

/// <summary>
/// <b>المخطط مفروضاً عند الحدّ.</b> مُخرَجٌ مشوَّه يفشل بصوت عالٍ ويسمّي ما فيه،
/// ولا يُصلَح بهدوء فيُنتج مسوّدةً معقولة وخاطئة.
/// </summary>
public sealed class ExtractionSchemaTests(ITestOutputHelper output)
{
    private const string Valid = """
        {
          "schema_version": "ai.capture.extraction.v1",
          "document": {
            "seller_name": {"value": "شركة سلاسل بابل للمقاولات", "confidence": 0.97},
            "seller_vat_number": {"value": "300000000000003", "confidence": 0.99},
            "invoice_number": {"value": "INV-4417", "confidence": 0.95},
            "issued_on": {"value": "2026-08-25", "confidence": 0.93},
            "net": {"value": "1000.00", "confidence": 0.94},
            "tax_total": {"value": "150.00", "confidence": 0.94},
            "gross_total": {"value": "1150.00", "confidence": 0.94}
          },
          "lines": [
            {"description": {"value": "خدمات صيانة", "confidence": 0.9},
             "quantity": {"value": "1", "confidence": 0.9},
             "unit_price": {"value": "1000.00", "confidence": 0.9},
             "net": {"value": "1000.00", "confidence": 0.9}}
          ]
        }
        """;

    [Fact]
    public void A_conforming_payload_is_accepted_and_its_money_is_read_as_decimal()
    {
        Result<ExtractedInvoice> result = ExtractionSchema.Validate(Valid);

        Assert.True(result.IsSuccess, Report(result));
        Assert.Equal(1000.00m, result.Value.Net.Value);
        Assert.Equal(150.00m, result.Value.TaxTotal.Value);
        Assert.Equal(new DateOnly(2026, 8, 25), result.Value.IssuedOn.Value);

        // العملة والنسبة غائبتان عن المستند — وغيابهما ليس عيباً، بل يُملأ من الإعدادات لاحقاً.
        Assert.Null(result.Value.Currency);
        Assert.Null(result.Value.TaxRate);
        output.WriteLine("قُبِل، والصافي " + result.Value.Net.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>حقل لا يعرفه المخطط يُرفض ولا يُتجاهَل: تجاهله يُخفي تغيّراً في مُخرَج المزوّد.</summary>
    [Fact]
    public void An_unknown_field_is_refused_and_named()
    {
        string payload = Valid.Replace(
            "\"invoice_number\":",
            "\"po_number\": {\"value\": \"PO-9\", \"confidence\": 0.5},\n    \"invoice_number\":",
            StringComparison.Ordinal);

        Result<ExtractedInvoice> result = ExtractionSchema.Validate(payload);

        output.WriteLine(Report(result));
        Assert.True(result.IsFailure);
        Error error = Assert.Single(result.Errors, e => e.Code == "ai.capture.unknown_field");
        Assert.Contains("po_number", error.MessageAr, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>حقل يسمّي حساباً يخرج برمز مستقلّ</b> — لأنه ليس انحرافاً في المخطط بل محاولة
    /// لتسمية حساب من خارج المصفوفة، ورسالته يجب أن تُرسل المُصلِح إلى مكان آخر.
    /// </summary>
    [Fact]
    public void A_field_that_names_a_ledger_code_is_refused_by_a_code_of_its_own()
    {
        string payload = Valid.Replace(
            "\"invoice_number\":",
            "\"account_code\": {\"value\": \"1210\", \"confidence\": 0.9},\n    \"invoice_number\":",
            StringComparison.Ordinal);

        Result<ExtractedInvoice> result = ExtractionSchema.Validate(payload);

        output.WriteLine(Report(result));
        Assert.True(result.IsFailure);
        Error error = Assert.Single(result.Errors, e => e.Code == "ai.capture.field_names_a_ledger_code");
        Assert.Contains("لا يرى دليل الحسابات", error.MessageAr, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Errors, e => e.Code == "ai.capture.unknown_field");
    }

    /// <summary>عدد حيث يجب نصّ: مالٌ يصل عدداً في JSON يفقد خانات قبل أن يبلغ <c>decimal</c>.</summary>
    [Fact]
    public void A_number_where_text_was_required_is_refused()
    {
        string payload = Valid.Replace("\"value\": \"1000.00\", \"confidence\": 0.94", "\"value\": 1000.00, \"confidence\": 0.94", StringComparison.Ordinal);

        Result<ExtractedInvoice> result = ExtractionSchema.Validate(payload);

        output.WriteLine(Report(result));
        Assert.True(result.IsFailure);
        Error error = Assert.Single(result.Errors, e => e.Code == "ai.capture.field_wrong_json_kind");
        Assert.Contains("مطلوب نصّاً وورد عدداً", error.MessageAr, StringComparison.Ordinal);
    }

    /// <summary>حقل إلزامي غائب يُسمّى باسمه وبقسمه.</summary>
    [Fact]
    public void A_missing_mandatory_field_is_refused_and_named()
    {
        string payload = Valid.Replace(
            "\"gross_total\": {\"value\": \"1150.00\", \"confidence\": 0.94}",
            "\"tax_total\": {\"value\": \"150.00\", \"confidence\": 0.94}",
            StringComparison.Ordinal);

        Result<ExtractedInvoice> result = ExtractionSchema.Validate(payload);

        output.WriteLine(Report(result));
        Assert.True(result.IsFailure);
        Error error = Assert.Single(result.Errors, e => e.Code == "ai.capture.missing_field");
        Assert.Contains("gross_total", error.MessageAr, StringComparison.Ordinal);
    }

    /// <summary>الفاصلة العربية داخل مبلغ: تُقرأ على الشاشة صحيحة، وليست العدد نفسه.</summary>
    [Fact]
    public void An_amount_written_with_the_Arabic_decimal_separator_is_refused()
    {
        string payload = Valid.Replace("\"1150.00\"", "\"1150٫00\"", StringComparison.Ordinal);

        Result<ExtractedInvoice> result = ExtractionSchema.Validate(payload);

        output.WriteLine(Report(result));
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors, e => e.Code == "ai.capture.field_not_a_decimal");
    }

    /// <summary>مُخرَج بلا سطور ليس نجاحاً: الرمز يحمل الإجماليات، والسطور هي عمل القراءة الضوئية.</summary>
    [Fact]
    public void An_extraction_with_no_lines_is_refused()
    {
        string payload = Valid.Replace(
            Valid[Valid.IndexOf("\"lines\"", StringComparison.Ordinal)..Valid.LastIndexOf('}')],
            "\"lines\": []\n",
            StringComparison.Ordinal);

        Result<ExtractedInvoice> result = ExtractionSchema.Validate(payload);

        output.WriteLine(Report(result));
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors, e => e.Code == "ai.capture.no_lines");
    }

    /// <summary>نصّ ليس JSON: العطل يُسمّى ولا يخرج استثناءً غير مُعالَج.</summary>
    [Fact]
    public void A_payload_that_is_not_json_is_refused_as_a_value_not_an_exception()
    {
        Result<ExtractedInvoice> result = ExtractionSchema.Validate("ليست حمولة");

        output.WriteLine(Report(result));
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors, e => e.Code == "ai.capture.payload_not_json");
    }

    /// <summary>إصدار مخطط آخر: الشكل قد يبقى والدلالة تتغيّر — وهذا أخطر من شكل مختلف.</summary>
    [Fact]
    public void An_unknown_schema_version_is_refused()
    {
        string payload = Valid.Replace("ai.capture.extraction.v1", "ai.capture.extraction.v2", StringComparison.Ordinal);

        Result<ExtractedInvoice> result = ExtractionSchema.Validate(payload);

        output.WriteLine(Report(result));
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors, e => e.Code == "ai.capture.schema_version_unknown");
    }

    /// <summary>درجة ثقة خارج المدى تُرفض — رقمٌ فوق الواحد يوحي بيقين لا يقيسه شيء.</summary>
    [Fact]
    public void A_confidence_outside_zero_to_one_is_refused()
    {
        string payload = Valid.Replace("\"confidence\": 0.97", "\"confidence\": 1.4", StringComparison.Ordinal);

        Result<ExtractedInvoice> result = ExtractionSchema.Validate(payload);

        output.WriteLine(Report(result));
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors, e => e.Code == "ai.capture.confidence_out_of_range");
    }

    /// <summary>
    /// حارس لافراغ: العيّنة السليمة تمرّ فعلاً، فلو صار كل شيء يُرفض لبدت الاختبارات
    /// أعلاه خضراء وهي لا تُثبت شيئاً.
    /// </summary>
    [Fact]
    public void The_negative_cases_mean_something_because_the_positive_case_passes()
    {
        Assert.True(ExtractionSchema.Validate(Valid).IsSuccess);
        Assert.True(ExtractionSchema.Validate(Valid.Replace("0.97", "0.98", StringComparison.Ordinal)).IsSuccess);
    }

    private static string Report<T>(Result<T> result) =>
        result.IsSuccess ? "نجح" : string.Join('\n', result.Errors.Select(static e => e.ToString()));
}
