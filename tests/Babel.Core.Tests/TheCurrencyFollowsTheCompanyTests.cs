using Babel.Core.CompanySetup;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Core.Tests;

/// <summary>
/// <b>العملةُ ووحدتُها الصغرى تتبعان المنشأة لا الشيفرة (ADR-0089).</b>
/// <para>
/// ما يُثبَت هنا: لا ريالَ يُخترَع عند غياب العملة، ولا عددُ خاناتٍ يُخمَّن لعملةٍ خارج
/// الجدول، والتقريبُ يتبع الوحدة الصغرى المخزَّنة — الفلسُ الكويتي ثلاثٌ والينُ صفر —
/// والمُحلّل يحفظ الجواب الناجح ولا يحفظ الرفض.
/// </para>
/// </summary>
public sealed class TheCurrencyFollowsTheCompanyTests
{
    private static readonly TenantId Company = new(new Guid("c0ffee00-0000-4000-8000-000000000089"));

    [Fact]
    public void غياب_العملة_يُرفض_باسمه_ولا_يُخترَع_ريال()
    {
        Result<CompanyMoney> missing = CompanyMoney.Of(null);
        Assert.True(missing.IsFailure);
        Assert.Equal("company_setup.currency_missing", Assert.Single(missing.Errors).Code);

        Result<CompanyMoney> blank = CompanyMoney.Of("   ");
        Assert.Equal("company_setup.currency_missing", Assert.Single(blank.Errors).Code);
    }

    [Theory]
    [InlineData("sar")]
    [InlineData("SA")]
    [InlineData("SAR1")]
    [InlineData("ر.س")]
    public void رمزٌ_مشوَّه_يُرفض_بشكله(string code)
    {
        Result<CompanyMoney> refused = CompanyMoney.Of(code);
        Assert.Equal("company_setup.currency_malformed", Assert.Single(refused.Errors).Code);
    }

    [Fact]
    public void عملةٌ_سليمة_الشكل_خارج_الجدول_تُرفض_ولا_يُخمَّن_لها_عددُ_خانات()
    {
        Result<CompanyMoney> refused = CompanyMoney.Of("XXX");
        Error error = Assert.Single(refused.Errors);
        Assert.Equal("company_setup.currency_not_in_reference_table", error.Code);
        // والعلاج مُسمّى في الرسالة: موضع الجدول لا «اتّصل بالدعم».
        Assert.Contains("Iso4217", error.MessageAr, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SAR", 2, "1.2345", "1.23")]
    [InlineData("SAR", 2, "1.2350", "1.24")]
    [InlineData("KWD", 3, "1.2345", "1.235")]
    [InlineData("BHD", 3, "0.0005", "0.001")]
    [InlineData("JPY", 0, "1234.5", "1235")]
    [InlineData("JPY", 0, "-1234.5", "-1235")]
    public void التقريبُ_يتبع_الوحدة_الصغرى_من_الجدول_والنصفُ_يبتعد_عن_الصفر(string code, int units, string value, string expected)
    {
        CompanyMoney money = CompanyMoney.Of(code).Value;
        Assert.Equal(units, money.MinorUnits);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), money.Round(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(code, money.Currency.Value);
    }

    [Fact]
    public void الجدولُ_المرجعي_لا_يتجاوز_مقياسَ_التخزين_ولا_يخلو_من_عملات_الخليج()
    {
        Assert.NotEmpty(Iso4217.KnownCodes);
        Assert.Equal(Iso4217.KnownCodes.Order(StringComparer.Ordinal), Iso4217.KnownCodes);

        foreach (string code in Iso4217.KnownCodes)
        {
            int? units = Iso4217.MinorUnitsOf(CurrencyCode.FromString(code));
            Assert.NotNull(units);
            Assert.InRange(units.Value, 0, Money.CanonicalScale);
        }

        foreach (string gulf in new[] { "SAR", "AED", "QAR", "KWD", "BHD", "OMR" })
        {
            Assert.True(Iso4217.IsKnown(CurrencyCode.FromString(gulf)), gulf);
        }

        Assert.Null(Iso4217.MinorUnitsOf(default));
    }

    [Fact]
    public void التأسيسُ_يرفض_غيابَ_العملة_مع_بقيّة_الأسباب_مجتمعةً_ويحمل_وحدتَها_الصغرى_حين_تصحّ()
    {
        Result<FoundedCompany> refused = FoundedCompany.Found(
            Company,
            new CompanySetupDraft("منشأة", null, CostCenterPlan.One, null, null, 9, null));

        Assert.True(refused.IsFailure);
        Assert.Contains(refused.Errors, static e => e.Code == "company_setup.currency_missing");
        Assert.Contains(refused.Errors, static e => e.Code == "company_setup.decimal_places_out_of_range");

        Result<FoundedCompany> founded = FoundedCompany.Found(
            Company,
            new CompanySetupDraft("شركة الخليج", null, CostCenterPlan.One, null, null, 3, " kwd ".ToUpperInvariant()));

        Assert.True(founded.IsSuccess, string.Join(" | ", founded.Errors.Select(static e => e.ToString())));
        Assert.Equal("KWD", founded.Value.Money.Currency.Value);
        Assert.Equal(3, founded.Value.Money.MinorUnits);

        // والاشتقاق بمراكز تكلفةٍ جديدة ينسخ العملة ولا يقبل غيرها — لا توقيع في الشجرة يحمل عملةً ثانية.
        FoundedCompany derived = founded.Value.WithCostCenters(founded.Value.CostCenters);
        Assert.Equal(founded.Value.Money, derived.Money);
    }

    [Fact]
    public async Task المُحلّل_يرفض_منشأةً_لم_تُؤسَّس_ثم_يجيب_بعد_تأسيسها_ويحفظ_الجواب()
    {
        InMemoryCompanySetupStore store = new();
        CompanyMoneyResolver resolver = new(store);

        Result<CompanyMoney> before = await resolver.ResolveAsync(Company, TestContext.Current.CancellationToken);
        Assert.Equal("company_setup.not_found", Assert.Single(before.Errors).Code);

        FoundedCompany founded = FoundedCompany.Found(
            Company,
            new CompanySetupDraft("منشأة", null, CostCenterPlan.One, null, null, 2, "SAR")).Value;
        Assert.True(await store.TryFoundAsync(founded, TestContext.Current.CancellationToken));

        Result<CompanyMoney> after = await resolver.ResolveAsync(Company, TestContext.Current.CancellationToken);
        Assert.True(after.IsSuccess);
        Assert.Equal("SAR", after.Value.Currency.Value);
        Assert.Equal(2, after.Value.MinorUnits);

        // الجوابُ الناجح محفوظ: قراءةٌ ثانية لا تعود إلى المخزن (المخزنُ يُستبدل ولا يتغيّر الجواب).
        Result<CompanyMoney> cached = await new CompanyMoneyResolverProbe(resolver).ResolveAsync(Company);
        Assert.Equal(after.Value, cached.Value);
    }

    /// <summary>مسبارٌ يعيد النداء على المُحلّل نفسه — الحفظ داخله لا هنا.</summary>
    private sealed class CompanyMoneyResolverProbe(CompanyMoneyResolver resolver)
    {
        public ValueTask<Result<CompanyMoney>> ResolveAsync(TenantId company) => resolver.ResolveAsync(company);
    }
}
