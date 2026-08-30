using System.Globalization;
using Npgsql;

namespace Babel.ControlPlane.Support;

/// <summary>
/// كل إعدادات مستوى التحكّم. <b>لا كلمة مرور ولا سرّ واحد داخل المستودع</b>:
/// كل قيمة تُقرأ من متغيّر بيئة، والافتراضي هو اتصال محلّي بلا كلمة مرور
/// (‏peer/trust على 127.0.0.1) — وهو الوضع الموصوف في README.
///
/// Every setting comes from an environment variable; the fallback is a
/// password-less local connection. No credential is ever committed.
/// </summary>
public sealed class ControlPlaneOptions
{
    /// <summary>اسم قاعدة بيانات التحكّم (سجل المستأجرين والاشتراكات والقياس).</summary>
    public string ControlDatabase { get; init; } =
        Env("BABEL_CP_CONTROL_DB_NAME", "babel_control");

    /// <summary>سابقة أسماء قواعد المستأجرين: <c>babel_t_&lt;code&gt;</c>.</summary>
    public string TenantDatabasePrefix { get; init; } =
        Env("BABEL_CP_TENANT_DB_PREFIX", "babel_t_");

    /// <summary>دور التطبيق — ليس superuser وليس مالك المخطط (فخ-30، ADR-0003).</summary>
    public string AppRole { get; init; } = Env("BABEL_CP_APP_ROLE", "babel_cp_app");

    /// <summary>
    /// <b>دور سطح الاشتراك</b> — الدور الذي يقرأ به الجذر التركيبي مستوى التحكّم ويكتب
    /// به الاشتراك والاستحقاق، ولا شيء غير ذلك.
    /// <para>
    /// وهو <b>ليس</b> <see cref="AppRole"/> ولا مستخدم الإدارة، والفرق مقصود: دور
    /// التطبيق مسحوبةٌ منه صلاحية مخطّط التحكّم كلّها بنصّ <c>ControlSchema.EnsureAsync</c>
    /// («مستوى التحكّم عملياتي لا يُقرأ من مسار طلب المستأجر»)، ومستخدمُ الإدارة يستطيع
    /// أن يُسقط الجدول الذي يقرؤه. فسطحُ الاشتراك — وهو مسار طلب يخدم الإنترنت — يحتاج
    /// دوراً ثالثاً: يقرأ الأسطول، ويكتب الاشتراك والاستحقاق وسطورهما التدقيقية،
    /// <b>ولا يحذف صفّاً واحداً في أي جدول</b>.
    /// </para>
    /// </summary>
    public string SurfaceRole { get; init; } = Env("BABEL_CP_SURFACE_ROLE", "babel_cp_surface");

    /// <summary>مضيف الخادم ومنفذه ومستخدم الإدارة — تُقرأ من البيئة.</summary>
    public string AdminHost { get; init; } = Env("BABEL_CP_HOST", "127.0.0.1");
    /// <summary>منفذ الخادم.</summary>
    public int AdminPort { get; init; } = EnvInt("BABEL_CP_PORT", 5432);
    /// <summary>مستخدم الإدارة — يُستعمل لإنشاء القواعد وتطبيق الـDDL فقط.</summary>
    public string AdminUser { get; init; } = Env("BABEL_CP_ADMIN_USER", "postgres");

    /// <summary>
    /// كلمة مرور الإدارة. الافتراض <b>فارغ</b> — أي اتصال محلي بلا كلمة مرور.
    /// في الإنتاج تُمرَّر عبر <c>BABEL_CP_ADMIN_PASSWORD</c> أو عبر
    /// <c>PGPASSFILE</c>، ولا تُكتب في ملف داخل المستودع أبداً.
    /// </summary>
    public string? AdminPassword { get; init; } =
        Environment.GetEnvironmentVariable("BABEL_CP_ADMIN_PASSWORD");

    // ---- حدود الاتصالات (القسم 3 من التسليم) --------------------------------

    /// <summary>السقف الصلب لعدد الاتصالات الفعلية عبر <b>كل</b> المستأجرين مجتمعين.</summary>
    public int GlobalConnectionCap { get; init; } =
        EnvInt("BABEL_CP_GLOBAL_CONN_CAP", 48);

    /// <summary>أقصى عدد اتصالات لمستأجر واحد — يمنع مستأجراً واحداً من ابتلاع السقف.</summary>
    public int MaxConnectionsPerTenant { get; init; } =
        EnvInt("BABEL_CP_TENANT_CONN_CAP", 4);

    /// <summary>أقصى عدد مصادر بيانات (‏pools) حيّة في الذاكرة قبل الإخلاء بالأقدمية.</summary>
    public int MaxLiveDataSources { get; init; } =
        EnvInt("BABEL_CP_MAX_LIVE_POOLS", 64);

    /// <summary>مهلة الخمول قبل إخلاء مصدر بيانات مستأجر لم يُستعمل.</summary>
    public TimeSpan IdleEviction { get; init; } =
        TimeSpan.FromSeconds(EnvInt("BABEL_CP_IDLE_EVICT_SECONDS", 60));

    /// <summary>مهلة انتظار حجز اتصال من السقف العام قبل الرفض السريع.</summary>
    public TimeSpan LeaseTimeout { get; init; } =
        TimeSpan.FromSeconds(EnvInt("BABEL_CP_LEASE_TIMEOUT_SECONDS", 5));

    // ---- قاطع الدارة ---------------------------------------------------------

    /// <summary>عدد الإخفاقات المتتالية التي تفتح قاطع دارة المستأجر.</summary>
    public int CircuitFailureThreshold { get; init; } =
        EnvInt("BABEL_CP_CB_FAILURES", 3);

    /// <summary>مدّة بقاء القاطع مفتوحاً قبل السماح بمحاولة استطلاع.</summary>
    public TimeSpan CircuitOpenDuration { get; init; } =
        TimeSpan.FromSeconds(EnvInt("BABEL_CP_CB_OPEN_SECONDS", 10));

    /// <summary>مهلة فتح الاتصال — قصيرة عمداً: مستأجر غير قابل للوصول يجب أن يفشل بسرعة.</summary>
    public int ConnectTimeoutSeconds { get; init; } =
        EnvInt("BABEL_CP_CONNECT_TIMEOUT", 3);

    // ---- الترحيل الأسطولي ----------------------------------------------------

    /// <summary>عدد القواعد التي يحجزها عامل الترحيل في الدفعة الواحدة.</summary>
    public int FleetBatchSize { get; init; } = EnvInt("BABEL_CP_FLEET_BATCH", 8);

    /// <summary>مهلة حجز هدف الترحيل: بانتهائها يُلتقط الهدف من عامل ميّت تلقائياً.</summary>
    public TimeSpan FleetLeaseDuration { get; init; } =
        TimeSpan.FromSeconds(EnvInt("BABEL_CP_FLEET_LEASE_SECONDS", 60));

    // ---- بناء سلاسل الاتصال --------------------------------------------------

    private NpgsqlConnectionStringBuilder BaseBuilder(string database) => new()
    {
        Host = AdminHost,
        Port = AdminPort,
        Database = database,
        Username = AdminUser,
        Password = string.IsNullOrEmpty(AdminPassword) ? null : AdminPassword,
        IncludeErrorDetail = true,
        Timeout = ConnectTimeoutSeconds,
        // مطلوب صراحةً: نحن نُحاسِب كل اتصال فعلي بأنفسنا، فلا نترك تجميعاً خفيّاً
        Pooling = false
    };

    /// <summary>اتصال الصيانة — قاعدة <c>postgres</c>؛ يُستعمل فقط لـ<c>CREATE/DROP DATABASE</c>.</summary>
    public string MaintenanceConnectionString => BaseBuilder("postgres").ConnectionString;

    /// <summary>
    /// اتصال قاعدة التحكّم (مالك المخطط). <b>مُجمَّع بسقف صغير ومُعلَن</b>، لأن
    /// اتصالات مستوى التحكّم تُحسب هي أيضاً من ميزانية <c>max_connections</c>
    /// نفسها — وتجاهلها هو أحد أسباب تجاوز السقف بلا تفسير.
    /// </summary>
    public int ControlPoolSize { get; init; } = EnvInt("BABEL_CP_CONTROL_POOL", 8);

    /// <summary>
    /// هل تُبنى سلسلة اتصال قاعدة التحكّم بدور <see cref="SurfaceRole"/> بدل مستخدم
    /// الإدارة؟ <b>يضبطها الجذر التركيبي وحده</b>، فيصير كل ما يقرؤه ويكتبه سطحُ
    /// الاشتراك مارّاً بدورٍ لا يملك <c>delete</c> ولا <c>drop</c> ولا مخطّط مستأجر.
    /// وتبقى الأدوات التشغيلية — التزويد والترحيل الأسطولي والأرشفة — على مستخدم
    /// الإدارة كما كانت، فلا سطر واحد منها يتغيّر.
    /// </summary>
    public bool UseSurfaceRole { get; init; }

    /// <summary>سلسلة الاتصال بقاعدة التحكّم، مُجمَّعة بسقف صغير مُعلَن.</summary>
    public string ControlConnectionString
    {
        get
        {
            var b = BaseBuilder(ControlDatabase);
            if (UseSurfaceRole)
            {
                b.Username = SurfaceRole;
                b.Password = Environment.GetEnvironmentVariable("BABEL_CP_SURFACE_PASSWORD");
            }

            b.Pooling = true;
            b.MinPoolSize = 0;
            b.MaxPoolSize = ControlPoolSize;
            b.ConnectionIdleLifetime = 30;
            return b.ConnectionString;
        }
    }

    /// <summary>اتصال مالك قاعدة مستأجر — لتنفيذ الـDDL والترحيلات فقط.</summary>
    public string TenantOwnerConnectionString(string databaseName) =>
        BaseBuilder(databaseName).ConnectionString;

    /// <summary>
    /// اتصال <b>التطبيق</b> بقاعدة مستأجر: دور غير مميّز، مع تجميع مضبوط بسقف
    /// المستأجر. هذا هو الاتصال الوحيد الذي يراه مسار الطلب.
    /// </summary>
    public string TenantAppConnectionString(string databaseName, int maxPoolSize)
    {
        var b = BaseBuilder(databaseName);
        b.Username = AppRole;
        b.Password = Environment.GetEnvironmentVariable("BABEL_CP_APP_PASSWORD");
        b.Pooling = true;
        b.MinPoolSize = 0;
        b.MaxPoolSize = maxPoolSize;
        // مهلة الخمول لا تنزل تحت الثانية (‏Npgsql يرفض 0)، ودورة التقليم
        // لا تتجاوزها — وإلا بقي الاتصال الفيزيائي الخامل قائماً بعد انتهاء عمره.
        var idle = Math.Max(1, (int)IdleEviction.TotalSeconds);
        b.ConnectionIdleLifetime = idle;
        b.ConnectionPruningInterval = Math.Max(1, Math.Min(5, idle));
        return b.ConnectionString;
    }

    /// <summary>
    /// اتصال فحص بدور التطبيق <b>بلا تجميع</b>. مطلوب صراحةً عند إثبات
    /// الأرشفة: الاتصال المُجمَّع قد يُعيد استعمال اتصال فيزيائي قديم أُنهي
    /// بـ<c>pg_terminate_backend</c>، فيُرجِع <c>57P01</c> بدل <c>42501</c> —
    /// أي يُخفي نتيجة الفحص خلف عَرَض جانبي للتجميع.
    /// </summary>
    public string TenantAppProbeConnectionString(string databaseName)
    {
        var b = BaseBuilder(databaseName);
        b.Username = AppRole;
        b.Password = Environment.GetEnvironmentVariable("BABEL_CP_APP_PASSWORD");
        b.Pooling = false;
        return b.ConnectionString;
    }

    /// <summary>اسم قاعدة بيانات مستأجر من رمزه.</summary>
    /// <param name="tenantCode">رمز المستأجر.</param>
    /// <returns>اسم القاعدة.</returns>
    public string TenantDatabaseName(string tenantCode) => TenantDatabasePrefix + tenantCode;

    private static string Env(string key, string fallback) =>
        Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : fallback;

    /// <summary>
    /// إعداد عددي من البيئة. <b>الثقافة الثابتة صريحة وإلزامية:</b> متغيّر البيئة
    /// إعداد آلي لا نصّ معروض، و<c>int.Parse</c> بلا ثقافة يقرأ ثقافة العملية —
    /// فتحت لغة عربية بأرقام هندية يفشل التحليل أو ينحرف، والنتيجة سقف اتصالات
    /// خاطئ يظهر كعطل عشوائي في الإنتاج لا كخطأ إعداد.
    /// ورقم غير صالح <b>يرمي</b> ولا يسقط بصمت إلى الافتراضي: إعداد مكتوب خطأً
    /// يجب أن يوقف الإقلاع، لا أن يعمل بقيمة لم يخترها أحد.
    /// </summary>
    private static int EnvInt(string key, int fallback)
    {
        if (Environment.GetEnvironmentVariable(key) is not { Length: > 0 } raw) return fallback;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return v;
        throw new InvalidOperationException(
            $"قيمة متغيّر البيئة «{key}» = «{raw}» ليست عدداً صحيحاً — يُرفض الإقلاع بدل العمل بقيمة افتراضية لم يخترها أحد.");
    }
}
