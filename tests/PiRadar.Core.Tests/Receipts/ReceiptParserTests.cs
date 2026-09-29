using PiRadar.Core.Domain;
using PiRadar.Core.Receipts;

namespace PiRadar.Core.Tests.Receipts;

public class ReceiptParserTests
{
    private static readonly DateOnly Today = new(2026, 9, 24);

    [Fact]
    public void Parse_ToleratesTypicalOcrErrors()
    {
        string[] rows =
        [
            "SUPERMERCADO EL ÉXITO S.A.",
            "NIT 890.900.608-9",
            "FACTURA ELECTRÓNI CA DE VENTA",
            "FECHA: 14/09/2026 HORA: 18:42",
            "CAFÉ M0LIDO 500G 21.450",
            "SUBTOTAL 69.294",
            "İVA 19% 12.856",
            "T0TAL A PA GAR $82. 150",
            "TARJETA DÉBITO ****4521 82.150",
        ];

        var result = ReceiptParser.Parse(rows, Today);

        Assert.Equal(new Money(82150, Currency.COP), result.Total);
        Assert.True(result.TotalConfirmed);
        Assert.Equal(new DateTime(2026, 9, 14, 18, 42, 0), result.Date);
        Assert.Equal("890900608-9", result.TaxId);
        Assert.True(result.TaxIdVerified);
        Assert.Equal("Supermercado El Éxito", result.Merchant);
    }

    [Fact]
    public void Parse_PrefersTotalWithTip_OverInvoiceTotal()
    {
        string[] rows =
        [
            "INVERSIONES GASTRONOMICAS AGUILA SAS",
            "N.I.T.: 900980767-9",
            "PUNTO DE VENTA: TRIBAL JARDIN PLAZA",
            "FECHA EMISION: 20/09/2026 18:45",
            "Total items: 1",
            "TOTAL FACTURA: 28.900",
            "*PROPINA: 2.100",
            "TOTAL CON PROPINA: 31.000",
        ];

        var result = ReceiptParser.Parse(rows, Today);

        Assert.Equal(new Money(31000, Currency.COP), result.Total);
        Assert.Equal([new Money(31000, Currency.COP), new Money(28900, Currency.COP)], result.TotalCandidates);
        Assert.Equal(["Tribal Jardin Plaza", "Inversiones Gastronomicas Aguila"], result.MerchantCandidates);
    }

    [Fact]
    public void Parse_PrefersLegalName_WhenBrandIsTruncatedLogo()
    {
        // Vision en un iPhone con iOS 26 leyó el logo "MR TENDERS" como "TENDER" (2026-09-28).
        string[] rows =
        [
            "TENDER",
            "MR TENDERS COLOMBIA S.A.S",
            "NIT: 901962299-0",
            "Sede: Ingenio",
            "24/9/2026 18:47",
            "Total venta $ 60.000",
        ];

        var result = ReceiptParser.Parse(rows, Today);

        Assert.Equal(["Mr Tenders", "Tender"], result.MerchantCandidates);
    }

    [Fact]
    public void Parse_KeepsBrandFirst_WhenLegalNameContainsItAsWholeWord()
    {
        string[] rows =
        [
            "TIENDAS ARA",
            "JERONIMO MARTINS COLOMBIA S.A.S",
            "TIENDAS ARA JERONIMO MARTINS S.A.S",
            "NIT 900.480.569-1",
            "FECHA: 26/09/2026",
            "TOTAL A PAGAR $17.190",
        ];

        var result = ReceiptParser.Parse(rows, Today);

        Assert.Equal("Tiendas Ara", result.Merchant);
    }

    [Fact]
    public void Parse_IgnoresTaxSummaryRowsAndItemCounts()
    {
        string[] rows =
        [
            "PRICESMART Colombia S.A.S.",
            "NIT: 900319753-3",
            "1 461523 Guanabana 47,700 B",
            "SUBTOTAL 47,700",
            "TOTAL 47,700",
            "Total Items: 1",
            "TOTAL 47,700 40,084 7,616",
        ];

        var result = ReceiptParser.Parse(rows, Today);

        Assert.Equal([new Money(47700, Currency.COP)], result.TotalCandidates);
    }

    [Fact]
    public void Parse_UsesPaymentMinusChange_WhenThereIsNoTotalKeyword()
    {
        string[] rows = ["HOMECENTER", "TOTAL AHORROS $ 0", "EFECTIVO $ 100,000", "CAMBIO 25,600"];

        Assert.Equal(new Money(74400, Currency.COP), ReceiptParser.Parse(rows, Today).Total);
    }

    [Fact]
    public void Parse_PairsLabelWithAmountOnNextRow()
    {
        string[] rows = ["SUBTOTAL 48.450", "AHORRO 16.958", "VALOR TOTAL", "31.492", "FORMA PAGO : CONTADO"];

        Assert.Equal(new Money(31492, Currency.COP), ReceiptParser.Parse(rows, Today).Total);
    }

    [Fact]
    public void Parse_SkipsCustomerAndSoftwareProviderNits()
    {
        string[] rows =
        [
            "MI TIENDA SAS",
            "Cliente C.C/Nit: 94405434",
            "NIT 901.137.699-5",
            "TOTAL $ 10.000",
            "Software FYM TECHNOLOGY SAS NIT 900.306.823-4",
        ];

        Assert.Equal("901137699-5", ReceiptParser.Parse(rows, Today).TaxId);
    }

    [Theory]
    [InlineData("ALMACENES EXITO S.A NIT 8909006089", "890900608-9", true)]
    [InlineData("NIT: 860351427", "860351427-7", false)]
    [InlineData("NIT 900.319.753-4", "900319753-4", false)]
    [InlineData("900.335.241-1 C.C GRAN ESTACION", "900335241-1", true)]
    public void Parse_NormalizesNit(string row, string expected, bool verified)
    {
        var result = ReceiptParser.Parse([row, "TOTAL 10.000"], Today);

        Assert.Equal(expected, result.TaxId);
        Assert.Equal(verified, result.TaxIdVerified);
    }

    [Fact]
    public void Parse_IgnoresResolutionAndFutureDates()
    {
        string[] rows =
        [
            "RESOLUCION DIAN 18764 DEL 16/02/2026 HASTA 16/02/2027",
            "Devoluciones hasta: Diciembre 07 de 2026",
            "24/9/2026 18:47",
            "TOTAL $ 60.000",
        ];

        Assert.Equal(new DateTime(2026, 9, 24, 18, 47, 0), ReceiptParser.Parse(rows, Today).Date);
    }

    [Fact]
    public void Parse_OnlyResolutionDates_ProposesNoDate()
    {
        string[] rows = ["RES.DIAN 18764100481469 FECHA 23/10/2025", "VENCE: 23/10/2027", "TOTAL 29.400"];

        Assert.Null(ReceiptParser.Parse(rows, Today).Date);
    }

    [Fact]
    public void Parse_TimeOnNeighborRow()
    {
        string[] rows = ["FECHA DE GENERACION:06/09/2026", "HORA DE GENERACION:04:07:18 PM", "TOTAL: $14,900"];

        var result = ReceiptParser.Parse(rows, Today);

        Assert.Equal(new DateTime(2026, 9, 6, 16, 7, 18), result.Date);
        Assert.True(result.HasTime);
    }

    [Fact]
    public void Parse_UsReceipt_IsUsd_WithMonthFirstDate()
    {
        string[] rows = ["amazon.com", "Your order of 09/06/2026", "Subtotal $200.96", "Shipping & Handling $17.06", "Order Total $264.18"];

        var result = ReceiptParser.Parse(rows, Today);

        Assert.Equal(Currency.USD, result.Currency);
        Assert.Equal(Money.FromDecimal(264.18m, Currency.USD), result.Total);
        Assert.Equal(new DateTime(2026, 9, 6), result.Date);
        Assert.Equal("Amazon", result.Merchant);
    }

    [Fact]
    public void Parse_RoundsCopCents()
    {
        Assert.Equal(new Money(8581, Currency.COP), ReceiptParser.Parse(["BURICA S.A.", "NIT 800130144-1", "TOTAL : 8,580.50"], Today).Total);
    }

    [Fact]
    public void Parse_EmptyInput_ProposesNothing()
    {
        var result = ReceiptParser.Parse(Array.Empty<string>(), Today);

        Assert.Null(result.Total);
        Assert.Null(result.Date);
        Assert.Null(result.Merchant);
        Assert.Null(result.TaxId);
        Assert.Equal(Currency.COP, result.Currency);
    }

    [Fact]
    public void Parse_Parking_UsesExitDate_AndKeepsOrdinalInName()
    {
        string[] rows =
        [
            "PARQUEADERO LA 5TA",
            "Entrada 2026-09-23 - 23:43:36",
            "Salida 2026-09-24 - 00:24:54",
            "Total $ 4,000.00",
        ];

        var result = ReceiptParser.Parse(rows, Today);

        Assert.Equal(new DateTime(2026, 9, 24, 0, 24, 54), result.Date);
        Assert.Equal("Parqueadero La 5ta", result.Merchant);
        Assert.Equal(new Money(4000, Currency.COP), result.Total);
    }

    [Fact]
    public void Parse_HeaderNitWithoutKeyword_WithCheckDigitReadAsL()
    {
        string[] rows =
        [
            "TIENDAS ARA JERONIMO MARTINS COLOMBIA S.A.S. 900480569-L",
            "GENERACION: 2026-09-24 09 15:03",
            "Total: 17 190",
        ];

        var result = ReceiptParser.Parse(rows, Today);

        Assert.Equal("900480569-1", result.TaxId);
        Assert.True(result.TaxIdVerified);
        Assert.Equal(new DateTime(2026, 9, 24, 9, 15, 3), result.Date);
        Assert.Contains("Tiendas Ara Jeronimo Martins", result.MerchantCandidates);
    }
}
