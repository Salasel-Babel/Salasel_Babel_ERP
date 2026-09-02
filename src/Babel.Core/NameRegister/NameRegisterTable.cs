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
    /// <param name="scopeColumns">
    /// أعمدة النطاق بالترتيب — <b>المنشأة أوّلاً والشركة ثانياً، وعمودان على الأكثر</b>.
    /// والترتيب عقدٌ لا اصطلاح: الربط بالموضع، وعكسُ الاثنين يقارن منشأة الجلسة بعمود
    /// الشركة — وكلاهما <c>uuid</c>، فلا شيء يلتقطه.
    /// </param>
    /// <param name="activeColumn">عمود «سارٍ» إن وُجد، فلا يُقترح طرفٌ مُوقَف.</param>
    /// <param name="subtitleColumn">عمود تمييزٍ يُعرض على الشاشة — رمز الطرف مثلاً.</param>
    public NameRegisterTable(
        string registerKey,
        string schema,
        string table,
        string idColumn,
        string nameColumn,
        IReadOnlyList<string> scopeColumns,
        string? activeColumn = null,
        string? subtitleColumn = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registerKey);
        ArgumentNullException.ThrowIfNull(scopeColumns);

        if (scopeColumns.Count == 0)
        {
            throw new ArgumentException(
                "سجلّ أسماء بلا عمود نطاق واحد يُطابق عبر المنشآت. "
                + "/ a name register with no scope column matches across tenants.",
                nameof(scopeColumns));
        }

        // ‏**والقيمتان المتاحتان للربط اثنتان — المنشأة والشركة — فلا يُوصَف ثالث.**
        // كان الوصف يقبل ثلاثة أعمدة فأكثر ويبني نصّاً يسمّي ‎`@scope2` لا يربطه أحد،
        // فيسقط أوّل سبرٍ **وقت التشغيل** بدل أن يسقط الوصف **وقت التركيب**. وعطلٌ
        // يُعلَن عند التركيب أرخص من عطلٍ يُعلَن عند أوّل مستخدم.
        if (scopeColumns.Count > 2)
        {
            throw new ArgumentException(
                "عمودا النطاق المتاحان اثنان: المنشأة ثم الشركة. ووصفٌ بثلاثة أعمدة فأكثر "
                + "يبني نصّاً يسمّي وسيطاً لا يُربط. / at most two scope columns are bindable: "
                + "the tenant then the company.",
                nameof(scopeColumns));
        }

        RegisterKey = registerKey;
        Schema = Identifier(schema, nameof(schema));
        Table = Identifier(table, nameof(table));
        IdColumn = Identifier(idColumn, nameof(idColumn));
        NameColumn = Identifier(nameColumn, nameof(nameColumn));
        ScopeColumns = [.. scopeColumns.Select(column => Identifier(column, nameof(scopeColumns)))];
        ActiveColumn = activeColumn is null ? null : Identifier(activeColumn, nameof(activeColumn));
        SubtitleColumn = subtitleColumn is null ? null : Identifier(subtitleColumn, nameof(subtitleColumn));
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

    /// <summary>أعمدة النطاق بالترتيب.</summary>
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
