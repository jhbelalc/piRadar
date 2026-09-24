using PiRadar.Core.Domain;

namespace PiRadar.Core.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData(82150, Currency.COP, 82150)]
    [InlineData(12.5, Currency.USD, 1250)]
    [InlineData(0.01, Currency.USD, 1)]
    public void FromDecimal_StoresMinorUnits(decimal amount, Currency currency, long expectedMinor)
    {
        Assert.Equal(expectedMinor, Money.FromDecimal(amount, currency).AmountMinor);
    }

    [Theory]
    [InlineData(10.5, Currency.COP)]
    [InlineData(1.001, Currency.USD)]
    public void FromDecimal_RejectsExtraDecimals(decimal amount, Currency currency)
    {
        Assert.Throws<ArgumentException>(() => Money.FromDecimal(amount, currency));
    }

    [Fact]
    public void ToDecimal_RoundTrips()
    {
        Assert.Equal(12.50m, new Money(1250, Currency.USD).ToDecimal());
        Assert.Equal(82150m, new Money(82150, Currency.COP).ToDecimal());
    }

    [Theory]
    [InlineData(1234567, Currency.COP, "$ 1.234.567")]
    [InlineData(1250, Currency.USD, "US$ 12,50")]
    [InlineData(-82150, Currency.COP, "-$ 82.150")]
    [InlineData(0, Currency.USD, "US$ 0,00")]
    public void ToString_UsesColombianFormat(long minor, Currency currency, string expected)
    {
        Assert.Equal(expected, new Money(minor, currency).ToString());
    }

    [Fact]
    public void Arithmetic_WorksWithinSameCurrency()
    {
        var a = new Money(1000, Currency.COP);
        var b = new Money(250, Currency.COP);

        Assert.Equal(new Money(1250, Currency.COP), a + b);
        Assert.Equal(new Money(750, Currency.COP), a - b);
        Assert.Equal(new Money(-1000, Currency.COP), -a);
    }

    [Fact]
    public void Arithmetic_RejectsMixedCurrencies()
    {
        Assert.Throws<InvalidOperationException>(() => new Money(1, Currency.COP) + new Money(1, Currency.USD));
    }

    [Fact]
    public void Addition_ThrowsOnOverflow()
    {
        Assert.Throws<OverflowException>(() => new Money(long.MaxValue, Currency.COP) + new Money(1, Currency.COP));
    }
}
