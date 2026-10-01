using PiRadar.Core.Domain;

namespace PiRadar.Core.Analytics;

/// <summary>
/// Gastos e ingresos de una moneda. Los totales nunca mezclan monedas. <see cref="Opening"/> es el saldo heredado de los
/// periodos anteriores: no cuenta como ingreso ni como gasto, pero sí suma al <see cref="Balance"/>.
/// </summary>
public sealed record CurrencyTotals(Money Expenses, Money Income)
{
    private readonly Money? _opening;

    public Currency Currency => Expenses.Currency;

    /// <summary>Saldo con que empieza el periodo (cero si no hay movimientos anteriores).</summary>
    public Money Opening
    {
        get => _opening ?? Money.Zero(Currency);
        init => _opening = value;
    }

    /// <summary>Hay movimientos anteriores al periodo: se muestra la fila "Saldo inicial".</summary>
    public bool HasOpening { get; init; }

    /// <summary>Saldo inicial + ingresos − gastos.</summary>
    public Money Balance => Opening + Income - Expenses;

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

    /// <summary>
    /// Igual, pero con el saldo inicial: incluye también las monedas que solo tienen saldo heredado (un mes nuevo sin
    /// movimientos todavía), con gastos e ingresos en cero.
    /// </summary>
    public static IReadOnlyList<CurrencyTotals> ByCurrency(
        IEnumerable<Transaction> transactions, IEnumerable<OpeningBalance> openings)
    {
        ArgumentNullException.ThrowIfNull(openings);
        var period = ByCurrency(transactions).ToDictionary(t => t.Currency);
        var carried = openings.Where(o => o.HasTransactions).ToDictionary(o => o.Currency);
        return period.Keys.Union(carried.Keys).Order()
            .Select(currency =>
            {
                var totals = period.TryGetValue(currency, out var found)
                    ? found
                    : new CurrencyTotals(Money.Zero(currency), Money.Zero(currency));
                return carried.TryGetValue(currency, out var opening)
                    ? totals with { Opening = opening.Amount, HasOpening = true }
                    : totals;
            })
            .ToList();
    }

    private static Money Sum(IEnumerable<Transaction> transactions, TransactionType type, Currency currency) =>
        transactions.Where(t => t.Type == type).Aggregate(Money.Zero(currency), (sum, t) => sum + t.Amount);
}
