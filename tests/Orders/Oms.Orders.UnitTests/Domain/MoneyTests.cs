using Oms.Orders.Domain;
using Oms.Orders.Domain.Errors;
using Xunit;

namespace Oms.Orders.UnitTests.Domain;

public sealed class MoneyTests
{
    [Fact]
    public void CreateWhenCurrencyHasLowercaseAndPaddingShouldSucceedAndNormaliseToThreeLetterUppercase()
    {
        var result = Money.Create(19.99m, " gbp ");

        Assert.True(result.IsSuccess);
        Assert.Equal(19.99m, result.Value.Amount);
        Assert.Equal("GBP", result.Value.Currency);
    }

    [Fact]
    public void CreateWhenAmountIsZeroShouldSucceed()
    {
        var result = Money.Create(0m, "gbp");

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value.Amount);
        Assert.Equal("GBP", result.Value.Currency);
        Assert.Equal(result.Value, Money.Zero("GBP"));
    }

    [Fact]
    public void CreateWhenAmountIsNegativeShouldReturnNegativeAmountError()
    {
        var result = Money.Create(-0.01m, "GBP");

        Assert.True(result.IsFailure);
        Assert.Equal(MoneyErrors.NegativeAmount.Code, result.Error.Code);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("GB")]
    [InlineData("GBPP")]
    [InlineData("G1P")]
    [InlineData("123")]
    [InlineData("£££")]
    public void CreateWhenCurrencyIsNullOrInvalidShouldReturnInvalidCurrencyError(string? invalidCurrency)
    {
        var result = Money.Create(10.00m, invalidCurrency);

        Assert.True(result.IsFailure);
        Assert.Equal(MoneyErrors.InvalidCurrency.Code, result.Error.Code);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void EqualityWhenAmountAndNormalisedCurrencyMatchShouldBeEqualByValue()
    {
        var first = Money.Create(25.50m, "eur").Value;
        var second = Money.Create(25.50m, "EUR").Value;
        var differentAmount = Money.Create(25.51m, "EUR").Value;
        var differentCurrency = Money.Create(25.50m, "USD").Value;

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, differentAmount);
        Assert.NotEqual(first, differentCurrency);
    }

    [Fact]
    public void AddAndMultiplyWhenCurrenciesMatchShouldPreserveExactDecimalPrecision()
    {
        var unitPrice = Money.Create(10.125m, "GBP").Value;
        var lineTotal = unitPrice.Multiply(3);
        var combined = lineTotal.Add(Money.Create(0.625m, "gbp").Value);

        Assert.Equal(Money.Create(30.375m, "GBP").Value, lineTotal);
        Assert.Equal(Money.Create(31.000m, "GBP").Value, combined);
        Assert.Equal(31.000m, (unitPrice * 3 + Money.Create(0.625m, "GBP").Value).Amount);
        Assert.Equal(lineTotal, 3 * unitPrice);
    }

    [Fact]
    public void AddWhenCurrenciesDifferShouldThrowInvalidOperationException()
    {
        var gbp = Money.Create(10.00m, "GBP").Value;
        var eur = Money.Create(10.00m, "EUR").Value;

        Assert.Throws<InvalidOperationException>(() => gbp.Add(eur));
        Assert.Throws<InvalidOperationException>(() => _ = gbp + eur);
    }

    [Fact]
    public void MultiplyWhenFactorIsNegativeShouldThrowArgumentOutOfRangeException()
    {
        var price = Money.Create(10.00m, "GBP").Value;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => price.Multiply(-1));

        Assert.Equal("factor", exception.ParamName);
    }

    [Fact]
    public void ArithmeticOnDefaultUninitialisedMoneyShouldThrowInvalidOperationException()
    {
        var uninitialised = default(Money);
        var valid = Money.Create(5.00m, "GBP").Value;

        Assert.False(uninitialised.IsInitialised);
        Assert.Throws<InvalidOperationException>(() => uninitialised.Add(valid));
        Assert.Throws<InvalidOperationException>(() => valid.Add(uninitialised));
        Assert.Throws<InvalidOperationException>(() => uninitialised.Multiply(2));
    }
}
