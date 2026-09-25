namespace PiRadar.Core.Domain;

/// <summary>
/// Filtro de movimientos. <see cref="From"/> es inclusivo y <see cref="To"/> exclusivo, ambos en hora local
/// sobre <c>OccurredAt</c>. Los resultados salen del más reciente al más antiguo.
/// </summary>
public sealed record TransactionFilter
{
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public TransactionType? Type { get; init; }
    public Currency? Currency { get; init; }
    public int? CategoryId { get; init; }
    public int? PaymentMethodId { get; init; }

    /// <summary>Para paginar la lista.</summary>
    public int Skip { get; init; }

    /// <summary><c>null</c> = todos.</summary>
    public int? Take { get; init; }

    public static TransactionFilter All { get; } = new();

    /// <summary>Movimientos con <c>OccurredAt</c> en <c>[from, to)</c>.</summary>
    public static TransactionFilter Between(DateTime from, DateTime to) => new() { From = from, To = to };
}
