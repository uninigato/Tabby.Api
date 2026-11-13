using Tabby.Api.Services;

namespace Tabby.Api.Tests;

public class SplitCalculatorTests
{
    [Fact]
    public void TenAcrossThree_GivesTheLeftoverCentToTheFirstPerson()
    {
        var result = SplitCalculator.EqualSplit(10.00m, 3);

        Assert.Equal(new[] { 3.34m, 3.33m, 3.33m }, result);
    }

    [Fact]
    public void EvenlyDivisibleTotal_SplitsExactly()
    {
        var result = SplitCalculator.EqualSplit(10.00m, 2);

        Assert.Equal(new[] { 5.00m, 5.00m }, result);
    }

    [Fact]
    public void OnePerson_GetsTheWholeAmount()
    {
        var result = SplitCalculator.EqualSplit(42.50m, 1);

        Assert.Equal(new[] { 42.50m }, result);
    }

    [Fact]
    public void OneCentAcrossThree_OnlyTheFirstPersonPays()
    {
        var result = SplitCalculator.EqualSplit(0.01m, 3);

        Assert.Equal(new[] { 0.01m, 0.00m, 0.00m }, result);
    }

    [Theory]
    [InlineData(10.00, 3)]
    [InlineData(100.00, 7)]
    [InlineData(0.99, 4)]
    [InlineData(59.97, 6)]
    [InlineData(1234.56, 5)]
    public void Shares_AlwaysAddUpToExactlyTheTotal(double totalValue, int people)
    {
        // Attributes can't hold decimal values, so they arrive as double
        var total = (decimal)totalValue;

        var result = SplitCalculator.EqualSplit(total, people);

        Assert.Equal(people, result.Length);
        Assert.Equal(total, result.Sum());
    }

    [Theory]
    [InlineData(10.00, 3)]
    [InlineData(100.00, 7)]
    [InlineData(59.97, 6)]
    public void NoTwoShares_DifferByMoreThanOneCent(double totalValue, int people)
    {
        var result = SplitCalculator.EqualSplit((decimal)totalValue, people);

        Assert.True(result.Max() - result.Min() <= 0.01m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FewerThanOnePerson_Throws(int people)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SplitCalculator.EqualSplit(10.00m, people));
    }
}