using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Babel.Ai.Suggestions;
using Babel.SharedKernel;

namespace Babel.Ai.Extraction.GitHubModels;

/// <summary>
/// <b>ما يُرسَل إلى النموذج بالضبط — ومسؤولية هذا الملف الأولى ما لا يُرسَل.</b>
/// <para>
/// لا دليل حسابات، ولا رمز حساب، ولا اسم حساب. يُرسَل: صورة المستند، والمخطط المطلوب،
/// و<b>قائمة رموز الأحداث والأدوار المغلقة</b> يختار منها. والمصفوفة — لا النموذج — هي
/// التي تحلّ الرمز إلى حساب هذا المستأجر (‏ADR-0024 بند سادساً).
/// </para>
/// <para>
/// <b>وحارسٌ على الخارج لا على الداخل وحده:</b> نصّ التوجيه يمرّ قبل الإرسال على نفس
/// كاشف رموز الحسابات الذي يمرّ عليه مُخرَج النموذج. سؤالٌ يذكر حساباً يُنتج جواباً يذكر
/// حساباً، وردُّه بعد وصوله أضعف من منعه قبل خروجه.
/// </para>
/// </summary>
public static class ExtractionPrompt
{
    /// <summary>أدنى عدد رموز يُقبل في المفردات قبل الإرسال — حارس ضدّ قائمة ضامرة.</summary>
    public const int MinimumEvents = 10;

    /// <summary>أدنى عدد أدوار.</summary>
    public const int MinimumRoles = 10;

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// التوجيه النظامي بالعربية. <b>والعربية هنا ليست ذوقاً:</b> المستندات عربية،
    /// وأسماء الموردين عربية، وتوجيهٌ إنجليزي يجعل النموذج ينقحر الاسم العربي فيصل
    /// السجلَّ اسمٌ ليس هو ما كتبه المُصدِر (‏ADR-0021).
    /// </summary>
    /// <param name="vocabulary">المفردات المغلقة.</param>
    public static Result<string> System(IPostingVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);

        if (vocabulary.EventCount < MinimumEvents || vocabulary.RoleCount < MinimumRoles)
        {
            return Result<string>.Failure(GitHubModelsErrors.VocabularyTooSmall(vocabulary.EventCount, vocabulary.RoleCount));
        }

        StringBuilder text = new();
        text.AppendLine("أنت مساعد قراءة فواتير موردين في نظام محاسبي سعودي. لغتك ولغة المستندات العربية.");
        text.AppendLine();
        text.AppendLine("اقرأ المستند وأعد كائن JSON واحداً فقط، بلا أي نصّ قبله أو بعده، مطابقاً للمخطط:");
        text.AppendLine(SchemaSketch());
        text.AppendLine();
        text.AppendLine("قواعد ملزمة:");
        text.AppendLine("١ · كل مبلغ ونسبة وكمية يُكتب نصّاً بنقطة عشرية إنجليزية وبلا فاصل آلاف: \"1500.00\".");
        text.AppendLine("٢ · التاريخ ميلادي بصيغة yyyy-MM-dd. لا تحوّل هجرياً من عندك؛ إن كان المطبوع هجرياً فاترك الحقل بأدنى ثقة.");
        text.AppendLine("٣ · لا تخترع قيمة لم تقرأها. ما لم تقرأه اتركه بثقة منخفضة، ولا تملأه بما «يبدو معقولاً».");
        text.AppendLine("٤ · لا تذكر رقم حساب ولا اسم حساب ولا دليل حسابات إطلاقاً، ولا تُضف أي حقل خارج المخطط.");
        text.AppendLine("٥ · اختر event_code من القائمة المغلقة أدناه حرفياً. إن لم يكن فيها ما يناسب فاحذف قسم suggestion كله.");
        text.AppendLine("٦ · schema_version قيمته حرفياً: " + ExtractionSchema.Version);
        text.AppendLine();
        text.AppendLine("رموز الأحداث المسموح بها (" + vocabulary.EventCount + "):");
        text.AppendLine(string.Join('\n', vocabulary.EventCodes));
        text.AppendLine();
        text.AppendLine("رموز الأدوار المسموح بها (" + vocabulary.RoleCount + "):");
        text.AppendLine(string.Join('\n', vocabulary.RoleCodes));

        string prompt = text.ToString();
        Result guard = RefuseLedgerCodes(prompt);

        return guard.IsFailure ? Result<string>.Failure(guard.Errors) : Result<string>.Success(prompt);
    }

    /// <summary>
    /// يبني جسم الطلب. <b>ويطلب مُخرَجاً مُهيكَلاً عند الحدّ البعيد أيضاً</b> —
    /// ‏<c>response_format</c> — <b>ولا يُبنى على ذلك شيء</b>: الإنفاذ الحقيقي هو
    /// <see cref="ExtractionSchema"/> عندنا. طلبٌ لطيف عند الطرف الآخر ليس ضمانة، ونموذجٌ
    /// لا يدعمه يعيد نصّاً حرّاً ويمرّ.
    /// </summary>
    /// <param name="options">الإعدادات.</param>
    /// <param name="systemPrompt">التوجيه النظامي.</param>
    /// <param name="request">طلب الاستخراج.</param>
    public static string Body(GitHubModelsOptions options, string systemPrompt, ExtractionRequest request)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(systemPrompt);
        ArgumentNullException.ThrowIfNull(request);

        using MemoryStream buffer = new();

        using (Utf8JsonWriter writer = new(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("model", options.ModelId);
            writer.WriteNumber("max_tokens", options.MaxOutputTokens);

            // حرارة صفر: نفس المستند يعطي نفس المُخرَج قدر ما يسمح النموذج. وهي أقرب ما
            // يمكن بلوغه من الحتمية عند حدٍّ احتمالي، ولا تُعلَن حتمية في القدرات.
            writer.WriteNumber("temperature", 0);

            writer.WriteStartObject("response_format");
            writer.WriteString("type", "json_object");
            writer.WriteEndObject();

            writer.WriteStartArray("messages");

            writer.WriteStartObject();
            writer.WriteString("role", "system");
            writer.WriteString("content", systemPrompt);
            writer.WriteEndObject();

            writer.WriteStartObject();
            writer.WriteString("role", "user");
            writer.WriteStartArray("content");

            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteString("text", "اقرأ هذه الفاتورة وأعد الكائن المطلوب.");
            writer.WriteEndObject();

            writer.WriteStartObject();
            writer.WriteString("type", "image_url");
            writer.WriteStartObject("image_url");
            writer.WriteString("url", DataUri(request));
            writer.WriteEndObject();
            writer.WriteEndObject();

            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// <b>يرفض أي نصّ خارج يسمّي حساباً</b>، بفحصين لا ثالث لهما:
    /// <list type="number">
    ///   <item><b>رمز حقل يسمّي حساباً</b> — بنفس مجموعة الأسماء التي يرفضها مخطط
    ///         الاستخراج على المُخرَج. مجموعةٌ واحدة تحرس الاتجاهين.</item>
    ///   <item><b>سلسلة أرقام بطول رمز حساب</b> — أربع خانات فأكثر. ودليلُ الحسابات إن
    ///         تسرّب يتسرّب أرقاماً لا أسماءً.</item>
    /// </list>
    /// <para>
    /// <b>والمطابقة على الرمز كاملاً لا على جزء منه</b>، وذلك درسٌ ظهر أول تشغيل:
    /// المصفوفة نفسها تحمل دوراً اسمه <c>settlement_account</c>، ومطابقةٌ على جزء الكلمة
    /// كانت ترفض <b>كل</b> توجيه صحيح — أي حارس أحمر دائماً، وهو أسوأ من غياب الحارس.
    /// </para>
    /// </summary>
    /// <param name="prompt">النصّ.</param>
    public static Result RefuseLedgerCodes(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        HashSet<string> tokens = new(StringComparer.OrdinalIgnoreCase);
        int run = 0;

        foreach (string token in Tokenise(prompt))
        {
            tokens.Add(token);
        }

        foreach (string name in SuggestionGuard.LedgerCodeFieldNames.Order(StringComparer.Ordinal))
        {
            if (tokens.Contains(name))
            {
                return Result.Failure(GitHubModelsErrors.PromptNamesLedgerCode(name));
            }
        }

        foreach (char character in prompt)
        {
            run = ArabicNumeralsLike(character) ? run + 1 : 0;

            if (run >= LedgerCodeDigits)
            {
                return Result.Failure(GitHubModelsErrors.PromptNamesLedgerCode(
                    "سلسلة من " + LedgerCodeDigits + " أرقام فأكثر"));
            }
        }

        return Result.Success();
    }

    /// <summary>طول سلسلة الأرقام التي تُعامَل رمزَ حساب. رموز المصفوفة لا تحمل أرقاماً أصلاً.</summary>
    public const int LedgerCodeDigits = 4;

    private static bool ArabicNumeralsLike(char character) =>
        character is >= '0' and <= '9' or >= '\u0660' and <= '\u0669' or >= '\u06F0' and <= '\u06F9';

    private static IEnumerable<string> Tokenise(string text)
    {
        int start = -1;

        for (int index = 0; index <= text.Length; index++)
        {
            bool word = index < text.Length && (char.IsAsciiLetterOrDigit(text[index]) || text[index] == '_');

            if (word && start < 0)
            {
                start = index;
            }
            else if (!word && start >= 0)
            {
                yield return text[start..index];
                start = -1;
            }
        }
    }

    private static string DataUri(ExtractionRequest request) =>
        "data:" + request.MediaType + ";base64," + Convert.ToBase64String(request.Content.Span);

    private static string SchemaSketch() =>
        """
        {"schema_version":"…","document":{"seller_name":{"value":"…","confidence":0.0},
        "seller_vat_number":{…},"invoice_number":{…},"issued_on":{…},"currency":{…},
        "net":{…},"tax_rate":{…},"tax_total":{…},"gross_total":{…}},
        "lines":[{"description":{…},"quantity":{…},"unit_price":{…},"net":{…}}],
        "suggestion":{"event_code":"…","role_code":"…","confidence":0.0,"rationale":"…"}}
        """;
}
