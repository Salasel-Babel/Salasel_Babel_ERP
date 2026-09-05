using Babel.SharedKernel;

namespace Babel.Storage;

/// <summary>
/// إعدادات مخزن المرفقات. <b>ولا كلمة مرور ولا مفتاح في المستودع</b> — كلها تُقرأ من
/// البيئة، والافتراضيات محلّية للتطوير وحده.
/// </summary>
public sealed class StorageOptions
{
    /// <summary>اسم قاعدة التطوير الافتراضية.</summary>
    public const string DefaultDatabase = "babel_storage";

    /// <summary>اسم دور التطبيق الافتراضي — <b>ليس مالكاً وليس superuser</b>.</summary>
    public const string DefaultAppRole = "babel_storage_app";

    /// <summary>السقف الافتراضي لحجم المرفق: عشرون ميبي‌بايت.</summary>
    public const long DefaultMaximumBytes = 20L * 1024 * 1024;

    /// <summary>السقف الافتراضي لعمر تذكرة الوصول: خمس دقائق.</summary>
    public static readonly TimeSpan DefaultTicketLifetimeCap = TimeSpan.FromMinutes(5);

    /// <summary>اسم متغيّر اتصال المالك.</summary>
    public const string OwnerConnectionVariable = "BABEL_STORAGE_OWNER_DB";

    /// <summary>اسم متغيّر اتصال دور التطبيق.</summary>
    public const string AppConnectionVariable = "BABEL_STORAGE_APP_DB";

    /// <summary>مفتاح إعداد اتصال المالك — <b>لا يقرؤه الخادم</b>.</summary>
    public const string OwnerConfigurationKey = "Babel:Storage:OwnerConnectionString";

    /// <summary>مفتاح إعداد اتصال دور التطبيق.</summary>
    public const string AppConfigurationKey = "Babel:Storage:AppConnectionString";

    /// <summary>
    /// اتصال <b>المالك</b> — للنشر وحده، ولا يُستعمل في التشغيل.
    /// <para>
    /// <b>وكان له ارتدادٌ صامت إلى المستخدم الفائق</b> على المِعوَد. فصار الغياب فراغاً،
    /// و<see cref="EnsureOwnerConfigured"/> يرفضه بالاسم.
    /// </para>
    /// </summary>
    public string OwnerConnectionString { get; set; } =
        DeploymentSetting.Connection(OwnerConnectionVariable, DefaultDatabase);

    /// <summary>
    /// اتصال <b>التطبيق</b> — بدور بلا <c>UPDATE</c> ولا <c>DELETE</c>.
    /// <b>فارغٌ يعني «لم يُضبط»</b>، و<see cref="EnsureAppConfigured"/> يرفضه بالاسم.
    /// </summary>
    public string AppConnectionString { get; set; } =
        DeploymentSetting.Connection(AppConnectionVariable, DefaultDatabase, DefaultAppRole);

    /// <summary>اسم دور التطبيق الذي تُمنح له الصلاحيات.</summary>
    public string AppRole { get; set; } =
        Environment.GetEnvironmentVariable("BABEL_STORAGE_APP_ROLE") ?? DefaultAppRole;

    /// <summary>جذر المخزن على القرص. مجلد يملكه مستخدم الخدمة وحده.</summary>
    public string RootPath { get; set; } =
        Environment.GetEnvironmentVariable("BABEL_STORAGE_ROOT") ?? "/var/lib/babel/attachments";

    /// <summary>
    /// السقف المطلق لحجم مرفق واحد بالبايت. <b>يُفحص قبل الشمّ وقبل أي تخصيص</b>.
    /// </summary>
    public long MaximumBytes { get; set; } = DefaultMaximumBytes;

    /// <summary>
    /// سقف عمر تذكرة الوصول. طلبٌ يتجاوزه <b>يُرفض ولا يُقصّ</b>: القصّ الصامت يجعل
    /// المستدعي يظنّ أنه أصدر ساعةً وقد أصدر خمس دقائق.
    /// </summary>
    public TimeSpan TicketLifetimeCap { get; set; } = DefaultTicketLifetimeCap;

    /// <summary>
    /// مفتاح توقيع التذاكر. <b>يُقرأ من البيئة ولا يُودَع</b>؛ وغيابُه في الإنتاج
    /// عطلٌ يُعلَن عند التركيب لا مفتاحٌ يُخترع.
    /// </summary>
    public byte[] TicketSigningKey { get; set; } =
        Environment.GetEnvironmentVariable("BABEL_STORAGE_TICKET_KEY") is { Length: > 0 } configured
            ? Convert.FromHexString(configured)
            : [];

    /// <summary>
    /// يرفض غياب اتصال دور التطبيق برسالةٍ تسمّي المتغيّر — <b>عند التركيب</b>.
    /// </summary>
    /// <exception cref="InvalidOperationException">إن كان الاتصال غائباً أو فارغاً.</exception>
    public void EnsureAppConfigured()
    {
        if (string.IsNullOrWhiteSpace(AppConnectionString))
        {
            throw DeploymentSetting.Missing(
                "storage.app_connection_not_configured",
                AppConnectionVariable,
                AppConfigurationKey,
                "اتصال دور التطبيق على قاعدة المرفقات",
                "the attachment store application-role database connection");
        }
    }

    /// <summary>
    /// يرفض غياب اتصال المالك برسالةٍ تسمّي المتغيّر. <b>يناديه مسار النشر وحده.</b>
    /// </summary>
    /// <exception cref="InvalidOperationException">إن كان الاتصال غائباً أو فارغاً.</exception>
    public void EnsureOwnerConfigured()
    {
        if (string.IsNullOrWhiteSpace(OwnerConnectionString))
        {
            throw DeploymentSetting.Missing(
                "storage.owner_connection_not_configured",
                OwnerConnectionVariable,
                OwnerConfigurationKey,
                "اتصال مالك قاعدة المرفقات",
                "the attachment store owner database connection");
        }
    }
}
