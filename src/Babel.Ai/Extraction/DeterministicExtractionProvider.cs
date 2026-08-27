using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Babel.Ai.Capture;
using Babel.SharedKernel;

namespace Babel.Ai.Extraction;

/// <summary>سطر في مُخرَج مُركَّب.</summary>
/// <param name="Description">البيان.</param>
/// <param name="Quantity">الكمية.</param>
/// <param name="UnitPrice">سعر الوحدة.</param>
/// <param name="LineNet">صافي السطر.</param>
public sealed record ComposedLine(string Description, decimal Quantity, decimal UnitPrice, decimal LineNet);

/// <summary>وصف مُخرَج استخراج يُبنى منه نصّ JSON مطابق للمخطط.</summary>
public sealed record ComposedExtraction
{
    /// <summary>اسم البائع.</summary>
    public required string SellerName { get; init; }

    /// <summary>الرقم الضريبي للبائع.</summary>
    public required string SellerVatNumber { get; init; }

    /// <summary>رقم الفاتورة.</summary>
    public required string InvoiceNumber { get; init; }

    /// <summary>تاريخ الإصدار الميلادي.</summary>
    public required DateOnly IssuedOn { get; init; }

    /// <summary>الصافي قبل الضريبة.</summary>
    public required decimal Net { get; init; }

    /// <summary>مبلغ الضريبة.</summary>
    public required decimal TaxTotal { get; init; }

    /// <summary>الإجمالي شامل الضريبة.</summary>
    public required decimal GrossTotal { get; init; }

    /// <summary>العملة إن طُبعت على المستند.</summary>
    public string? Currency { get; init; }

    /// <summary>نسبة الضريبة إن طُبعت على المستند.</summary>
    public decimal? TaxRate { get; init; }

    /// <summary>السطور.</summary>
    public IReadOnlyList<ComposedLine> Lines { get; init; } = [];

    /// <summary>رمز الحدث المقترح، أو فارغ فلا اقتراح.</summary>
    public string SuggestedEventCode { get; init; } = string.Empty;

    /// <summary>رمز الدور المقترح.</summary>
    public string SuggestedRoleCode { get; init; } = string.Empty;

    /// <summary>تعليل النموذج كما كتبه.</summary>
    public string Rationale { get; init; } = string.Empty;

    /// <summary>درجة الثقة على حقول المستند وسطوره.</summary>
    public decimal Confidence { get; init; } = 0.94m;

    /// <summary>درجة الثقة على الاقتراح.</summary>
    public decimal SuggestionConfidence { get; init; } = 0.80m;
}

/// <summary>
/// <b>مزوّد استخراج حتمي يعمل بلا شبكة.</b> نفس شكل <c>Babel.Compliance.FakeProvider</c>
/// وللسبب نفسه (‏ADR-0015): يُشغَّل به <b>كامل</b> مسار العمل في الاختبارات وفي العرض
/// التوضيحي، فلا يبقى مسار لا يُختبر إلا بخدمة خارجية.
/// <para>
/// <b>وما لا يفعله:</b> لا يخترع جواباً. مستندٌ لم يُبذَر له مُخرَج يُعاد عنه رفضٌ صريح،
/// لا مسوّدةٌ مصنوعة من فراغ — لأن مسوّدةً معقولة من فراغ هي أسوأ ما يُنتجه مزوّد وهمي.
/// </para>
/// </summary>
public sealed class DeterministicExtractionProvider : IInvoiceExtractionProvider
{
    /// <summary>معرّف المزوّد كما يُسجَّل على كل مسوّدة.</summary>
    public const string Id = "babel.fake.extractor.v1";

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly Dictionary<string, string> _answers = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ExtractionProviderCapabilities Capabilities { get; } = new(
        ProviderId: Id,
        DisplayNameKey: "ai.capture.provider.fake",
        Residency: ExtractionResidency.InProcess,
        ReadsLineItems: true,
        IsDeterministic: true,
        Timeout: TimeSpan.FromSeconds(5));

    /// <summary>يبذر مُخرَجاً لمستند بعينه. يعيد المزوّد نفسه كي تتسلسل البذور.</summary>
    /// <param name="documentId">معرّف المستند.</param>
    /// <param name="json">المُخرَج كما سيعود حرفياً.</param>
    public DeterministicExtractionProvider Answering(string documentId, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentNullException.ThrowIfNull(json);
        _answers[documentId] = json;
        return this;
    }

    /// <summary>يبذر مُخرَجاً مُركَّباً مطابقاً للمخطط.</summary>
    /// <param name="documentId">معرّف المستند.</param>
    /// <param name="extraction">وصف المُخرَج.</param>
    public DeterministicExtractionProvider Answering(string documentId, ComposedExtraction extraction) =>
        Answering(documentId, Compose(extraction));

    /// <inheritdoc />
    public ValueTask<Result<ExtractionOutput>> ExtractAsync(
        ExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(_answers.TryGetValue(request.DocumentId, out string? json)
            ? Result<ExtractionOutput>.Success(new ExtractionOutput(Id, json))
            : Result<ExtractionOutput>.Failure(CaptureErrors.ProviderHasNoAnswer(request.DocumentId)));
    }

    /// <summary>
    /// يبني نصّ JSON مطابقاً للمخطط. <b>المال نصّ</b> وبثقافة ثابتة: هذه هي الصورة نفسها
    /// التي يجب أن يرسلها مزوّد حقيقي، فبناؤها هنا يجعل المخطط مُختبَراً في الاتجاهين.
    /// </summary>
    /// <param name="extraction">وصف المُخرَج.</param>
    public static string Compose(ComposedExtraction extraction)
    {
        ArgumentNullException.ThrowIfNull(extraction);

        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("schema_version", ExtractionSchema.Version);

            writer.WriteStartObject("document");
            WriteValue(writer, "seller_name", extraction.SellerName, extraction.Confidence);
            WriteValue(writer, "seller_vat_number", extraction.SellerVatNumber, extraction.Confidence);
            WriteValue(writer, "invoice_number", extraction.InvoiceNumber, extraction.Confidence);
            WriteValue(writer, "issued_on", extraction.IssuedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), extraction.Confidence);

            if (extraction.Currency is not null)
            {
                WriteValue(writer, "currency", extraction.Currency, extraction.Confidence);
            }

            WriteValue(writer, "net", Amount(extraction.Net), extraction.Confidence);

            if (extraction.TaxRate is not null)
            {
                WriteValue(writer, "tax_rate", Rate(extraction.TaxRate.Value), extraction.Confidence);
            }

            WriteValue(writer, "tax_total", Amount(extraction.TaxTotal), extraction.Confidence);
            WriteValue(writer, "gross_total", Amount(extraction.GrossTotal), extraction.Confidence);
            writer.WriteEndObject();

            writer.WriteStartArray("lines");
            foreach (ComposedLine line in extraction.Lines)
            {
                writer.WriteStartObject();
                WriteValue(writer, "description", line.Description, extraction.Confidence);
                WriteValue(writer, "quantity", Rate(line.Quantity), extraction.Confidence);
                WriteValue(writer, "unit_price", Amount(line.UnitPrice), extraction.Confidence);
                WriteValue(writer, "net", Amount(line.LineNet), extraction.Confidence);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            if (extraction.SuggestedEventCode.Length > 0)
            {
                writer.WriteStartObject("suggestion");
                writer.WriteString("event_code", extraction.SuggestedEventCode);

                if (extraction.SuggestedRoleCode.Length > 0)
                {
                    writer.WriteString("role_code", extraction.SuggestedRoleCode);
                }

                writer.WriteNumber("confidence", extraction.SuggestionConfidence);

                if (extraction.Rationale.Length > 0)
                {
                    writer.WriteString("rationale", extraction.Rationale);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteValue(Utf8JsonWriter writer, string name, string value, decimal confidence)
    {
        writer.WriteStartObject(name);
        writer.WriteString("value", value);
        writer.WriteNumber("confidence", confidence);
        writer.WriteEndObject();
    }

    private static string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Rate(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
