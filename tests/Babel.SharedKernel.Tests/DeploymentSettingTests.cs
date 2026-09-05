using Xunit;

namespace Babel.SharedKernel.Tests;

/// <summary>
/// <b>مسارُ النشر يرفض، ووضعُ التطوير باسمه.</b>
/// <para>
/// وهذه الدوالّ صافية عمداً — لا تلمس بيئة العملية — كي يكون هذا الحكم قابلاً
/// للتشغيل بلا ترتيبٍ بين الاختبارات، وكي لا يُقرأ نجاحُه من متغيّرٍ تركه غيره.
/// </para>
/// </summary>
public sealed class DeploymentSettingTests
{
    /// <summary>
    /// <b>هذا هو الحكم:</b> بلا قيمةٍ مضبوطة وبلا إعلان تطوير، النتيجة <b>فراغ</b> —
    /// أي «لم يُضبط»، ومن يحتاجه يرفض. ولا نصّ اتصالٍ يُخترع، ولا مستخدمَ فائقٍ يُبلَغ.
    /// </summary>
    [Fact]
    public void The_deployment_path_resolves_to_nothing_and_never_to_a_local_default()
    {
        string resolved = DeploymentSetting.Resolve(
            configured: null,
            localDevelopmentDeclared: false,
            database: "babel_sales",
            role: DeploymentSetting.LocalDevelopmentOwnerRole);

        Assert.Equal(string.Empty, resolved);
    }

    /// <summary>والفراغ المضبوط كالغياب: مسافةٌ في متغيّرٍ ليست إعداداً.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_configured_value_is_treated_as_absent(string configured)
    {
        Assert.Equal(
            string.Empty,
            DeploymentSetting.Resolve(configured, localDevelopmentDeclared: false, "babel_sales", "postgres"));
    }

    /// <summary>
    /// <b>الشاهد الموجب:</b> وضعُ التطوير المُعلَن يُنتج اتصالاً محلّياً فعلاً — فالحارس
    /// يرفض ما يجب لا كلَّ شيء، والمطوّر على جهازه لا يُترك بلا طريق.
    /// </summary>
    [Fact]
    public void The_declared_local_development_mode_builds_a_loopback_connection()
    {
        string resolved = DeploymentSetting.Resolve(
            configured: null,
            localDevelopmentDeclared: true,
            database: "babel_sales",
            role: DeploymentSetting.LocalDevelopmentOwnerRole);

        Assert.Contains(DeploymentSetting.LoopbackHost, resolved, StringComparison.Ordinal);
        Assert.Contains("Database=babel_sales", resolved, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", resolved, StringComparison.Ordinal);
    }

    /// <summary>والقيمة المضبوطة تسبق كلَّ شيء، في الوضعين معاً.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_configured_value_wins_in_both_modes(bool localDevelopmentDeclared)
    {
        const string configured = "Host=db;Port=5432;Database=babel_sales;Username=babel_app";

        Assert.Equal(
            configured,
            DeploymentSetting.Resolve(configured, localDevelopmentDeclared, "babel_sales", "postgres"));
    }

    /// <summary>
    /// وإعلانُ التطوير <b>يُقرأ صريحاً</b>: ما ليس «1» أو «true» أو «yes» ليس إعلاناً.
    /// وقيمةٌ مثل <c>0</c> أو <c>false</c> لا تفتح الباب — وهو ما يجعل متغيّراً
    /// منسيّاً في بيئة خادمٍ غيرَ قادرٍ على أن يُعيد الارتداد من حيث لا يُدرى.
    /// </summary>
    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("yes", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("", false)]
    [InlineData("dev", false)]
    [InlineData(null, false)]
    public void The_local_development_declaration_is_read_strictly(string? value, bool expected) =>
        Assert.Equal(expected, DeploymentSetting.IsAffirmative(value));

    /// <summary>
    /// ورسالةُ الرفض <b>تسمّي المتغيّر ومفتاح إعداده ووضعَ التطوير</b> — فمن يقرأها
    /// يعرف ماذا يضبط وأين، ولا يُترك يبحث. <b>ولا قيمةَ فيها ولا اعتماد</b>: الاسم وحده.
    /// </summary>
    [Fact]
    public void The_refusal_names_what_the_reader_must_set_and_nothing_else()
    {
        InvalidOperationException refusal = DeploymentSetting.Missing(
            "sales.connection_not_configured",
            "BABEL_SALES_DB",
            "Babel:Sales:ConnectionString",
            "اتصال قاعدة المبيعات",
            "the Sales database connection");

        Assert.Contains("sales.connection_not_configured", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("BABEL_SALES_DB", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("Babel:Sales:ConnectionString", refusal.Message, StringComparison.Ordinal);
        Assert.Contains(DeploymentSetting.LocalDevelopmentVariable, refusal.Message, StringComparison.Ordinal);

        // ولا يُذكر مضيفٌ ولا دورٌ ولا نصّ اتصال في رسالةٍ قد تُكتب في سجلّ.
        Assert.DoesNotContain(DeploymentSetting.LoopbackHost, refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Host=", refusal.Message, StringComparison.Ordinal);
    }
}
