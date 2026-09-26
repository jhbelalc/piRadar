using PiRadar.Core.Domain;

namespace PiRadar.Core.Analytics;

/// <summary>Gastos e ingresos de un tramo del periodo (una hora, un día o un mes), en una moneda.</summary>
public sealed record BucketTotals(DateTime Start, DateTime End, string Label, Money Expenses, Money Income);

/// <summary>Total de un medio de pago (<c>null</c> = sin medio de pago) y su proporción (0–1).</summary>
public sealed record PaymentMethodShare(int? PaymentMethodId, Money Total, decimal Share);

/// <summary>Agregaciones en memoria para la página de análisis. Nunca mezcla monedas.</summary>
public static class SpendingAnalyzer
{
    /// <summary>
    /// Tramos del periodo para el gráfico: horas de un día (etiqueta cada 6 h), días de una semana ("lu", "ma"…),
    /// días de un mes (etiqueta el 1 y cada 5) y meses de un año ("ene", "feb"…). Las etiquetas vacías son a propósito,
    /// para que no se amontonen en el teléfono.
    /// </summary>
    public static IReadOnlyList<(DateTime Start, DateTime End, string Label)> Buckets(Period period)
    {
        var result = new List<(DateTime, DateTime, string)>();
        switch (period.Granularity)
        {
            case PeriodGranularity.Day:
                for (var h = 0; h < 24; h++)
                {
                    result.Add((period.Start.AddHours(h), period.Start.AddHours(h + 1), h % 6 == 0 ? $"{h}h" : ""));
                }

                break;
            case PeriodGranularity.Week:
                for (var d = period.Start; d < period.End; d = d.AddDays(1))
                {
                    result.Add((d, d.AddDays(1), DateLabels.DayName(d.DayOfWeek)[..2]));
                }

                break;
            case PeriodGranularity.Month:
                for (var d = period.Start; d < period.End; d = d.AddDays(1))
                {
                    result.Add((d, d.AddDays(1), d.Day == 1 || d.Day % 5 == 0 ? d.Day.ToString(System.Globalization.CultureInfo.InvariantCulture) : ""));
                }

                break;
            default:
                for (var m = period.Start; m < period.End; m = m.AddMonths(1))
                {
                    result.Add((m, m.AddMonths(1), DateLabels.MonthAbbreviation(m.Month)));
                }

                break;
        }

        return result;
    }

    /// <summary>Gastos e ingresos en <paramref name="currency"/> por cada tramo de <see cref="Buckets"/>.</summary>
    public static IReadOnlyList<BucketTotals> Series(IEnumerable<Transaction> transactions, Period period, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        var inCurrency = transactions.Where(t => t.Amount.Currency == currency && period.Contains(t.OccurredAt)).ToList();
        return Buckets(period)
            .Select(b =>
            {
                var inBucket = inCurrency.Where(t => t.OccurredAt >= b.Start && t.OccurredAt < b.End).ToList();
                return new BucketTotals(b.Start, b.End, b.Label,
                    Sum(inBucket, TransactionType.Expense, currency), Sum(inBucket, TransactionType.Income, currency));
            })
            .ToList();
    }

    /// <summary>Totales por medio de pago, del mayor al menor; "sin medio de pago" va al final.</summary>
    public static IReadOnlyList<PaymentMethodShare> ByPaymentMethod(IEnumerable<Transaction> transactions,
        TransactionType type, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        var totals = transactions
            .Where(t => t.Type == type && t.Amount.Currency == currency)
            .GroupBy(t => t.PaymentMethodId)
            .Select(g => (Id: g.Key, Minor: g.Sum(t => t.Amount.AmountMinor)))
            .ToList();
        var all = totals.Sum(t => t.Minor);

        return totals
            .OrderBy(t => t.Id is null)
            .ThenByDescending(t => t.Minor)
            .ThenBy(t => t.Id)
            .Select(t => new PaymentMethodShare(t.Id, new Money(t.Minor, currency), all == 0 ? 0 : (decimal)t.Minor / all))
            .ToList();
    }

    /// <summary>
    /// Días del periodo que ya pasaron (incluido hoy), para promediar: un mes en curso al día 26 cuenta 26 días,
    /// uno terminado cuenta todos. 0 si el periodo es futuro.
    /// </summary>
    public static int ElapsedDays(Period period, DateTime today)
    {
        var end = period.End < today.Date.AddDays(1) ? period.End : today.Date.AddDays(1);
        return Math.Max(0, (end - period.Start).Days);
    }

    /// <summary>Promedio diario de <paramref name="total"/>, redondeado a la escala de la moneda; <c>null</c> sin días.</summary>
    public static Money? DailyAverage(Money total, Period period, DateTime today)
    {
        var days = ElapsedDays(period, today);
        if (days == 0)
        {
            return null;
        }

        var minor = decimal.Round((decimal)total.AmountMinor / days, 0, MidpointRounding.AwayFromZero);
        return new Money((long)minor, total.Currency);
    }

    private static Money Sum(IEnumerable<Transaction> transactions, TransactionType type, Currency currency) =>
        new(transactions.Where(t => t.Type == type).Sum(t => t.Amount.AmountMinor), currency);
}
