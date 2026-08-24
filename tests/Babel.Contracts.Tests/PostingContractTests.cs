using Babel.Contracts.Posting;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Contracts.Tests;

/// <summary>عقد الترحيل: ما فيه، والأهم — ما ليس فيه.</summary>
public sealed class PostingContractTests
{
    [Fact]
    public void PostingLine_DescribesARoleNotAnAccount()
    {
        // القاعدة 2 من زاوية الاستعمال: الوحدة تصف الحدث، والمصفوفة تختار الحساب.
        PostingLine line = new()
        {
            Role = PostingRole.OutputTax,
            Side = PostingSide.Credit,
            Amount = Money.Of(150m, CurrencyCode.Sar),
            Subledger = new SubledgerReference(SubledgerKind.Customer, "CUST-001"),
        };

        Assert.Equal(PostingRole.OutputTax, line.Role);
        Assert.DoesNotContain(
            typeof(PostingLine).GetProperties(),
            property => property.Name.Contains("Account", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PostingRequest_RequiresAnIdempotencyKey()
    {
        // القاعدة المعمارية 4: الحصانة لكل قيد ومستقلة عن الترتيب.
        Assert.Contains(
            typeof(PostingRequest).GetProperties(),
            property => property.Name == nameof(PostingRequest.IdempotencyKey) && property.PropertyType == typeof(IdempotencyKey));
    }
}
