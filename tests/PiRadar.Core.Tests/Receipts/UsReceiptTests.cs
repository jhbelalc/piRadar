using PiRadar.Core.Domain;
using PiRadar.Core.Receipts;

namespace PiRadar.Core.Tests.Receipts;

/// <summary>
/// Recibos estadounidenses: primero inventados (comercios y datos ficticios) con la estructura típica de un punto de
/// venta; al final, cinco reales anonimizados (ver tools/ocr-fixtures/README.md).
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
    public void Gas_pump_receipt_uses_the_fuel_total_not_the_gallons_or_price()
    {
        // Real receipt (photo), merchant and address anonymized.
        var result = ReceiptParser.Parse(
        [
            "Corner Kitchen 100",
            "100 Main St",
            "Anytown SC 29000",
            "10/12/2026",
            "18:09:13",
            "Pump # 02 - Self",
            "Unleaded 10.131g",
            "Price/Gal 3.949",
            "Fuel Ttl $40.01",
            "Authorization",
            "Visa",
            "XXXXXXXXXXXX0773",
            "Contactless",
            "US DEBIT",
            "Mode: Issuer",
            "AID: A0000000980840",
            "TVR: 0000000000",
            "TSI: 0000",
            "ARC: 00",
            "10/12/2026 18:06:59",
            "I agree to pay the",
            "above Total Amount",
            "according to Card",
            "Issuer Agreement.",
            "TRAN:1414842",
            "Thank You!",
        ], new DateOnly(2026, 10, 12), Currency.USD);

        Assert.Equal(Currency.USD, result.Currency);
        Assert.Equal(new Money(4001, Currency.USD), result.Total);
        Assert.Equal(new DateOnly(2026, 10, 12), result.Date is { } d ? DateOnly.FromDateTime(d) : default);
    }

    // Recibos reales (fotos), con comercio, dirección y datos personales anonimizados.
    private static readonly DateOnly RealToday = new(2026, 10, 13);

    [Fact]
    public void Coffee_shop_receipt_without_a_total_line_uses_the_payment()
    {
        var result = ReceiptParser.Parse(
        [
            "Welcome to Coffee Shop",
            "Store #: 100000",
            "100 MAIN AVE",
            "ANYTOWN, SC 29000",
            "CHK 9835",
            "10/13/2026 4:27 PM",
            "Eat In",
            "1 Md Ice Orig Cof 3.49",
            "2 French Vanilla Swl",
            "3 Almond Milk",
            "1 Sm Hot Orig Cof 2.59",
            "Visa $6.62",
            "************1980",
            "Tran Type : Purchase",
            "Entry Mode : TAPPED",
            "Auth Code : 022774",
            "VISA DEBIT",
            "AID: A0000000031010",
            "No Signature Required",
            "Subtotal $6.08",
            "Goose Creek $0.06",
            "Berkeley Co Tr $0.06",
            "BCECIT $0.06",
            "South Carolina $0.36",
            "Payment $6.62",
            "Change Due $0.00",
            "Check Closed",
            "10/13/2026 4:27 PM",
        ], RealToday, Currency.USD);

        Assert.Equal(new Money(662, Currency.USD), result.Total);
        Assert.Equal(new DateOnly(2026, 10, 13), result.Date is { } d ? DateOnly.FromDateTime(d) : default);
    }

    [Fact]
    public void Long_grocery_receipt_uses_the_balance_due_not_savings_or_tax()
    {
        var result = ReceiptParser.Parse(
        [
            "GROCERY STORE",
            "Store #1000 (555) 555-0100",
            "100 Main Ave, Anytown, SC",
            "FROZEN/DAIRY",
            "FL FRUIT EXPLOSION 3.49 A",
            "FL MANGO CHUNKS 3.49 A",
            "GROCERY",
            "MAHATMA BROWN RICE 4.99 A",
            "SCOTT BTH 12 RL 1PLY 14.59 B",
            "Savings",
            "You saved:",
            "HUNTS NSA DCD TOMATO -0.31",
            "SCOTT BTH 12 RL 1PLY -2.70",
            "Tax Paid",
            "2.00% Tax 1 51.07 1.02",
            "9.00% Tax 2 34.87 3.14",
            "TOTAL TAX 4.16",
            "40 BALANCE DUE 90.10",
            "Debit Card $90.10",
            "SALE",
            "XXXXXXXXXXXX5429",
            "Debit Card",
            "10/13/2026",
            "INVOICE: 064697",
            "US DEBIT",
            "AID: A0000000980840",
            "Total: USD$ 90.10",
            "APPROVED",
            "PIN VERIFIED",
            "CHANGE 0.00",
            "You saved $13.21!",
            "Year to Date",
            "Savings 235.30",
        ], RealToday, Currency.USD);

        Assert.Equal(new Money(9010, Currency.USD), result.Total);
    }

    [Fact]
    public void Superstore_receipt_uses_the_total_not_the_tax_or_subtotal()
    {
        var result = ReceiptParser.Parse(
        [
            "Give us feedback @ survey.example.com",
            "Walmart",
            "WM Supercenter",
            "843-555-0100 Mgr. NAME",
            "100 MAIN AVE",
            "ANYTOWN SC 29000",
            "ST# 02928 OP# 009001 TE# 01 TR# 00535",
            "# ITEMS SOLD 4",
            "TC# 7834 3547 3004 8135 1392",
            "STB FS HWN 022000332810 F 3.96 R",
            "SKT SHRKR 022000333140 F 3.96 R",
            "LDY MITCH 2P 309973105910 6.97 X",
            "96/20 PAPER 842356061680 7.92 X",
            "SUBTOTAL 22.81",
            "TAX1 9.0000 % 1.34",
            "TAX2 2.0000 % 0.16",
            "TOTAL 24.31",
            "DEBIT TEND 24.31",
            "CHANGE DUE 0.00",
            "EFT DEBIT PAY FROM PRIMARY",
            "24.31 TOTAL PURCHASE",
            "US DEBIT- 0773 C 0",
            "REF # U061FZ257442",
            "NETWORK ID. 0008 APPR. CODE 731938",
            "AID A0000000980840",
            "TERMINAL # 55932508",
            "*No Signature Required",
            "10/13/26 16:40:39",
        ], RealToday, Currency.USD);

        Assert.Equal(new Money(2431, Currency.USD), result.Total);
        Assert.Equal(new DateOnly(2026, 10, 13), result.Date is { } d ? DateOnly.FromDateTime(d) : default);
    }

    [Fact]
    public void Car_wash_receipt_with_zero_tax()
    {
        var result = ReceiptParser.Parse(
        [
            "CAR WASH",
            "906 - Plantation",
            "100 Main Avenue",
            "Anytown, SC, 29000",
            "+1(555)555-0100",
            "XPT2, 10/13/26, 02:55 PM",
            "Shift-1, Empl-1,",
            "Sale#000610132610506",
            "1 Deluxe Wax 30.00",
            "1 Deluxe Wax Raincheck 0.00",
            "Subtotal 30.00",
            "Sales Tax 0.00",
            "Total 30.00",
            "XXXX0773 30.00",
            "(Sale Appr #421745)",
            "Please verify or complete the",
            "following vehicle and contact",
            "information.",
            "Customer Name: JOHN Q PUBLIC",
            "Vehicle Year :",
            "TVR 0000000000",
            "AID A0000000031010",
            "IAD 06011203A00000",
            "000/Approval",
        ], RealToday, Currency.USD);

        Assert.Equal(new Money(3000, Currency.USD), result.Total);
        Assert.Equal(new DateOnly(2026, 10, 13), result.Date is { } d ? DateOnly.FromDateTime(d) : default);
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
