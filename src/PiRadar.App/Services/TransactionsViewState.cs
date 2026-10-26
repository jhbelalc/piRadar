using PiRadar.Core.Analytics;
using PiRadar.Core.Domain;

namespace PiRadar.App.Services;

/// <summary>Periodo y filtro de la lista de movimientos; se conservan al ir a editar uno y volver.</summary>
public sealed class TransactionsViewState
{
    public Period Period { get; set; } = Period.Containing(DateTime.Today, PeriodGranularity.Month);

    /// <summary><c>null</c> = gastos e ingresos.</summary>
    public TransactionType? Type { get; set; }

    /// <summary>Barra de búsqueda abierta.</summary>
    public bool SearchOpen { get; set; }

    /// <summary>Palabras clave de la búsqueda (vacío = sin texto).</summary>
    public string SearchText { get; set; } = string.Empty;

    /// <summary>Categoría de la búsqueda (incluye sus subcategorías); <c>null</c> = todas.</summary>
    public int? SearchCategoryId { get; set; }

    /// <summary>Fechas de la búsqueda; al abrir y al cerrar la lupa vuelven al mes en curso hasta hoy.</summary>
    public SearchDateRange SearchRange { get; set; } = SearchDateRange.MonthToDate(DateOnly.FromDateTime(DateTime.Today));

    /// <summary>"Todo el historial": la búsqueda ignora <see cref="SearchRange"/> (que se conserva para cuando se apague).</summary>
    public bool SearchWholeHistory { get; set; }

    /// <summary>Con texto o categoría la lista ignora el periodo y usa el rango de la búsqueda o todo el historial.</summary>
    public bool IsSearching => SearchOpen && (!string.IsNullOrWhiteSpace(SearchText) || SearchCategoryId is not null);
}
