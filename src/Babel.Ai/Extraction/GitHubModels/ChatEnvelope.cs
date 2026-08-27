using System.Text.Json;
using Babel.SharedKernel;

namespace Babel.Ai.Extraction.GitHubModels;

/// <summary>
/// <b>قارئ غلاف الردّ.</b> يفصل ثلاثة أشياء يخلطها كثيرون فيدفعون ثمنه في عرض حيّ:
/// <b>عطلٌ في النقل</b>، و<b>امتناعُ النموذج</b>، و<b>مُخرَجٌ مشوَّه</b>. لكلٍّ رسالته،
/// ولكلٍّ ما يفعله المستخدم بعده.
/// <para>
/// ⚠ <b>شكل الغلاف مُستنتَج لا مُتحقَّق من مصدر:</b> كُتب على شكل واجهة إكمال المحادثة
/// المتوافقة مع OpenAI، ولم يُشغَّل نداءٌ واحد على GitHub Models من بيئة هذا البناء —
/// مضيف الخدمة محجوب عليها. وما يجعل هذا مقبولاً أن <b>الإنفاذ ليس هنا</b>: مهما أعاد
/// الغلاف، فالمحتوى لا يصير مسوّدةً إلا بعد <see cref="ExtractionSchema"/>. وهذا الملف
/// يقرأ الغلاف ليُسمّي العطل، لا ليثق بما فيه.
/// </para>
/// </summary>
public static class ChatEnvelope
{
    /// <summary>سبب انتهاء التوليد حين يُقطع المُخرَج عند السقف.</summary>
    public const string TruncatedReason = "length";

    /// <summary>أسباب انتهاء تدلّ على امتناع لا على عطل.</summary>
    private static readonly HashSet<string> RefusalReasons =
        new(["content_filter", "refusal", "safety"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// يستخرج نصّ المحتوى من الغلاف، أو يُسمّي سببَ عدم وجوده.
    /// </summary>
    /// <param name="body">جسم الردّ.</param>
    /// <param name="maxOutputTokens">سقف المُخرَج، لتُسمّى حالة القطع باسمها.</param>
    public static Result<string> Content(string body, int maxOutputTokens)
    {
        ArgumentNullException.ThrowIfNull(body);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException error)
        {
            return Result<string>.Failure(GitHubModelsErrors.EnvelopeUnreadable(error.Message));
        }

        using (document)
        {
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result<string>.Failure(GitHubModelsErrors.EnvelopeUnreadable("الجذر ليس كائناً"));
            }

            // خطأٌ مُبلَّغ داخل جسم ناجح الرمز — يقع فعلاً عند بوّابات وسيطة.
            if (root.TryGetProperty("error", out JsonElement error))
            {
                return Result<string>.Failure(GitHubModelsErrors.UnexpectedStatus(
                    200, ModelFaultClassifier.Excerpt(error.ToString())));
            }

            if (!root.TryGetProperty("choices", out JsonElement choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                return Result<string>.Failure(GitHubModelsErrors.NoContent);
            }

            JsonElement choice = choices[0];
            string reason = choice.TryGetProperty("finish_reason", out JsonElement finish) && finish.ValueKind == JsonValueKind.String
                ? finish.GetString()!
                : string.Empty;

            if (RefusalReasons.Contains(reason))
            {
                return Result<string>.Failure(GitHubModelsErrors.ModelRefused(reason));
            }

            if (!choice.TryGetProperty("message", out JsonElement message) || message.ValueKind != JsonValueKind.Object)
            {
                return Result<string>.Failure(GitHubModelsErrors.NoContent);
            }

            // الامتناع المُعلَن حقلاً مستقلاً: جوابٌ مفهوم من النموذج لا خلل في النظام.
            if (message.TryGetProperty("refusal", out JsonElement refusal) && refusal.ValueKind == JsonValueKind.String)
            {
                return Result<string>.Failure(GitHubModelsErrors.ModelRefused(refusal.GetString() ?? string.Empty));
            }

            if (!message.TryGetProperty("content", out JsonElement content)
                || content.ValueKind != JsonValueKind.String
                || content.GetString() is not { Length: > 0 } text)
            {
                // القطع يُفحَص هنا: مُخرَجٌ مقطوع يصل فارغاً أو ناقص القوس، ورسالة
                // «ليس JSON صالحاً» تصف العَرَض لا السبب.
                return Result<string>.Failure(string.Equals(reason, TruncatedReason, StringComparison.OrdinalIgnoreCase)
                    ? GitHubModelsErrors.OutputTruncated(maxOutputTokens)
                    : GitHubModelsErrors.NoContent);
            }

            return string.Equals(reason, TruncatedReason, StringComparison.OrdinalIgnoreCase)
                ? Result<string>.Failure(GitHubModelsErrors.OutputTruncated(maxOutputTokens))
                : Result<string>.Success(text);
        }
    }
}
