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

    /// <summary>اتصال دور التطبيق — الأقل امتيازاً، وهو وحده ما يستعمله الخادم.</summary>
    public string AppConnectionString { get; set; } =
        Environment.GetEnvironmentVariable("BABEL_CORE_APP_DB")
        ?? $"Host=127.0.0.1;Port=5432;Database={DefaultDatabase};Username={DefaultAppRole};Include Error Detail=true";

    /// <summary>اتصال المالك — الهجرات والصلاحيات وحدها.</summary>
    public string OwnerConnectionString { get; set; } =
        Environment.GetEnvironmentVariable("BABEL_CORE_OWNER_DB")
        ?? $"Host=127.0.0.1;Port=5432;Database={DefaultDatabase};Username=postgres;Include Error Detail=true";

    /// <summary>اسم دور التطبيق في PostgreSQL.</summary>
    public string AppRole { get; set; } =
        Environment.GetEnvironmentVariable("BABEL_CORE_APP_ROLE") ?? DefaultAppRole;
}
