using System.Text.Encodings.Web;
using System.Text.Json;
using Babel.Api.Endpoints;
using Babel.Contracts.Posting;
using Babel.SharedKernel;

namespace Babel.Api.OpenApi;

/// <summary>
/// مولّد وثيقة العقد المنشور.
/// <para>
/// <b>الحتمية هي كل شيء هنا.</b> وثيقة تتغيّر بايتاتها بين تشغيلين تجعل حارس الانحراف
/// عديم القيمة — <b>وأسوأ من ذلك: تجعله يبدو عاملاً</b>. ولذلك كل مصدر لاحتمالية
/// مُغلق صراحةً:
/// </para>
/// <list type="bullet">
///   <item><description>لا طابع زمني، ولا إصدار تجميعة، ولا اسم جهاز، ولا معرّف بناء.</description></item>
///   <item><description>لا قاموس يُكتب بترتيب تعدّاده: كل مجموعة تُكتب من قائمة مرتَّبة صراحةً بـ<c>StringComparer.Ordinal</c>.</description></item>
///   <item><description>لا فرز ثقافي: <c>Ordinal</c> في كل موضع — الفرز الثقافي يختلف بين <c>tr-TR</c> و<c>en-US</c> على الحروف نفسها.</description></item>
///   <item><description>سطر جديد مثبَّت <c>\n</c>، ومسافة بادئة مثبَّتة، ومُرمِّز مثبَّت، وبلا علامة ترتيب بايتات.</description></item>
///   <item><description>قوائم أعضاء التعدادات تُقرأ من التعداد نفسه بترتيب إعلانه — لا نصّاً مكتوباً بيد ينحرف عنه.</description></item>
/// </list>
/// <para>
/// <b>وحارس ثانٍ داخل المولّد نفسه:</b> قبل الكتابة يُقارَن ما وثّقناه بما سجّله التطبيق
/// فعلاً من مسارات وأفعال. اختلافٌ في أي اتجاه يُفشل التوليد — فلا يمكن أن يُودَع عقد
/// يصف باباً غير موجود، ولا أن يوجد باب غير موصوف.
/// </para>
/// </summary>
internal static class OpenApiEmitter
{
    /// <summary>إصدار مواصفة OpenAPI المُستعمل.</summary>
    public const string SpecVersion = "3.1.0";

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        IndentCharacter = ' ',
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        SkipValidation = false,
    };

    /// <summary>يولّد الوثيقة بايتات UTF-8 بلا علامة ترتيب بايتات.</summary>
    /// <param name="registeredOperations">
    /// ما سجّله التطبيق فعلاً: (المسار، الفعل). يُقارَن بما توثّقه هذه الوثيقة.
    /// </param>
    public static byte[] Emit(IReadOnlyCollection<(string Path, string Method)> registeredOperations)
    {
        ArgumentNullException.ThrowIfNull(registeredOperations);

        CrossCheck(registeredOperations);

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, WriterOptions))
        {
            WriteDocument(writer);
        }

        // سطر أخير: ملف نصّي مُودَع في git ينتهي بسطر جديد، وإلا أظهر كل فرق سطراً زائفاً.
        stream.Write("\n"u8);
        return stream.ToArray();
    }

    /// <summary>
    /// كل عملية توثّقها هذه الوثيقة: المسار والفعل ومعرّفها.
    /// <b>مرتَّبة صراحةً</b> — لا بترتيب كتابتها في هذا الملف.
    /// </summary>
    private static IReadOnlyList<Operation> Operations { get; } =
    [
        .. new List<Operation>
        {
            new(ApiRoutes.Health, "get", "health",
                "حالة الخدمة وثقافتها", "Service health and culture",
                "تُرجع حالة الخدمة، وثقافة العملية وتقويمها الافتراضي. خارج المصادقة وخارج نطاق الشركة.",
                "Returns service status plus the process culture and its default calendar. Unauthenticated and outside company scope.",
                Body: null, Response: "HealthResponse", Success: 200, Anonymous: true, Query: []),

            new(ApiRoutes.PostJournalEntry, "post", "postJournalEntry",
                "ترحيل قيد", "Post a journal entry",
                "يرحّل قيداً عبر محرّك الترحيل. حصين ضد التكرار بمفتاح idempotencyKey: الوصول الثاني بالمفتاح نفسه "
                + "يُرجع الإيصال ذاته و‏alreadyPosted = true ورمز 200 بدل 201، ولا يُنشئ قيداً ثانياً — مهما كان ترتيب الوصول.",
                "Posts an entry through the posting engine. Idempotent by idempotencyKey: a second arrival with the same key "
                + "returns the same receipt with alreadyPosted = true and status 200 instead of 201, and never creates a second entry — whatever the arrival order.",
                Body: "PostJournalEntryRequest", Response: "PostingReceipt", Success: 201, Anonymous: false, Query: []),

            new(ApiRoutes.ReadJournalEntry, "get", "readJournalEntry",
                "قراءة قيد بسطوره", "Read one entry with its lines",
                "يقرأ قيداً واحداً بسطوره داخل نطاق الشركة.",
                "Reads a single entry with its lines within the company scope.",
                Body: null, Response: "JournalEntry", Success: 200, Anonymous: false, Query: []),

            new(ApiRoutes.ReverseJournalEntry, "post", "reverseJournalEntry",
                "عكس قيد", "Reverse an entry",
                "ينشئ قيد عكس مرتبطاً بالقيد الأصلي. القيد الأصلي لا يُمسّ ولا يُحذف ولا يُعدَّل — ولا يوجد على هذا السطح فعل حذف أصلاً.",
                "Creates a reversing entry linked to the original. The original is never touched, deleted, or amended — and no delete verb exists on this surface at all.",
                Body: "ReverseJournalEntryRequest", Response: "PostingReceipt", Success: 201, Anonymous: false, Query: []),

            new(ApiRoutes.TrialBalance, "get", "readTrialBalance",
                "ميزان المراجعة", "Trial balance",
                "ميزان المراجعة مبنيّاً من سطور القيود غير القابلة للتعديل — لا من جدول الأرصدة. "
                + "ولا يحمل مجموعاً: جمع عمود مالي حساب على المال، ولا يقع في طبقة HTTP.",
                "The trial balance built from the immutable journal lines — not from the balance table. "
                + "It carries no totals: summing a monetary column is money arithmetic and does not happen in the HTTP layer.",
                Body: null, Response: "TrialBalance", Success: 200, Anonymous: false,
                Query:
                [
                    new QueryParameter("book", true, "الدفتر داخل الشركة.", "The book within the company.", "string"),
                    new QueryParameter("period", false, "رمز الفترة yyyy-MM ميلادياً، أو غيابه فكل الفترات.", "Gregorian period code yyyy-MM, or omit for all periods.", "period"),
                ]),

            new(ApiRoutes.ChainVerification, "get", "verifyLedgerChain",
                "إعادة التحقق من سلسلة البصمات", "Verify the hash chain",
                "يعيد بناء كل مستند من الحقيقة المجالية المخزَّنة ويقارن بصمته، ويسمّي أول تسلسل منحرف إن وُجد.",
                "Rebuilds every document from the stored domain truth, compares its hash, and names the first divergent sequence if any.",
                Body: null, Response: "ChainVerification", Success: 200, Anonymous: false,
                Query:
                [
                    new QueryParameter("book", true, "الدفتر داخل الشركة.", "The book within the company.", "string"),
                    new QueryParameter("fiscalYear", true, "السنة المالية الميلادية بأربعة أرقام لاتينية.", "The Gregorian fiscal year, four Latin digits.", "year"),
                ]),
        }.OrderBy(static o => o.Path, StringComparer.Ordinal).ThenBy(static o => o.Method, StringComparer.Ordinal),
    ];

    private static void CrossCheck(IReadOnlyCollection<(string Path, string Method)> registered)
    {
        HashSet<string> documented = [.. Operations.Select(static o => o.Method + " " + o.Path)];
        HashSet<string> actual = [.. registered.Select(static r => r.Method.ToLowerInvariant() + " " + r.Path)];

        List<string> undocumented = [.. actual.Except(documented, StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        List<string> phantom = [.. documented.Except(actual, StringComparer.Ordinal).Order(StringComparer.Ordinal)];

        if (undocumented.Count > 0 || phantom.Count > 0)
        {
            throw new InvalidOperationException(
                "العقد المنشور لا يطابق ما سجّله التطبيق فعلاً — والتوليد يتوقّف قبل أن يُودَع عقد كاذب.\n"
                + "The published contract does not match what the application actually registered; generation stops before a false contract is committed.\n"
                + (undocumented.Count > 0 ? "مسجَّل بلا توثيق / registered but undocumented:\n  " + string.Join("\n  ", undocumented) + "\n" : string.Empty)
                + (phantom.Count > 0 ? "موثَّق بلا تسجيل / documented but not registered:\n  " + string.Join("\n  ", phantom) : string.Empty));
        }
    }

    private static void WriteDocument(Utf8JsonWriter w)
    {
        w.WriteStartObject();

        w.WriteString("openapi", SpecVersion);

        w.WriteStartObject("info");
        w.WriteString("title", "سلاسل بابل — سطح دفتر الأستاذ / Salasel Babel — Ledger API");
        w.WriteString("summary", "العقد المنشور بين الواجهات والخلفية. لا يقرأ فريق الواجهة شيفرة خلفية ليبني عليه.");
        w.WriteString("description", ContractDescription);
        w.WriteString("version", ApiRoutes.Version);
        w.WriteEndObject();

        w.WriteStartArray("servers");
        w.WriteStartObject();
        w.WriteString("url", "/");
        w.WriteString("description", "الخادم نفسه الذي تُقرأ منه هذه الوثيقة. / The same server this document is read from.");
        w.WriteEndObject();
        w.WriteEndArray();

        w.WriteStartArray("security");
        w.WriteStartObject();
        w.WriteStartArray("bearerAuth");
        w.WriteEndArray();
        w.WriteEndObject();
        w.WriteEndArray();

        WritePaths(w);
        WriteComponents(w);

        w.WriteEndObject();
    }

    private static void WritePaths(Utf8JsonWriter w)
    {
        w.WriteStartObject("paths");

        foreach (IGrouping<string, Operation> byPath in Operations
            .GroupBy(static o => o.Path, StringComparer.Ordinal)
            .OrderBy(static g => g.Key, StringComparer.Ordinal))
        {
            w.WriteStartObject(byPath.Key);

            if (byPath.Key.Contains("{companyId}", StringComparison.Ordinal))
            {
                w.WriteStartArray("parameters");
                WritePathParameter(w, "companyId", "معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم.",
                    "The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body.", "uuid");
                if (byPath.Key.Contains("{entryId}", StringComparison.Ordinal))
                {
                    WritePathParameter(w, "entryId", "معرّف القيد.", "The entry identifier.", "uuid");
                }

                w.WriteEndArray();
            }

            foreach (Operation operation in byPath.OrderBy(static o => o.Method, StringComparer.Ordinal))
            {
                WriteOperation(w, operation);
            }

            w.WriteEndObject();
        }

        w.WriteEndObject();
    }

    private static void WritePathParameter(Utf8JsonWriter w, string name, string ar, string en, string format)
    {
        w.WriteStartObject();
        w.WriteString("name", name);
        w.WriteString("in", "path");
        w.WriteBoolean("required", true);
        w.WriteString("description", ar + " / " + en);
        w.WriteStartObject("schema");
        w.WriteString("type", "string");
        w.WriteString("format", format);
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void WriteOperation(Utf8JsonWriter w, Operation operation)
    {
        w.WriteStartObject(operation.Method);

        w.WriteString("operationId", operation.OperationId);
        w.WriteString("summary", operation.SummaryAr + " / " + operation.SummaryEn);
        w.WriteString("description", operation.DescriptionAr + "\n\n" + operation.DescriptionEn);

        if (operation.Anonymous)
        {
            w.WriteStartArray("security");
            w.WriteEndArray();
        }

        if (operation.Query.Count > 0)
        {
            w.WriteStartArray("parameters");
            foreach (QueryParameter parameter in operation.Query.OrderBy(static p => p.Name, StringComparer.Ordinal))
            {
                w.WriteStartObject();
                w.WriteString("name", parameter.Name);
                w.WriteString("in", "query");
                w.WriteBoolean("required", parameter.Required);
                w.WriteString("description", parameter.DescriptionAr + " / " + parameter.DescriptionEn);
                w.WriteStartObject("schema");
                switch (parameter.Kind)
                {
                    case "period":
                        w.WriteString("type", "string");
                        w.WriteString("pattern", "^[0-9]{4}-(0[1-9]|1[0-2])$");
                        break;
                    case "year":
                        w.WriteString("type", "string");
                        w.WriteString("pattern", "^[0-9]{4}$");
                        break;
                    default:
                        w.WriteString("type", "string");
                        w.WriteNumber("maxLength", 32);
                        break;
                }

                w.WriteEndObject();
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        if (operation.Body is not null)
        {
            w.WriteStartObject("requestBody");
            w.WriteBoolean("required", true);
            w.WriteStartObject("content");
            w.WriteStartObject("application/json");
            WriteRef(w, "schema", operation.Body);
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteEndObject();
        }

        w.WriteStartObject("responses");
        WriteResponse(w, operation.Success, "استجابة ناجحة.", "Successful response.", operation.Response);

        if (operation.OperationId == "postJournalEntry")
        {
            WriteResponse(w, 200,
                "المفتاح نفسه رُحِّل من قبل: الإيصال ذاته و‏alreadyPosted = true. لا قيد ثانٍ.",
                "The same key was already posted: the same receipt with alreadyPosted = true. No second entry.",
                operation.Response);
        }

        if (!operation.Anonymous)
        {
            WriteProblemResponse(w, 401, "اعتماد مفقود أو غير مقبول.", "Missing or unacceptable credential.");
            WriteProblemResponse(w, 403, "الاعتماد لا يبلغ هذه الشركة، أو الاستحقاق يمنع هذا الوصول.", "The credential does not reach this company, or entitlement forbids this access.");
        }

        if (operation.Body is not null)
        {
            WriteProblemResponse(w, 400, "الجسم لا يطابق العقد: حقل غير معروف، أو مبلغ وصل رمزاً رقمياً، أو صيغة مرفوضة.", "The body does not match the contract: an unknown field, an amount that arrived as a number token, or a refused spelling.");
            WriteProblemResponse(w, 409, "تعارض مع حالة قائمة: فترة مقفلة، أو قيد معكوس من قبل.", "Conflict with existing state: a closed period, or an already-reversed entry.");
            WriteProblemResponse(w, 422, "الطلب مفهوم ومرفوض محاسبياً: قيد غير متوازن، أو دور لا يُحلّ إلى حساب.", "Understood and refused on accounting grounds: an unbalanced entry, or a role that resolves to no account.");
        }

        if (operation.OperationId == "readJournalEntry")
        {
            WriteProblemResponse(w, 501,
                "سطح قراءة القيد المفرد لم يهبط بعد في دفتر الأستاذ. الرمز الثابت: ledger.read.entry_surface_unavailable.",
                "The single-entry read surface has not landed in the ledger yet. Stable code: ledger.read.entry_surface_unavailable.");
        }

        WriteProblemResponse(w, 500, "عطل في الخادم. لا تفصيل داخلي يعبر — معرّف التتبّع فقط.", "Server failure. No internal detail crosses — only the trace id.");
        w.WriteEndObject();

        w.WriteEndObject();
    }

    private static void WriteResponse(Utf8JsonWriter w, int status, string ar, string en, string schema)
    {
        w.WriteStartObject(status.ToString(System.Globalization.CultureInfo.InvariantCulture));
        w.WriteString("description", ar + " / " + en);
        w.WriteStartObject("content");
        w.WriteStartObject("application/json");
        WriteRef(w, "schema", schema);
        w.WriteEndObject();
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void WriteProblemResponse(Utf8JsonWriter w, int status, string ar, string en)
    {
        w.WriteStartObject(status.ToString(System.Globalization.CultureInfo.InvariantCulture));
        w.WriteString("description", ar + " / " + en);
        w.WriteStartObject("content");
        w.WriteStartObject("application/problem+json");
        WriteRef(w, "schema", "Problem");
        w.WriteEndObject();
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void WriteRef(Utf8JsonWriter w, string property, string schema)
    {
        w.WriteStartObject(property);
        w.WriteString("$ref", "#/components/schemas/" + schema);
        w.WriteEndObject();
    }

    private static void WriteComponents(Utf8JsonWriter w)
    {
        w.WriteStartObject("components");

        w.WriteStartObject("securitySchemes");
        w.WriteStartObject("bearerAuth");
        w.WriteString("type", "http");
        w.WriteString("scheme", "bearer");
        w.WriteString("description",
            "الهوية — المستأجر والمستخدم والشركات المسموح بلوغها — تُشتق من الاعتماد وحده. "
            + "لا ترويسة مستأجر يكتبها العميل، ولا حقل مستأجر في أي جسم. / "
            + "Identity — tenant, user, and reachable companies — is derived from the credential alone. "
            + "No client-written tenant header, and no tenant field in any body.");
        w.WriteEndObject();
        w.WriteEndObject();

        w.WriteStartObject("schemas");
        foreach ((string name, Action<Utf8JsonWriter> write) in Schemas().OrderBy(static s => s.Name, StringComparer.Ordinal))
        {
            w.WriteStartObject(name);
            write(w);
            w.WriteEndObject();
        }

        w.WriteEndObject();

        w.WriteEndObject();
    }

    private static IEnumerable<(string Name, Action<Utf8JsonWriter> Write)> Schemas()
    {
        yield return ("Money", static w =>
        {
            w.WriteString("type", "string");
            w.WriteString("pattern", @"^-?(0|[1-9][0-9]*)(\.[0-9]{1,4})?$");
            w.WriteString("description", MoneyDescription);
            w.WriteStartArray("examples");
            w.WriteStringValue("0.4013");
            w.WriteStringValue("-1250.0000");
            w.WriteStringValue("100");
            w.WriteEndArray();
        });

        yield return ("ExchangeRate", static w =>
        {
            w.WriteString("type", "string");
            w.WriteString("pattern", @"^-?(0|[1-9][0-9]*)(\.[0-9]{1,8})?$");
            w.WriteString("description",
                "سعر صرف نصّاً بمقياس لا يتجاوز ثمانياً، بالقواعد نفسها التي تحكم المبالغ. / "
                + "An exchange rate as a string with at most eight decimal places, under the same rules as amounts.");
        });

        yield return ("Int64String", static w =>
        {
            w.WriteString("type", "string");
            w.WriteString("pattern", "^-?(0|[1-9][0-9]*)$");
            w.WriteString("description",
                "عدد صحيح 64 بت نصّاً: Number في JavaScript يفقد الدقّة فوق 2^53، ورقم القيد معرّف لا كمّية. / "
                + "A 64-bit integer as a string: JavaScript Number loses precision above 2^53, and an entry number is an identifier, not a quantity.");
        });

        yield return ("LocalizedText", static w =>
        {
            w.WriteString("type", "object");
            w.WriteString("description", "نصّ ثنائي اللغة. الطرفان إلزاميان — العربية أساسية لا ترجمة ثانية. / Bilingual text; both sides are mandatory.");
            w.WriteStartObject("properties");
            WriteStringProperty(w, "ar", "النصّ العربي.", "The Arabic text.", 512);
            WriteStringProperty(w, "en", "النصّ الإنجليزي.", "The English text.", 512);
            w.WriteEndObject();
            WriteRequired(w, "ar", "en");
            w.WriteBoolean("additionalProperties", false);
        });

        yield return ("SourceDocument", static w =>
        {
            w.WriteString("type", "object");
            w.WriteStartObject("properties");
            WriteEnumProperty(w, "module", "الوحدة المالكة للمستند.", "The module that owns the document.", Enum.GetNames<BabelModule>());
            WriteStringProperty(w, "documentType", "نوع المستند داخل تلك الوحدة.", "The document type within that module.", 64);
            WriteStringProperty(w, "documentId", "معرّف المستند داخل تلك الوحدة.", "The document identifier within that module.", 128);
            w.WriteEndObject();
            WriteRequired(w, "documentId", "documentType", "module");
            w.WriteBoolean("additionalProperties", false);
        });

        yield return ("Scope", static w =>
        {
            w.WriteString("type", "object");
            w.WriteStartObject("properties");
            WriteNullableStringProperty(w, "branchId", "الفرع.", "The branch.", 64);
            WriteNullableStringProperty(w, "costCenterId", "مركز التكلفة.", "The cost centre.", 64);
            WriteNullableStringProperty(w, "projectId", "المشروع.", "The project.", 64);
            w.WriteEndObject();
            w.WriteBoolean("additionalProperties", false);
        });

        yield return ("Subledger", static w =>
        {
            w.WriteString("type", "object");
            w.WriteStartObject("properties");
            WriteEnumProperty(w, "kind", "نوع الدفتر المساعد.", "The subledger kind.", Enum.GetNames<SubledgerKind>());
            WriteStringProperty(w, "partyId", "معرّف الطرف داخل الوحدة المالكة له.", "The party identifier within its owning module.", 128);
            w.WriteEndObject();
            WriteRequired(w, "kind", "partyId");
            w.WriteBoolean("additionalProperties", false);
        });

        yield return ("NameValue", static w =>
        {
            w.WriteString("type", "object");
            w.WriteStartObject("properties");
            WriteStringProperty(w, "name", "الاسم.", "The name.", 128);
            WriteStringProperty(w, "value", "القيمة.", "The value.", 256);
            w.WriteEndObject();
            WriteRequired(w, "name", "value");
            w.WriteBoolean("additionalProperties", false);
        });

        yield return ("NamedAmount", static w =>
        {
            w.WriteString("type", "object");
            w.WriteStartObject("properties");
            WriteStringProperty(w, "name", "اسم المبلغ كما تعرّفه مصفوفة الترحيل.", "The amount name as the posting matrix defines it.", 64);
            WriteRefProperty(w, "value", "Money");
            w.WriteEndObject();
            WriteRequired(w, "name", "value");
            w.WriteBoolean("additionalProperties", false);
        });

        yield return ("PostingLine", static w =>
        {
            w.WriteString("type", "object");
            w.WriteString("description",
                "سطر ترحيل. ولاحظ ما ليس فيه: لا حساب ولا رقم حساب. السطر يحمل دوراً، والدور يُحلّ إلى حساب "
                + "داخل الدفتر عبر خريطة هذه الشركة — فتعديل دليل الحسابات صفٌّ في جدول، لا نشرُ إصدار. / "
                + "A posting line. Note what is absent: no account, no account code. A line carries a role; the ledger "
                + "resolves the role to an account through this company's map, so changing the chart of accounts is a table row, not a release.");
            w.WriteStartObject("properties");
            WriteRefProperty(w, "amount", "Money");
            WriteArrayRefProperty(w, "dimensions", "NameValue", "أبعاد هذا السطر فوق أبعاد الطلب.", "Dimensions for this line on top of the request dimensions.");
            WriteRefProperty(w, "narration", "LocalizedText");
            WriteStringProperty(w, "qualifier", "مؤهّل الدور حين يُحلّ الدور الواحد إلى حسابات متعددة.", "The role qualifier when one role resolves to several accounts.", 64);
            WriteEnumProperty(w, "role", "دور السطر في الحدث التجاري — لا حساباً.", "The line's role in the business event — never an account.", Enum.GetNames<PostingRole>());
            WriteRefProperty(w, "scope", "Scope");
            WriteEnumProperty(w, "side", "الجانب.", "The side.", Enum.GetNames<PostingSide>());
            WriteRefProperty(w, "subledger", "Subledger");
            w.WriteEndObject();
            WriteRequired(w, "amount", "role", "side");
            w.WriteBoolean("additionalProperties", false);
        });

        yield return ("ClosedPeriodAuthorisation", static w =>
        {
            w.WriteString("type", "object");
            w.WriteString("description",
                "إذن استثنائي بالترحيل في فترة مقفلة. ليس علماً منطقياً بل إذن موثَّق: من أذن وبأي صلاحية ولأي سبب. "
                + "والفترة المقفلة نهائياً لا يفتحها هذا الإذن ولا غيره. / "
                + "A documented exceptional permission to post into a closed period — who authorised it, under which permission, and why. "
                + "A permanently closed period is opened by no permission.");
            w.WriteStartObject("properties");
            WriteStringProperty(w, "authorisedBy", "معرّف المُصرِّح — مستخدم حقيقي، لا فاعل نظام.", "The authoriser — a real user, never a system actor.", 36);
            WriteStringProperty(w, "permissionCode", "رمز الصلاحية الاستثنائية.", "The exceptional permission code.", 64);
            WriteRefProperty(w, "reason", "LocalizedText");
            w.WriteEndObject();
            WriteRequired(w, "authorisedBy", "permissionCode", "reason");
            w.WriteBoolean("additionalProperties", false);
        });

        yield return ("PostJournalEntryRequest", static w =>
        {
            w.WriteString("type", "object");
            w.WriteString("description",
                "طلب ترحيل. ولاحظ ما ليس فيه: لا حقل مستأجر ولا حقل شركة — النطاق من الاعتماد ومن المسار. "
                + "وأي حقل غير معروف يُرفض الطلب كلّه بسببه. / "
                + "A posting request. Note what is absent: no tenant field and no company field — scope comes from the credential and the path. "
                + "Any unknown field fails the whole request.");
            w.WriteStartObject("properties");
            WriteArrayRefProperty(w, "amounts", "NamedAmount", "مفردات المبالغ التي يقرؤها قالب الحدث.", "The amount vocabulary the event template reads.");
            WriteStringProperty(w, "book", "الدفتر داخل الشركة. الافتراضي MAIN.", "The book within the company. Default MAIN.", 32);
            WriteRefProperty(w, "closedPeriodAuthorisation", "ClosedPeriodAuthorisation");
            WriteCurrencyProperty(w);
            WriteDateProperty(w, "documentDate", "تاريخ المستند الميلادي. الفترة المالية تُشتق منه داخل الدفتر.", "The Gregorian document date; the ledger derives the fiscal period from it.");
            WriteArrayRefProperty(w, "dimensions", "NameValue", "الأبعاد التحليلية على مستوى الطلب.", "Analytical dimensions at request level.");
            WriteStringProperty(
                w,
                "event",
                "رمز الحدث في مصفوفة الترحيل بصيغة <وحدة>.<كيان>.<فعل>. **إلزامي على المسارين معاً**: "
                + "الرمز يعطي القيد هويّته، والسطور — إن وُجدت — تعطيه محتواه. ورمزٌ غائب أو فارغ يجعل حدثين "
                + "مختلفين من المستند نفسه عند الإطلاق نفسه هويةً واحدة، فيُبتلع الثاني بصمت بلا خطأ ولا اختلال توازن. "
                + "والقيد اليدوي ليس استثناءً: له حدثه المعرَّف في المصفوفة.",
                "The posting-matrix event code, shaped <module>.<entity>.<action>. **Mandatory on both paths**: "
                + "the code gives the entry its identity, and the lines — where present — give it its content. "
                + "A missing or blank code collapses two different events of the same document at the same trigger into one "
                + "identity, and the second is swallowed silently, with no error and no imbalance. "
                + "A manual voucher is no exception: it has its own defined event in the matrix.",
                128);
            WriteRefProperty(w, "exchangeRate", "ExchangeRate");
            WriteArrayRefProperty(w, "facts", "NameValue", "وقائع السياق التي تُقيَّم عليها الشروط وقواعد الحجب.", "Context facts against which conditions and guard rules are evaluated.");
            WriteIntegerProperty(w, "generation", 1, 1000, "جيل الترحيل. يبدأ من 1 ولا يزيد إلا بعد عكس مشروع.", "The posting generation. Starts at 1 and increases only after a legitimate reversal.");
            WriteStringProperty(w, "idempotencyKey", "مفتاح الحصانة ضد التكرار، محارف [0-9A-Za-z-_:.] فقط. مستقلّ عن الترتيب.", "The idempotency key, characters [0-9A-Za-z-_:.] only. Order-independent.", 128);
            WriteArrayRefProperty(
                w,
                "lines",
                "PostingLine",
                "سطور الطلب — تُرسَل في المسار الصريح (قيد يدوي) وتُترك فارغة في مسار القالب. "
                + "وهي وحدها ما يختار المسار؛ و‏event إلزامي في الحالتين.",
                "The request lines: sent on the explicit path (a manual voucher) and left empty on the template path. "
                + "They alone select the path; event is mandatory either way.");
            WriteRefProperty(w, "narration", "LocalizedText");
            WriteRefProperty(w, "source", "SourceDocument");
            WriteEnumProperty(w, "trigger", "الحدث الذي أطلق الترحيل.", "What triggered the posting.", Enum.GetNames<PostingTrigger>());
            w.WriteEndObject();
            WriteRequired(w, "documentDate", "event", "idempotencyKey", "narration", "source", "trigger");
            w.WriteBoolean("additionalProperties", false);
        });

        yield return ("ReverseJournalEntryRequest", static w =>
        {
            w.WriteString("type", "object");
            w.WriteStartObject("properties");
            WriteRefProperty(w, "closedPeriodAuthorisation", "ClosedPeriodAuthorisation");
            WriteRefProperty(w, "reason", "LocalizedText");
            WriteDateProperty(w, "reversalDate", "تاريخ قيد العكس، أو غيابه فيُتخذ تاريخ القيد الأصلي.", "The reversing entry's date; omit to take the original entry's date.");
            w.WriteEndObject();
            WriteRequired(w, "reason");
            w.WriteBoolean("additionalProperties", false);
        });

        yield return ("PostingReceipt", static w =>
        {
            w.WriteString("type", "object");
            w.WriteStartObject("properties");
            WriteBooleanProperty(w, "alreadyPosted", "هل كان مفتاح الحصانة مُرحَّلاً من قبل؟ الوصول الثاني لا يفعل شيئاً ولا يُعدّ خطأ.", "Was the idempotency key already posted? A second arrival does nothing and is not an error.");
            WriteRefProperty(w, "chainSequence", "Int64String");
            WriteStringProperty(w, "entryHash", "بصمة القيد في السلسلة، hex صغير.", "The entry hash in the chain, lower-case hex.", 128);
            WriteStringProperty(w, "entryId", "معرّف القيد.", "The entry identifier.", 36);
            WriteRefProperty(w, "entryNumber", "Int64String");
            WriteIntegerProperty(w, "generation", 1, 1000, "جيل الترحيل.", "The posting generation.");
            WriteIntegerProperty(w, "lineCount", 0, 1000, "عدد السطور الناتجة بعد تقييم الشروط.", "The number of resulting lines after conditions were evaluated.");
            WritePeriodProperty(w, "periodCode");
            w.WriteEndObject();
            WriteRequired(w, "alreadyPosted", "chainSequence", "entryHash", "entryId", "entryNumber", "generation", "lineCount", "periodCode");
            w.WriteBoolean("additionalProperties", false);
        });

        yield return ("TrialBalanceRow", static w =>
        {
            w.WriteString("type", "object");
            w.WriteStartObject("properties");
            WriteStringProperty(w, "accountCode", "رمز الحساب كما هو في دليل حسابات هذه الشركة.", "The account code as it stands in this company's chart of accounts.", 32);
            WriteRefProperty(w, "credit", "Money");
            WriteRefProperty(w, "debit", "Money");
            WriteStringProperty(w, "nameAr", "الاسم العربي.", "The Arabic name.", 256);
            WriteStringProperty(w, "nameEn", "الاسم الإنجليزي.", "The English name.", 256);
            w.WriteEndObject();
            WriteRequired(w, "accountCode", "credit", "debit", "nameAr", "nameEn");
            w.WriteBoolean("additionalProperties", false);
        });

        yield return ("TrialBalance", static w =>
        {
            w.WriteString("type", "object");
            w.WriteString("description",
                "ميزان المراجعة بلا مجموع: جمع عمود مالي حساب على المال ولا يقع في طبقة HTTP، وجمعُه في المتصفّح "
                + "يعيد الفخّ نفسه إلى العميل لأن Number فاصلة عائمة ثنائية. الموضع الصحيح للمجموع sum() على numeric. / "
                + "The trial balance without totals: summing a monetary column is money arithmetic and does not belong in the HTTP layer, "
                + "and summing it in the browser reproduces the same trap because Number is a binary float. Totals belong in sum() over numeric.");
            w.WriteStartObject("properties");
            WriteStringProperty(w, "book", "الدفتر.", "The book.", 32);
            WriteNullablePeriodProperty(w, "periodCode");
            WriteIntegerProperty(w, "rowCount", 0, 1000000, "عدد الصفوف.", "The number of rows.");
            WriteArrayRefProperty(w, "rows", "TrialBalanceRow", "الصفوف مرتَّبة برمز الحساب.", "The rows ordered by account code.");
            w.WriteEndObject();
            WriteRequired(w, "book", "periodCode", "rowCount", "rows");
            w.WriteBoolean("additionalProperties", false);
        });

        yield return ("ChainVerification", static w =>
        {
            w.WriteString("type", "object");
            w.WriteString("description",
                "حكم إعادة التحقق. ولماذا «أول تسلسل منحرف» لا «هل السلسلة سليمة»: المدقّق يسأل أين ومتى وما الذي "
                + "بعده يجب أن يُراجَع؛ وإجابة منطقية واحدة لا تصلح تقريراً. / "
                + "The re-verification verdict. Why the first divergent sequence rather than a boolean: an auditor asks where, when, "
                + "and what after it must be reviewed — a single boolean is not a report.");
            w.WriteStartObject("properties");
            WriteIntegerProperty(w, "checked", 0, 100000000, "عدد السجلات المفحوصة، بما فيها السجل المنحرف.", "The number of records checked, including the divergent one.");
            WriteNullableStringProperty(w, "detail", "تفاصيل فنّية: البصمات المتوقّعة والمخزَّنة.", "Technical detail: the expected and stored hashes.", 4096);
            w.WriteStartObject("firstDivergentSequence");
            w.WriteStartArray("oneOf");
            w.WriteStartObject();
            w.WriteString("$ref", "#/components/schemas/Int64String");
            w.WriteEndObject();
            w.WriteStartObject();
            w.WriteString("type", "null");
            w.WriteEndObject();
            w.WriteEndArray();
            w.WriteString("description", "أول رقم تسلسل منحرف، أو null. / The first divergent sequence number, or null.");
            w.WriteEndObject();
            WriteBooleanProperty(w, "ok", "هل النطاق سليم كاملاً؟", "Is the whole scope intact?");
            WriteStringProperty(w, "reasonAr", "شرح عربي صالح للعرض في تقرير تدقيق.", "An Arabic explanation fit for an audit report.", 2048);
            WriteStringProperty(w, "verdict", "رمز الحكم الثابت.", "The stable verdict code.", 64);
            w.WriteEndObject();
            WriteRequired(w, "checked", "detail", "firstDivergentSequence", "ok", "reasonAr", "verdict");
            w.WriteBoolean("additionalProperties", false);
        });

        yield return ("JournalLine", static w =>
        {
            w.WriteString("type", "object");
            w.WriteStartObject("properties");
            WriteRefProperty(w, "credit", "Money");
            WriteCurrencyProperty(w);
            WriteRefProperty(w, "debit", "Money");
            WriteStringProperty(w, "descriptionAr", "بيان السطر بالعربية.", "The line narration in Arabic.", 512);
            WriteStringProperty(w, "descriptionEn", "بيان السطر بالإنجليزية.", "The line narration in English.", 512);
            WriteIntegerProperty(w, "lineNo", 1, 1000, "رقم السطر.", "The line number.");
            WriteStringProperty(w, "qualifier", "مؤهّل الدور.", "The role qualifier.", 64);
            WriteStringProperty(w, "role", "رمز الدور كما خُزِّن.", "The role code as stored.", 64);
            w.WriteEndObject();
            WriteRequired(w, "credit", "currency", "debit", "descriptionAr", "descriptionEn", "lineNo", "qualifier", "role");
            w.WriteBoolean("additionalProperties", false);
        });

        yield return ("JournalEntry", static w =>
        {
            w.WriteString("type", "object");
            w.WriteStartObject("properties");
            WriteStringProperty(w, "book", "الدفتر.", "The book.", 32);
            WriteRefProperty(w, "chainSequence", "Int64String");
            WriteCurrencyProperty(w);
            WriteDateProperty(w, "entryDate", "تاريخ القيد الميلادي.", "The Gregorian entry date.");
            WriteStringProperty(w, "entryHash", "بصمة القيد.", "The entry hash.", 128);
            WriteStringProperty(w, "entryId", "معرّف القيد.", "The entry identifier.", 36);
            WriteRefProperty(w, "entryNumber", "Int64String");
            WriteArrayRefProperty(w, "lines", "JournalLine", "سطور القيد.", "The entry's lines.");
            WriteStringProperty(w, "memoAr", "البيان بالعربية.", "The memo in Arabic.", 512);
            WriteStringProperty(w, "memoEn", "البيان بالإنجليزية.", "The memo in English.", 512);
            WritePeriodProperty(w, "periodCode");
            WriteNullableStringProperty(w, "reversesEntryId", "القيد الذي يعكسه هذا القيد، إن كان قيد عكس.", "The entry this one reverses, when it is a reversal.", 36);
            WriteStringProperty(w, "status", "حالة القيد.", "The entry status.", 32);
            w.WriteEndObject();
            WriteRequired(w, "book", "chainSequence", "currency", "entryDate", "entryHash", "entryId", "entryNumber", "lines", "memoAr", "memoEn", "periodCode", "reversesEntryId", "status");
            w.WriteBoolean("additionalProperties", false);
        });

        yield return ("HealthResponse", static w =>
        {
            w.WriteString("type", "object");
            w.WriteStartObject("properties");
            WriteStringProperty(w, "apiVersion", "إصدار السطح.", "The surface version.", 8);
            WriteStringProperty(w, "calendar", "التقويم الافتراضي لتلك الثقافة. GregorianCalendar هو المتوقّع؛ UmAlQuraCalendar يعني أن أي تنسيق تاريخ ضمني على هذا الخادم يكتب هجرياً.", "The default calendar of that culture. GregorianCalendar is expected; UmAlQuraCalendar means any implicit date formatting on this server writes Hijri.", 64);
            WriteStringProperty(w, "culture", "ثقافة العملية الفعلية.", "The actual process culture.", 32);
            WriteStringProperty(w, "status", "الحالة.", "The status.", 16);
            w.WriteEndObject();
            WriteRequired(w, "apiVersion", "calendar", "culture", "status");
            w.WriteBoolean("additionalProperties", false);
        });

        yield return ("ApiError", static w =>
        {
            w.WriteString("type", "object");
            w.WriteStartObject("properties");
            WriteStringProperty(w, "code", "الرمز الثابت — نقطة الاعتماد البرمجية الوحيدة. لا يُقرأ نصّ رسالة لاتخاذ قرار أبداً.", "The stable code — the only thing to program against. Message text is never parsed to make a decision.", 128);
            WriteNullableStringProperty(w, "field", "الحقل المعنيّ على السلك.", "The wire field concerned.", 128);
            WriteStringProperty(w, "messageAr", "الرسالة العربية.", "The Arabic message.", 2048);
            WriteStringProperty(w, "messageEn", "الرسالة الإنجليزية.", "The English message.", 2048);
            w.WriteEndObject();
            WriteRequired(w, "code", "field", "messageAr", "messageEn");
            w.WriteBoolean("additionalProperties", false);
        });

        yield return ("Problem", static w =>
        {
            w.WriteString("type", "object");
            w.WriteString("description",
                "تفاصيل المشكلة بصيغة RFC 9457 بامتدادين: رمز ثابت، ورسالة عربية إلى جانب الإنجليزية. "
                + "ولا يعبر منها أبداً: نصّ خطأ قاعدة بيانات، أو أثر مكدّس، أو شذرة SQL. / "
                + "RFC 9457 problem details with two extensions: a stable code and an Arabic message alongside the English one. "
                + "Never crossing: database error text, a stack trace, or a SQL fragment.");
            w.WriteStartObject("properties");
            WriteStringProperty(w, "code", "رمز أول خطأ — نقطة الاعتماد البرمجية.", "The first error's code — the programmatic contract.", 128);
            WriteStringProperty(w, "detail", "شرح بالإنجليزية.", "The English explanation.", 2048);
            WriteStringProperty(w, "detailAr", "شرح بالعربية.", "The Arabic explanation.", 2048);
            WriteArrayRefProperty(w, "errors", "ApiError", "كل الأخطاء لا أوّلها فقط: قيد يخالف ثلاث قواعد يُرجعها الثلاث في نداء واحد.", "Every error, not just the first: an entry that breaks three rules returns all three in one call.");
            WriteStringProperty(w, "instance", "مسار الطلب.", "The request path.", 512);
            WriteIntegerProperty(w, "status", 100, 599, "رمز حالة HTTP.", "The HTTP status code.");
            WriteStringProperty(w, "title", "عنوان قصير بالإنجليزية.", "A short English title.", 256);
            WriteStringProperty(w, "titleAr", "عنوان قصير بالعربية.", "A short Arabic title.", 256);
            WriteStringProperty(w, "traceId", "معرّف التتبّع — الرابط الوحيد مع سجلّ الخادم.", "The trace id — the only link to the server log.", 64);
            WriteStringProperty(w, "type", "المرجع الذي يُعرّف نوع المشكلة.", "The reference that identifies the problem type.", 512);
            w.WriteEndObject();
            WriteRequired(w, "code", "detail", "detailAr", "errors", "instance", "status", "title", "titleAr", "traceId", "type");
            w.WriteBoolean("additionalProperties", false);
        });
    }

    private static void WriteStringProperty(Utf8JsonWriter w, string name, string ar, string en, int maxLength)
    {
        w.WriteStartObject(name);
        w.WriteString("type", "string");
        w.WriteNumber("maxLength", maxLength);
        w.WriteString("description", ar + " / " + en);
        w.WriteEndObject();
    }

    private static void WriteNullableStringProperty(Utf8JsonWriter w, string name, string ar, string en, int maxLength)
    {
        w.WriteStartObject(name);
        w.WriteStartArray("type");
        w.WriteStringValue("string");
        w.WriteStringValue("null");
        w.WriteEndArray();
        w.WriteNumber("maxLength", maxLength);
        w.WriteString("description", ar + " / " + en);
        w.WriteEndObject();
    }

    private static void WriteBooleanProperty(Utf8JsonWriter w, string name, string ar, string en)
    {
        w.WriteStartObject(name);
        w.WriteString("type", "boolean");
        w.WriteString("description", ar + " / " + en);
        w.WriteEndObject();
    }

    private static void WriteIntegerProperty(Utf8JsonWriter w, string name, int minimum, int maximum, string ar, string en)
    {
        w.WriteStartObject(name);
        w.WriteString("type", "integer");
        w.WriteNumber("minimum", minimum);
        w.WriteNumber("maximum", maximum);
        w.WriteString("description", ar + " / " + en);
        w.WriteEndObject();
    }

    private static void WriteEnumProperty(Utf8JsonWriter w, string name, string ar, string en, IReadOnlyList<string> members)
    {
        w.WriteStartObject(name);
        w.WriteString("type", "string");
        w.WriteStartArray("enum");
        foreach (string member in members)
        {
            w.WriteStringValue(member);
        }

        w.WriteEndArray();
        w.WriteString("description",
            ar + " يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / "
            + en + " Matched literally and case-sensitively; a number is never accepted in place of a name.");
        w.WriteEndObject();
    }

    private static void WriteRefProperty(Utf8JsonWriter w, string name, string schema)
    {
        w.WriteStartObject(name);
        w.WriteString("$ref", "#/components/schemas/" + schema);
        w.WriteEndObject();
    }

    private static void WriteArrayRefProperty(Utf8JsonWriter w, string name, string schema, string ar, string en)
    {
        w.WriteStartObject(name);
        w.WriteString("type", "array");
        WriteRef(w, "items", schema);
        w.WriteString("description", ar + " / " + en);
        w.WriteEndObject();
    }

    private static void WriteCurrencyProperty(Utf8JsonWriter w)
    {
        w.WriteStartObject("currency");
        w.WriteString("type", "string");
        w.WriteString("pattern", "^[A-Z]{3}$");
        w.WriteString("description",
            "رمز عملة ISO 4217 بثلاثة محارف لاتينية كبيرة. واللاتينية هنا شرط سلامة سلسلة التجزئة لا تفضيل عرض. / "
            + "An ISO 4217 currency code, three upper-case ASCII letters. ASCII here is a hash-chain safety requirement, not a display preference.");
        w.WriteEndObject();
    }

    private static void WriteDateProperty(Utf8JsonWriter w, string name, string ar, string en)
    {
        w.WriteStartObject(name);
        w.WriteString("type", "string");
        w.WriteString("pattern", "^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$");
        w.WriteString("description",
            ar + " ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / "
            + en + " Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period.");
        w.WriteEndObject();
    }

    private static void WritePeriodProperty(Utf8JsonWriter w, string name)
    {
        w.WriteStartObject(name);
        w.WriteString("type", "string");
        w.WriteString("pattern", "^[0-9]{4}-(0[1-9]|1[0-2])$");
        w.WriteString("description",
            "رمز الفترة المالية yyyy-MM ميلادياً دائماً — لا يتغيّر بثقافة الخادم ولا بثقافة العميل. / "
            + "The fiscal period code yyyy-MM, always Gregorian — unaffected by server or client culture.");
        w.WriteEndObject();
    }

    private static void WriteNullablePeriodProperty(Utf8JsonWriter w, string name)
    {
        w.WriteStartObject(name);
        w.WriteStartArray("type");
        w.WriteStringValue("string");
        w.WriteStringValue("null");
        w.WriteEndArray();
        w.WriteString("pattern", "^[0-9]{4}-(0[1-9]|1[0-2])$");
        w.WriteString("description",
            "رمز الفترة المالية yyyy-MM ميلادياً، أو null حين يشمل الطلب كل الفترات. / "
            + "The fiscal period code yyyy-MM, or null when the request spans all periods.");
        w.WriteEndObject();
    }

    private static void WriteRequired(Utf8JsonWriter w, params string[] names)
    {
        w.WriteStartArray("required");
        foreach (string name in names.OrderBy(static n => n, StringComparer.Ordinal))
        {
            w.WriteStringValue(name);
        }

        w.WriteEndArray();
    }

    private const string MoneyDescription =
        "مبلغ نصّاً، بمقياس لا يتجاوز أربع خانات عشرية. النحو المقبول كاملاً: -?(0|[1-9][0-9]*)(\\.[0-9]{1,4})? — "
        + "فتُرفض الصيغة الأسّية، والصفر البادئ، والإشارة الموجبة الصريحة، والفراغ، والأرقام العربية-الهندية والديفاناغارية، "
        + "وكل ما زاد على أربع خانات. ورمزٌ رقمي في هذا الحقل يُرفض الطلب بسببه: JSON لا يملك نوعاً عشرياً، "
        + "وأغلب العملاء يمرّرون الرمز الرقمي على فاصلة عائمة ثنائية فيقع فقدان الدقّة قبل أن يصل الطلب. / "
        + "An amount as a string with at most four decimal places. The full accepted grammar is -?(0|[1-9][0-9]*)(\\.[0-9]{1,4})? — "
        + "exponent notation, leading zeros, an explicit plus sign, whitespace, Arabic-Indic and Devanagari digits, and any fifth "
        + "decimal are all refused. A JSON number token in this field fails the request: JSON has no decimal type, and most clients "
        + "route a number token through a binary double, so precision is lost before the request arrives.";

    private const string ContractDescription =
        "العزل التام بين الواجهات والخلفية: هذه الوثيقة هي كل ما يحتاجه فريق الواجهة، ولا يقرأ شيفرة خلفية.\n\n"
        + "سياسة الإصدار — ما يبقى في v1 وما يفرض v2:\n"
        + "• يبقى في v1: إضافة نقطة نهاية · إضافة حقل اختياري في استجابة · إضافة حقل اختياري في طلب له افتراض معلن · "
        + "إضافة عضو إلى تعداد يُقرأ من الخادم إلى العميل · إضافة رمز خطأ جديد · توسيع مدى مسموح · تحسين نصّ وصف أو رسالة.\n"
        + "• يفرض v2: حذف حقل أو نقطة نهاية · إعادة تسمية أي منهما · تضييق نوع أو مدى أو نمط · جعل حقل اختياري إلزامياً · "
        + "تغيير معنى رمز خطأ قائم أو رمز الحالة الذي يصحبه · إزالة عضو من تعداد يُرسله العميل · تغيير الافتراض المعلن لحقل.\n"
        + "• والقاعدة الحاكمة: تغييرٌ يجعل عميلاً مطابقاً للعقد القديم يعمل خطأً — لا يفشل، بل يعمل خطأً — هو v2 دائماً.\n"
        + "• ونطاق هذه السياسة: **تلزم من أول نشر للعقد فصاعداً**. غرضها حماية عميل مطابق قائم، فحيث لا عميل "
        + "لا شيء تحميه — ويبقى تعديل v1 في مكانه جائزاً ما دامت الوثيقة لم تُنشر لأي مستهلك، بشرط أن يُسجَّل "
        + "التعديل بتاريخه وسببه في سجل القرارات لا أن يمرّ صامتاً.\n"
        + "• تعديل مُسجَّل — 2026-08-24: صار الحقل event إلزامياً في PostJournalEntryRequest. وهو تضييق يفرض v2 "
        + "بنصّ السياسة، ونُفِّذ في v1 في مكانه لأن العقد لم يُنشر بعد لأي مستهلك ولا يوجد عميل مطابق واحد. "
        + "السبب: رمز الحدث جزء من هوية الترحيل، وغيابه يبتلع حدثاً محاسبياً بصمت (ADR-0016 · ADR-0018).\n\n"
        + "Total isolation between front end and back end: this document is everything a front-end team needs; it reads no back-end code.\n\n"
        + "Versioning policy — what stays in v1 and what forces v2:\n"
        + "• Stays in v1: adding an endpoint; adding an optional response field; adding an optional request field with a published default; "
        + "adding a member to a server-to-client enum; adding a new error code; widening an allowed range; improving a description or message.\n"
        + "• Forces v2: removing a field or endpoint; renaming either; narrowing a type, range, or pattern; making an optional field required; "
        + "changing the meaning of an existing error code or the status that accompanies it; removing a member from a client-to-server enum; "
        + "changing a field's published default.\n"
        + "• The governing rule: a change that makes a client conforming to the old contract behave wrongly — not fail, but behave wrongly — is always v2.\n"
        + "• Scope of this policy: **it binds from the contract's first publication onward**. Its purpose is to protect an existing "
        + "conforming client, so where there is no client there is nothing to protect — and amending v1 in place remains legitimate "
        + "while the document has not been published to any consumer, provided the amendment is recorded with its date and reason in "
        + "the decision record rather than passing silently.\n"
        + "• Recorded amendment — 2026-08-24: the event field became required on PostJournalEntryRequest. That is a narrowing which the "
        + "policy text forces to v2, and it was made in v1 in place because the contract has not yet been published to any consumer and "
        + "no conforming client exists. Reason: the event code is part of the posting identity, and its absence swallows an accounting "
        + "event silently (ADR-0016, ADR-0018).";

    private sealed record Operation(
        string Path,
        string Method,
        string OperationId,
        string SummaryAr,
        string SummaryEn,
        string DescriptionAr,
        string DescriptionEn,
        string? Body,
        string Response,
        int Success,
        bool Anonymous,
        IReadOnlyList<QueryParameter> Query);

    private sealed record QueryParameter(string Name, bool Required, string DescriptionAr, string DescriptionEn, string Kind);
}
