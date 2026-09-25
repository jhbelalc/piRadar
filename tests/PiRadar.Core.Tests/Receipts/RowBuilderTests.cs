using PiRadar.Core.Receipts;

namespace PiRadar.Core.Tests.Receipts;

public class RowBuilderTests
{
    [Fact]
    public void IosLineBoxes_AreJoinedInOriginalOrder_AndRowsSortedTopToBottom()
    {
        // Vision: las palabras de un tramo comparten recuadro; llegan por columnas, no en orden de lectura.
        OcrElement[] elements =
        [
            new("$82.150", 480, 858, 130, 30),
            new("TOTAL", 50, 860, 240, 28), new("A", 50, 860, 240, 28), new("PAGAR", 50, 860, 240, 28),
            new("SUBTOTAL", 50, 762, 150, 28),
            new("69.294", 500, 760, 110, 30),
        ];

        var rows = RowBuilder.Build(elements);

        Assert.Equal(["SUBTOTAL 69.294", "TOTAL A PAGAR $82.150"], rows.Select(r => r.Text));
    }

    [Fact]
    public void AndroidWordBoxes_OnASlantedPage_StayOnTheirRow()
    {
        // Cada palabra con su recuadro; la línea baja 4 px por palabra y la siguiente línea está 30 px abajo.
        OcrElement[] elements =
        [
            new("VALOR", 50, 100, 80, 24), new("TOTAL", 140, 104, 80, 24), new("28.000", 400, 112, 90, 24),
            new("FORMA", 50, 130, 80, 24), new("PAGO", 140, 134, 70, 24), new("CONTADO", 400, 142, 110, 24),
        ];

        var rows = RowBuilder.Build(elements.Reverse());

        Assert.Equal(["VALOR TOTAL 28.000", "FORMA PAGO CONTADO"], rows.Select(r => r.Text));
    }

    [Fact]
    public void ChunksThatOverlapHorizontally_AreDifferentRows()
    {
        // Dos líneas muy juntas (interlineado apretado) no se mezclan aunque sus recuadros se toquen.
        OcrElement[] elements =
        [
            new("SUBTOTAL 53.362", 50, 100, 300, 20),
            new("IVA 19% 10.138", 50, 112, 300, 20),
        ];

        var rows = RowBuilder.Build(elements);

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void TallerChunks_JoinARowWhenTheyShareMostOfTheSmallerHeight()
    {
        // "TOTAL" impreso en letra grande junto a un monto normal.
        OcrElement[] elements = [new("TOTAL", 50, 100, 120, 44), new("56.378", 400, 110, 90, 24)];

        Assert.Equal("TOTAL 56.378", Assert.Single(RowBuilder.Build(elements)).Text);
    }

    [Fact]
    public void Empty_ReturnsNoRows()
    {
        Assert.Empty(RowBuilder.Build([]));
    }
}
