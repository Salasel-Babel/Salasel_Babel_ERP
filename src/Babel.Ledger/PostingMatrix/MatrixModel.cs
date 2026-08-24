namespace Babel.Ledger.PostingMatrix;

/// <summary>مبلغ مُسمّى في مفردات الحدث، ومن أين يأتي.</summary>
internal sealed record MatrixAmount(string Name, string NameAr, string NameEn, string DerivationAr);

/// <summary>شرط يغيّر شكل القيد.</summary>
internal sealed record MatrixCondition(string Name, string NameAr, string NameEn, string Expression);

/// <summary>
/// سطر في قالب الحدث. <b>يذكر دوراً لا رمز حساب أبداً</b> — وهذا هو كل الفرق
/// بين مصفوفة بيانات وشيفرة ترحيل مكتوبة بيد.
/// </summary>
internal sealed record MatrixLine
{
    public required int LineNo { get; init; }

    /// <summary><c>role</c> · <c>sweep</c> · <c>import</c> · <c>manual</c> · <c>mirror</c>.</summary>
    public required string LineKind { get; init; }

    public string Role { get; init; } = string.Empty;

    /// <summary>من أين يأتي المؤهّل: <c>document.x</c> · <c>line.x</c> · <c>constant:x</c> · <c>null</c>.</summary>
    public string? QualifierSource { get; init; }

    public required string Side { get; init; }

    /// <summary>تعبير خطي على متغيرات <c>amounts</c>. لا نسبة ولا حدّ ولا نسبة ضريبة فيه.</summary>
    public required string Amount { get; init; }

    public IReadOnlyList<string> Dimensions { get; init; } = [];

    public string? Subledger { get; init; }

    /// <summary>الشرط الذي يجعل السطر ينشأ أصلاً. <c>null</c> = دائماً.</summary>
    public string? When { get; init; }

    public string NoteAr { get; init; } = string.Empty;

    public string NoteEn { get; init; } = string.Empty;
}

/// <summary>حدث تجاري في المصفوفة: القالب الذي يفتحه رمز الحدث.</summary>
internal sealed record MatrixEvent
{
    public required string EventCode { get; init; }

    public required string NameAr { get; init; }

    public required string NameEn { get; init; }

    public required string Module { get; init; }

    /// <summary><c>drafted</c> أو <c>proposed</c>. و<c>proposed</c> يعني «لم يراجعه محاسب».</summary>
    public required string Status { get; init; }

    public string SourceRef { get; init; } = string.Empty;

    /// <summary><c>false</c> تعني <b>لا قيد وهذا بيان سياسة محاسبية</b> لا إغفال.</summary>
    public required bool PostsEntry { get; init; }

    public IReadOnlyDictionary<string, MatrixAmount> Amounts { get; init; } =
        new Dictionary<string, MatrixAmount>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, MatrixCondition> Conditions { get; init; } =
        new Dictionary<string, MatrixCondition>(StringComparer.Ordinal);

    public IReadOnlyList<MatrixLine> Lines { get; init; } = [];

    /// <summary>التحفّظات المنقولة من وثائق التحليل. <c>⚠️</c> = تحفّظ نظامي لم يُرفع بعد.</summary>
    public IReadOnlyList<string> Caveats { get; init; } = [];
}

/// <summary>ما تنطبق عليه قاعدة الحجب.</summary>
internal sealed record GuardApplicability(string Kind, string? Role, string? Property, string? ExpectedValue, bool NonEmpty);

/// <summary>
/// قاعدة حجب. <c>severity = block</c> تعني <b>رفض القيد كاملاً برسالة مفهومة</b>،
/// لا تحذيراً يُتجاوَز ولا انضباطاً من المستخدم.
/// </summary>
internal sealed record GuardRule
{
    public required string RuleId { get; init; }

    public required string NameAr { get; init; }

    public required string NameEn { get; init; }

    public required string Severity { get; init; }

    public required GuardApplicability AppliesTo { get; init; }

    public required string Condition { get; init; }

    public required string MessageAr { get; init; }

    public required string MessageEn { get; init; }
}
