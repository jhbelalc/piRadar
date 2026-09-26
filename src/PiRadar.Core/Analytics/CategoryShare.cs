using PiRadar.Core.Domain;

namespace PiRadar.Core.Analytics;

/// <summary>Total de una categoría principal en una moneda y su proporción (0–1) sobre el total del tipo.</summary>
public sealed record CategoryShare(int CategoryId, Money Total, decimal Share)
{
    /// <summary>
    /// Gastos en <paramref name="currency"/> agrupados por categoría principal: las subcategorías suman en su padre.
    /// Del mayor al menor. Una categoría que no está en <paramref name="categories"/> cuenta por sí sola.
    /// </summary>
    public static IReadOnlyList<CategoryShare> Expenses(IEnumerable<Transaction> transactions, Currency currency,
        IReadOnlyDictionary<int, Category> categories) =>
        ByCategory(transactions, TransactionType.Expense, currency, categories);

    /// <summary>Como <see cref="Expenses"/>, para gastos o ingresos.</summary>
    public static IReadOnlyList<CategoryShare> ByCategory(IEnumerable<Transaction> transactions, TransactionType type,
        Currency currency, IReadOnlyDictionary<int, Category> categories)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(categories);

        var totals = transactions
            .Where(t => t.Type == type && t.Amount.Currency == currency)
            .GroupBy(t => RootId(t.CategoryId, categories))
            .Select(g => (Id: g.Key, Minor: g.Sum(t => t.Amount.AmountMinor)))
            .ToList();
        var all = totals.Sum(t => t.Minor);

        return totals
            .OrderByDescending(t => t.Minor)
            .ThenBy(t => t.Id)
            .Select(t => new CategoryShare(t.Id, new Money(t.Minor, currency), all == 0 ? 0 : (decimal)t.Minor / all))
            .ToList();
    }

    private static int RootId(int id, IReadOnlyDictionary<int, Category> categories)
    {
        // Límite de saltos por si una BD dañada trae un ciclo.
        for (var hops = 0; hops < 10 && categories.GetValueOrDefault(id)?.ParentId is { } parent; hops++)
        {
            id = parent;
        }

        return id;
    }
}
