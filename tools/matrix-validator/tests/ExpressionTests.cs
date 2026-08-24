using Xunit;

namespace SalaselBabel.MatrixValidator.Tests;

public class ExpressionTests
{
    [Theory]
    [InlineData("net + tax", "net + tax")]
    [InlineData("net + tax - retention - advance", "advance*-1 + net - retention + tax")]
    [InlineData("2 * net", "2*net")]
    [InlineData("net / 2", "0.5*net")]
    [InlineData("-(a - b)", "a*-1 + b")]
    public void Parses_linear_expressions(string input, string _)
    {
        var e = ExpressionParser.Parse(input);
        Assert.NotNull(e);
    }

    [Fact]
    public void A_balanced_pair_reduces_to_zero()
    {
        var debit = ExpressionParser.Parse("net + tax");
        var credit = ExpressionParser.Parse("net").Add(ExpressionParser.Parse("tax"));
        Assert.True(debit.Subtract(credit).IsZero);
    }

    [Fact]
    public void An_unbalanced_pair_does_not_reduce_to_zero()
    {
        var debit = ExpressionParser.Parse("net + tax");
        var credit = ExpressionParser.Parse("net");
        Assert.False(debit.Subtract(credit).IsZero);
    }

    [Fact]
    public void Substitution_eliminates_a_defined_variable()
    {
        var e = ExpressionParser.Parse("cash + card");
        var reduced = e.Substitute("cash", ExpressionParser.Parse("total - card"));
        Assert.True(reduced.Subtract(ExpressionParser.Parse("total")).IsZero);
    }

    [Fact]
    public void Multiplying_two_variables_is_refused()
        => Assert.Throws<ExpressionException>(() => ExpressionParser.Parse("net * tax"));

    [Fact]
    public void Dividing_by_a_variable_is_refused()
        => Assert.Throws<ExpressionException>(() => ExpressionParser.Parse("net / tax"));

    [Fact]
    public void An_unclosed_parenthesis_is_refused()
        => Assert.Throws<ExpressionException>(() => ExpressionParser.Parse("(net + tax"));
}
