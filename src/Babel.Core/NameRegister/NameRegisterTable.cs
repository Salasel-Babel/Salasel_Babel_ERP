using System.Text.RegularExpressions;

namespace Babel.Core.NameRegister;

/// <summary>
/// <b>وصف جدول أسماءٍ تملكه وحدةٌ أخرى.</b>
/// <para>
/// الوحدة المالكة تصف جدولها وتُسجّل محوّله بنفسها؛ ولا يظهر اسم وحدةٍ ولا اسم جدول في
/// شيفرة البحث. وهو الشكل نفسه الذي يمرّ منه <c>IVoiceIntentCatalogue</c>: منفذٌ في
/// العقد، وإسهامٌ من المالك، ولا يعرف أحدهما الآخر.
/// </para>
/// <para>
/// <b>ونطاق العزل قائمةٌ لا عمودٌ مفترض.</b> المقيس على هذه الشجرة: <c>sales.customer</c>
/// و<c>purchasing.supplier</c> و<c>hr.employee</c> و<c>inventory.item</c> تحمل
/// <c>TenantId</c> <b>ولا تحمل <c>CompanyId</c> إطلاقاً</b>، بينما جداول العقارات والدفتر
/// تحمل الاثنين. فوصفٌ يفترض عمود شركةٍ يسقط على أربعةٍ من ستّة.
/// </para>
/// </summary>
public sealed partial record NameRegisterTable
{
    /// <summary>ينشئ الوصف ويتحقّق من كل معرّف فيه.</summary>
    /// <param name="registerKey">مفتاح السجلّ كما يُسمّيه المالك (‏<c>customer</c> …).</param>
    /// <param name="schema">اسم المخطّط.</param>
    /// <param name="table">اسم الجدول.</param>
    /// <param name="idColumn">عمود المعرّف.</param>
    /// <param name="nameColumn">العمود العربيّ الذي يُطوى.</param>
    /// <param name="tenantColumn">عمود المنشأة — <b>إلزاميّ</b>، فلا سجلّ يُطابَق عبر المنشآت.</param>
    /// <param name="companyColumn">
    /// عمود الشركة إن وُجد. <b>ويُسمّى ولا يُوضَع في قائمة</b>: كان النطاق قائمةً تُربَط
    /// <b>بالموضع</b>، فكان وصفٌ يعكس العمودين يقارن منشأة الجلسة بعمود الشركة — وكلاهما
    /// <c>uuid</c>، فلا شيء يلتقطه؛ وكان وصفٌ بثلاثة أعمدة يبني نصّاً يسمّي وسيطاً لا يربطه
    /// أحد فيسقط أوّل سبرٍ <b>وقت التشغيل</b>. والوسيطان المُسمّيان يجعلان الحالتين
    /// <b>غير قابلتين للتعبير</b>.
    /// </param>
    /// <param name="activeColumn">عمود «سارٍ» إن وُجد، فلا يُقترح طرفٌ مُوقَف.</param>
    /// <param name="subtitleColumn">عمود تمييزٍ يُعرض على الشاشة — رمز الطرف مثلاً.</param>
    public NameRegisterTable(
        string registerKey,
        string schema,
        string table,
        string idColumn,
        string nameColumn,
        string tenantColumn,
        string? companyColumn = null,
        string? activeColumn = null,
        string? subtitleColumn = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registerKey);

        RegisterKey = registerKey;
        Schema = Identifier(schema, nameof(schema));
        Table = Identifier(table, nameof(table));
        IdColumn = Identifier(idColumn, nameof(idColumn));
        NameColumn = Identifier(nameColumn, nameof(nameColumn));
        TenantColumn = Identifier(tenantColumn, nameof(tenantColumn));
        CompanyColumn = companyColumn is null ? null : Identifier(companyColumn, nameof(companyColumn));
        ActiveColumn = activeColumn is null ? null : Identifier(activeColumn, nameof(activeColumn));
        SubtitleColumn = subtitleColumn is null ? null : Identifier(subtitleColumn, nameof(subtitleColumn));

        ScopeColumns = CompanyColumn is null ? [TenantColumn] : [TenantColumn, CompanyColumn];
    }

    /// <summary>مفتاح السجلّ.</summary>
    public string RegisterKey { get; }

    /// <summary>المخطّط.</summary>
    public string Schema { get; }

    /// <summary>الجدول.</summary>
    public string Table { get; }

    /// <summary>عمود المعرّف.</summary>
    public string IdColumn { get; }

    /// <summary>العمود الذي يُطوى.</summary>
    public string NameColumn { get; }

    /// <summary>عمود المنشأة.</summary>
    public string TenantColumn { get; }

    /// <summary>عمود الشركة أو <c>null</c>.</summary>
    public string? CompanyColumn { get; }

    /// <summary>
    /// أعمدة النطاق بترتيبها — <b>مشتقّةٌ من العمودين المُسمّيَين لا مُمرَّرة</b>، فلا
    /// يوجد ترتيبٌ يُخطئ أحدٌ في كتابته.
    /// </summary>
    public IReadOnlyList<string> ScopeColumns { get; }

    /// <summary>عمود «سارٍ» أو <c>null</c>.</summary>
    public string? ActiveColumn { get; }

    /// <summary>عمود التمييز المعروض أو <c>null</c>.</summary>
    public string? SubtitleColumn { get; }

    /// <summary>الاسم المؤهَّل مقتبساً — للاستعمال في نصّ الاستعلام.</summary>
    public string QualifiedName => Quote(Schema) + "." + Quote(Table);

    /// <summary>يقتبس معرّفاً بعلامتَي اقتباس مزدوجتين.</summary>
    /// <param name="identifier">المعرّف — مُتحقَّقٌ منه عند الإنشاء.</param>
    public static string Quote(string identifier) => "\"" + identifier + "\"";

    /// <summary>
    /// <b>كل معرّف يُتحقَّق منه عند الإنشاء لا عند الاستعمال.</b>
    /// <para>
    /// أسماء الجداول هنا تأتي من الشيفرة لا من المستخدم، <b>وذلك بالضبط ما يجعل النسيان
    /// سهلاً</b>: أول من يقرأ اسم مخطّطٍ من إعدادٍ أو من جدولٍ في القاعدة يجد الطريق
    /// مفتوحاً. النمط يُغلقه من اليوم، والاقتباس لا يكفي وحده لأن معرّفاً فيه علامة
    /// اقتباس يهرب منه.
    /// </para>
    /// </summary>
    private static string Identifier(string value, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);

        if (!IdentifierPattern().IsMatch(value))
        {
            throw new ArgumentException(
                "المعرّف «" + value + "» ليس معرّف PostgreSQL مقبولاً في هذا الموضع. "
                + "/ '" + value + "' is not an acceptable PostgreSQL identifier here.",
                parameter);
        }

        return value;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,62}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();
}
