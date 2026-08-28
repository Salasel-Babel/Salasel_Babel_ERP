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

    /// <summary>اتصال <b>المالك</b> — للنشر وحده، ولا يُستعمل في التشغيل.</summary>
    public string OwnerConnectionString { get; set; } =
        Environment.GetEnvironmentVariable("BABEL_STORAGE_OWNER_DB")
        ?? $"Host=127.0.0.1;Port=5432;Database={DefaultDatabase};Username=postgres;Include Error Detail=true";

    /// <summary>اتصال <b>التطبيق</b> — بدور بلا <c>UPDATE</c> ولا <c>DELETE</c>.</summary>
    public string AppConnectionString { get; set; } =
        Environment.GetEnvironmentVariable("BABEL_STORAGE_APP_DB")
        ?? $"Host=127.0.0.1;Port=5432;Database={DefaultDatabase};Username={DefaultAppRole};Include Error Detail=true";

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
}
