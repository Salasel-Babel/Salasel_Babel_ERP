using Babel.SharedKernel;

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

    /// <summary>
    /// عملة المنشأة. <b>والرواتب تُرحَّل بالريال السعودي حصراً</b> بحكم البيانات لا
    /// بحكم هذا الحقل: حساب التأمينات المستحقة معلَنٌ في دليل الحسابات
    /// <c>currency_mode=company_only</c> بعملة <c>SAR</c>، فأي عملة أخرى يرفضها
    /// المخطِّط بـ<c>ledger.posting.currency_not_allowed</c>. والحقل هنا كي يُقرأ
    /// القيد من الإعداد لا كي يُفتح.
    /// </summary>
    public string CompanyCurrency { get; set; } = "SAR";

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

        try
        {
            _ = CurrencyCode.FromString(CompanyCurrency);
        }
        catch (ArgumentException reason)
        {
            throw new InvalidOperationException(
                "hr.currency_not_configured — عملة المنشأة غير مضبوطة أو غير صالحة. / "
                + "hr.currency_not_configured — the company currency is unset or invalid.",
                reason);
        }
    }
}
