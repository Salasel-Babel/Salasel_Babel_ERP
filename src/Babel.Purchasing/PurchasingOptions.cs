using Babel.SharedKernel;

namespace Babel.Purchasing;

/// <summary>
/// إعدادات وحدة المشتريات.
/// <para>
/// <b>ولا اعتماد ولا مضيف ولا نصّ اتصال مكتوبٌ هنا</b>: الاتصال يُقرأ من
/// <see cref="ConnectionVariable"/>، و<b>غيابُه عطلٌ يُعلَن عند التركيب برمزه</b> لا
/// نصٌّ يُخترع. وكان هنا ارتدادٌ صامت إلى المستخدم الفائق للعنقود على المِعوَد — أي أن
/// نشرةً ينقصها المتغيّر كانت تعمل <b>بصلاحيةٍ كاملة على القواعد كلّها</b> بلا سطرٍ
/// واحد يقول ذلك
/// (‏<c>docs/evidence/traps.md#fakh-a-missing-connection-variable-silently-grants-the-cluster-superuser</c>).
/// </para>
/// <para>
/// وللتطوير على جهازٍ محلّي: <c>BABEL_LOCAL_DEV=1</c> — <b>وضعٌ صريحٌ باسمه لا ارتداد</b>
/// (‏<c>ADR-جديد-the-absent-deployment-value-is-refused-not-guessed</c>).
/// </para>
/// </summary>
public sealed class PurchasingOptions
{
    /// <summary>اسم متغيّر البيئة الذي يُقرأ منه نصّ الاتصال.</summary>
    public const string ConnectionVariable = "BABEL_PURCHASING_DB";

    /// <summary>مفتاح الإعداد المكافئ في الجذر التركيبي.</summary>
    public const string ConfigurationKey = "Babel:Purchasing:ConnectionString";

    /// <summary>اسم قاعدة التطوير المحلّي — لا يُبلَغ إلا في وضع التطوير المُعلَن.</summary>
    public const string DefaultDatabase = "babel_purchasing";

    /// <summary>نصّ الاتصال. <b>فارغٌ يعني «لم يُضبط»</b>، ومن يحتاجه يرفض ولا يخمّن.</summary>
    public string ConnectionString { get; set; } =
        DeploymentSetting.Connection(ConnectionVariable, DefaultDatabase);

    /// <summary>عملة الشركة. ⚠️ مكانها الطبيعي جدول إعدادات الشركة، لا ثابت.</summary>
    public string CompanyCurrency { get; set; } = "SAR";

    /// <summary>
    /// يرفع عطلاً مقروءاً إن لم يُضبط نصّ الاتصال — <b>عند التركيب لا عند أول نداء</b>.
    /// </summary>
    /// <exception cref="InvalidOperationException">إن كان نصّ الاتصال غائباً أو فارغاً.</exception>
    public void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw DeploymentSetting.Missing(
                "purchasing.connection_not_configured",
                ConnectionVariable,
                ConfigurationKey,
                "اتصال قاعدة المشتريات",
                "the Purchasing database connection");
        }
    }
}
