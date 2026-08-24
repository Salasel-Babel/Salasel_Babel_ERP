namespace Babel.Core.Tests;

/// <summary>ساعة ثابتة. أرخص من حزمة، وتكفي لإثبات أن الطابع الزمني يُكتب فعلاً.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
