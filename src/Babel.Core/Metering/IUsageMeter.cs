namespace Babel.Core.Metering;

/// <summary>
/// التقاط الاستخدام على المحورين معاً، لأن التسعير بالوحدة <b>وبالمستخدم</b>.
/// <para>
/// الحدّ موجود من اليوم الأول رغم أن المخزن جذع: الفارق بين محورين ملتقَطين ومحورين ضائعين
/// هو فارق لا يمكن تعويضه بأثر رجعي — لا يوجد استعلام يستخرج ما لم يُكتب.
/// </para>
/// </summary>
public interface IUsageMeter
{
    /// <summary>يسجّل استخداماً على محور الوحدة.</summary>
    ValueTask RecordModuleUsageAsync(ModuleUsageEvent usage, CancellationToken cancellationToken = default);

    /// <summary>يسجّل نشاطاً على محور المستخدم.</summary>
    ValueTask RecordUserActivityAsync(UserActivityEvent activity, CancellationToken cancellationToken = default);
}
