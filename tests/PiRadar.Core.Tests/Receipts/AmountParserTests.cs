using PiRadar.Core.Receipts;

namespace PiRadar.Core.Tests.Receipts;

public class AmountParserTests
{
    [Theory]
    [InlineData("TOTAL A PAGAR $82.150", 82150)]
    [InlineData("Total $ 63.500", 63500)]
    [InlineData("TOTAL 47,700", 47700)]
    [InlineData("TOTAL COP 50900", 50900)]
    [InlineData("TOTAL 1.234.567", 1234567)]
    [InlineData("SUBT C/DTO:$13,271.00", 13271)]
    [InlineData("DEBITO MASTER 527493 $8,580.00", 8580)]
    [InlineData("Total 1.234,56", 1234.56)]
    [InlineData("Order Total $264.18", 264.18)]
    [InlineData("TOTAL: $14,900.000", 14900)]
    [InlineData("$82. 150", 82150)]
    [InlineData("VALOR TOTAL 28.OOO", 28000)]
    [InlineData("Rosc%n Guayaba G 6 250", 6250)]
    public void Find_ReadsLastAmount(string row, double expected)
    {
        var amounts = AmountParser.Find(row);

        Assert.NotEmpty(amounts);
        Assert.Equal((decimal)expected, amounts[^1].Value);
    }

    [Theory]
    [InlineData("TEL 3154277266")]
    [InlineData("NUMERO: RJ49175108")]
    [InlineData("Comprobante de Entrega: 0208")]
    [InlineData("SIN MONTOS")]
    public void Find_IgnoresCodesAndPhones(string row)
    {
        Assert.Empty(AmountParser.Find(row));
    }

    [Fact]
    public void Find_ReturnsAllAmountsInOrder()
    {
        var amounts = AmountParser.Find("TOTAL 93,400 84,034 9,366");

        Assert.Equal([93400m, 84034m, 9366m], amounts.Select(a => a.Value));
    }

    [Fact]
    public void Find_FlagsCurrencySymbolAndDecimals()
    {
        var amounts = AmountParser.Find("IVA 19.00% : $1,629.00");

        var last = amounts[^1];
        Assert.Equal(1629m, last.Value);
        Assert.True(last.HasCurrencySymbol);
        Assert.True(last.HasDecimals);
    }
}
