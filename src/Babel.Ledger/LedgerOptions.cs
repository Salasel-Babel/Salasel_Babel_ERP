using Babel.SharedKernel;

namespace Babel.Ledger;

/// <summary>
/// إعدادات الدفتر.
/// <para>
/// <b>لا كلمة مرور واحدة في هذا المستودع.</b> كل شيء يُقرأ من متغيّرات البيئة وله
/// قيمة افتراضية محلية بلا كلمة مرور (‏<c>pg_hba: trust</c> على 127.0.0.1)، وهو ما
/// يجعل التشغيل المحلي ممكناً دون أن يُودَع سرّ في تاريخ git.
/// </para>
/// <list type="table">
///   <item><term><c>BABEL_LEDGER_APP_DB</c></term>
///         <description>اتصال <b>التطبيق</b>: دور غير مالك وغير superuser، له
///         <c>INSERT</c> و<c>SELECT</c> فقط على الدفتر. هذا هو الاتصال الذي
///         يستعمله محرك الترحيل.</description></item>
///   <item><term><c>BABEL_LEDGER_OWNER_DB</c></term>
///         <description>اتصال <b>المالك</b>: الهجرات والصلاحيات وبذر البيانات
///         المرجعية وإقفال الفترات. لا يستعمله مسار الترحيل أبداً.</description></item>
///   <item><term><c>BABEL_LEDGER_APP_ROLE</c></term>
///         <description>اسم دور التطبيق الذي تُمنح له الصلاحيات.</description></item>
/// </list>
/// <para>
/// الفصل بين الاتصالين ليس ترتيباً تنظيمياً بل <b>هو</b> الطبقة الأولى من الحصانة:
/// دورٌ يملك DDL يستطيع إسقاط المشغّل المؤجَّل ثم الكتابة، فتسقط الطبقتان معاً
/// (ADR-0003).
/// </para>
/// </summary>
public sealed class LedgerOptions
{
    /// <summary>اسم قاعدة البيانات الافتراضية للتشغيل المحلي.</summary>
    public const string DefaultDatabase = "babel_ledger";

    /// <summary>اسم دور التطبيق الافتراضي.</summary>
    public const string DefaultAppRole = "babel_ledger_app";

    /// <summary>اسم متغيّر اتصال دور التطبيق.</summary>
    public const string AppConnectionVariable = "BABEL_LEDGER_APP_DB";

    /// <summary>اسم متغيّر اتصال المالك.</summary>
    public const string OwnerConnectionVariable = "BABEL_LEDGER_OWNER_DB";

    /// <summary>مفتاح إعداد اتصال دور التطبيق.</summary>
    public const string AppConfigurationKey = "Babel:Ledger:AppConnectionString";

    /// <summary>مفتاح إعداد اتصال المالك — <b>لا يقرؤه الخادم ولا يجوز أن يقرأه</b>.</summary>
    public const string OwnerConfigurationKey = "Babel:Ledger:OwnerConnectionString";

    /// <summary>
    /// اتصال دور التطبيق — الأقل امتيازاً.
    /// <b>فارغٌ يعني «لم يُضبط»</b>، و<see cref="EnsureAppConfigured"/> يرفضه بالاسم.
    /// </summary>
    public string AppConnectionString { get; set; } =
        DeploymentSetting.Connection(AppConnectionVariable, DefaultDatabase, DefaultAppRole);

    /// <summary>
    /// اتصال المالك — الهجرات والبذر وحدها.
    /// <para>
    /// <b>وكان له ارتدادٌ صامت إلى المستخدم الفائق</b> على المِعوَد — وهو نظيرُ ما وُجد
    /// في الوحدات السبع، ولم يذكره المسح. فصار الغياب فراغاً،
    /// و<see cref="EnsureOwnerConfigured"/> يرفضه بالاسم.
    /// </para>
    /// </summary>
    public string OwnerConnectionString { get; set; } =
        DeploymentSetting.Connection(OwnerConnectionVariable, DefaultDatabase);

    /// <summary>اسم دور التطبيق في PostgreSQL.</summary>
    public string AppRole { get; set; } =
        Environment.GetEnvironmentVariable("BABEL_LEDGER_APP_ROLE") ?? DefaultAppRole;

    /// <summary>
    /// عملة الشركة — العملة التي يُفحص بها التوازن عند COMMIT.
    /// ⚠️ ثابت هذا التسليم إلى أن يوجد جدول إعدادات الشركة؛ لا تُقرأ نسبة ولا عملة
    /// من الكود في التصميم النهائي (CONTRIBUTING §3.6).
    /// </summary>
    public string CompanyCurrency { get; set; } = "SAR";

    /// <summary>
    /// إصدار الشكل القانوني الذي تُكتب به <b>القيود الجديدة</b>. الافتراضي
    /// <c>v2</c>.
    /// <para>
    /// <b>ولا علاقة له بالقراءة.</b> إعادة التحقق توزّع كل سجل على مُوحِّد
    /// <c>canon_version</c> <b>المخزَّن بجواره</b>، فسجلات v1 تبقى قابلة للتحقق إلى
    /// الأبد مهما تغيّر هذا الإعداد، ولا يُعاد تجزئة سجل قديم بإصدار أحدث أبداً
    /// (SPEC §12 بند 6). وهذا الإعداد موجود كي يبقى الإصدار الأقدم <b>قابلاً
    /// للكتابة في اختبار</b> يُثبت الثغرة التي أُغلقت — لا كي يُخفَّض في الإنتاج.
    /// </para>
    /// </summary>
    public string CanonVersion { get; set; } = Canonicalization.CanonicalV2.Version;

    /// <summary>
    /// يرفض غياب اتصال دور التطبيق برسالةٍ تسمّي المتغيّر — <b>عند التركيب</b>.
    /// </summary>
    /// <exception cref="InvalidOperationException">إن كان الاتصال غائباً أو فارغاً.</exception>
    public void EnsureAppConfigured()
    {
        if (string.IsNullOrWhiteSpace(AppConnectionString))
        {
            throw DeploymentSetting.Missing(
                "ledger.app_connection_not_configured",
                AppConnectionVariable,
                AppConfigurationKey,
                "اتصال دور التطبيق على قاعدة الدفتر",
                "the Ledger application-role database connection");
        }
    }

    /// <summary>
    /// يرفض غياب اتصال المالك برسالةٍ تسمّي المتغيّر. <b>يناديه مسار النشر وحده</b> —
    /// والخادم لا يحمل هذا الاتصال ولا يجوز أن يحمله (ADR-0003).
    /// </summary>
    /// <exception cref="InvalidOperationException">إن كان الاتصال غائباً أو فارغاً.</exception>
    public void EnsureOwnerConfigured()
    {
        if (string.IsNullOrWhiteSpace(OwnerConnectionString))
        {
            throw DeploymentSetting.Missing(
                "ledger.owner_connection_not_configured",
                OwnerConnectionVariable,
                OwnerConfigurationKey,
                "اتصال مالك قاعدة الدفتر",
                "the Ledger owner database connection");
        }
    }
}
