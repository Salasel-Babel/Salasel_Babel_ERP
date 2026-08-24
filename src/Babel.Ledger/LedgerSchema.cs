using Babel.Ledger.Persistence;

namespace Babel.Ledger;

/// <summary>
/// <b>نقطة الدخول المعلَنة لنشر مخطّط الدفتر.</b>
/// <para>
/// <b>لماذا وُجدت:</b> ناشر المخطّط <c>LedgerSchemaDeployer</c> نوع <c>internal</c>
/// ولا يراه إلا <c>Babel.Ledger.Tests</c>. فكل مستهلك آخر — بيئتا اختبار مستقلّتان على
/// الأقل، والجذر التركيبي — كان يبلغه <b>بالانعكاس</b>. و<b>ثلاث</b> جهات مستقلّة كتبت
/// الالتفاف نفسه بالتعليق الاعتذاري نفسه؛ وتكرارُ اختراع الالتفاف دليلٌ على أن
/// <b>الحدّ خاطئ</b>، لا على أن الكاتبين مقصّرون.
/// </para>
/// <para>
/// <b>ولماذا واجهة معلَنة لا <c>InternalsVisibleTo</c> ثالث ورابع:</b> نشر المخطّط ليس
/// حاجة اختبارٍ بل <b>فعل تشغيلي</b> — يحتاجه المُنصِّب وأداة الترقية وبيئة العرض،
/// ولا شيء منها مشروع اختبار يُسمّى في ملف مشروع. و<c>InternalsVisibleTo</c> يحلّ
/// حالة الاختبار وحدها ويترك الفعل التشغيلي بلا باب، فيبقى الانعكاس هو الباب.
/// </para>
/// <para>
/// <b>وما لم يتغيّر:</b> النشر يبقى <b>بدور المالك حصراً</b> — يقرأ
/// <see cref="LedgerOptions.OwnerConnectionString"/> — ويبقى خارج حاوية اعتماديات
/// التطبيق. التطبيق لا يملك DDL، ولو ملكه لأسقط المشغّل المؤجَّل ثم كتب ما شاء
/// (ADR-0003). أي أن هذا النوع يُصرّح بباب كان موجوداً ويُدخَل من النافذة؛ ولا يفتح باباً
/// جديداً ولا يوسّع صلاحية.
/// </para>
/// </summary>
public static class LedgerSchema
{
    /// <summary>
    /// ينشر مخطّط الدفتر كاملاً — الهجرات، ثم المشغّلات والدوال داخلها، ثم الصلاحيات —
    /// بدور المالك.
    /// </summary>
    /// <param name="options">
    /// إعدادات الدفتر. يُقرأ منها <see cref="LedgerOptions.OwnerConnectionString"/>
    /// و<see cref="LedgerOptions.AppRole"/>؛ واتصال التطبيق لا يُستعمل هنا إطلاقاً.
    /// </param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static Task DeployAsync(LedgerOptions options, CancellationToken cancellationToken = default)
        => LedgerSchemaDeployer.DeployAsync(options, cancellationToken);
}
