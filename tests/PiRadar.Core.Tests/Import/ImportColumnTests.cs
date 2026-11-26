using PiRadar.Core.Domain;
using PiRadar.Core.Import;
using static PiRadar.Core.Tests.Import.ImportTestSheets;
using F = PiRadar.Core.Import.ImportField;

namespace PiRadar.Core.Tests.Import;

public class ImportColumnTests
{
    private static readonly DateTime D = new(2026, 8, 3);

    [Fact]
    public void Header_in_first_row()
    {
        var s = Sheet(["Fecha", "Valor"], [D, 100]);
        Assert.Equal(0, HeaderDetector.Find(s));
    }

    [Fact]
    public void Title_empty_row_then_header()
    {
        var s = Sheet(["Mis gastos 2026", null], [null, null], ["Fecha", "Valor"], [D, 100]);
        Assert.Equal(2, HeaderDetector.Find(s));
    }

    [Fact]
    public void No_header_when_only_data()
    {
        var s = Sheet([D, "Mercado", 100], [D, "Taxi", 200]);
        Assert.Null(HeaderDetector.Find(s));
    }

    [Fact]
    public void No_header_when_text_rows_look_like_data()
    {
        var s = Sheet(["03/08/2026", "Mercado", "5.000"], ["04/08/2026", "Taxi", "6.000"]);
        Assert.Null(HeaderDetector.Find(s));
    }

    [Fact]
    public void Guess_income_expense_headers()
    {
        var s = Sheet(["Descripción", "Fecha", "Ingreso", "Gasto", "Categoria", "Medio de pago", "Moneda"],
            ["x", D, null, 5, "a", "b", "COP"]);
        var m = ImportColumnGuesser.Guess(s, 0);
        Assert.Equal([F.Description, F.Date, F.Income, F.Expense, F.Category, F.PaymentMethod, F.Currency], m.Columns);
        Assert.Equal(AmountMode.IncomeExpense, m.AmountMode);
        Assert.True(m.IsComplete);
    }

    [Fact]
    public void Guess_same_headers_other_order()
    {
        var s = Sheet(["Moneda", "Gasto", "Medio de pago", "Fecha", "Categoria", "Ingreso", "Descripción"],
            ["COP", 5, "b", D, "a", null, "x"]);
        var m = ImportColumnGuesser.Guess(s, 0);
        Assert.Equal([F.Currency, F.Expense, F.PaymentMethod, F.Date, F.Category, F.Income, F.Description], m.Columns);
    }

    [Fact]
    public void Guess_english()
    {
        var s = Sheet(["Date", "Amount", "Type", "Category", "Payee", "Account"], [D, 5, "Expense", "Food", "Shop", "Visa"]);
        var m = ImportColumnGuesser.Guess(s, 0);
        Assert.Equal([F.Date, F.Amount, F.Type, F.Category, F.Merchant, F.PaymentMethod], m.Columns);
        Assert.Equal(AmountMode.AmountWithType, m.AmountMode);
    }

    [Fact]
    public void Subcategory_does_not_fall_in_category()
    {
        var s = Sheet(["Fecha", "Subcategoría", "Categoría", "Valor"], [D, "a", "b", 5]);
        var m = ImportColumnGuesser.Guess(s, 0);
        Assert.Equal([F.Date, F.Subcategory, F.Category, F.Amount], m.Columns);
        Assert.Equal(AmountMode.Signed, m.AmountMode);
    }

    [Fact]
    public void Movement_type_and_value()
    {
        var s = Sheet(["Fecha", "Tipo de movimiento", "Valor"], [D, "Gasto", 5]);
        var m = ImportColumnGuesser.Guess(s, 0);
        Assert.Equal([F.Date, F.Type, F.Amount], m.Columns);
    }

    [Fact]
    public void Odd_headers_use_content()
    {
        var s = Sheet(["Col1", "Col2", "Col3"], [D, "Mercado", 5000], [D.AddDays(1), "Taxi", 3000], [D.AddDays(2), "Cine", 9000]);
        var m = ImportColumnGuesser.Guess(s, 0);
        Assert.Equal([F.Date, F.Description, F.Amount], m.Columns);
    }

    [Fact]
    public void No_header_uses_content_with_text_dates()
    {
        var s = Sheet(["03/08/2026", "Mercado", "5.000"], ["04/08/2026", "Taxi", "6.000"]);
        var m = ImportColumnGuesser.Guess(s, null);
        Assert.Equal([F.Date, F.Description, F.Amount], m.Columns);
        Assert.Null(m.Fingerprint(s));
    }

    [Fact]
    public void Extra_column_is_ignored()
    {
        var s = Sheet(["Fecha", "Descripción", "Valor", "Proyecto"], [D, "x", 5, "Casa"]);
        var m = ImportColumnGuesser.Guess(s, 0);
        Assert.Equal([F.Date, F.Description, F.Amount, F.Ignore], m.Columns);
        Assert.Equal("FECHA|DESCRIPCION|VALOR|PROYECTO", m.Fingerprint(s));
    }

    [Theory]
    [InlineData("Fecha;Hora;Tipo;Monto;Moneda;Categoría;Subcategoría;Medio de pago;Comercio;Descripción;Notas")]
    [InlineData("Date,Time,Type,Amount,Currency,Category,Subcategory,Payment method,Merchant,Description,Notes")]
    public void Piradar_csv_headers(string header)
    {
        var s = Sheet(header.Split(header.Contains(';') ? ';' : ',').Cast<object?>().ToArray(),
            ["2026-08-03", "14:30", "Gasto", "5000", "COP", "a", "b", "c", "d", "e", "f"]);
        var m = ImportColumnGuesser.Guess(s, 0);
        Assert.Equal([F.Date, F.Time, F.Type, F.Amount, F.Currency, F.Category, F.Subcategory, F.PaymentMethod,
            F.Merchant, F.Description, F.Notes], m.Columns);
    }

    [Fact]
    public void With_moves_field_and_amount_excludes_income_expense()
    {
        var m = Map(0, F.Date, F.Income, F.Expense, F.Ignore);
        var moved = m.With(3, F.Income);
        Assert.Equal([F.Date, F.Ignore, F.Expense, F.Income], moved.Columns);
        var amount = m.With(3, F.Amount);
        Assert.Equal([F.Date, F.Ignore, F.Ignore, F.Amount], amount.Columns);
        var back = amount.With(1, F.Expense);
        Assert.Equal([F.Date, F.Expense, F.Ignore, F.Ignore], back.Columns);
    }

    [Fact]
    public void Matcher_categories_and_payment_methods()
    {
        Category C(int id, string name, TransactionType k, bool archived = false, int? parent = null) =>
            new() { Id = id, Name = name, Kind = k, Icon = "i", Color = "#000000", IsArchived = archived, ParentId = parent };
        var cats = new[]
        {
            C(1, "Mercado", TransactionType.Expense), C(2, "Salario", TransactionType.Income),
            C(3, "Viejo", TransactionType.Expense, archived: true), C(4, "Frutas", TransactionType.Expense, parent: 1),
        };
        var names = new[]
        {
            new ImportName("mercado ", TransactionType.Expense, 1), new ImportName("Salario", TransactionType.Expense, 1),
            new ImportName("Viejo", TransactionType.Expense, 1),
        };
        var r = ImportCategoryMatcher.Propose(names, cats);
        Assert.Equal(1, r[names[0]]);
        Assert.Null(r[names[1]]);
        Assert.Null(r[names[2]]);
        Assert.Equal(4, ImportCategoryMatcher.FindSubcategory("frutas", 1, TransactionType.Expense, cats));
        Assert.Null(ImportCategoryMatcher.FindSubcategory("frutas", 2, TransactionType.Expense, cats));

        var pms = new[]
        {
            new PaymentMethod { Id = 7, Name = "Tarjeta débito", Icon = "i" },
            new PaymentMethod { Id = 8, Name = "Vieja", Icon = "i", IsArchived = true },
        };
        var pn = new[] { new ImportName("TARJETA DEBITO", TransactionType.Expense, 1), new ImportName("Vieja", TransactionType.Expense, 1) };
        var pr = ImportCategoryMatcher.ProposePaymentMethods(pn, pms);
        Assert.Equal(7, pr[pn[0]]);
        Assert.Null(pr[pn[1]]);
    }
}
