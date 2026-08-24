using System.Globalization;

namespace Babel.Canonicalization;

/// <summary>رمز النوع في الشكل القانوني. محرف ASCII واحد، مجمّد في v1.</summary>
public enum CanonicalKind
{
    /// <summary><c>T</c> — نصّ UTF-8 مطبَّع NFC.</summary>
    Text,
    /// <summary><c>N</c> — غياب القيمة. مختلف عن نصّ فارغ.</summary>
    Null,
    /// <summary><c>D</c> — مبلغ بمقياس 4.</summary>
    Amount,
    /// <summary><c>I</c> — عدد صحيح 64 بت.</summary>
    Integer,
    /// <summary><c>S</c> — لحظة UTC بدقّة الميكروثانية.</summary>
    Instant,
    /// <summary><c>A</c> — تاريخ مجرّد.</summary>
    Date,
    /// <summary><c>U</c> — معرّف UUID بأحرف صغيرة.</summary>
    Uuid,
    /// <summary><c>L</c> — منطقي.</summary>
    Bool,
    /// <summary><c>B</c> — بايتات بترميز hex صغير.</summary>
    Bytes,
    /// <summary><c>E</c> — رمز ثابت من مجموعة معدودة (ASCII كبير وأرقام وشرطة سفلية).</summary>
    Token,
    /// <summary><c>G</c> — مجموعة مكرّرة (السطور). القيمة هي عدد العناصر.</summary>
    Group
}

/// <summary>
/// قيمة قانونية. <b>صالحة بالبناء</b>: كل مصنع هنا يتحقّق أو يرمي، فلا يمكن أن
/// توجد <see cref="CanonicalValue"/> غير صالحة، ولا يمكن أن يُفاجأ المُوحِّد لاحقاً.
///
/// وهذا مقصود: التحقق يقع عند إنشاء القيمة، لا عند كتابة البايتات، حتى تكون
/// رسالة الخطأ عند مصدر البيانات لا في نهاية خط الأنابيب.
/// </summary>
public abstract record CanonicalValue
{
    /// <summary>رمز النوع.</summary>
    public abstract CanonicalKind Kind { get; }

    /// <summary>محرف النوع في الشكل السلكي.</summary>
    public char Tag => Kind switch
    {
        CanonicalKind.Text => 'T',
        CanonicalKind.Null => 'N',
        CanonicalKind.Amount => 'D',
        CanonicalKind.Integer => 'I',
        CanonicalKind.Instant => 'S',
        CanonicalKind.Date => 'A',
        CanonicalKind.Uuid => 'U',
        CanonicalKind.Bool => 'L',
        CanonicalKind.Bytes => 'B',
        CanonicalKind.Token => 'E',
        CanonicalKind.Group => 'G',
        _ => throw new InvalidOperationException()
    };

    /// <summary>الحمولة النصية القانونية لهذه القيمة.</summary>
    public abstract string Payload { get; }

    // =====================================================================
    //  المصانع
    // =====================================================================

    /// <summary>نصّ مُوقَّع. يجب أن يكون NFC وخالياً من كل ما ترفضه <see cref="TextRules"/>.</summary>
    public static CanonicalValue Text(string value, string? field = null)
        => new TextValue(TextRules.RequireCanonical(value, field));

    /// <summary>
    /// ✋ <b>خطأ ترجمة مقصود.</b> مفتاح البحث مشتقّ ولا يُجزَّأ أبداً.
    /// </summary>
    [Obsolete(
        "لا يُجزَّأ مفتاح البحث أبداً. ArabicSearch.Normalize يزيل التطويل ويوحّد أشكال الألف " +
        "والتاء المربوطة، وهي فروق حقيقية في القيمة المُوقَّعة. القيمة الموقَّعة وقيمة البحث " +
        "عمودان منفصلان: خزّن الأصل في العمود المُوقَّع، ومفتاح البحث في عمود مشتقّ مستثنى من " +
        "التجزئة. A SearchKey must never be hashed.",
        error: true)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1801")]
    public static CanonicalValue Text(ArabicSearch.SearchKey key)
        => throw new InvalidOperationException("unreachable: compile-time error");

    /// <summary>نصّ أو غياب. <c>null</c> و<c>""</c> يعطيان بايتات مختلفة عمداً.</summary>
    public static CanonicalValue TextOrNull(string? value, string? field = null)
        => value is null ? NullValue.Instance : Text(value, field);

    /// <summary>غياب القيمة.</summary>
    public static CanonicalValue Null() => NullValue.Instance;

    /// <summary>مبلغ. مقياس 4، ثقافة ثابتة، بلا صفر سالب.</summary>
    public static CanonicalValue Amount(decimal value, string? field = null)
        => new AmountValue(Amounts.Require(value, field));

    /// <summary>عدد صحيح.</summary>
    public static CanonicalValue Integer(long value) => new IntegerValue(value);

    /// <summary>لحظة UTC مقصوصة إلى الميكروثانية.</summary>
    public static CanonicalValue Instant(DateTime value, string? field = null)
        => new InstantValue(Instants.Require(value, field));

    /// <summary>تاريخ مجرّد.</summary>
    public static CanonicalValue Date(DateOnly value) => new DateValue(value);

    /// <summary>معرّف UUID.</summary>
    public static CanonicalValue Uuid(Guid value) => new UuidValue(value);

    /// <summary>قيمة منطقية.</summary>
    public static CanonicalValue Bool(bool value) => new BoolValue(value);

    /// <summary>بايتات (بصمات، تواقيع). تُكتب hex بأحرف صغيرة.</summary>
    public static CanonicalValue Bytes(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new BytesValue((byte[])value.Clone());
    }

    /// <summary>رمز من مجموعة معدودة: <c>[A-Z0-9_]{1,64}</c>. مثال: <c>POSTED</c>، <c>CANCELLED</c>.</summary>
    public static CanonicalValue Token(string value, string? field = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length is 0 or > 64)
            throw new CanonicalizationException(CanonErrors.TokenInvalid,
                $"الرمز «{value}» يجب أن يكون بين 1 و64 محرفاً.", -1, field);
        foreach (var ch in value)
            if (!(ch is >= 'A' and <= 'Z' || ch is >= '0' and <= '9' || ch == '_'))
                throw new CanonicalizationException(CanonErrors.TokenInvalid,
                    $"الرمز «{value}» يقبل [A-Z0-9_] فقط. الرموز لا تُترجم ولا تُعرض؛ " +
                    "النص المعروض حقل نصّي مستقلّ.", -1, field);
        return new TokenValue(value);
    }

    // =====================================================================

    internal sealed record TextValue(string Value) : CanonicalValue
    {
        public override CanonicalKind Kind => CanonicalKind.Text;
        public override string Payload => Value;
    }

    internal sealed record NullValue : CanonicalValue
    {
        internal static readonly NullValue Instance = new();
        public override CanonicalKind Kind => CanonicalKind.Null;
        public override string Payload => string.Empty;
    }

    internal sealed record AmountValue(decimal Value) : CanonicalValue
    {
        public override CanonicalKind Kind => CanonicalKind.Amount;
        public override string Payload => Amounts.Render(Value);
    }

    internal sealed record IntegerValue(long Value) : CanonicalValue
    {
        public override CanonicalKind Kind => CanonicalKind.Integer;
        public override string Payload => Value.ToString(CultureInfo.InvariantCulture);
    }

    internal sealed record InstantValue(DateTime Value) : CanonicalValue
    {
        public override CanonicalKind Kind => CanonicalKind.Instant;
        public override string Payload => Instants.Render(Value);
    }

    internal sealed record DateValue(DateOnly Value) : CanonicalValue
    {
        public override CanonicalKind Kind => CanonicalKind.Date;
        public override string Payload => Instants.RenderDate(Value);
    }

    internal sealed record UuidValue(Guid Value) : CanonicalValue
    {
        public override CanonicalKind Kind => CanonicalKind.Uuid;
        public override string Payload => Value.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant();
    }

    internal sealed record BoolValue(bool Value) : CanonicalValue
    {
        public override CanonicalKind Kind => CanonicalKind.Bool;
        public override string Payload => Value ? "true" : "false";
    }

    internal sealed record BytesValue(byte[] Value) : CanonicalValue
    {
        public override CanonicalKind Kind => CanonicalKind.Bytes;
        public override string Payload => Convert.ToHexString(Value).ToLowerInvariant();
        public bool Equals(BytesValue? other) => other is not null && Value.AsSpan().SequenceEqual(other.Value);
        public override int GetHashCode() => Payload.GetHashCode(StringComparison.Ordinal);
    }

    internal sealed record TokenValue(string Value) : CanonicalValue
    {
        public override CanonicalKind Kind => CanonicalKind.Token;
        public override string Payload => Value;
    }
}
