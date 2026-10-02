using PiRadar.Core.Domain;

namespace PiRadar.Core.Tests;

public class DefaultCategoriesTests
{
    private static Category New(int id, string name, string icon = "home", bool system = true, bool archived = false,
        TransactionType kind = TransactionType.Expense, int? parent = null) =>
        new() { Id = id, Name = name, Icon = icon, Color = "#757575", IsSystem = system, IsArchived = archived, Kind = kind, ParentId = parent };

    [Fact]
    public void Finds_the_seed_category_by_its_icon_even_if_renamed()
    {
        var found = DefaultCategories.FindOtherExpense(
            [New(1, "Mercado", "shopping_cart"), New(2, "Varios", "more_horiz"), New(3, "Otros ingresos", "add_circle", kind: TransactionType.Income)]);

        Assert.Equal(2, found?.Id);
    }

    [Theory]
    [InlineData("Otros gastos")]
    [InlineData("Other expenses")]
    [InlineData("OTROS GASTOS")]
    public void Falls_back_to_the_name_in_either_language(string name)
    {
        Assert.Equal(7, DefaultCategories.FindOtherExpense([New(1, "Mercado"), New(7, name, "more_vert")])?.Id);
    }

    [Fact]
    public void Ignores_archived_user_created_income_and_subcategories()
    {
        Category[] categories =
        [
            New(1, "Otros gastos", "more_horiz", archived: true),
            New(2, "Otros gastos", "more_horiz", system: false),
            New(3, "Otros ingresos", "more_horiz", kind: TransactionType.Income),
            New(4, "Otros gastos", "more_horiz", parent: 9),
        ];

        Assert.Null(DefaultCategories.FindOtherExpense(categories));
    }

    [Fact]
    public void Returns_null_when_there_is_none() => Assert.Null(DefaultCategories.FindOtherExpense([]));
}
