using PiRadar.Core.Domain;
using PiRadar.Core.Receipts;

namespace PiRadar.Core.Tests.Receipts;

/// <summary>
/// Recibos estadounidenses inventados (comercios y datos ficticios) con la estructura típica de un punto de venta.
/// Reemplazar o ampliar con recibos reales anonimizados cuando haya (ver tools/ocr-fixtures/README.md).
/// </summary>
public class UsReceiptTests
{
    private static readonly DateOnly Today = new(2026, 9, 30);

    private static ReceiptParseResult Parse(string[] rows, Currency fallback = Currency.USD) =>
        ReceiptParser.Parse(rows, Today, fallback);

    [Fact]
    public void Grocery_receipt()
    {
        var result = Parse(
        [
            "FRESH MARKET SUPERCENTER",
            "123 MAIN STREET",
            "SPRINGFIELD IL 62701",
            "(555) 123-4567",
            "ST# 1234 OP# 001 TE# 05 TR# 0678",
            "MILK 1 GAL 3.48",
            "BREAD WHEAT 2.98",
            "EGGS LARGE 4.12",
            "SUBTOTAL 10.58",
            "TAX 1 6.250 % 0.66",
            "TOTAL 11.24",
            "DEBIT TEND 11.24",
            "CHANGE DUE 0.00",
            "09/28/2026 14:32:10",
            "THANK YOU FOR SHOPPING",
        ]);

        Assert.Equal(Currency.USD, result.Currency);
        Assert.Equal(new Money(1124, Currency.USD), result.Total);
        Assert.Equal(new DateOnly(2026, 9, 28), result.Date is { } d ? DateOnly.FromDateTime(d) : default);
        Assert.True(result.HasTime);
        Assert.Equal("Fresh Market Supercenter", result.Merchant);
        Assert.Null(result.TaxId);
    }

    [Fact]
    public void Restaurant_with_tip_uses_the_amount_due()
    {
        var result = Parse(
        [
            "THE GRILL HOUSE LLC",
            "456 OAK AVE",
            "Server: Maria   Table 12",
            "Burger 14.00",
            "Fries 5.00",
            "Subtotal 19.00",
            "Tax 1.52",
            "Total 20.52",
            "Suggested tip 18% 3.69",
            "Tip 4.00",
            "Amount Due 24.52",
            "Sep 27, 2026 7:45 PM",
        ]);

        Assert.Equal(Currency.USD, result.Currency);
        Assert.Equal(new Money(2452, Currency.USD), result.Total);
        Assert.Equal("The Grill House", result.Merchant);
        Assert.Equal(new DateTime(2026, 9, 27, 19, 45, 0), result.Date);
    }

    [Fact]
    public void Gas_station_receipt()
    {
        var result = Parse(
        [
            "QUICK FUEL #2210",
            "789 HIGHWAY 9",
            "PUMP 05 UNLEADED",
            "10.234 GAL @ $3.599",
            "FUEL SALE $36.83",
            "TOTAL $36.83",
            "CREDIT $36.83",
            "09/29/26 08:15",
        ]);

        Assert.Equal(Currency.USD, result.Currency);
        Assert.Equal(new Money(3683, Currency.USD), result.Total);
    }

    [Fact]
    public void Receipt_without_any_english_word_follows_the_fallback_currency()
    {
        string[] rows = ["CORNER CAFE", "Latte 4.50", "Muffin 3.25", "TOTAL $7.75", "09/29/2026"];

        Assert.Equal(new Money(775, Currency.USD), Parse(rows, Currency.USD).Total);
        Assert.Equal(Currency.USD, Parse(rows, Currency.USD).Currency);
        Assert.Equal(Currency.COP, Parse(rows, Currency.COP).Currency);
    }

    [Fact]
    public void Tax_line_alone_is_not_taken_as_the_total()
    {
        var result = Parse(["SHOP", "Item 20.00", "Sales Tax 1.60", "Total 21.60"]);

        Assert.Equal(new Money(2160, Currency.USD), result.Total);
    }

    [Fact]
    public void Colombian_receipt_keeps_cop_even_with_a_usd_fallback()
    {
        var result = Parse(
        [
            "ALMACEN LA 14 S.A.",
            "NIT 890.900.943-2",
            "TOTAL A PAGAR $82.150",
            "FECHA 25/09/2026",
        ]);

        Assert.Equal(Currency.COP, result.Currency);
        Assert.Equal(new Money(82150, Currency.COP), result.Total);
    }
}
