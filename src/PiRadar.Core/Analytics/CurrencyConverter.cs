using PiRadar.Core.Domain;

namespace PiRadar.Core.Analytics;

/// <summary>
/// Convierte montos con las tasas ingresadas a mano: usa la última tasa del par con <c>EffectiveDate</c> ≤ la fecha
/// del movimiento. Si solo hay tasa del par inverso (COP→USD para pasar USD a COP), usa su inverso.
/// </summary>
public sealed class CurrencyConverter
{
    private readonly IReadOnlyList<ExchangeRate> _rates;

    public CurrencyConverter(IEnumerable<ExchangeRate> rates)
    {
        ArgumentNullException.ThrowIfNull(rates);
        _rates = rates.Where(r => r.RateMicros > 0).OrderByDescending(r => r.EffectiveDate).ToList();
    }

    /// <summary>Tasa para pasar 1 <paramref name="from"/> a <paramref name="to"/> en esa fecha; <c>null</c> si no hay.</summary>
    public decimal? RateAt(Currency from, Currency to, DateTime at)
    {
        if (from == to)
        {
            return 1m;
        }

        // Entre la directa y la inversa gana la más reciente; a igual fecha, la directa.
        var day = at.Date;
        var best = _rates
            .Where(r => r.EffectiveDate.Date <= day && ((r.From == from && r.To == to) || (r.From == to && r.To == from)))
            .OrderByDescending(r => r.EffectiveDate.Date)
            .ThenBy(r => r.From == from ? 0 : 1)
            .FirstOrDefault();
        return best is null ? null : best.From == from ? best.Rate : 1m / best.Rate;
    }

    /// <summary>El monto en <paramref name="to"/>, redondeado a su escala; <c>null</c> si no hay tasa.</summary>
    public Money? Convert(Money amount, Currency to, DateTime at)
    {
        if (RateAt(amount.Currency, to, at) is not { } rate)
        {
            return null;
        }

        var converted = decimal.Round(amount.ToDecimal() * rate, to.Scale(), MidpointRounding.AwayFromZero);
        return Money.FromDecimal(converted, to);
    }

    /// <summary>
    /// Copias de los movimientos con el monto en <paramref name="target"/>, para la vista consolidada. Los que no
    /// tienen tasa van aparte en <see cref="Consolidation.Missing"/> y no se suman.
    /// </summary>
    public Consolidation Consolidate(IEnumerable<Transaction> transactions, Currency target)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        var converted = new List<Transaction>();
        var missing = new List<Transaction>();
        foreach (var t in transactions)
        {
            if (Convert(t.Amount, target, t.OccurredAt) is { } amount)
            {
                converted.Add(CopyWith(t, amount));
            }
            else
            {
                missing.Add(t);
            }
        }

        return new Consolidation(converted, missing);
    }

    private static Transaction CopyWith(Transaction t, Money amount) => new()
    {
        Id = t.Id,
        Type = t.Type,
        Amount = amount,
        OccurredAt = t.OccurredAt,
        CategoryId = t.CategoryId,
        PaymentMethodId = t.PaymentMethodId,
        Merchant = t.Merchant,
        Description = t.Description,
        Notes = t.Notes,
        ReceiptId = t.ReceiptId,
        Source = t.Source,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
    };
}

/// <summary>Movimientos convertidos a una moneda y los que quedaron fuera por no tener tasa.</summary>
public sealed record Consolidation(IReadOnlyList<Transaction> Converted, IReadOnlyList<Transaction> Missing);
