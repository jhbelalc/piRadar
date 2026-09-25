using PiRadar.Core.Receipts;

namespace PiRadar.Core.Tests.Receipts;

public class TaxIdTests
{
    [Theory]
    [InlineData("890900608", 9)]
    [InlineData("901137699", 5)]
    [InlineData("900319753", 3)]
    [InlineData("860076919", 1)]
    [InlineData("800130144", 1)]
    [InlineData("901962299", 0)]
    [InlineData("800244387", 4)]
    [InlineData("860351427", 7)]
    [InlineData("12345678", 8)]
    [InlineData("800242106", 2)]
    [InlineData("901784030", 3)]
    [InlineData("900335241", 1)]
    [InlineData("830112317", 1)]
    [InlineData("900980767", 9)]
    public void CheckDigit_MatchesPrintedNits(string baseNumber, int expected)
    {
        Assert.Equal(expected, TaxId.CheckDigit(baseNumber));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12A4")]
    public void CheckDigit_RejectsInvalidInput(string baseNumber)
    {
        Assert.Throws<ArgumentException>(() => TaxId.CheckDigit(baseNumber));
    }
}
