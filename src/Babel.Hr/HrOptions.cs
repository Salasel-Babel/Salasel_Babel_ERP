
namespace Babel.Hr;

/// <summary>
/// إعدادات وحدة الموارد البشرية.
/// <para>
/// <b>ولا كلمة مرور ولا مضيف ولا اسم قاعدة مكتوبٌ في المستودع</b>: نصّ الاتصال يُقرأ من
/// البيئة، وغيابُه <b>عطلٌ يُعلَن عند التركيب برمزه</b> لا افتراضيٌّ يُخترع.
/// </para>
/// <para>
/// <b>ولماذا يخالف هذا شكلَ المبيعات والمشتريات والمخزون:</b> الثلاثة تحمل نصّاً
/// افتراضياً يشير إلى المضيف المحلي، فكان كل خادم يشير إليه مهما كان النشر — عطلٌ لم
/// يظهر إلا حين نُشر لها سطح HTTP، لأن <b>مسارٌ لا يُسلَك لا يُظهر إعداداً خاطئاً</b>
/// (‏<c>traps.md#fakh-one-module-connection-still-read-from-a-default-after-its-siblings-were-fixed</c>).
/// وهذه الوحدة أثقل جدول بيانات شخصية في المنتج، فخادمٌ يشير بها إلى قاعدة أخرى بصمت
/// ليس عطلَ إعدادٍ بل حادثة بيانات. فالضابط هنا <b>يرفض الغياب</b>.
/// </para>
/// </summary>
public sealed class HrOptions
{
    /// <summary>اسم متغيّر البيئة الذي يُقرأ منه نصّ الاتصال حين لا يُضبط صراحةً.</summary>
    public const string ConnectionVariable = "BABEL_HR_DB";

    /// <summary>اتصال قاعدة بيانات الموارد البشرية. فارغٌ يعني «لم يُضبط».</summary>
    public string ConnectionString { get; set; } =
        Environment.GetEnvironmentVariable(ConnectionVariable) ?? string.Empty;

    // ‏**ولا عملةَ هنا.** كانت `CompanyCurrency = "SAR"` قيمةً ابتدائية في هذا النوع؛ وموضعُها
    // الصحيح صفُّ التأسيس، وتصل الوحدةَ عبر `ICompanyMoneyResolver` في كلّ نداء (ADR-0089).
    // وأمّا حصرُ ترحيل الرواتب في عملة حساب التأمينات فبحكم دليل الحسابات
    // (`currency_mode=company_only`) لا بحكم حقلٍ هنا.

    /// <summary>
    /// يرفع عطلاً مقروءاً إن لم يُضبط نصّ الاتصال — <b>عند التركيب لا عند أول نداء</b>.
    /// </summary>
    /// <exception cref="InvalidOperationException">إن كان نصّ الاتصال غائباً أو فارغاً.</exception>
    public void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException(
                "hr.connection_not_configured — اتصال قاعدة الموارد البشرية غير مضبوط. اضبط "
                + "Babel:Hr:ConnectionString أو متغيّر البيئة " + ConnectionVariable
                + "؛ ولا افتراضي يُخترع لوحدةٍ تحمل بيانات شخصية. / "
                + "hr.connection_not_configured — the HR database connection is not configured. Set "
                + "Babel:Hr:ConnectionString or the " + ConnectionVariable
                + " environment variable; no default is invented for a module that holds personal data.");
        }

    }
}
