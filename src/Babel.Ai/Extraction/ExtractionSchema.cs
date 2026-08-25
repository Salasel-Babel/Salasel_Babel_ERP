using System.Globalization;
using System.Text.Json;
using Babel.Ai.Capture;
using Babel.Ai.Suggestions;
using Babel.SharedKernel;

namespace Babel.Ai.Extraction;

/// <summary>
/// <b>مخطط مُخرَج المزوّد، مفروضاً عند الحدّ.</b>
/// <para>
/// مُخرَجٌ مشوَّه يجب أن <b>يفشل بصوت عالٍ</b>، لا أن يُنتج مسوّدةً معقولة وخاطئة. وهذا
/// نفس المبدأ الذي يطبّقه سطح HTTP على المال: <b>الرفض لا التطبيع الصامت</b>.
/// </para>
/// <para>
/// وثلاثة أصناف من العيوب لكلٍّ منها رمزه ورسالته: <b>حقل مجهول</b> (تغيّر المزوّد ولم
/// يخبرنا)، و<b>نوع خاطئ</b> (عدد حيث يجب نصّ)، و<b>حقل إلزامي غائب</b>. وصنف رابع
/// يخرج برمز مستقلّ لأنه ليس انحرافاً بل محاولة: <b>حقل يسمّي حساباً</b>.
/// </para>
/// <para>
/// <b>والمال نصّ على الحدّ</b>: كل مبلغ ونسبة وكمية تصل سلسلةً تُقرأ بثقافة ثابتة. عددُ
/// JSON مزدوج الدقة يفقد خانات على مبالغ كبيرة قبل أن يصل إلى <c>decimal</c> أصلاً.
/// </para>
/// </summary>
public static class ExtractionSchema
{
    /// <summary>إصدار المخطط المقبول وحده.</summary>
    public const string Version = "ai.capture.extraction.v1";

    private const string RootSection = "(الجذر)";
    private const string DocumentSection = "document";
    private const string LinesSection = "lines";
    private const string SuggestionSection = "suggestion";

    private static readonly IReadOnlySet<string> RootFields =
        new HashSet<string>(StringComparer.Ordinal) { "schema_version", DocumentSection, LinesSection, SuggestionSection };

    private static readonly IReadOnlySet<string> RequiredRootFields =
        new HashSet<string>(StringComparer.Ordinal) { "schema_version", DocumentSection, LinesSection };

    private static readonly IReadOnlySet<string> DocumentFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "seller_name", "seller_vat_number", "invoice_number", "issued_on",
        "currency", "net", "tax_rate", "tax_total", "gross_total",
    };

    /// <summary>
    /// الحقول الإلزامية في القسم <c>document</c>. <b>العملة والنسبة ليستا منهما عمداً:</b>
    /// كثير من فواتير الموردين لا تطبع النسبة، وغيابها ليس عيباً في المُخرَج. وحين تغيب
    /// تُؤخذ من إعدادات المستأجر بمصدر <c>defaulted</c> — <b>ويُعرَض ذلك للإنسان</b>،
    /// لأن نسبةً مفترَضة تُنتج ضريبةً «متّسقة حسابياً» وخاطئة على مشتريات معفاة.
    /// </summary>
    private static readonly IReadOnlySet<string> RequiredDocumentFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "seller_name", "seller_vat_number", "invoice_number", "issued_on", "net", "tax_total", "gross_total",
    };

    private static readonly IReadOnlySet<string> LineFields =
        new HashSet<string>(StringComparer.Ordinal) { "description", "quantity", "unit_price", "net" };

    private static readonly IReadOnlySet<string> SuggestionFields =
        new HashSet<string>(StringComparer.Ordinal) { "event_code", "role_code", "confidence", "rationale" };

    private static readonly IReadOnlySet<string> ValueFields =
        new HashSet<string>(StringComparer.Ordinal) { "value", "confidence" };

    /// <summary>
    /// يتحقق من نصّ JSON ويحوّله إلى النوع المُهيكَل. يعيد <b>كل</b> العيوب لا أوّلها.
    /// </summary>
    /// <param name="json">مُخرَج المزوّد كما ورد.</param>
    public static Result<ExtractedInvoice> Validate(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException error)
        {
            return Result<ExtractedInvoice>.Failure(CaptureErrors.PayloadNotJson(error.Message));
        }

        using (document)
        {
            return Validate(document.RootElement);
        }
    }

    private static Result<ExtractedInvoice> Validate(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Result<ExtractedInvoice>.Failure(CaptureErrors.PayloadNotAnObject);
        }

        List<Error> errors = [];
        CheckNames(errors, root, RootFields, RootSection);
        RequireAll(errors, root, RequiredRootFields, RootSection);

        if (errors.Count > 0)
        {
            return Result<ExtractedInvoice>.Failure(errors);
        }

        if (root.GetProperty("schema_version").ValueKind != JsonValueKind.String)
        {
            errors.Add(CaptureErrors.WrongJsonKind("schema_version", "نصّاً", Describe(root.GetProperty("schema_version").ValueKind)));
            return Result<ExtractedInvoice>.Failure(errors);
        }

        string version = root.GetProperty("schema_version").GetString()!;
        if (!string.Equals(version, Version, StringComparison.Ordinal))
        {
            return Result<ExtractedInvoice>.Failure(CaptureErrors.SchemaVersionUnknown(version, Version));
        }

        JsonElement documentElement = root.GetProperty(DocumentSection);
        if (documentElement.ValueKind != JsonValueKind.Object)
        {
            errors.Add(CaptureErrors.WrongJsonKind(DocumentSection, "كائناً", Describe(documentElement.ValueKind)));
        }
        else
        {
            CheckNames(errors, documentElement, DocumentFields, DocumentSection);
            RequireAll(errors, documentElement, RequiredDocumentFields, DocumentSection);
        }

        JsonElement linesElement = root.GetProperty(LinesSection);
        if (linesElement.ValueKind != JsonValueKind.Array)
        {
            errors.Add(CaptureErrors.WrongJsonKind(LinesSection, "مصفوفة", Describe(linesElement.ValueKind)));
        }
        else if (linesElement.GetArrayLength() == 0)
        {
            errors.Add(CaptureErrors.NoLines);
        }

        if (errors.Count > 0)
        {
            return Result<ExtractedInvoice>.Failure(errors);
        }

        ExtractedText sellerName = Text(errors, documentElement, "seller_name");
        ExtractedText vatNumber = Text(errors, documentElement, "seller_vat_number");
        ExtractedText invoiceNumber = Text(errors, documentElement, "invoice_number");
        ExtractedDate issuedOn = Date(errors, documentElement, "issued_on");
        ExtractedCurrency? currency = documentElement.TryGetProperty("currency", out _)
            ? Currency(errors, documentElement, "currency")
            : null;
        ExtractedNumber net = Number(errors, documentElement, "net");
        ExtractedNumber? taxRate = documentElement.TryGetProperty("tax_rate", out _)
            ? Number(errors, documentElement, "tax_rate")
            : null;
        ExtractedNumber taxTotal = Number(errors, documentElement, "tax_total");
        ExtractedNumber grossTotal = Number(errors, documentElement, "gross_total");

        List<ExtractedLine> lines = [];
        int index = 0;
        foreach (JsonElement line in linesElement.EnumerateArray())
        {
            index++;
            if (line.ValueKind != JsonValueKind.Object)
            {
                errors.Add(CaptureErrors.LineNotAnObject(index));
                continue;
            }

            string section = FormattableString.Invariant($"{LinesSection}[{index}]");
            CheckNames(errors, line, LineFields, section);
            RequireAll(errors, line, LineFields, section);

            if (errors.Count > 0)
            {
                continue;
            }

            lines.Add(new ExtractedLine(
                index,
                Text(errors, line, "description", section),
                Number(errors, line, "quantity", section),
                Number(errors, line, "unit_price", section),
                Number(errors, line, "net", section)));
        }

        ExtractedSuggestion? suggestion = null;
        if (root.TryGetProperty(SuggestionSection, out JsonElement suggestionElement))
        {
            suggestion = Suggestion(errors, suggestionElement);
        }

        return errors.Count == 0
            ? Result<ExtractedInvoice>.Success(new ExtractedInvoice
            {
                SellerName = sellerName,
                SellerVatNumber = vatNumber,
                InvoiceNumber = invoiceNumber,
                IssuedOn = issuedOn,
                Currency = currency,
                Net = net,
                TaxRate = taxRate,
                TaxTotal = taxTotal,
                GrossTotal = grossTotal,
                Lines = lines,
                Suggestion = suggestion,
            })
            : Result<ExtractedInvoice>.Failure(errors);
    }

    /// <summary>
    /// كل اسم في القسم معروف. والاسم الذي يسمّي حساباً يخرج <b>برمز مستقلّ</b>: هو ليس
    /// انحرافاً في المخطط بل محاولة لتسمية حساب من خارج المصفوفة.
    /// </summary>
    private static void CheckNames(List<Error> errors, JsonElement element, IReadOnlySet<string> allowed, string section)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (allowed.Contains(property.Name))
            {
                continue;
            }

            errors.Add(SuggestionGuard.LedgerCodeFieldNames.Contains(property.Name)
                ? CaptureErrors.FieldNamesLedgerCode(section, property.Name)
                : CaptureErrors.UnknownField(section, property.Name));
        }
    }

    private static void RequireAll(List<Error> errors, JsonElement element, IReadOnlySet<string> required, string section)
    {
        foreach (string name in required.Order(StringComparer.Ordinal))
        {
            if (!element.TryGetProperty(name, out _))
            {
                errors.Add(CaptureErrors.MissingField(section, name));
            }
        }
    }

    /// <summary>
    /// كل قيمة كائن من حقلين بالضبط: <c>value</c> نصّاً و<c>confidence</c> عدداً.
    /// وشكل واحد لكل الحقول يجعل «عدد حيث يجب نصّ» عيباً <b>واحداً</b> يُفحص في موضع واحد.
    /// </summary>
    private static (string Text, decimal Confidence) Value(List<Error> errors, JsonElement parent, string field, string section)
    {
        string path = section + "." + field;
        JsonElement element = parent.GetProperty(field);

        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add(CaptureErrors.WrongJsonKind(path, "كائناً من {value, confidence}", Describe(element.ValueKind)));
            return (string.Empty, 0m);
        }

        CheckNames(errors, element, ValueFields, path);
        RequireAll(errors, element, ValueFields, path);

        if (!element.TryGetProperty("value", out JsonElement value) || !element.TryGetProperty("confidence", out JsonElement confidence))
        {
            return (string.Empty, 0m);
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            errors.Add(CaptureErrors.WrongJsonKind(path + ".value", "نصّاً", Describe(value.ValueKind)));
            return (string.Empty, 0m);
        }

        if (confidence.ValueKind != JsonValueKind.Number)
        {
            errors.Add(CaptureErrors.WrongJsonKind(path + ".confidence", "عدداً", Describe(confidence.ValueKind)));
            return (value.GetString()!, 0m);
        }

        decimal score = confidence.GetDecimal();
        if (score is < 0m or > 1m)
        {
            errors.Add(CaptureErrors.ConfidenceOutOfRange(path, score));
        }

        return (value.GetString()!, score);
    }

    private static ExtractedText Text(List<Error> errors, JsonElement parent, string field, string section = DocumentSection)
    {
        (string text, decimal confidence) = Value(errors, parent, field, section);
        return new ExtractedText(text, confidence);
    }

    private static ExtractedNumber Number(List<Error> errors, JsonElement parent, string field, string section = DocumentSection)
    {
        (string text, decimal confidence) = Value(errors, parent, field, section);

        if (!decimal.TryParse(
                text,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out decimal parsed))
        {
            errors.Add(CaptureErrors.NotADecimal(section + "." + field, text));
            return new ExtractedNumber(0m, confidence);
        }

        return new ExtractedNumber(parsed, confidence);
    }

    private static ExtractedDate Date(List<Error> errors, JsonElement parent, string field, string section = DocumentSection)
    {
        (string text, decimal confidence) = Value(errors, parent, field, section);

        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed))
        {
            errors.Add(CaptureErrors.DateNotIso(section + "." + field, text));
            return new ExtractedDate(DateOnly.MinValue, confidence);
        }

        return new ExtractedDate(parsed, confidence);
    }

    private static ExtractedCurrency Currency(List<Error> errors, JsonElement parent, string field, string section = DocumentSection)
    {
        (string text, decimal confidence) = Value(errors, parent, field, section);

        if (text.Length != 3 || text.Any(static c => c is < 'A' or > 'Z'))
        {
            errors.Add(CaptureErrors.CurrencyNotAcceptable(text));
            return new ExtractedCurrency(CurrencyCode.Sar, confidence);
        }

        return new ExtractedCurrency(CurrencyCode.FromString(text), confidence);
    }

    private static ExtractedSuggestion? Suggestion(List<Error> errors, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add(CaptureErrors.WrongJsonKind(SuggestionSection, "كائناً", Describe(element.ValueKind)));
            return null;
        }

        CheckNames(errors, element, SuggestionFields, SuggestionSection);

        foreach (string name in new[] { "event_code", "confidence" })
        {
            if (!element.TryGetProperty(name, out _))
            {
                errors.Add(CaptureErrors.MissingField(SuggestionSection, name));
            }
        }

        if (errors.Count > 0)
        {
            return null;
        }

        JsonElement eventCode = element.GetProperty("event_code");
        if (eventCode.ValueKind != JsonValueKind.String)
        {
            errors.Add(CaptureErrors.WrongJsonKind("suggestion.event_code", "نصّاً", Describe(eventCode.ValueKind)));
            return null;
        }

        JsonElement confidence = element.GetProperty("confidence");
        if (confidence.ValueKind != JsonValueKind.Number)
        {
            errors.Add(CaptureErrors.WrongJsonKind("suggestion.confidence", "عدداً", Describe(confidence.ValueKind)));
            return null;
        }

        return new ExtractedSuggestion(
            eventCode.GetString()!,
            OptionalText(errors, element, "role_code"),
            confidence.GetDecimal(),
            OptionalText(errors, element, "rationale"));
    }

    private static string OptionalText(List<Error> errors, JsonElement element, string field)
    {
        if (!element.TryGetProperty(field, out JsonElement value))
        {
            return string.Empty;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            errors.Add(CaptureErrors.WrongJsonKind(SuggestionSection + "." + field, "نصّاً", Describe(value.ValueKind)));
            return string.Empty;
        }

        return value.GetString()!;
    }

    /// <summary>وصف نوع JSON بالعربية — الرسالة تقول ما ورد لا «نوع غير متوقّع».</summary>
    private static string Describe(JsonValueKind kind) => kind switch
    {
        JsonValueKind.Object => "كائناً",
        JsonValueKind.Array => "مصفوفة",
        JsonValueKind.String => "نصّاً",
        JsonValueKind.Number => "عدداً",
        JsonValueKind.True or JsonValueKind.False => "قيمة منطقية",
        JsonValueKind.Null => "فراغاً",
        _ => "قيمة غير معروفة",
    };
}
