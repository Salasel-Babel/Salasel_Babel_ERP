using System.Text.RegularExpressions;
using Babel.Ai.Capture;
using Babel.SharedKernel;

namespace Babel.Ai.Suggestions;

/// <summary>
/// حارس الاقتراح: <b>حدثٌ ودور من مفردات مغلقة، ولا رمز حساب بحال</b>.
/// <para>
/// ثلاثة أبواب يدخل منها رمز الحساب، وكلها مغلقة هنا:
/// <list type="number">
///   <item><b>حقل في مُخرَج المزوّد يسمّي حساباً</b> — يرفضه مخطط الاستخراج برمز مستقلّ.</item>
///   <item><b>رمز حدث مقطعُه رقم</b> (‏<c>purchasing.1210</c>) — يرفضه هذا الحارس.</item>
///   <item><b>رمز حدث مخترَع</b> ليس في المصفوفة — يرفضه هذا الحارس، وهو الباب الذي
///         قيس وهو يُنتج ترحيلاً مكرَّراً صامتاً.</item>
/// </list>
/// </para>
/// </summary>
public static partial class SuggestionGuard
{
    /// <summary>أسماء حقول تسمّي حساباً. وجود أحدها في مُخرَج المزوّد رفضٌ بذاته.</summary>
    public static IReadOnlySet<string> LedgerCodeFieldNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "account", "account_code", "account_id", "account_no", "account_number",
        "gl_account", "gl_code", "coa", "coa_code", "debit_account", "credit_account",
        "chart_of_accounts", "ledger_account",
    };

    /// <summary>شكل رمز الحدث: مقاطع لاتينية صغيرة تفصلها نقاط، ومقطعان على الأقل.</summary>
    [GeneratedRegex(@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeShape();

    /// <summary>مقطع رقمي صرف داخل رمز — أوضح شكل يتسلّل به رمز حساب.</summary>
    [GeneratedRegex(@"(^|\.)[0-9]+($|\.)", RegexOptions.CultureInvariant)]
    private static partial Regex NumericSegment();

    /// <summary>
    /// يتحقق من الاقتراح. يعيد كل الأخطاء لا أوّلها: مُخرَجٌ مشوَّه يُصحَّح مرّة واحدة.
    /// </summary>
    /// <param name="suggestion">الاقتراح.</param>
    /// <param name="vocabulary">المفردات المغلقة.</param>
    public static Result Validate(PostingSuggestion suggestion, IPostingVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(suggestion);
        ArgumentNullException.ThrowIfNull(vocabulary);

        List<Error> errors = [];

        if (string.IsNullOrWhiteSpace(suggestion.EventCode))
        {
            errors.Add(CaptureErrors.MissingField("suggestion", "event_code"));
        }
        else
        {
            CheckCode(errors, suggestion.EventCode, isEvent: true, vocabulary);
        }

        if (!string.IsNullOrEmpty(suggestion.RoleCode))
        {
            CheckCode(errors, suggestion.RoleCode, isEvent: false, vocabulary);
        }

        if (suggestion.Confidence is < 0m or > 1m)
        {
            errors.Add(CaptureErrors.ConfidenceOutOfRange("suggestion", suggestion.Confidence));
        }

        return errors.Count == 0 ? Result.Success() : Result.Failure(errors);
    }

    private static void CheckCode(List<Error> errors, string code, bool isEvent, IPostingVocabulary vocabulary)
    {
        // الرقمي أولاً: رمزٌ مقطعُه رقم يُرفض بوصفه «يسمّي حساباً» لا بوصفه «شكلاً خاطئاً»،
        // لأن الرسالتين تُرسلان المُصلِح إلى مكانين مختلفين.
        if (NumericSegment().IsMatch(code))
        {
            errors.Add(CaptureErrors.SuggestionNamesLedgerCode(code));
            return;
        }

        if (isEvent && !CodeShape().IsMatch(code))
        {
            errors.Add(CaptureErrors.EventCodeMalformed(code));
            return;
        }

        if (isEvent && !vocabulary.KnowsEvent(code))
        {
            errors.Add(CaptureErrors.EventCodeNotInMatrix(code));
        }
        else if (!isEvent && !vocabulary.KnowsRole(code))
        {
            errors.Add(CaptureErrors.RoleCodeNotInMatrix(code));
        }
    }
}
