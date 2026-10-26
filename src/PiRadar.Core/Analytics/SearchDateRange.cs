namespace PiRadar.Core.Analytics;

/// <summary>
/// Fechas de la búsqueda de movimientos: de <see cref="From"/> a <see cref="To"/>, ambos días completos.
/// Nunca queda al revés: al mover una punta más allá de la otra, la otra pasa a ese mismo día.
/// </summary>
public sealed record SearchDateRange(DateOnly From, DateOnly To)
{
    /// <summary>Lo que se sugiere al abrir la búsqueda: del día 1 del mes hasta hoy.</summary>
    public static SearchDateRange MonthToDate(DateOnly today) => new(new DateOnly(today.Year, today.Month, 1), today);

    public SearchDateRange WithFrom(DateOnly from) => from > To ? new(from, from) : this with { From = from };

    public SearchDateRange WithTo(DateOnly to) => to < From ? new(to, to) : this with { To = to };

    /// <summary>Inicio del primer día (inclusivo), en hora local como <c>OccurredAt</c>.</summary>
    public DateTime Start => From.ToDateTime(TimeOnly.MinValue);

    /// <summary>Inicio del día siguiente al último (exclusivo, como <see cref="Domain.TransactionFilter.To"/>).</summary>
    public DateTime EndExclusive => To.AddDays(1).ToDateTime(TimeOnly.MinValue);
}
