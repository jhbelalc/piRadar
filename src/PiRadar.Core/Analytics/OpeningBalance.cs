using PiRadar.Core.Domain;

namespace PiRadar.Core.Analytics;

/// <summary>
/// Saldo inicial de un periodo en una moneda: lo que quedó (ingresos − gastos) con todos los movimientos anteriores a
/// su inicio. Es un valor calculado, no un movimiento guardado: siempre refleja los movimientos actuales aunque se
/// corrija o borre uno viejo. El saldo de cierre de un mes es el inicial del siguiente, así que se va heredando.
/// </summary>
/// <param name="Amount">Puede ser negativo (se gastó más de lo que entró).</param>
/// <param name="TransactionCount">Cuántos movimientos anteriores lo forman; con 0 no hay saldo que heredar.</param>
public sealed record OpeningBalance(Money Amount, int TransactionCount)
{
    public Currency Currency => Amount.Currency;

    public bool HasTransactions => TransactionCount > 0;

    /// <summary>Saldo inicial por moneda a partir de los movimientos anteriores al periodo, en el orden de <see cref="Currency"/>.</summary>
    public static IReadOnlyList<OpeningBalance> ByCurrency(IEnumerable<Transaction> before)
    {
        ArgumentNullException.ThrowIfNull(before);
        return before
            .GroupBy(t => t.Amount.Currency)
            .OrderBy(g => g.Key)
            .Select(g => new OpeningBalance(
                g.Aggregate(Money.Zero(g.Key), (sum, t) => t.Type == TransactionType.Income ? sum + t.Amount : sum - t.Amount),
                g.Count()))
            .ToList();
    }
}
