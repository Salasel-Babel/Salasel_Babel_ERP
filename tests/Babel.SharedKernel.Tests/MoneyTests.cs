using Xunit;

namespace Babel.SharedKernel.Tests;

/// <summary>
/// <see cref="Money"/> نوع قيمة، وقيمته كلها في ما يمنعه.
/// </summary>
public sealed class MoneyTests
{
    [Fact]
    public void Of_NormalisesToTheCanonicalScaleOfFour()
    {
        // §8.2 مصيدة 2: 100.00m و100.0000m القيمة نفسها ببايتات مقياس مختلفة،
        // فتختلف البصمة. تثبيت المقياس في النوع يقطع المسار كله.
        Money money = Money.Of(100.00m, CurrencyCode.Sar);

        Assert.Equal("100.0000", money.ToCanonicalString());
        Assert.Equal(decimal.GetBits(100.0000m)[3], decimal.GetBits(money.Amount)[3]);
    }

    [Fact]
    public void Of_RejectsMoreThanFourDecimalPlaces()
    {
        // التقريب قرار محاسبي صريح على مستوى السطر، لا سلوك ضمني في نوع قيمة
        // (03-accounting-core.md §5).
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.Of(1.00005m, CurrencyCode.Sar));
    }

    [Fact]
    public void ToCanonicalString_IsCultureInvariant()
    {
        // §8.2 مصيدة 3: جهاز واحد بـLC_NUMERIC مختلف ينتج سلسلة غير قابلة للتحقق، بصمت.
        System.Globalization.CultureInfo previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("1234.5600", Money.Of(1234.56m, CurrencyCode.Sar).ToCanonicalString());
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Add_RejectsMixedCurrenciesWithoutAnExplicitRate()
    {
        Money sar = Money.Of(10m, CurrencyCode.Sar);
        Money usd = Money.Of(10m, CurrencyCode.FromString("USD"));

        Assert.Throws<InvalidOperationException>(() => sar.Add(usd));
    }

    [Fact]
    public void Arithmetic_KeepsTheCanonicalScale()
    {
        Money total = Money.Of(0.1m, CurrencyCode.Sar) + Money.Of(0.2m, CurrencyCode.Sar);

        Assert.Equal("0.3000", total.ToCanonicalString());
    }
}
