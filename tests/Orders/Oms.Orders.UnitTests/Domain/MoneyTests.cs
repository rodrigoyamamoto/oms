using Oms.Orders.Domain;
using Xunit;

namespace Oms.Orders.UnitTests.Domain;

public sealed class MoneyTests
{
    [Fact]
    public void ConstructorWhenCurrencyHasLowercaseAndPaddingShouldNormaliseToThreeLetterUppercase()
    {
        var money = new Money(19.99m, " gbp ");

        Assert.Equal(19.99m, money.Amount);
        Assert.Equal("GBP", money.Currency);
    }

    [Fact]
    public void EqualityWhenAmountAndNormalisedCurrencyMatchShouldBeEqualByValue()
    {
        var first = new Money(25.50m, "eur");
        var second = new Money(25.50m, "EUR");
        var differentAmount = new Money(25.51m, "EUR");
        var differentCurrency = new Money(25.50m, "USD");

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, differentAmount);
        Assert.NotEqual(first, differentCurrency);
    }

    [Fact]
    public void ZeroWhenCreatedWithValidCurrencyShouldRepresentZeroAmount()
    {
        var zero = Money.Zero("gbp");

        Assert.Equal(0m, zero.Amount);
        Assert.Equal("GBP", zero.Currency);
    }

    [Fact]
    public void ConstructorWhenAmountIsNegativeShouldThrowArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Money(-0.01m, "GBP"));

        Assert.Equal("amount", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("GB")]
    [InlineData("GBPP")]
    [InlineData("G1P")]
    [InlineData("123")]
    [InlineData("£££")]
    public void ConstructorWhenCurrencyIsInvalidShouldThrowArgumentException(string invalidCurrency)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Money(10.00m, invalidCurrency));
    }

    [Fact]
    public void ConstructorWhenCurrencyIsNullShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Money(10.00m, null!));
    }

    [Fact]
    public void AddAndMultiplyWhenCurrenciesMatchShouldPreserveExactDecimalPrecision()
    {
        var unitPrice = new Money(10.125m, "GBP");
        var lineTotal = unitPrice.Multiply(3);
        var combined = lineTotal.Add(new Money(0.625m, "gbp"));

        Assert.Equal(new Money(30.375m, "GBP"), lineTotal);
        Assert.Equal(new Money(31.000m, "GBP"), combined);
        Assert.Equal(31.000m, (unitPrice * 3 + new Money(0.625m, "GBP")).Amount);
        Assert.Equal(lineTotal, 3 * unitPrice);
    }

    [Fact]
    public void AddWhenCurrenciesDifferShouldThrowInvalidOperationException()
    {
        var gbp = new Money(10.00m, "GBP");
        var eur = new Money(10.00m, "EUR");

        Assert.Throws<InvalidOperationException>(() => gbp.Add(eur));
        Assert.Throws<InvalidOperationException>(() => _ = gbp + eur);
    }

    [Fact]
    public void MultiplyWhenFactorIsNegativeShouldThrowArgumentOutOfRangeException()
    {
        var price = new Money(10.00m, "GBP");

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => price.Multiply(-1));

        Assert.Equal("factor", exception.ParamName);
    }

    [Fact]
    public void ArithmeticOnDefaultUninitialisedMoneyShouldThrowInvalidOperationException()
    {
        var uninitialised = default(Money);
        var valid = new Money(5.00m, "GBP");

        Assert.Throws<InvalidOperationException>(() => uninitialised.Add(valid));
        Assert.Throws<InvalidOperationException>(() => valid.Add(uninitialised));
        Assert.Throws<InvalidOperationException>(() => uninitialised.Multiply(2));
    }
}
