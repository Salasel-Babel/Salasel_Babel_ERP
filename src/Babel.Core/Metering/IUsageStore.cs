namespace Babel.Core.Metering;

/// <summary>
/// مخزن الاستخدام. مفصول عن <see cref="IUsageMeter"/> عمداً: الالتقاط يجب أن يكون
/// رخيصاً وغير معطِّل لمسار الطلب، والتخزين قد يصير دفعات أو طابوراً لاحقاً
/// دون تعديل سطر واحد في الوحدات.
/// </summary>
public interface IUsageStore
{
    /// <summary>يلحق دفعة استخدام وحدات.</summary>
    ValueTask AppendModuleUsageAsync(IReadOnlyList<ModuleUsageEvent> batch, CancellationToken cancellationToken = default);

    /// <summary>يلحق دفعة نشاط مستخدمين.</summary>
    ValueTask AppendUserActivityAsync(IReadOnlyList<UserActivityEvent> batch, CancellationToken cancellationToken = default);
}
