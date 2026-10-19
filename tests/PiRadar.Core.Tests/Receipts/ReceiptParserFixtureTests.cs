using System.Globalization;
using System.Text;
using System.Text.Json;
using PiRadar.Core.Domain;
using PiRadar.Core.Receipts;

namespace PiRadar.Core.Tests.Receipts;

/// <summary>
/// OCR real (Vision en iOS 18.1 y ML Kit en Android 35) de 20 recibos fotografiados con iPhone más el recibo de
/// muestra, anonimizados. Cada campo que el parser acierta queda fijado; los que el OCR no alcanzó a leer se marcan
/// como no verificados con el motivo.
/// </summary>
public class ReceiptParserFixtureTests
{
    private static readonly DateOnly Today = new(2026, 9, 26);

    /// <param name="Date">"yyyy-MM-dd"; <c>"—"</c> = no debe proponer fecha; <c>null</c> = no se verifica.</param>
    /// <param name="Time">"HH:mm"; <c>null</c> = no se verifica.</param>
    /// <param name="TaxId"><c>"—"</c> = no debe proponer NIT; <c>null</c> = no se verifica.</param>
    /// <param name="Merchant">Texto que debe aparecer en algún candidato (sin tildes ni mayúsculas).</param>
    public sealed record Expected(string Platform, string Fixture, long Total, string? Date, string? Time,
        string? TaxId, string Merchant)
    {
        public override string ToString() => $"{Platform}/{Fixture}";
    }

    public static TheoryData<Expected> Receipts =>
    [
        new Expected("ios", "ara", 17190, "2026-09-26", "09:15", "900480569-1", "tiendas ara"),
        new Expected("ios", "ava", 29400, "2026-09-20", "18:47", "901784030-3", "ava gyro shack"),
        new Expected("ios", "burica", 8580, "2026-09-06", "17:34", "800130144-1", "burica"),
        // La línea "CARULLA LA MARIA" (inclinada) no la lee ningún motor: queda la razón social.
        new Expected("ios", "carulla", 31492, "2026-09-14", "16:50", "890900608-9", "almacenes exito"),
        // Vision no lee el número del NIT (queda "NIT." solo).
        new Expected("ios", "crepes-waffles", 175100, "2026-09-19", "13:18", null, "crepes y waffles"),
        new Expected("ios", "exito-muestra", 82150, "2026-09-14", "18:42", "890900608-9", "supermercado el exito"),
        new Expected("ios", "exito-wow", 28000, "2026-09-21", "17:36", "890900608-9", "wow unicali"),
        // NIT y fecha van sobreimpresos en el papel: ilegibles; la fecha sale de "Puntos al 20-09-2026".
        new Expected("ios", "homecenter", 74400, "2026-09-20", null, null, "homecenter"),
        new Expected("ios", "juan-valdez", 20200, "2026-09-18", "08:50", "830112317-1", "juan valdez"),
        new Expected("ios", "la-parrilla", 52800, "2026-09-18", "12:55", "860351427-7", "la parrilla"),
        // Vision lee "300.244.387-6" (8→3): el DV no cuadra y se marca sin verificar.
        new Expected("ios", "mcdonalds", 50900, "2026-09-20", "19:12", null, "mcdonalds"),
        new Expected("ios", "miniso", 63500, "2026-09-16", "17:22", "901137699-5", "miniso"),
        new Expected("ios", "mis-carnes", 56378, "2026-09-18", "09:46", "900335241-1", "mis carnes parrilla"),
        new Expected("ios", "mr-tenders", 60000, "2026-09-24", "18:47", "901962299-0", "mr tenders"),
        new Expected("ios", "pan-pa-ya", 14900, "2026-09-06", "16:07", "12345678-8", "pan pa ya"),
        new Expected("ios", "pricesmart-guanabana", 47700, "2026-09-16", "16:38", "900319753-3", "pricesmart"),
        new Expected("ios", "pricesmart-sin-fecha", 93400, "—", null, "900319753-3", "pricesmart"),
        new Expected("ios", "tribal", 31000, "2026-09-20", "18:45", "900980767-9", "tribal"),
        // Montos con formato inglés ("$ 4,000.00"); fecha ISO de salida; el único NIT es del proveedor del software.
        new Expected("ios", "parqueadero-la-5ta", 4000, "2026-09-26", "00:24", "—", "parqueadero la 5ta"),
        // Factura de talonario escrita a mano: la fecha manuscrita no se lee. El texto vertical del borde
        // ("IMPRESO POR ... NIT ... TEL ... CEL") queda en la fila de "TOTAL $ 120.000" y no la vuelve un resumen.
        new Expected("ios", "factura-manual", 120000, "—", null, "900123456-8", "constru-repuestos"),

        // ML Kit lee la hora "09:15:03" como "69 15:03" y el NIT como "9004B0569-L".
        new Expected("android", "ara", 17190, "2026-09-26", null, null, "tiendas ara"),
        new Expected("android", "ava", 29400, "2026-09-20", "18:47", "901784030-3", "ava gyro shack"),
        new Expected("android", "burica", 8580, "2026-09-06", "17:34", "800130144-1", "burica"),
        new Expected("android", "carulla", 31492, "2026-09-14", "16:50", "890900608-9", "almacenes exito"),
        new Expected("android", "crepes-waffles", 175100, "2026-09-19", "13:18", "860076919-1", "restaurante americas"),
        new Expected("android", "exito-muestra", 82150, "2026-09-14", "18:42", "890900608-9", "supermercado el exito"),
        new Expected("android", "exito-wow", 28000, "2026-09-21", "17:36", "890900608-9", "wow"),
        new Expected("android", "homecenter", 74400, "2026-09-20", null, "800242106-2", "sodimac"),
        // ML Kit lee "08/09/2026" y pierde el NIT.
        new Expected("android", "juan-valdez", 20200, null, "08:50", null, "juan valdez"),
        new Expected("android", "la-parrilla", 52800, "2026-09-18", "12:55", "860351427-7", "la parr"),
        // ML Kit pierde el 8 inicial del NIT.
        new Expected("android", "mcdonalds", 50900, "2026-09-20", "19:12", null, "mcdonalds"),
        new Expected("android", "miniso", 63500, "2026-09-16", "17:22", "901137699-5", "miniso"),
        new Expected("android", "mis-carnes", 56378, "2026-09-18", "09:46", "900335241-1", "carnes"),
        new Expected("android", "mr-tenders", 60000, "2026-09-24", "18:47", "901962299-0", "mr tenders"),
        new Expected("android", "pan-pa-ya", 14900, "2026-09-06", "16:07", "12345678-8", "pan pa ya"),
        new Expected("android", "pricesmart-guanabana", 47700, "2026-09-16", "16:38", "900319753-3", "pricesmart"),
        new Expected("android", "pricesmart-sin-fecha", 93400, "—", null, "900319753-3", "pricesm"),
        // ML Kit lee mal el NIT.
        new Expected("android", "tribal", 31000, "2026-09-20", "18:45", null, "tribal"),
        new Expected("android", "parqueadero-la-5ta", 4000, "2026-09-26", "00:24", "—", "parqueadero la 5ta"),
    ];

    [Theory]
    [MemberData(nameof(Receipts))]
    public void Parse_RealReceipt(Expected expected)
    {
        var result = ReceiptParser.Parse(Load(expected.Platform, expected.Fixture), Today);

        Assert.Equal(Currency.COP, result.Currency);
        Assert.Equal(new Money(expected.Total, Currency.COP), result.Total);

        if (expected.Date == "—")
        {
            Assert.Null(result.Date);
        }
        else if (expected.Date is not null)
        {
            Assert.Equal(expected.Date, result.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (expected.Time is not null)
        {
            Assert.True(result.HasTime);
            Assert.Equal(expected.Time, result.Date?.ToString("HH:mm", CultureInfo.InvariantCulture));
        }

        if (expected.TaxId == "—")
        {
            Assert.Null(result.TaxId);
        }
        else if (expected.TaxId is not null)
        {
            Assert.Equal(expected.TaxId, result.TaxId);
        }

        Assert.Contains(result.MerchantCandidates, c => Fold(c).Contains(expected.Merchant, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_BancolombiaButton_UsesTotalPaidNotTheMaskedAccount()
    {
        // Captura de pantalla de "Botón Bancolombia" (PSE) leída con ML Kit: etiquetas y valores en columnas, y la
        // cuenta "Cuenta de ahorros **** 7753" separada de los asteriscos. Referencias y cuenta anonimizadas.
        var result = ReceiptParser.Parse(Load("android", "bancolombia-boton"), new DateOnly(2026, 10, 14));

        Assert.Equal(Currency.COP, result.Currency);
        Assert.Equal(new Money(25958, Currency.COP), result.Total);
    }

    [Theory]
    [InlineData("ios")]
    [InlineData("android")]
    public void Parse_Amazon_IsUsd_WithUnconfirmedTotal(string platform)
    {
        // La línea "Order Total $264.18" quedó fuera de la foto: no hay total confiable que proponer.
        var result = ReceiptParser.Parse(Load(platform, "amazon"), Today);

        Assert.Equal(Currency.USD, result.Currency);
        Assert.False(result.TotalConfirmed);
        Assert.All(result.TotalCandidates, c => Assert.Equal(Currency.USD, c.Currency));
        Assert.Equal(new DateTime(2026, 9, 11), result.Date);
        Assert.False(result.HasTime);
        Assert.Null(result.TaxId);
        Assert.Equal("Amazon", result.Merchant);
    }

    [Theory]
    [InlineData("ios", "miniso")]
    [InlineData("android", "crepes-waffles")]
    [InlineData("ios", "tribal")]
    public void Parse_FromSavedRows_GivesSameResult(string platform, string fixture)
    {
        // Receipt.OcrText guarda las filas; re-parsear desde ahí debe dar lo mismo que desde el OCR.
        var fromOcr = ReceiptParser.Parse(Load(platform, fixture), Today);
        var fromRows = ReceiptParser.Parse(fromOcr.Rows, Today);

        Assert.Equal(fromOcr.Total, fromRows.Total);
        Assert.Equal(fromOcr.Date, fromRows.Date);
        Assert.Equal(fromOcr.TaxId, fromRows.TaxId);
        Assert.Equal(fromOcr.MerchantCandidates, fromRows.MerchantCandidates);
    }

    private static OcrPage Load(string platform, string fixture)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Receipts", "Fixtures", platform, fixture + ".json");
        return JsonSerializer.Deserialize<OcrPage>(File.ReadAllText(path))!;
    }

    private static string Fold(string text) =>
        new string(text.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
        .ToLowerInvariant();
}
