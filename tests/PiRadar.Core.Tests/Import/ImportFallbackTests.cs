using PiRadar.Core.Domain;
using PiRadar.Core.Import;

namespace PiRadar.Core.Tests.Import;

public class ImportFallbackTests
{
    private static Category Cat(int id, string name, TransactionType kind, int order, int? parent = null, bool archived = false) =>
        new() { Id = id, Name = name, Kind = kind, Icon = "x", Color = "#000000", SortOrder = order, ParentId = parent, IsArchived = archived };

    [Fact]
    public void Picks_other_expenses_by_name_in_both_languages()
    {
        var es = new[] { Cat(1, "Mercado", TransactionType.Expense, 0), Cat(2, "Otros gastos", TransactionType.Expense, 1), Cat(3, "Ropa", TransactionType.Expense, 2) };
        var en = new[] { Cat(1, "Groceries", TransactionType.Expense, 0), Cat(2, "OTHER  expenses", TransactionType.Expense, 1), Cat(3, "Clothing", TransactionType.Expense, 2) };
        Assert.Equal(2, ImportCategoryMatcher.SuggestFallback(es, TransactionType.Expense));
        Assert.Equal(2, ImportCategoryMatcher.SuggestFallback(en, TransactionType.Expense));
    }

    [Fact]
    public void Picks_other_income_by_name()
    {
        var list = new[] { Cat(1, "Salario", TransactionType.Income, 0), Cat(2, "Otros ingresos", TransactionType.Income, 1), Cat(3, "Other income", TransactionType.Expense, 2) };
        Assert.Equal(2, ImportCategoryMatcher.SuggestFallback(list, TransactionType.Income));
        Assert.Equal(3, ImportCategoryMatcher.SuggestFallback([Cat(3, "Other income", TransactionType.Income, 2)], TransactionType.Income));
    }

    [Fact]
    public void Without_name_uses_last_main_category_of_the_type()
    {
        var list = new[]
        {
            Cat(1, "A", TransactionType.Expense, 0), Cat(2, "B", TransactionType.Expense, 5),
            Cat(3, "Sub", TransactionType.Expense, 9, parent: 1), Cat(4, "C", TransactionType.Income, 7),
            Cat(5, "Archivada", TransactionType.Expense, 8, archived: true),
        };
        Assert.Equal(2, ImportCategoryMatcher.SuggestFallback(list, TransactionType.Expense));
        Assert.Equal(4, ImportCategoryMatcher.SuggestFallback(list, TransactionType.Income));
    }

    [Fact]
    public void Ignores_archived_and_subcategories_named_other_and_returns_null_when_none()
    {
        var list = new[] { Cat(1, "Otros gastos", TransactionType.Expense, 0, archived: true), Cat(2, "Otros gastos", TransactionType.Expense, 1, parent: 9) };
        Assert.Null(ImportCategoryMatcher.SuggestFallback(list, TransactionType.Expense));
        Assert.Null(ImportCategoryMatcher.SuggestFallback([], TransactionType.Income));
    }
}
