using PiRadar.Core.Domain;
using PiRadar.Core.Import;
using static PiRadar.Core.Tests.Import.ImportTestSheets;
using F = PiRadar.Core.Import.ImportField;

namespace PiRadar.Core.Tests.Import;

public class ImportRowParserTests
{
    private static ImportOptions Opts(ImportMapping m, DateOrder o = DateOrder.DayFirst, Currency c = Currency.COP, bool neg = true) =>
        new(m, o, c, neg);

    private static ImportPreview Run(object?[][] rows, ImportMapping m, DateOrder o = DateOrder.DayFirst, Currency c = Currency.COP, bool neg = true) =>
        ImportRowParser.Parse(Sheet(rows), Opts(m, o, c, neg));

    private static readonly ImportMapping DA = Map(null, F.Date, F.Amount);

    [Fact]
    public void Month_fixture()
    {
        var m = Map(0, F.Description, F.Date, F.Income, F.Expense, F.Category, F.PaymentMethod, F.Currency);
        var sheet = Sheet(
            ["Descripción", "Fecha", "Ingreso", "Gasto", "Categoria", "Medio de pago", "Moneda"],
            ["Mes Ant.", new DateTime(2026, 8, 1), 1000000m, null, "Saldo", null, null],
            ["Compra", new DateTime(2026, 8, 3), null, 3456789.3499999996m, "Mercado", null, null],
            ["Compra 2", new DateTime(2026, 8, 5), null, 437.05m, "Mercado ", null, null],
            ["Sueldo", new DateTime(2026, 8, 15), 2500000m, null, "Salario", null, null],
            ["Taxi", new DateTime(2026, 8, 20), null, 12000m, "Transporte", null, null],
            ["Cine", new DateTime(2026, 8, 31), null, 30000m, "Ocio", null, null],
            [null, null, null, null, null, null, null],
            [null, null, null, null, null, null, null]);
        var p = ImportRowParser.Parse(sheet, Opts(m));

        Assert.Empty(p.Errors);
        Assert.Equal(6, p.Rows.Count);
        Assert.Equal([3, 4], p.RoundedRows);
        Assert.Equal([2], p.OpeningBalanceRows);
        var t = Assert.Single(p.Totals);
        Assert.Equal(Currency.COP, t.Currency);
        Assert.Equal(3_500_000, t.Income.AmountMinor);
        Assert.Equal(3456789 + 437 + 12000 + 30000, t.Expense.AmountMinor);
        Assert.Equal(new DateTime(2026, 8, 1, 12, 0, 0), t.From);
        Assert.Equal(new DateTime(2026, 8, 31, 12, 0, 0), t.To);
        Assert.All(p.Rows, r => Assert.Equal(DateTimeKind.Local, r.OccurredAt.Kind));
        Assert.Equal(TransactionType.Expense, p.Rows[1].Type);
        Assert.Equal(3456789, p.Rows[1].Amount.AmountMinor);

        Assert.Equal(5, p.Categories.Count);
        var mercado = p.Categories.Single(c => c.Name == "Mercado");
        Assert.Equal(2, mercado.Count);
        Assert.Equal(TransactionType.Expense, mercado.Type);
        Assert.Equal(TransactionType.Income, p.Categories.Single(c => c.Name == "Salario").Type);
        Assert.Empty(p.PaymentMethods);
    }

    [Fact]
    public void Text_dates_by_order()
    {
        var day = Run([["03/08/2026", 5]], DA, DateOrder.DayFirst);
        Assert.Equal(new DateTime(2026, 8, 3, 12, 0, 0), day.Rows[0].OccurredAt);
        var month = Run([["03/08/2026", 5]], DA, DateOrder.MonthFirst);
        Assert.Equal(new DateTime(2026, 3, 8, 12, 0, 0), month.Rows[0].OccurredAt);
    }

    [Theory]
    [InlineData("2026-08-03", 2026, 8, 3, 12, 0)]
    [InlineData("3-ago-2026", 2026, 8, 3, 12, 0)]
    [InlineData("3 de agosto de 2026", 2026, 8, 3, 12, 0)]
    [InlineData("Aug 3, 2026", 2026, 8, 3, 12, 0)]
    [InlineData("3 sept. 2026", 2026, 9, 3, 12, 0)]
    [InlineData("3/8/26 2:30 p. m.", 2026, 8, 3, 14, 30)]
    [InlineData("3/8/2026 14:05", 2026, 8, 3, 14, 5)]
    [InlineData("2026-08-03T09:15:00", 2026, 8, 3, 9, 15)]
    [InlineData("3/8/2026 12:10 AM", 2026, 8, 3, 0, 10)]
    public void Text_date_formats(string text, int y, int mo, int d, int h, int mi)
    {
        var p = Run([[text, 5]], DA);
        Assert.Empty(p.Errors);
        Assert.Equal(new DateTime(y, mo, d, h, mi, 0), p.Rows[0].OccurredAt);
    }

    [Fact]
    public void Serial_number_date_and_time_column()
    {
        var m = Map(null, F.Date, F.Time, F.Amount);
        var p = Run([[46237m, 0.5m, 5], [46237m, "2:30 p. m.", 5], [new DateTime(2026, 8, 3, 8, 0, 0), null, 5]], m);
        Assert.Empty(p.Errors);
        Assert.Equal(new DateTime(2026, 8, 3, 12, 0, 0), p.Rows[0].OccurredAt);
        Assert.Equal(new DateTime(2026, 8, 3, 14, 30, 0), p.Rows[1].OccurredAt);
        Assert.Equal(new DateTime(2026, 8, 3, 8, 0, 0), p.Rows[2].OccurredAt);
    }

    [Fact]
    public void Invalid_date_is_row_error()
    {
        var p = Run([["x", 1], [new DateTime(2026, 8, 3), 5], ["31/02/2026", 5]], DA);
        Assert.Equal(2, p.Errors.Count);
        Assert.Equal(1, p.Errors[0].RowNumber);
        Assert.Equal("Fila 1: la fecha no es válida.", p.Errors[0].Message);
        Assert.Single(p.Rows);
        using var _ = AppLocale.Use(AppLanguage.English);
        var e = Run([[null, 1], ["x", 1], ["x", 1], ["x", 1], ["x", 1], ["x", 1], ["x", 1], ["x", 1], ["x", 1], ["x", 1], ["x", 1], ["bad", 5]], DA);
        Assert.Equal("Row 12: the date is not valid.", e.Errors.Single(x => x.RowNumber == 12).Message);
    }

    [Fact]
    public void Suggest_date_order()
    {
        var m = Map(null, F.Date, F.Amount);
        Assert.Equal(DateOrder.DayFirst, ImportRowParser.SuggestDateOrder(Sheet(["25/08/2026", 1], ["03/08/2026", 1]), m));
        Assert.Equal(DateOrder.MonthFirst, ImportRowParser.SuggestDateOrder(Sheet(["08/25/2026", 1], ["03/08/2026", 1]), m));
        var amb = Sheet(["03/08/2026", 1]);
        Assert.Equal(DateOrder.DayFirst, ImportRowParser.SuggestDateOrder(amb, m));
        using var _ = AppLocale.Use(AppLanguage.English);
        Assert.Equal(DateOrder.MonthFirst, ImportRowParser.SuggestDateOrder(amb, m));
    }

    [Theory]
    [InlineData("$ 1.234.567", 1234567, null)]
    [InlineData("1,234.56", 1234.56, null)]
    [InlineData("US$ 12,50", 12.5, Currency.USD)]
    [InlineData("(500)", -500, null)]
    [InlineData("-500", -500, null)]
    [InlineData("500-", -500, null)]
    [InlineData("+1.000", 1000, null)]
    [InlineData("COP 50.000", 50000, Currency.COP)]
    [InlineData(" $ 1.500 ", 1500, null)]
    public void Parse_amount(string text, double expected, Currency? cur)
    {
        Assert.True(ImportRowParser.ParseAmount(text, out var v, out var c));
        Assert.Equal((decimal)expected, v);
        Assert.Equal(cur, c);
    }

    [Fact]
    public void Parse_amount_fails_on_text() => Assert.False(ImportRowParser.ParseAmount("abc", out _, out _));

    [Fact]
    public void Signed_mode_and_inverted()
    {
        var rows = new object?[][] { [new DateTime(2026, 8, 3), -100], [new DateTime(2026, 8, 4), 200] };
        var p = Run(rows, DA);
        Assert.Equal([TransactionType.Expense, TransactionType.Income], p.Rows.Select(r => r.Type));
        var q = Run(rows, DA, neg: false);
        Assert.Equal([TransactionType.Income, TransactionType.Expense], q.Rows.Select(r => r.Type));
        Assert.All(q.Rows, r => Assert.True(r.Amount.AmountMinor > 0));
    }

    [Fact]
    public void Amount_with_type()
    {
        var m = Map(null, F.Date, F.Amount, F.Type);
        var d = new DateTime(2026, 8, 3);
        var p = Run([[d, 100, "Ingreso"], [d, 100, "expense"], [d, 100, "+"], [d, 100, "-"], [d, -50, "otro"], [d, 50, "otro"], [d, 50, null]], m);
        Assert.Equal([TransactionType.Income, TransactionType.Expense, TransactionType.Income, TransactionType.Expense, TransactionType.Expense],
            p.Rows.Select(r => r.Type));
        Assert.Equal(50, p.Rows[4].Amount.AmountMinor);
        Assert.Equal([6, 7], p.Errors.Select(e => e.RowNumber));
        Assert.Contains("otro", p.Errors[0].Message);
    }

    [Fact]
    public void Income_expense_errors_and_zero()
    {
        var m = Map(null, F.Date, F.Income, F.Expense);
        var d = new DateTime(2026, 8, 3);
        var p = Run([[d, 10, 20], [d, null, null], [d, 0, null], [d, null, 0], [d, 5, null]], m);
        Assert.Equal(2, p.Errors.Count);
        Assert.Contains("a la vez", p.Errors[0].Message);
        Assert.Contains("no tiene monto", p.Errors[1].Message);
        Assert.Equal(2, p.SkippedZero);
        Assert.Single(p.Rows);
    }

    [Fact]
    public void Currency_column_and_text()
    {
        var m = Map(null, F.Date, F.Amount, F.Currency);
        var d = new DateTime(2026, 8, 3);
        var p = Run([[d, 10, "USD"], [d, 10, "dólares"], [d, 10, "€"], [d, "US$ 5,50", null], [d, 10, null], [d, "7", "$"]], m);
        Assert.Equal(Currency.USD, p.Rows[0].Amount.Currency);
        Assert.Equal(Currency.USD, p.Rows[1].Amount.Currency);
        Assert.Equal(Currency.USD, p.Rows[2].Amount.Currency);
        Assert.Equal(550, p.Rows[2].Amount.AmountMinor);
        Assert.Equal(Currency.COP, p.Rows[3].Amount.Currency);
        Assert.Equal(Currency.COP, p.Rows[4].Amount.Currency);
        var err = Assert.Single(p.Errors);
        Assert.Equal(3, err.RowNumber);
        Assert.Contains("€", err.Message);

        var q = Run([[d, 10, null]], m, c: Currency.USD);
        Assert.Equal(Currency.USD, q.Rows[0].Amount.Currency);
    }

    [Fact]
    public void Usd_rounding_away_from_zero()
    {
        var p = Run([[new DateTime(2026, 8, 3), 12.345m]], DA, c: Currency.USD);
        Assert.Equal(1235, p.Rows[0].Amount.AmountMinor);
        Assert.True(p.Rows[0].Rounded);
        Assert.Equal([1], p.RoundedRows);
        var z = Run([[new DateTime(2026, 8, 3), 0.004m]], DA, c: Currency.USD);
        Assert.Equal(1, z.SkippedZero);
    }

    [Fact]
    public void Total_rows_are_skipped_but_dated_ones_import()
    {
        var m = Map(null, F.Date, F.Description, F.Amount);
        var d = new DateTime(2026, 8, 3);
        var p = Run([[d, "Total gasolina", 5000], ["Total", null, 9000], [null, "TOTAL GASTOS", 9000], [d, "Mercado", 100]], m);
        Assert.Equal(2, p.SkippedTotals);
        Assert.Equal(2, p.Rows.Count);
        Assert.Empty(p.Errors);
    }

    [Fact]
    public void Texts_are_cleaned_and_subcategories_counted()
    {
        var m = Map(null, F.Date, F.Amount, F.Category, F.Subcategory, F.PaymentMethod, F.Merchant, F.Notes);
        var d = new DateTime(2026, 8, 3);
        var p = Run([[d, -1, "  Casa  ", "Luz", "Débito", " Tienda  X ", "n"], [d, -1, "casa", "luz ", "debito", null, null]], m);
        Assert.Equal("Tienda X", p.Rows[0].Merchant);
        Assert.Equal(("Casa", 2), (p.Categories[0].Name, p.Categories[0].Count));
        var s = Assert.Single(p.Subcategories);
        Assert.Equal(("Casa", "Luz", 2), (s.Parent, s.Name, s.Count));
        var pm = Assert.Single(p.PaymentMethods);
        Assert.Equal(("Débito", 2), (pm.Name, pm.Count));
    }

    [Fact]
    public void Incomplete_mapping_throws()
    {
        Assert.Throws<ArgumentException>(() => Run([[1, 1]], Map(null, F.Date, F.Ignore)));
    }

    [Fact]
    public void Header_row_is_skipped_and_numbers_are_excel_rows()
    {
        var p = Run([["Fecha", "Valor"], [new DateTime(2026, 8, 3), -5]], Map(0, F.Date, F.Amount));
        Assert.Equal(2, p.Rows[0].RowNumber);
    }
}

public class ImportDashAndHeaderTests
{
    private static readonly DateTime D = new(2026, 8, 3);

    [Fact]
    public void Dash_is_empty_in_income_expense()
    {
        var m = Map(null, F.Date, F.Income, F.Expense);
        var p = ImportRowParser.Parse(Sheet([D, "-", 5000], [D, " - ", "–"], [D, "$ -", "—"]),
            new ImportOptions(m, DateOrder.DayFirst, Currency.COP));
        var row = Assert.Single(p.Rows);
        Assert.Equal(TransactionType.Expense, row.Type);
        Assert.Equal(5000, row.Amount.AmountMinor);
        Assert.Equal([2, 3], p.Errors.Select(e => e.RowNumber));
        Assert.Contains("no tiene monto", p.Errors[0].Message);
    }

    [Fact]
    public void Dash_in_signed_mode_has_no_amount()
    {
        var p = ImportRowParser.Parse(Sheet([D, "$ -"]), new ImportOptions(Map(null, F.Date, F.Amount), DateOrder.DayFirst, Currency.COP));
        Assert.Contains("no tiene monto", Assert.Single(p.Errors).Message);
    }

    [Fact]
    public void Guesser_ignores_dashes_in_numeric_column()
    {
        var s = Sheet(["Col1", "Col2"], [D, 5], [D, "-"], [D, 7], [D, 8], [D, "-"]);
        Assert.Equal([F.Date, F.Amount], ImportColumnGuesser.Guess(s, 0).Columns);
    }

    [Fact]
    public void Expense_type_header_is_category()
    {
        var a = ImportColumnGuesser.Guess(Sheet(["Fecha", "Tipo de gasto", "Valor"], [D, "x", 5]), 0);
        Assert.Equal([F.Date, F.Category, F.Amount], a.Columns);
        Assert.Equal(AmountMode.Signed, a.AmountMode);
        var b = ImportColumnGuesser.Guess(Sheet(["Fecha", "Tipo", "Tipo de gasto", "Valor"], [D, "Gasto", "x", 5]), 0);
        Assert.Equal([F.Date, F.Type, F.Category, F.Amount], b.Columns);
    }

    [Fact]
    public void Text_fields_drop_the_formula_guard_apostrophe()
    {
        var m = Map(0, F.Date, F.Amount, F.Description, F.Notes, F.Merchant);
        var sheet = Sheet(
            ["Fecha", "Monto", "Descripción", "Notas", "Comercio"],
            [new DateTime(2026, 8, 3), 100m, "'=SUMA(A1)", "'\tx", "'ok"]);
        var row = Assert.Single(ImportRowParser.Parse(sheet, new ImportOptions(m, DateOrder.DayFirst, Currency.COP)).Rows);
        Assert.Equal("=SUMA(A1)", row.Description);
        Assert.Equal("x", row.Notes);
        Assert.Equal("'ok", row.Merchant);
    }
}
