using Babel.Hr;
using Babel.Inventory;
using Babel.Projects;
using Babel.Purchasing;
using Babel.RealEstate;
using Babel.Sales;
using Babel.Storage;

namespace BabelDemoCompany;

/// <summary>
/// قاعدةُ وحدةٍ واحدة كما يراها المُنشئ: اسمها، واتصال مالكها، وناشر مخطّطها،
/// ومخطّطها الذي يُمنح لدور التطبيق.
/// </summary>
/// <param name="Label">اسمها العربي في مُخرَج التشغيل.</param>
/// <param name="Database">اسم قاعدة البيانات — يُنشئها <see cref="Bootstrap"/>.</param>
/// <param name="OwnerConnectionString">اتصال المالك — <b>لا يبلغ الخادم أبداً</b>.</param>
/// <param name="Schema">
/// اسم المخطّط الذي تُمنح عليه حقوق الدفتر المساعد، أو <c>null</c> إن كان الناشر
/// <b>يمنح بنفسه</b> — كمخزن المرفقات، فصلاحياته أضيق من صلاحيات الدفتر المساعد
/// (‏إضافة وقراءة بلا تعديل) ولا يجوز أن توسّعها حلقةٌ عامّة.
/// </param>
/// <param name="Deploy">ناشر المخطّط، بدور المالك.</param>
internal sealed record ModuleDatabase(
    string Label,
    string Database,
    string OwnerConnectionString,
    string? Schema,
    Func<CancellationToken, Task> Deploy);

/// <summary>
/// <b>قائمةُ ما يُزوَّد — في موضع واحد، لا في ثلاثة.</b>
/// <para>
/// كان التزويد مكتوباً بيدٍ ثلاث مرّات: إنشاء القاعدة في <see cref="Bootstrap"/>، ونشر
/// المخطّط في <see cref="Schema"/>، ومنح دور التطبيق بجانبه. فوحدةٌ جديدة تحتاج ثلاثة
/// تعديلات في ثلاثة مواضع، ونسيانُ أحدها <b>لا يُفشل شيئاً</b>: لا اختبار يحمرّ، ولا
/// خادم يسقط — يقلع الخادم ويفشل أول نداءٍ يبلغ الوحدة بعطل اتصال يُقرأ «عطلُ شبكة في
/// قاعدة البيانات» لا «إعدادٌ ناقص». وهو ما وقع في <b>سبع وحدات</b>
/// (<c>docs/evidence/traps.md#fakh-a-module-fully-built-fully-tested-and-never-provisioned</c>).
/// </para>
/// <para>
/// <b>فالقائمة صارت واحدة، ويقرؤها الثلاثة.</b> وإضافةُ وحدة صارت سطراً هنا: قاعدةٌ
/// تُنشأ، ومخطّطٌ يُنشر بدور المالك، ومخطّطٌ يُمنح لدور التطبيق — الثلاثة معاً أو لا
/// شيء منها.
/// </para>
/// <para>
/// <b>وما لا تفعله هذه القائمة:</b> لا تحمل اتصال تطبيقٍ واحداً. الخادم يأخذ اتصالاته
/// من <c>deploy/compose.yml</c> وحده، وهذه الأداة تحمل اتصالات المالك — والفصل بينهما
/// هو ADR-0003 مطبَّقاً على النشر، ويبقى بالبناء لا بالانضباط.
/// </para>
/// </summary>
internal static class ModuleProvisioning
{
    /// <summary>قواعد الوحدات كلّها بترتيب نشرها.</summary>
    /// <param name="settings">الإعدادات.</param>
    public static IReadOnlyList<ModuleDatabase> Of(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return
        [
            new ModuleDatabase(
                "المبيعات",
                settings.SalesDatabase,
                settings.SalesOwner.ConnectionString,
                "sales",
                token => SalesSchemaDeployer.DeployAsync(settings.SalesOwner, token)),

            new ModuleDatabase(
                "المشتريات",
                settings.PurchasingDatabase,
                settings.PurchasingOwner.ConnectionString,
                "purchasing",
                token => PurchasingSchemaDeployer.DeployAsync(settings.PurchasingOwner, token)),

            new ModuleDatabase(
                "المخزون",
                settings.InventoryDatabase,
                settings.InventoryOwner.ConnectionString,
                "inventory",
                token => InventorySchemaDeployer.DeployAsync(settings.InventoryOwner, token)),

            new ModuleDatabase(
                "العقارات",
                settings.RealEstateDatabase,
                settings.RealEstateOwner.ConnectionString,
                "realestate",
                token => RealEstateSchemaDeployer.DeployAsync(settings.RealEstateOwner, token)),

            new ModuleDatabase(
                "المقاولات",
                settings.ProjectsDatabase,
                settings.ProjectsOwner.ConnectionString,
                "projects",
                token => ProjectsSchemaDeployer.DeployAsync(settings.ProjectsOwner, token)),

            new ModuleDatabase(
                "الموارد البشرية",
                settings.HrDatabase,
                settings.HrOwner.ConnectionString,
                "hr",
                token => HrSchemaDeployer.DeployAsync(settings.HrOwner, token)),

            // ‏**والمرفقات آخرها وناشرها يمنح بنفسه**: صلاحياتها أضيق من صلاحيات
            // الدفتر المساعد — إضافةٌ وقراءة بلا `update` — لأن المرفق سندُ إثبات
            // لا مستندٌ حيّ (ADR-0046). فلو مرّت بالحلقة العامّة لنالت `update`.
            new ModuleDatabase(
                "مخزن المرفقات",
                settings.StorageDatabase,
                settings.StorageOwner.OwnerConnectionString,
                null,
                token => StorageSchema.DeployAsync(settings.StorageOwner, token)),
        ];
    }
}
