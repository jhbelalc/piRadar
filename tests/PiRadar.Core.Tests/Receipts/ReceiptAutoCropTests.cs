using System.Text.Json;
using PiRadar.Core.Images;
using PiRadar.Core.Receipts;

namespace PiRadar.Core.Tests.Receipts;

public class ReceiptAutoCropTests
{
    [Fact]
    public void NoText_KeepsTheWholePhoto()
    {
        Assert.Equal(CropRect.Full, ReceiptAutoCrop.Suggest([], 1000, 2000));
    }

    [Fact]
    public void Frame_WrapsTheReceiptText_WithAMargin()
    {
        // Recibo en el centro de una foto de 1000 × 2000; texto de 20 px de alto.
        var receipt = Lines(left: 300, top: 600, rows: 20, width: 400);

        var crop = ReceiptAutoCrop.Suggest(receipt, 1000, 2000);

        Assert.InRange(crop.Left, 0.2, 0.3);
        Assert.InRange(crop.Right, 0.7, 0.8);
        Assert.InRange(crop.Top, 0.25, 0.3);
        Assert.InRange(crop.Bottom, 0.6, 0.65);
        Assert.All(receipt, e => Assert.True(Inside(crop, e, 1000, 2000)));
    }

    [Fact]
    public void TextOnAnotherPaperAside_DoesNotStretchTheFrame()
    {
        // Otro papel a la derecha con unas pocas palabras, lejos del recibo (foto horizontal de 3000 × 2000).
        var receipt = Lines(left: 100, top: 300, rows: 25, width: 400);
        OcrElement[] aside = [new("FACTURA", 2400, 900, 120, 20), new("6.326", 2400, 940, 80, 20)];

        var crop = ReceiptAutoCrop.Suggest([.. receipt, .. aside], 3000, 2000);

        Assert.True(crop.Right < 0.25);
        Assert.All(aside, e => Assert.False(Inside(crop, e, 3000, 2000)));
    }

    [Fact]
    public void LabelAndAmountFarApartOnTheSameRow_StayInTheFrame()
    {
        var receipt = Lines(left: 100, top: 300, rows: 15, width: 300).ToList();
        receipt.Add(new("TOTAL", 100, 300 + 16 * 30, 90, 20));
        receipt.Add(new("$82.150", 700, 300 + 16 * 30, 110, 20));

        var crop = ReceiptAutoCrop.Suggest(receipt, 1000, 2000);

        Assert.All(receipt, e => Assert.True(Inside(crop, e, 1000, 2000)));
    }

    [Fact]
    public void CropTo_KeepsOnlyTheTextInsideTheFrame()
    {
        var page = new OcrPage([new("DENTRO", 100, 100, 50, 20), new("FUERA", 900, 900, 50, 20)], "test");

        var cropped = page.CropTo(new CropRect(0, 0, 0.5, 0.5), 1000, 1000);

        Assert.Equal(["DENTRO"], cropped.Elements.Select(e => e.Text));
        Assert.Equal("test", cropped.Engine);
    }

    [Theory]
    [MemberData(nameof(ReceiptParserFixtureTests.Receipts), MemberType = typeof(ReceiptParserFixtureTests))]
    public void RealReceipts_AutomaticFrame_KeepsWhatTheParserFinds(ReceiptParserFixtureTests.Expected expected)
    {
        // El marco automático no puede cambiar lo que se propone en los recibos reales.
        var page = Load(expected.Platform, expected.Fixture);
        var width = (int)Math.Ceiling(page.Elements.Max(e => e.X + e.Width)) + 1;
        var height = (int)Math.Ceiling(page.Elements.Max(e => e.Y + e.Height)) + 1;
        var today = new DateOnly(2026, 9, 26);

        var crop = ReceiptAutoCrop.Suggest(page.Elements, width, height);
        var full = ReceiptParser.Parse(page, today);
        var cropped = ReceiptParser.Parse(page.CropTo(crop, width, height), today);

        Assert.Equal(full.Total, cropped.Total);
        Assert.Equal(full.Date, cropped.Date);
        Assert.Equal(full.TaxId, cropped.TaxId);
    }

    private static OcrElement[] Lines(double left, double top, int rows, double width) =>
        Enumerable.Range(0, rows).Select(i => new OcrElement($"LINEA {i}", left, top + i * 30, width, 20)).ToArray();

    private static bool Inside(CropRect crop, OcrElement e, int width, int height)
    {
        var (x, y, w, h) = crop.ToPixels(width, height);
        return e.X >= x && e.Y >= y && e.X + e.Width <= x + w && e.Y + e.Height <= y + h;
    }

    private static OcrPage Load(string platform, string fixture)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Receipts", "Fixtures", platform, fixture + ".json");
        return JsonSerializer.Deserialize<OcrPage>(File.ReadAllText(path))!;
    }
}
