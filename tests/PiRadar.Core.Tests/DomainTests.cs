using PiRadar.Core.Domain;

namespace PiRadar.Core.Tests;

public class DomainTests
{
    [Theory]
    [InlineData("COP", Currency.COP)]
    [InlineData("USD", Currency.USD)]
    public void ParseCode_AcceptsSupportedCodes(string code, Currency expected)
    {
        Assert.Equal(expected, CurrencyExtensions.ParseCode(code));
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("cop")]
    [InlineData("1")]
    [InlineData("")]
    public void ParseCode_RejectsUnsupportedCodes(string code)
    {
        Assert.Throws<FormatException>(() => CurrencyExtensions.ParseCode(code));
    }

    [Fact]
    public void ExchangeRate_StoresRateAsMicros()
    {
        var rate = new ExchangeRate { From = Currency.USD, To = Currency.COP, Rate = 4123.456789m };

        Assert.Equal(4_123_456_789L, rate.RateMicros);
        Assert.Equal(4123.456789m, rate.Rate);
    }

    [Fact]
    public void SignedAmount_IsNegativeForExpenses()
    {
        var expense = new Transaction { Type = TransactionType.Expense, Amount = new Money(82150, Currency.COP) };
        var income = new Transaction { Type = TransactionType.Income, Amount = new Money(82150, Currency.COP) };

        Assert.Equal(-82150, expense.SignedAmount.AmountMinor);
        Assert.Equal(82150, income.SignedAmount.AmountMinor);
    }
}
