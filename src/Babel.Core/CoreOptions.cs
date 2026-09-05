using Babel.SharedKernel;

namespace Babel.Core;

/// <summary>
/// إعدادات النواة — <b>اتصالان ودور</b>، على نمط <c>LedgerOptions</c> حرفياً.
/// <para>
/// <b>ولا كلمة مرور واحدة هنا ولا في أي ملف في هذا المستودع:</b> كل قيمة تُقرأ من
/// متغيّر بيئة، وللتشغيل المحلي افتراضٌ بلا كلمة مرور يعمل مع <c>pg_hba: trust</c> على
/// 127.0.0.1. والقيمة على الخادم تُبنى من سرّ في مخزن الأسرار لحظة النشر ولا تمرّ بـgit.
/// </para>
/// <list type="table">
///   <item><term><c>BABEL_CORE_APP_DB</c></term>
///         <description>اتصال <b>التطبيق</b>: دور غير مالك وغير superuser. هذا وحده ما
///         يدخل حاوية اعتماديات الخادم.</description></item>
///   <item><term><c>BABEL_CORE_OWNER_DB</c></term>
///         <description>اتصال <b>المالك</b>: الهجرات والصلاحيات وحدها. لا يدخل الحاوية.</description></item>
///   <item><term><c>BABEL_CORE_APP_ROLE</c></term>
///         <description>اسم دور التطبيق الذي تُمنح له الصلاحيات.</description></item>
/// </list>
/// <para>
/// <b>ولماذا الفصل هنا أيضاً وقد كانت النواة بلا قاعدة:</b> لأن الفصل يُركَّب مع الجدول
/// لا بعده. مخطّطٌ يُنشر بدور واحد يملك كل شيء لا يُقسَّم لاحقاً بلا هجرة صلاحيات
/// كاملة، وهو ما يجعل «سنفصل لاحقاً» جملةً لا تقع (ADR-0003).
/// </para>
/// </summary>
public sealed class CoreOptions
{
    /// <summary>اسم قاعدة البيانات الافتراضية للتشغيل المحلي.</summary>
    public const string DefaultDatabase = "babel_core";

    /// <summary>اسم دور التطبيق الافتراضي.</summary>
    public const string DefaultAppRole = "babel_core_app";

    /// <summary>اسم متغيّر اتصال دور التطبيق.</summary>
    public const string AppConnectionVariable = "BABEL_CORE_APP_DB";

    /// <summary>اسم متغيّر اتصال المالك.</summary>
    public const string OwnerConnectionVariable = "BABEL_CORE_OWNER_DB";

    /// <summary>مفتاح إعداد اتصال دور التطبيق.</summary>
    public const string AppConfigurationKey = "Babel:Core:AppConnectionString";

    /// <summary>مفتاح إعداد اتصال المالك — <b>لا يقرؤه الخادم ولا يجوز أن يقرأه</b>.</summary>
    public const string OwnerConfigurationKey = "Babel:Core:OwnerConnectionString";

    /// <summary>
    /// اتصال دور التطبيق — الأقل امتيازاً، وهو وحده ما يستعمله الخادم.
    /// <b>فارغٌ يعني «لم يُضبط»</b>، و<see cref="EnsureAppConfigured"/> يرفضه بالاسم.
    /// </summary>
    public string AppConnectionString { get; set; } =
        DeploymentSetting.Connection(AppConnectionVariable, DefaultDatabase, DefaultAppRole);

    /// <summary>
    /// اتصال المالك — الهجرات والصلاحيات وحدها.
    /// <para>
    /// <b>وكان له ارتدادٌ صامت إلى المستخدم الفائق</b>: حاويةُ ترحيلٍ ينقصها المتغيّر
    /// كانت تعمل بامتيازٍ كامل على العنقود بلا سطرٍ يقول ذلك. فصار الغياب فراغاً،
    /// و<see cref="EnsureOwnerConfigured"/> يرفضه بالاسم.
    /// </para>
    /// </summary>
    public string OwnerConnectionString { get; set; } =
        DeploymentSetting.Connection(OwnerConnectionVariable, DefaultDatabase);

    /// <summary>اسم دور التطبيق في PostgreSQL.</summary>
    public string AppRole { get; set; } =
        Environment.GetEnvironmentVariable("BABEL_CORE_APP_ROLE") ?? DefaultAppRole;

    /// <summary>
    /// يرفض غياب اتصال دور التطبيق برسالةٍ تسمّي المتغيّر — <b>عند التركيب</b>.
    /// </summary>
    /// <exception cref="InvalidOperationException">إن كان الاتصال غائباً أو فارغاً.</exception>
    public void EnsureAppConfigured()
    {
        if (string.IsNullOrWhiteSpace(AppConnectionString))
        {
            throw DeploymentSetting.Missing(
                "core.app_connection_not_configured",
                AppConnectionVariable,
                AppConfigurationKey,
                "اتصال دور التطبيق على قاعدة النواة",
                "the Core application-role database connection");
        }
    }

    /// <summary>
    /// يرفض غياب اتصال المالك برسالةٍ تسمّي المتغيّر. <b>يناديه مسار النشر وحده</b> —
    /// والخادم لا يحمل هذا الاتصال أصلاً (ADR-0003).
    /// </summary>
    /// <exception cref="InvalidOperationException">إن كان الاتصال غائباً أو فارغاً.</exception>
    public void EnsureOwnerConfigured()
    {
        if (string.IsNullOrWhiteSpace(OwnerConnectionString))
        {
            throw DeploymentSetting.Missing(
                "core.owner_connection_not_configured",
                OwnerConnectionVariable,
                OwnerConfigurationKey,
                "اتصال مالك قاعدة النواة",
                "the Core owner database connection");
        }
    }
}
