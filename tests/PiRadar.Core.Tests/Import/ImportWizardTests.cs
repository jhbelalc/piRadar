using PiRadar.Core.Domain;
using PiRadar.Core.Import;

namespace PiRadar.Core.Tests.Import;

public class ImportWizardTests
{
    private static ImportRow Row(int n, TransactionType type = TransactionType.Expense, string? cat = null, string? pm = null,
        int day = 3) =>
        new(n, type, new Money(1000, Currency.COP), new DateTime(2026, 8, day, 12, 0, 0), cat, null, pm,
            null, null, null, false, false);

    private static SheetData Sheet(params string?[][] rows) =>
        new("S", rows.Select(r => (IReadOnlyList<CellValue>)r.Select(t => CellValue.FromText(t)).ToArray()).ToList(),
            rows.Max(r => r.Length));

    [Fact]
    public void Samples_skip_header_and_empty_and_truncate()
    {
        var sheet = Sheet(["Fecha", "Nota"], ["1", null], ["2", new string('x', 60)], ["3", "c"], ["4", "d"]);
        var notes = ImportWizard.Samples(sheet, 1, 0);
        Assert.Equal(3, notes.Count);
        Assert.Equal(40, notes[0].Length);
        Assert.EndsWith("…", notes[0]);
        Assert.Equal(["c", "d"], notes.Skip(1));
        Assert.Equal(["1", "2", "3"], ImportWizard.Samples(sheet, 0, 0));
        Assert.Equal("Nota", ImportWizard.Header(sheet, 1, 0));
        Assert.Null(ImportWizard.Header(sheet, 1, null));
    }

    [Fact]
    public void Samples_show_binary_numbers_with_two_decimals_at_most()
    {
        IReadOnlyList<CellValue>[] rows =
        [
            [CellValue.FromText("Ingreso")],
            [CellValue.FromNumber(3456789.3499999996m)],
            [CellValue.FromNumber(437.05m)],
            [CellValue.FromNumber(18000m)],
        ];
        var sheet = new SheetData("S", rows, 1);
        Assert.Equal(["3456789.35", "437.05", "18000"], ImportWizard.Samples(sheet, 0, 0));
    }

    [Fact]
    public void Samples_out_of_range_column_is_empty()
    {
        Assert.Empty(ImportWizard.Samples(Sheet(["a"]), 5, null));
    }

    [Fact]
    public void Fallbacks_needed_per_type()
    {
        var (e, i) = ImportWizard.FallbacksNeeded([Row(2, cat: "A"), Row(3, TransactionType.Income)]);
        Assert.False(e);
        Assert.True(i);
    }

    [Fact]
    public void Included_rows_drop_duplicates_unless_included()
    {
        var preview = new ImportPreview { Rows = [Row(2), Row(3), Row(4)] };
        var dup = new HashSet<int> { 3 };
        Assert.Equal([2, 4], ImportWizard.IncludedRows(preview, dup, false).Select(r => r.RowNumber));
        Assert.Equal(3, ImportWizard.IncludedRows(preview, dup, true).Count);
    }

    [Fact]
    public void Plan_keeps_only_choices_and_fallbacks_that_included_rows_use()
    {
        var rows = new[] { Row(2, cat: "Comida", pm: "Nequi"), Row(3, TransactionType.Income) };
        var plan = ImportWizard.BuildPlan("a.csv", rows,
            [new CategoryChoice("comida", TransactionType.Expense, 5), new CategoryChoice("Otra", TransactionType.Expense, null),
             new CategoryChoice("Comida", TransactionType.Income, null)],
            [new PaymentMethodChoice("NEQUI", 9), new PaymentMethodChoice("Efectivo", null)],
            fallbackExpenseCategoryId: 1, fallbackIncomeCategoryId: 2);
        Assert.Equal("a.csv", plan.FileName);
        Assert.Single(plan.Categories);
        Assert.Equal(5, plan.Categories[0].ExistingId);
        Assert.Single(plan.PaymentMethods);
        Assert.Null(plan.FallbackExpenseCategoryId);
        Assert.Equal(2, plan.FallbackIncomeCategoryId);
    }

    [Fact]
    public void Date_range_is_inclusive_of_last_day()
    {
        var range = ImportWizard.DateRange([Row(2, day: 5), Row(3, day: 2)]);
        Assert.Equal(new DateTime(2026, 8, 2), range!.Value.From);
        Assert.Equal(new DateTime(2026, 8, 6), range.Value.To);
        Assert.Null(ImportWizard.DateRange([]));
    }

    [Fact]
    public void Summarize_groups_by_currency_and_sums()
    {
        var usd = new ImportRow(9, TransactionType.Income, new Money(250, Currency.USD), new DateTime(2026, 9, 1), null, null, null,
            null, null, null, false, false);
        var rows = new[] { usd, Row(2, day: 5), Row(3, TransactionType.Income, day: 1) };
        var s = ImportWizard.Summarize(rows);
        Assert.Equal([Currency.COP, Currency.USD], s.Select(x => x.Currency));
        Assert.Equal(2, s[0].Count);
        Assert.Equal(1000, s[0].Income.AmountMinor);
        Assert.Equal(1000, s[0].Expense.AmountMinor);
        Assert.Equal(new DateTime(2026, 8, 1, 12, 0, 0), s[0].From);
    }

    [Fact]
    public void Subcategories_match_parent_ignoring_case_and_accents()
    {
        var preview = new ImportPreview
        {
            Subcategories = [new ImportSubName("COMIDA", "Cena", TransactionType.Expense, 2),
                             new ImportSubName("Comida", "X", TransactionType.Income, 1)],
        };
        var subs = ImportWizard.SubcategoriesOf(preview, new ImportName("Comída", TransactionType.Expense, 5));
        Assert.Equal(["Cena"], subs.Select(x => x.Name));
    }
}
