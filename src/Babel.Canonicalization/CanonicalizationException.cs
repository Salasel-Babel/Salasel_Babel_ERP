namespace Babel.Canonicalization;

/// <summary>
/// كل رفض في هذه المكتبة يحمل رمزاً ثابتاً. الرموز جزء من المواصفة:
/// المتجهات الذهبية تخزّن الرمز المتوقّع للحالات المرفوضة، فلا يكفي أن يفشل
/// الإدخال — بل يجب أن يفشل <b>بالسبب نفسه</b> في كل بناء.
///
/// Every rejection carries a stable code. The codes are part of the specification:
/// the golden vectors assert the code, not merely "it threw".
/// </summary>
public static class CanonErrors
{
    // ---- بيئة التشغيل / runtime ----
    public const string RuntimeInvariantGlobalization = "CANON-ENV-INVARIANT-GLOBALIZATION";
    public const string RuntimeNormalizationBroken = "CANON-ENV-NFC-BROKEN";

    // ---- نص / text ----
    public const string TextLoneSurrogate = "CANON-TXT-LONE-SURROGATE";
    public const string TextNotNfc = "CANON-TXT-NOT-NFC";
    public const string TextFormatControl = "CANON-TXT-FORMAT-CONTROL";
    public const string TextControlChar = "CANON-TXT-CONTROL-CHAR";
    public const string TextNul = "CANON-TXT-NUL";
    public const string TextCarriageReturn = "CANON-TXT-CR";
    public const string TextNonAsciiSpace = "CANON-TXT-NON-ASCII-SPACE";
    public const string TextNonAsciiDigit = "CANON-TXT-NON-ASCII-DIGIT";
    public const string TextPresentationForm = "CANON-TXT-PRESENTATION-FORM";
    public const string TextNoncharacter = "CANON-TXT-NONCHARACTER";
    public const string TextPrivateUse = "CANON-TXT-PRIVATE-USE";
    public const string TextTooLong = "CANON-TXT-TOO-LONG";

    // ---- مبالغ / amounts ----
    public const string AmountScaleExceeded = "CANON-AMT-SCALE-EXCEEDED";
    public const string AmountOutOfRange = "CANON-AMT-OUT-OF-RANGE";
    public const string AmountBadLiteral = "CANON-AMT-BAD-LITERAL";

    // ---- أوقات / instants ----
    public const string InstantKindUnspecified = "CANON-TS-KIND-UNSPECIFIED";
    public const string InstantOutOfRange = "CANON-TS-OUT-OF-RANGE";
    public const string InstantBadLiteral = "CANON-TS-BAD-LITERAL";

    // ---- رموز وأسماء / tokens and names ----
    public const string TokenInvalid = "CANON-TOK-INVALID";
    public const string FieldNameInvalid = "CANON-FLD-NAME-INVALID";

    // ---- مستند / document ----
    public const string SchemaUnknownField = "CANON-DOC-UNKNOWN-FIELD";
    public const string SchemaDuplicateField = "CANON-DOC-DUPLICATE-FIELD";
    public const string SchemaMissingField = "CANON-DOC-MISSING-FIELD";
    public const string SchemaWrongKind = "CANON-DOC-WRONG-KIND";
    public const string SchemaExcludedField = "CANON-DOC-EXCLUDED-FIELD";
    public const string SchemaNestedGroup = "CANON-DOC-NESTED-GROUP";
    public const string DocumentUnbound = "CANON-DOC-UNBOUND";
    public const string DocumentTooManyItems = "CANON-DOC-TOO-MANY-ITEMS";

    // ---- سلسلة / chain ----
    public const string ChainBadPreviousHash = "CANON-CHN-BAD-PREV-HASH";
    public const string ChainBadSequence = "CANON-CHN-BAD-SEQUENCE";
    public const string ChainUnknownVersion = "CANON-CHN-UNKNOWN-VERSION";
}

/// <summary>
/// رفض من مكتبة التوحيد القياسي. يحمل رمزاً ثابتاً، وموقع المحرف حين ينطبق.
/// A canonicalisation rejection: stable code, plus the offending index when applicable.
/// </summary>
public sealed class CanonicalizationException : Exception
{
    public string Code { get; }

    /// <summary>موقع المحرف المخالف داخل النص، أو -1.</summary>
    public int Index { get; }

    /// <summary>اسم الحقل المخالف إن عُرف.</summary>
    public string? Field { get; }

    public CanonicalizationException(string code, string message, int index = -1, string? field = null)
        : base(Format(code, message, index, field))
    {
        Code = code;
        Index = index;
        Field = field;
    }

    private static string Format(string code, string message, int index, string? field)
    {
        var where = field is null ? "" : $" [field={field}]";
        var at = index >= 0 ? $" [index={index}]" : "";
        return $"{code}{where}{at}: {message}";
    }

    internal static CanonicalizationException Text(string code, string message, int index, string? field = null)
        => new(code, message, index, field);
}
