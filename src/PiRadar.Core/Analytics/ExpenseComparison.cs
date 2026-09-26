using PiRadar.Core.Domain;

namespace PiRadar.Core.Analytics;

/// <summary>
/// Gasto del periodo en curso hasta hoy frente al mismo tramo del periodo anterior (del 1 al 26 de septiembre contra
/// del 1 al 26 de agosto), para no comparar un mes a medias con uno completo.
/// </summary>
public sealed record ExpenseComparison(Money Current, Money Previous)
{
    public Currency Currency => Current.Currency;

    /// <summary>Variación relativa (0,25 = 25 % más); <c>null</c> si el tramo anterior no tuvo gastos.</summary>
    public decimal? Change => Previous.IsZero ? null : (decimal)(Current - Previous).AmountMinor / Previous.AmountMinor;

    /// <summary>
    /// El tramo del periodo anterior equivalente a lo que va de <paramref name="current"/> hasta el final del día
    /// de <paramref name="now"/>. Si el periodo en curso ya terminó, es el periodo anterior completo.
    /// </summary>
    public static (DateTime Start, DateTime End) PreviousSpan(Period current, DateTime now)
    {
        var previous = current.Previous();
        if (now >= current.End)
        {
            return (previous.Start, previous.End);
        }

        var days = Math.Max(0, (now.Date - current.Start).Days) + 1;
        var end = previous.Start.AddDays(days);
        return (previous.Start, end < previous.End ? end : previous.End);
    }

    /// <summary>
    /// Una comparación por moneda con gastos en alguno de los dos tramos, en el orden de <see cref="Currency"/>.
    /// <paramref name="transactions"/> puede traer movimientos de ambos periodos (o más): se filtran por fecha.
    /// </summary>
    public static IReadOnlyList<ExpenseComparison> ByCurrency(IEnumerable<Transaction> transactions, Period current, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        var (previousStart, previousEnd) = PreviousSpan(current, now);
        var expenses = transactions.Where(t => t.Type == TransactionType.Expense).ToList();

        return expenses
            .Select(t => t.Amount.Currency)
            .Distinct()
            .Order()
            .Select(currency => new ExpenseComparison(
                Sum(expenses, currency, current.Start, current.End),
                Sum(expenses, currency, previousStart, previousEnd)))
            .Where(c => !c.Current.IsZero || !c.Previous.IsZero)
            .ToList();
    }

    private static Money Sum(IEnumerable<Transaction> expenses, Currency currency, DateTime start, DateTime end) =>
        new(expenses.Where(t => t.Amount.Currency == currency && t.OccurredAt >= start && t.OccurredAt < end)
            .Sum(t => t.Amount.AmountMinor), currency);
}
