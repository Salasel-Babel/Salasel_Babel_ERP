using Babel.Inventory.Application;
using Xunit;

namespace Babel.Inventory.Tests;

/// <summary>
/// حساب المتوسط المرجّح المتحرّك — بلا قاعدة بيانات وبلا ترحيل.
/// <para>
/// وموضعها منفصلاً عن اختبار التكامل مقصود: هذه تُثبت <b>الأرقام</b>، وذاك يُثبت أن
/// الرقم الذي حُسب هنا هو الرقم الذي وصل الدفتر. خلطهما يجعل فشلاً واحداً يحتمل
/// تفسيرين.
/// </para>
/// </summary>
public sealed class WeightedAverageCostTests
{
    [Fact]
    public void الوارد_الأول_يضع_أساس_التكلفة()
    {
        StockEffect effect = WeightedAverageCost.Receive(WeightedAverageCost.Empty, 100m, 1_000.0000m);

        Assert.Equal(100m, effect.After.Quantity);
        Assert.Equal(1_000.0000m, effect.After.Value);
        Assert.Equal(10.000000m, effect.After.UnitCost);
        Assert.True(effect.After.HasCostBasis);
        Assert.False(effect.DrewOnNegativeStock);
    }

    [Fact]
    public void واردان_بسعرين_يعطيان_متوسطاً_مرجّحاً_لا_آخر_سعر()
    {
        StockPosition first = WeightedAverageCost.Receive(WeightedAverageCost.Empty, 100m, 1_000.0000m).After;
        StockEffect second = WeightedAverageCost.Receive(first, 100m, 1_400.0000m);

        // ‏(1000 + 1400) ÷ 200 = 12 — لا 14 وهو آخر سعر شراء، والمصفوفة تنهى عنه بنصّها.
        Assert.Equal(200m, second.After.Quantity);
        Assert.Equal(2_400.0000m, second.After.Value);
        Assert.Equal(12.000000m, second.After.UnitCost);
    }

    [Fact]
    public void الصرف_ينزّل_بالمتوسط_ولا_يغيّره()
    {
        StockPosition position = WeightedAverageCost.Receive(WeightedAverageCost.Empty, 100m, 1_000.0000m).After;
        StockEffect issue = WeightedAverageCost.Issue(position, 30m);

        Assert.Equal(300.0000m, issue.Value);
        Assert.Equal(70m, issue.After.Quantity);
        Assert.Equal(700.0000m, issue.After.Value);
        Assert.Equal(10.000000m, issue.After.UnitCost);
        Assert.False(issue.DrewOnNegativeStock);
    }

    /// <summary>
    /// الصرف الذي يُفرغ الرصيد بالضبط يُنزّل <b>القيمة كلّها</b>، فلا يبقى فُتات تقريب
    /// على كميةٍ صفرية. والحالة مبنيّة على سعر لا يقبل القسمة: ثلاث وحدات بعشرة ريالات.
    /// </summary>
    [Fact]
    public void الصرف_المُفرِغ_ينزّل_القيمة_كلّها_فلا_يبقى_فُتات()
    {
        StockPosition position = WeightedAverageCost.Receive(WeightedAverageCost.Empty, 3m, 10.0000m).After;

        // ‏10 ÷ 3 = 3.333333، وثلاث وحدات بها = 9.999999 ⇒ 10.0000 بعد التقريب… لكن
        // الاعتماد على ذلك رهانٌ على التقريب. فالقاعدة صريحة: المُفرِغ يأخذ الكل.
        StockEffect issue = WeightedAverageCost.Issue(position, 3m);

        Assert.Equal(10.0000m, issue.Value);
        Assert.Equal(0m, issue.After.Quantity);
        Assert.Equal(0.0000m, issue.After.Value);
    }

    /// <summary>
    /// البيع على المكشوف: الكمية تصير سالبة والقيمة كذلك، والحركة <b>تُوسم</b>.
    /// وهذا ليس تساهلاً: البيع قبل إدخال الاستلام واقعة يومية، ومنعُها يمنع تسجيل
    /// الواقع فيلتفّ عليها المستخدم بمستند مخترَع.
    /// </summary>
    [Fact]
    public void الصرف_على_المكشوف_يُوسَم_ولا_يُخفى()
    {
        StockPosition position = WeightedAverageCost.Receive(WeightedAverageCost.Empty, 10m, 120.0000m).After;
        StockEffect issue = WeightedAverageCost.Issue(position, 15m);

        Assert.Equal(180.0000m, issue.Value);
        Assert.Equal(-5m, issue.After.Quantity);
        Assert.Equal(-60.0000m, issue.After.Value);
        Assert.True(issue.DrewOnNegativeStock);
    }

    /// <summary>
    /// التكلفة المتأخّرة: استلامٌ يهبط على رصيد سالب بسعر يخالف ما نُزِّل.
    /// <para>
    /// ‏15 وحدة نُزِّلت بـ12 (‏180) وتكلفتها الفعلية 10×12 + 5×14 = 190. فيبقى 10
    /// في المخزون على كمية صفر — وهو <b>بالضبط</b> ما نُقص من تكلفة المبيعات.
    /// والقيد المُرحَّل لا يُعاد كتابته (‏ADR-0002)، والفارق يظهر ولا يُبتلع.
    /// </para>
    /// </summary>
    [Fact]
    public void الاستلام_المتأخّر_يترك_الفارق_ظاهراً_على_كمية_صفر()
    {
        StockPosition afterFirstReceipt = WeightedAverageCost.Receive(WeightedAverageCost.Empty, 10m, 120.0000m).After;
        StockPosition afterIssue = WeightedAverageCost.Issue(afterFirstReceipt, 15m).After;
        StockEffect late = WeightedAverageCost.Receive(afterIssue, 5m, 70.0000m);

        Assert.Equal(0m, late.After.Quantity);
        Assert.Equal(10.0000m, late.After.Value);

        // والمتوسط لا يُعاد حسابه على كمية صفرية: القسمة على صفر ليست رقماً محاسبياً.
        Assert.Equal(12.000000m, late.After.UnitCost);
    }

    [Fact]
    public void رمز_الطريقة_ثابت_ولا_يُشتقّ()
        => Assert.Equal("moving_weighted_average", WeightedAverageCost.MethodCode);
}
