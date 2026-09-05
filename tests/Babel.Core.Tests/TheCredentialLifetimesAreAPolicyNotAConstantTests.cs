using Babel.Core.Access;
using Xunit;

namespace Babel.Core.Tests;

/// <summary>
/// <b>مُدَد الاعتمادات سياسةٌ تُضبَط ولها سقف — لا ثوابتُ شيفرة.</b>
/// <para>
/// <b>ما كان:</b> ثلاث <c>static readonly TimeSpan</c> في <c>AccessLimits</c>، وأخطرها
/// أربعةَ عشرَ يوماً لاعتماد التجديد — وهي <b>المدّة التي يبقى فيها اعتمادٌ مسروق
/// صالحاً</b>. ورقمٌ في شيفرة يعني أن تشديد السياسة يوم حادثةٍ يمرّ ببناءٍ ونشرةٍ
/// كاملة، وهو زمنٌ لا يملكه أحد ساعتها.
/// </para>
/// <para>
/// <b>وما صار:</b> تُقرأ من البيئة، ولها <b>سقفٌ يُرفض تجاوزه ولا يُقصّ</b> — لأن
/// القصّ الصامت يجعل من ضبط ثلاثين يوماً يظنّ أنه ضبطها.
/// </para>
/// </summary>
public sealed class TheCredentialLifetimesAreAPolicyNotAConstantTests
{
    /// <summary>
    /// <b>الشاهد الموجب:</b> السياسة المُعلَنة تمرّ. حارسٌ يرفض كلَّ شيء لا يُفرَّق عن
    /// حارسٍ يرفض ما يجب، وسقفٌ يرفض افتراضَه هو سقفٌ يُعطَّل في أوّل أسبوع.
    /// </summary>
    [Fact]
    public void The_declared_policy_is_inside_its_own_ceilings()
    {
        AccessPolicy policy = new()
        {
            AccessLifetime = AccessPolicy.DeclaredAccessLifetime,
            RefreshLifetime = AccessPolicy.DeclaredRefreshLifetime,
            EnrolmentLifetime = AccessPolicy.DeclaredEnrolmentLifetime,
        };

        policy.EnsureWithinCeiling();

        Assert.Equal(TimeSpan.FromMinutes(15), AccessPolicy.DeclaredAccessLifetime);
        Assert.Equal(TimeSpan.FromDays(14), AccessPolicy.DeclaredRefreshLifetime);
        Assert.Equal(TimeSpan.FromDays(7), AccessPolicy.DeclaredEnrolmentLifetime);
    }

    /// <summary>ومدّةٌ فوق سقفها تُرفض باسم متغيّرها، ولا تُقصّ إليه.</summary>
    [Fact]
    public void A_refresh_lifetime_above_the_ceiling_is_refused_by_the_name_of_its_variable()
    {
        AccessPolicy policy = new() { RefreshLifetime = AccessPolicy.MaximumRefreshLifetime + TimeSpan.FromDays(1) };

        InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(policy.EnsureWithinCeiling);

        Assert.Contains("access.policy_out_of_range", refusal.Message, StringComparison.Ordinal);
        Assert.Contains(AccessPolicy.RefreshLifetimeVariable, refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>ومدّةٌ صفريّة أو سالبة تُرفض كذلك: «بلا انقضاء» ليست سياسة.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_non_positive_lifetime_is_refused(int minutes)
    {
        AccessPolicy policy = new() { AccessLifetime = TimeSpan.FromMinutes(minutes) };

        InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(policy.EnsureWithinCeiling);

        Assert.Contains(AccessPolicy.AccessLifetimeVariable, refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// وسياسةٌ متناقضة تُرفض: تجديدٌ أقصر من الاعتماد الفاعل يعني جلسةً تنتهي قبل أن
    /// يوجد ما يجدّدها — وكلا الرقمين داخل سقفه، فلا يمسكها فحصُ السقوف وحده.
    /// </summary>
    [Fact]
    public void A_refresh_shorter_than_the_access_credential_is_refused()
    {
        AccessPolicy policy = new()
        {
            AccessLifetime = TimeSpan.FromMinutes(30),
            RefreshLifetime = TimeSpan.FromMinutes(10),
        };

        InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(policy.EnsureWithinCeiling);

        Assert.Contains("access.policy_incoherent", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>الغياب يعني «أبقِ السياسة المعلَنة» — وهذا معلَنٌ لا مخبوء.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void An_absent_value_keeps_the_declared_policy(string? raw) =>
        Assert.Equal(
            AccessPolicy.DeclaredRefreshLifetime,
            AccessPolicy.FromConfigured(
                raw, TimeSpan.FromHours(1), AccessPolicy.DeclaredRefreshLifetime, AccessPolicy.RefreshLifetimeVariable));

    /// <summary>والقيمة المضبوطة تُقرأ بوحدتها المُعلَنة.</summary>
    [Fact]
    public void A_configured_value_is_read_in_its_declared_unit() =>
        Assert.Equal(
            TimeSpan.FromHours(24),
            AccessPolicy.FromConfigured(
                "24", TimeSpan.FromHours(1), AccessPolicy.DeclaredRefreshLifetime, AccessPolicy.RefreshLifetimeVariable));

    /// <summary>
    /// <b>وخطأٌ مطبعي يُرفض ولا يُبتلع إلى المُعلَن.</b> وهذا هو الارتداد الصامت في
    /// أخطر صوره هنا: من ضبط <c>"14d"</c> ظنّاً أنه قصّر المدّة يبقى على أربعةَ عشرَ
    /// يوماً وهو يظنّ غير ذلك — أي حادثةٌ يُظنّ أنها عُولجت.
    /// </summary>
    [Theory]
    [InlineData("14d")]
    [InlineData("أربعة")]
    [InlineData("1.5")]
    public void A_value_that_is_not_an_integer_is_refused_and_not_swallowed(string raw)
    {
        InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(
            () => AccessPolicy.FromConfigured(
                raw, TimeSpan.FromHours(1), AccessPolicy.DeclaredRefreshLifetime, AccessPolicy.RefreshLifetimeVariable));

        Assert.Contains("access.policy_not_a_number", refusal.Message, StringComparison.Ordinal);
        Assert.Contains(AccessPolicy.RefreshLifetimeVariable, refusal.Message, StringComparison.Ordinal);
    }
}
