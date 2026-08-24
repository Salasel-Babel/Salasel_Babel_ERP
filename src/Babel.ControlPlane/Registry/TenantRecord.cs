namespace Babel.ControlPlane.Registry;

/// <summary>
/// حالة المستأجر في سجل مستوى التحكّم. الانتقال بينها يقع في مكان واحد
/// (<see cref="TenantRegistry"/>)، ولا تُكتب الحالة من أي مسار آخر.
/// </summary>
public enum TenantStatus
{
    /// <summary>التزويد جارٍ ولم يكتمل — القاعدة قد تكون نصف مبنيّة، فلا يُوجَّه إليها طلب.</summary>
    Provisioning,

    /// <summary>مُفعَّل: القراءة والكتابة متاحتان بحسب الاستحقاق.</summary>
    Active,

    /// <summary>موقوف مؤقتاً (‏عدم سداد مثلاً): ما يزال قابلاً للوصول تقنياً، والقرار تجاري.</summary>
    Suspended,

    /// <summary>
    /// مؤرشف: الوصول التطبيقي مقطوع <b>والبيانات باقية</b>. إنهاء الخدمة أرشفة لا حذف،
    /// لأن السجلات المحاسبية تحمل التزامات احتفاظ (‏المدّة نفسها غير مُتحقَّق منها — انظر README).
    /// </summary>
    Archived
}

/// <summary>
/// نموذج العزل — <b>عمود لا افتراض</b>. ADR-0009 مفتوح عمداً؛ ولذلك يوجد
/// الحقل في السجل ويوجد مُحلّل توجيه يقرؤه، ولا توجد سلسلة اتصال واحدة
/// مطبوعة في الشيفرة.
/// </summary>
public enum IsolationModel
{
    /// <summary>قاعدة بيانات مستقلّة لكل مستأجر — الميل الحالي (‏نطاق الانفجار واحتواء البيانات الشخصية).</summary>
    DatabasePerTenant,

    /// <summary>مخطّط مشترك مع عمود مستأجر — الخيار الآخر، غير مبنيّ بعد والدرز مفتوح له.</summary>
    SharedSchema
}

/// <summary>موقع البيانات: عندنا أم لدى العميل. ADR-0010: مؤجَّل لا مستحيل.</summary>
public enum Residency
{
    /// <summary>على بنيتنا التحتية — الوضع الوحيد المبنيّ اليوم.</summary>
    Provider,

    /// <summary>على قاعدة يستضيفها العميل — مؤجَّل عمداً، والعمود موجود حتى لا يستحيل لاحقاً.</summary>
    Customer
}

/// <summary>
/// صفّ المستأجر في سجل مستوى التحكّم: هويته، وأين تقع قاعدته، وعلى أي إصدار مخطط هي،
/// وحالته. هذا هو <b>المصدر الوحيد</b> لتوجيه أي طلب إلى قاعدة مستأجر.
/// </summary>
/// <param name="TenantId">المعرّف الداخلي الثابت — لا يتغيّر ولو تغيّر الرمز أو الاسم.</param>
/// <param name="TenantCode">الرمز القصير <c>[a-z0-9_]</c>؛ يدخل في اسم قاعدة البيانات.</param>
/// <param name="NameAr">الاسم العربي — إلزامي على كل كيان بيانات رئيسية.</param>
/// <param name="NameEn">الاسم الإنجليزي — إلزامي كذلك.</param>
/// <param name="Status">حالة المستأجر؛ انظر <see cref="TenantStatus"/>.</param>
/// <param name="Isolation">نموذج العزل المُطبَّق على هذا المستأجر بعينه.</param>
/// <param name="Residency">موقع البيانات: عندنا أم لدى العميل.</param>
/// <param name="Host">مضيف قاعدة المستأجر.</param>
/// <param name="Port">منفذ قاعدة المستأجر.</param>
/// <param name="DatabaseName">اسم قاعدة بيانات المستأجر.</param>
/// <param name="SchemaVersion">
/// إصدار المخطط المُطبَّق فعلاً على هذه القاعدة. يختلف بين المستأجرين أثناء الإصدار،
/// وهذا هو سبب وجود التوسيع/الانكماش.
/// </param>
/// <param name="CreatedAt">لحظة إنشاء الصفّ (‏مقصوصة إلى الميكروثانية).</param>
/// <param name="ActivatedAt">لحظة اكتمال التزويد؛ <c>null</c> ما دام التزويد جارياً.</param>
/// <param name="ArchivedAt">لحظة الأرشفة؛ <c>null</c> ما لم يُؤرشف.</param>
/// <param name="ArchiveReason">سبب الأرشفة المُسجَّل — لا أرشفة بلا سبب.</param>
/// <param name="ArchiveActor">من نفّذ الأرشفة — لا أرشفة بلا فاعل مُسمّى.</param>
public sealed record TenantRecord(
    Guid TenantId,
    string TenantCode,
    string NameAr,
    string NameEn,
    TenantStatus Status,
    IsolationModel Isolation,
    Residency Residency,
    string Host,
    int Port,
    string DatabaseName,
    int SchemaVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? ArchivedAt,
    string? ArchiveReason,
    string? ArchiveActor)
{
    /// <summary>
    /// هل يجوز فتح اتصال بقاعدة هذا المستأجر؟ <c>Provisioning</c> مستثناة لأن القاعدة
    /// قد تكون نصف مبنيّة، و<c>Archived</c> مستثناة لأن الوصول التطبيقي مقطوع عمداً.
    /// </summary>
    public bool IsReachable => Status is TenantStatus.Active or TenantStatus.Suspended;
}

/// <summary>يُرفع حين يُطلب مستأجر مؤرشف من مسار التطبيق.</summary>
/// <param name="tenantCode">رمز المستأجر المؤرشف.</param>
public sealed class TenantArchivedException(string tenantCode)
    : Exception($"المستأجر «{tenantCode}» مؤرشف: بياناته محفوظة، والوصول التطبيقي مقطوع.")
{
    /// <summary>رمز المستأجر الذي رُفض الوصول إليه.</summary>
    public string TenantCode { get; } = tenantCode;
}

/// <summary>يُرفع حين لا يوجد مستأجر بهذا الرمز في السجل إطلاقاً.</summary>
/// <param name="tenantCode">الرمز المطلوب.</param>
public sealed class TenantNotFoundException(string tenantCode)
    : Exception($"لا يوجد مستأجر بالرمز «{tenantCode}» في سجل مستوى التحكّم.");
