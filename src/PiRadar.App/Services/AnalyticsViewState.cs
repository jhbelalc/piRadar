using PiRadar.Core.Analytics;
using PiRadar.Core.Domain;

namespace PiRadar.App.Services;

/// <summary>Periodo, moneda y tipo de la página de análisis; se conservan al salir y volver.</summary>
public sealed class AnalyticsViewState
{
    public Period Period { get; set; } = Period.Containing(DateTime.Today, PeriodGranularity.Month);

    /// <summary>Moneda elegida; <c>null</c> = la moneda base si hay movimientos en ella.</summary>
    public Currency? Currency { get; set; }

    /// <summary>Todo convertido a la moneda base con las tasas de cambio.</summary>
    public bool Consolidated { get; set; }

    /// <summary>Qué se desglosa por categoría y medio de pago.</summary>
    public TransactionType Type { get; set; } = TransactionType.Expense;
}
