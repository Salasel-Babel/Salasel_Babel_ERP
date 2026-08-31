namespace Babel.Ledger.Persistence;

/// <summary>
/// صف الحساب في دليل الحسابات. <c>internal</c> — لا يعبر حدّ الدفتر (القاعدة 1، الطبقة الثانية).
/// <para>
/// <see cref="NameAr"/> هو الحقل المُوقَّع كما أدخله المستخدم، و<see cref="NameArSearch"/>
/// مشتقّ للبحث ولا يدخل التجزئة أبداً. عمودان لا عمود واحد: أي تطبيع بحثي يُطبَّق على
/// عمود موقَّع يُبطل كل البصمات السابقة دفعةً واحدة (فخ-26).
/// </para>
/// </summary>
internal sealed class AccountRow
{
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameArSearch { get; set; } = string.Empty;
    public string? ParentCode { get; set; }
    public int Level { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public string NaturalSide { get; set; } = string.Empty;
    public bool IsPostable { get; set; }
    public bool IsContra { get; set; }
    public string? StatementSection { get; set; }
    public string SubledgerType { get; set; } = "none";
    public string[] RequiredDimensions { get; set; } = [];
    public string CurrencyMode { get; set; } = "any";
    public string? CurrencyCode { get; set; }
    public bool IsProtected { get; set; }
    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = "drafted";
    public string? SourceRef { get; set; }
    public string? CaveatAr { get; set; }
    public string? CaveatEn { get; set; }
}

/// <summary>كتالوج الأدوار المحاسبية. الدور لا الحساب هو ما تراه المصفوفة.</summary>
internal sealed class PostingRoleRow
{
    public string RoleCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? ExpectedAccountType { get; set; }
    public string? ExpectedSide { get; set; }
    public string Status { get; set; } = "drafted";
    public string? NoteAr { get; set; }
    public string? NoteEn { get; set; }
}

/// <summary>
/// خريطة (شركة × دور × مؤهّل) ⇒ حساب.
/// <para>
/// هذا الجدول وحده هو ما يجعل مستأجرين يُنتجان حسابين مختلفين من الحدث نفسه
/// دون سطر كود واحد. المؤهّل <c>*</c> إلزامي لكل دور، وإلا وقف المحرك.
/// </para>
/// </summary>
internal sealed class RoleAccountMapRow
{
    public Guid CompanyId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string Qualifier { get; set; } = "*";
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = "drafted";
    public string? NoteAr { get; set; }
    public string? NoteEn { get; set; }
}

/// <summary>بُعد العقار — الطبقة الثالثة لقاعدة الحجب GR-RE-001.</summary>
internal sealed class PropertyDimensionRow
{
    public Guid CompanyId { get; set; }
    public string PropertyId { get; set; } = string.Empty;
    public string OwnershipModel { get; set; } = "own_property";
    public string NameAr { get; set; } = string.Empty;
}

/// <summary>الفترة المالية. الترحيل في فترة مقفلة مرفوض افتراضاً.</summary>
internal sealed class FiscalPeriodRow
{
    public Guid CompanyId { get; set; }
    public int FiscalYear { get; set; }
    public int PeriodNo { get; set; }
    public string PeriodCode { get; set; } = string.Empty;
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public string State { get; set; } = "open";
    public string NameAr { get; set; } = string.Empty;
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
}

/// <summary>
/// صفّ العدّاد بلا فجوات لكل (شركة × دفتر × سنة مالية).
/// <b>ليس <c>SEQUENCE</c>:</b> التسلسل غير معاملاتي ويُهدر أرقاماً عند التراجع (فخ-12).
/// </summary>
internal sealed class PostingCounterRow
{
    public Guid CompanyId { get; set; }
    public string BookId { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public long NextEntryNo { get; set; } = 1;
    public long NextChainSeq { get; set; } = 1;
}

/// <summary>حلقة في سلسلة البصمات. البايتات القانونية تُخزَّن ولا تُشتقّ مجدداً (فخ-20).</summary>
internal sealed class ChainLinkRow
{
    public Guid CompanyId { get; set; }
    public string BookId { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public long ChainSeq { get; set; }
    public Guid EntryId { get; set; }
    public string CanonVersion { get; set; } = string.Empty;
    public byte[] PreviousHash { get; set; } = [];
    public byte[] EntryHash { get; set; } = [];
    public byte[] CanonicalBytes { get; set; } = [];
}

/// <summary>إسقاط الأرصدة، مصون داخل معاملة الترحيل نفسها (ADR-0004).</summary>
internal sealed class AccountBalanceRow
{
    public Guid CompanyId { get; set; }
    public string BookId { get; set; } = string.Empty;
    public string PeriodCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public long EntryCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>سجلّ العمليات: يسجّل ما فشل أيضاً — والمرفوض هو ما يُثبت أن الرقابة عملت (فخ-08).</summary>
internal sealed class ProcessEventRow
{
    public long ProcessEventId { get; set; }
    public Guid CompanyId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string? EventCode { get; set; }
    public string? SourceDocType { get; set; }
    public string? SourceDocId { get; set; }
    public string? ReasonCode { get; set; }
    public string? MessageAr { get; set; }
    public string? MessageEn { get; set; }
    public string? Detail { get; set; }
}

/// <summary>
/// <b>ترجمة اسم كيان مرجعي — صفٌّ لا عمود.</b>
/// <para>
/// هذا الجدول هو ADR-0021 بند 2 مُنفَّذاً: العربي عمودٌ إلزامي على الكيان نفسه لأنه
/// <b>السجلّ</b>، وكل ما سواه صفٌّ هنا بمفتاح (كيان × لغة). فإضافة الأردية أو الهندية
/// أو الأمهرية <b>إدخالُ صفوف لا هجرةُ مخطّط ولا إصدار برمجي</b> — وهو الفرق العملي
/// الوحيد بين «متعدّد اللغات» و«ثنائي اللغة».
/// </para>
/// <para>
/// <b>ولا يدخل هذا الجدول بصمةً ولا دفتراً ولا شكلاً قانونياً</b> (بند 3): لا يُقرأ في
/// مسار الترحيل أصلاً، ولا يظهر اسمٌ منه في <c>Babel.Canonicalization</c>. وذلك هو ما
/// يجعل إضافة لغة عمليةً لا تمسّ سلسلة البصمات بحرف.
/// </para>
/// </summary>
internal sealed class NameTranslationRow
{
    /// <summary>
    /// الشركة، أو <see cref="NameTranslationScope.Global"/> للكيانات العامّة على مستوى
    /// المنتج — والأدوار المحاسبية وحدها كذلك، ويفرض ذلك قيدٌ في المخطّط لا اتفاق.
    /// </summary>
    public Guid CompanyId { get; set; }

    /// <summary>نوع الكيان — مجموعة مغلقة يفرضها قيد في قاعدة البيانات.</summary>
    public string EntityKind { get; set; } = string.Empty;

    /// <summary>المفتاح الطبيعي للكيان: رمز الحساب، أو رمز الدور، أو معرّف العقار، أو رمز الفترة.</summary>
    public string EntityKey { get; set; } = string.Empty;

    /// <summary>وسم اللغة BCP-47. والعربية مرفوضة هنا: هي السجلّ لا ترجمةً له.</summary>
    public string LanguageTag { get; set; } = string.Empty;

    /// <summary>النصّ المترجَم. غير فارغ — والغياب يُعبَّر عنه بغياب الصفّ.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>أنواع الكيانات التي تُترجَم أسماؤها، ونطاقها. مجموعة مغلقة يقابلها قيد في المخطّط.</summary>
internal static class NameTranslationScope
{
    /// <summary>نطاق الكيانات العامّة — الأدوار المحاسبية وحدها اليوم.</summary>
    public static readonly Guid Global = Guid.Empty;

    /// <summary>حساب في دليل الحسابات. المفتاح رمز الحساب.</summary>
    public const string Account = "account";

    /// <summary>دور محاسبي. المفتاح رمز الدور، والنطاق عامّ.</summary>
    public const string PostingRole = "posting_role";

    /// <summary>بُعد عقار. المفتاح معرّف العقار.</summary>
    public const string Property = "property";

    /// <summary>فترة مالية. المفتاح رمز الفترة.</summary>
    public const string FiscalPeriod = "fiscal_period";

    /// <summary>الأنواع كلّها، بالترتيب الذي يظهر به القيد في المخطّط.</summary>
    public static readonly string[] All = [Account, FiscalPeriod, PostingRole, Property];
}
