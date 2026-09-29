using PiRadar.Core.Domain;

namespace PiRadar.Core.Tests;

public class HomeTitleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t ")]
    public void Normalize_Empty_MeansDefault(string? text) => Assert.Null(HomeTitle.Normalize(text));

    [Fact]
    public void Normalize_TrimsAndCollapsesSpaces() =>
        Assert.Equal("Mis finanzas", HomeTitle.Normalize("  Mis \t  finanzas \n"));

    [Fact]
    public void Normalize_KeepsUpToMaxLength()
    {
        var fifty = new string('a', HomeTitle.MaxLength);

        Assert.Equal(fifty, HomeTitle.Normalize(fifty));
        Assert.Equal(fifty, HomeTitle.Normalize(fifty + "bcd"));
    }

    [Fact]
    public void Normalize_CutsWithoutSplittingAnEmoji()
    {
        // 49 letras + un emoji de 2 unidades UTF-16: no cabe entero, así que se quita.
        var text = new string('a', HomeTitle.MaxLength - 1) + "💰";

        Assert.Equal(new string('a', HomeTitle.MaxLength - 1), HomeTitle.Normalize(text));
    }

    [Fact]
    public void Normalize_UsesComposedAccents()
    {
        // "é" como "e" + tilde combinada (algunos teclados) cuenta como un carácter.
        var decomposed = "Mi cartera de café";

        Assert.Equal("Mi cartera de café", HomeTitle.Normalize(decomposed));
    }

    [Fact]
    public void Default_FitsMaxLength() => Assert.True(HomeTitle.Default.Length <= HomeTitle.MaxLength);
}
