using PiRadar.Core.Analytics;
using PiRadar.Core.Domain;

namespace PiRadar.Core.Tests.Analytics;

public class CategoryShareTests
{
    private static readonly Dictionary<int, Category> Categories = new[]
    {
        New(1, "Comida"),
        New(2, "Restaurantes", parentId: 1),
        New(3, "Transporte"),
        New(4, "Parqueadero", parentId: 3),
    }.ToDictionary(c => c.Id);

    [Fact]
    public void Expenses_RollsSubcategoriesIntoParent_AndSortsDescending()
    {
        Transaction[] transactions =
        [
            Expense(2, 60000),
            Expense(1, 15000),
            Expense(4, 4000),
            Expense(3, 21000),
            Expense(3, 99, Currency.USD),
            Income(1, 500000),
        ];

        var shares = CategoryShare.Expenses(transactions, Currency.COP, Categories);

        Assert.Collection(shares,
            food =>
            {
                Assert.Equal(1, food.CategoryId);
                Assert.Equal(new Money(75000, Currency.COP), food.Total);
                Assert.Equal(0.75m, food.Share);
            },
            transport =>
            {
                Assert.Equal(3, transport.CategoryId);
                Assert.Equal(new Money(25000, Currency.COP), transport.Total);
                Assert.Equal(0.25m, transport.Share);
            });
    }

    [Fact]
    public void Expenses_UnknownCategory_CountsOnItsOwn()
    {
        var shares = CategoryShare.Expenses([Expense(99, 1000)], Currency.COP, Categories);

        Assert.Equal(99, Assert.Single(shares).CategoryId);
    }

    [Fact]
    public void Expenses_ParentCycle_DoesNotHang()
    {
        var cyclic = new Dictionary<int, Category> { [1] = New(1, "A", parentId: 2), [2] = New(2, "B", parentId: 1) };

        Assert.Single(CategoryShare.Expenses([Expense(1, 1000)], Currency.COP, cyclic));
    }

    [Fact]
    public void Expenses_IsEmptyWithoutExpensesInThatCurrency()
    {
        Assert.Empty(CategoryShare.Expenses([Expense(1, 1000)], Currency.USD, Categories));
    }

    private static Category New(int id, string name, int? parentId = null) =>
        new() { Id = id, Name = name, Icon = "category", Color = "#000000", ParentId = parentId };

    private static Transaction Expense(int categoryId, long minor, Currency currency = Currency.COP) =>
        new() { Type = TransactionType.Expense, Amount = new Money(minor, currency), CategoryId = categoryId, OccurredAt = new DateTime(2026, 9, 1) };

    private static Transaction Income(int categoryId, long minor) =>
        new() { Type = TransactionType.Income, Amount = new Money(minor, Currency.COP), CategoryId = categoryId, OccurredAt = new DateTime(2026, 9, 1) };
}
