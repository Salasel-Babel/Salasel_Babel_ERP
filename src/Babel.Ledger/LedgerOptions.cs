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

    /// <summary>اتصال دور التطبيق — الأقل امتيازاً.</summary>
    public string AppConnectionString { get; set; } =
        Environment.GetEnvironmentVariable("BABEL_LEDGER_APP_DB")
        ?? $"Host=127.0.0.1;Port=5432;Database={DefaultDatabase};Username={DefaultAppRole};Include Error Detail=true";

    /// <summary>اتصال المالك — الهجرات والبذر وحدها.</summary>
    public string OwnerConnectionString { get; set; } =
        Environment.GetEnvironmentVariable("BABEL_LEDGER_OWNER_DB")
        ?? $"Host=127.0.0.1;Port=5432;Database={DefaultDatabase};Username=postgres;Include Error Detail=true";

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
}
