using PiRadar.Core.Analytics;
using PiRadar.Core.Domain;

namespace PiRadar.App.Services;

/// <summary>Periodo y filtro de la lista de movimientos; se conservan al ir a editar uno y volver.</summary>
public sealed class TransactionsViewState
{
    public Period Period { get; set; } = Period.Containing(DateTime.Today, PeriodGranularity.Month);

    /// <summary><c>null</c> = gastos e ingresos.</summary>
    public TransactionType? Type { get; set; }
}
