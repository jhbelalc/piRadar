using PiRadar.Core.Receipts;

namespace PiRadar.Core.Tests.Receipts;

public class MerchantSuggestionsTests
{
    [Fact]
    public void Combine_PutsRememberedFirstWithoutDuplicates()
    {
        Assert.Equal(
            ["Éxito Laureles", "EXITO", "Almacenes Éxito"],
            MerchantSuggestions.Combine("Éxito Laureles", ["EXITO", "Almacenes Éxito", " éxito laureles "]));
    }

    [Fact]
    public void Combine_WithoutMemory_KeepsParserOrderAndSkipsBlanks()
    {
        Assert.Equal(["D1", "Koba Colombia"], MerchantSuggestions.Combine(null, ["D1", " ", "Koba Colombia"]));
    }
}
