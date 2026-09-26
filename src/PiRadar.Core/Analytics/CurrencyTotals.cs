using PiRadar.Core.Domain;

namespace PiRadar.Core.Analytics;

/// <summary>Gastos e ingresos de una moneda. Los totales nunca mezclan monedas.</summary>
public sealed record CurrencyTotals(Money Expenses, Money Income)
{
    public Currency Currency => Expenses.Currency;

    public Money Balance => Income - Expenses;

    /// <summary>Totales por moneda, solo de las monedas que aparecen, en el orden de <see cref="Currency"/>.</summary>
    public static IReadOnlyList<CurrencyTotals> ByCurrency(IEnumerable<Transaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        return transactions
            .GroupBy(t => t.Amount.Currency)
            .OrderBy(g => g.Key)
            .Select(g => new CurrencyTotals(Sum(g, TransactionType.Expense, g.Key), Sum(g, TransactionType.Income, g.Key)))
            .ToList();
    }

    private static Money Sum(IEnumerable<Transaction> transactions, TransactionType type, Currency currency) =>
        transactions.Where(t => t.Type == type).Aggregate(Money.Zero(currency), (sum, t) => sum + t.Amount);
}
