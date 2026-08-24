using Xunit;

namespace Babel.SharedKernel.Tests;

/// <summary>أنواع القيمة الباقية: ما ترفضه هو ما يجعلها مفيدة.</summary>
public sealed class ValueTypeTests
{
    [Fact]
    public void LocalizedName_RequiresBothLanguages()
    {
        // CONTRIBUTING §3 بند 5: لا جدول ترجمات منفصل يُنسى ملؤه.
        Assert.Throws<ArgumentException>(() => new LocalizedName("مبيعات", string.Empty));
        Assert.Throws<ArgumentException>(() => new LocalizedName(" ", "Sales"));
    }

    [Fact]
    public void IdempotencyKey_RejectsNonAsciiCharacters()
    {
        // المفتاح يدخل مفتاحاً أساسياً ويُجزَّأ: أرقام ومحارف ASCII فقط (§8.3 ع-3).
        Assert.Throws<ArgumentException>(() => new IdempotencyKey("فاتورة-١٢٣"));
        Assert.Equal("SalesInvoice:2026-08-24:00017", new IdempotencyKey("SalesInvoice:2026-08-24:00017").Value);
    }

    [Fact]
    public void CurrencyCode_RequiresThreeUpperCaseLatinLetters()
    {
        Assert.Throws<ArgumentException>(() => CurrencyCode.FromString("sar"));
        Assert.Throws<ArgumentException>(() => CurrencyCode.FromString("SARX"));
        Assert.Equal("SAR", CurrencyCode.Sar.Value);
    }

    [Fact]
    public void BillingPeriod_DerivesFromUtc()
    {
        BillingPeriod period = BillingPeriod.FromInstant(new DateTimeOffset(2026, 8, 24, 3, 0, 0, TimeSpan.FromHours(3)));

        Assert.Equal(new BillingPeriod(2026, 8), period);
        Assert.Equal("2026-08", period.ToString());
    }

    [Fact]
    public void Result_CarriesErrorsOnFailureAndNoneOnSuccess()
    {
        Result success = Result.Success();
        Result failure = Result.Failure(new Error("x.y", "خطأ", "error"));

        Assert.True(success.IsSuccess);
        Assert.Empty(success.Errors);
        Assert.True(failure.IsFailure);
        Assert.Single(failure.Errors);
        Assert.Throws<ArgumentException>(() => Result.Failure(Array.Empty<Error>()));
    }
}
