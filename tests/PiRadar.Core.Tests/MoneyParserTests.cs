using PiRadar.Core.Domain;

namespace PiRadar.Core.Tests;

public class MoneyParserTests
{
    [Theory]
    [InlineData("82150", Currency.COP, 82150)]
    [InlineData("82.150", Currency.COP, 82150)]
    [InlineData("1.234.567", Currency.COP, 1234567)]
    [InlineData("$ 1.234.567", Currency.COP, 1234567)]
    [InlineData("12.500,00", Currency.COP, 12500)]
    [InlineData(" 7 500 ", Currency.COP, 7500)]
    [InlineData("0", Currency.COP, 0)]
    [InlineData("12,50", Currency.USD, 1250)]
    [InlineData("12.50", Currency.USD, 1250)]
    [InlineData("12,5", Currency.USD, 1250)]
    [InlineData("US$ 1.234,56", Currency.USD, 123456)]
    [InlineData("1,234.56", Currency.USD, 123456)]
    [InlineData("12", Currency.USD, 1200)]
    [InlineData(",99", Currency.USD, 99)]
    public void TryParse_AcceptsColombianAndTolerantFormats(string text, Currency currency, long expectedMinor)
    {
        Assert.True(MoneyParser.TryParse(text, currency, out var money, out var error), error);
        Assert.Equal(new Money(expectedMinor, currency), money);
    }

    [Theory]
    [InlineData("", "Ingresa un monto.")]
    [InlineData("  $ ", "Ingresa un monto.")]
    [InlineData("abc", "El monto no es válido.")]
    [InlineData("-500", "El monto no es válido.")]
    [InlineData("1.23.456", "El monto no es válido.")]
    [InlineData("1.2345", "Los pesos no llevan centavos.")]
    [InlineData("1,234,56.7", "El monto no es válido.")]
    [InlineData("5,", "El monto no es válido.")]
    [InlineData("12,50", "Los pesos no llevan centavos.")]
    [InlineData("99999999999999", "El monto no es válido.")]
    public void TryParse_RejectsInvalidCop(string text, string expectedError)
    {
        Assert.False(MoneyParser.TryParse(text, Currency.COP, out _, out var error));
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public void TryParse_RejectsExtraUsdDecimals()
    {
        Assert.False(MoneyParser.TryParse("1,234.567", Currency.USD, out _, out var error));
        Assert.Equal("Máximo 2 decimales.", error);
    }

    [Theory]
    [InlineData("4.150,25", 4150.25)]
    [InlineData("4150.25", 4150.25)]
    [InlineData("4150,257", 4150.257)]
    [InlineData("0,000241", 0.000241)]
    [InlineData("0.500", 0.5)]
    [InlineData("4.150", 4150)]
    public void TryParseDecimal_ReadsRates(string text, decimal expected)
    {
        Assert.True(MoneyParser.TryParseDecimal(text, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(1234567, Currency.COP, "1.234.567")]
    [InlineData(1250, Currency.USD, "12,50")]
    [InlineData(0, Currency.USD, "0,00")]
    public void FormatForInput_OmitsSymbolAndRoundTrips(long minor, Currency currency, string expected)
    {
        var money = new Money(minor, currency);
        var text = MoneyParser.FormatForInput(money);

        Assert.Equal(expected, text);
        Assert.True(MoneyParser.TryParse(text, currency, out var parsed, out _));
        Assert.Equal(money, parsed);
    }

    [Theory]
    [InlineData(4150.25, 0, "4.150,25")]
    [InlineData(4150, 0, "4.150")]
    [InlineData(0.000241, 0, "0,000241")]
    [InlineData(4150, 2, "4.150,00")]
    public void FormatDecimal_KeepsSignificantDecimals(decimal value, int minDecimals, string expected)
    {
        Assert.Equal(expected, MoneyParser.FormatDecimal(value, minDecimals));
    }
}
