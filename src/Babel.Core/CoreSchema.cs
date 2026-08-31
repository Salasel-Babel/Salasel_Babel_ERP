using Babel.Core.Persistence;

namespace Babel.Core;

/// <summary>
/// <b>نقطة الدخول المعلَنة لنشر مخطّط النواة.</b>
/// <para>
/// ناشر المخطّط <c>CoreSchemaDeployer</c> نوع <c>internal</c> بحكم القاعدة 5 — لا نوع
/// في مساحة <c>*.Persistence</c> يُرى خارج وحدته. ونشرُ المخطّط <b>فعلٌ تشغيلي</b> لا
/// حاجةُ اختبار: تحتاجه أداة الترحيل وبيئة العرض وبيئتا اختبار. فالباب معلَن هنا، على
/// نمط <c>Babel.Ledger.LedgerSchema</c> حرفياً — والبديل المُجرَّب في هذا
/// المستودع هو أن يخترع كل مستهلك التفافاً بالانعكاس، وقد اختُرع ثلاث مرات.
/// </para>
/// <para>
/// <b>وما لا يفتحه هذا الباب:</b> النشر يبقى <b>بدور المالك حصراً</b> — يقرأ
/// <see cref="CoreOptions.OwnerConnectionString"/> — ويبقى خارج حاوية اعتماديات
/// التطبيق. الخادم لا يملك DDL، ولو ملكه لأسقط مشغّل ثبات مقياس العرض ثم كتب مقياساً
/// آخر على منشأة قائمة (ADR-0003).
/// </para>
/// </summary>
public static class CoreSchema
{
    /// <summary>
    /// ينشر مخطّط النواة كاملاً — الهجرات، ثم المشغّلات داخلها، ثم الصلاحيات — بدور المالك.
    /// </summary>
    /// <param name="options">
    /// إعدادات النواة. يُقرأ منها <see cref="CoreOptions.OwnerConnectionString"/>
    /// و<see cref="CoreOptions.AppRole"/>؛ واتصال التطبيق لا يُستعمل هنا إطلاقاً.
    /// </param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static Task DeployAsync(CoreOptions options, CancellationToken cancellationToken = default)
        => CoreSchemaDeployer.DeployAsync(options, cancellationToken);
}
