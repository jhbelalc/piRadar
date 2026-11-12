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

    /// <summary>
    /// Medio de pago de la búsqueda; <c>null</c> = todos y <see cref="TransactionSearch.NoPaymentMethod"/> = solo los
    /// movimientos sin medio de pago.
    /// </summary>
    public int? SearchPaymentMethodId { get; set; }

    /// <summary>La búsqueda la abrió Análisis al tocar una categoría o un medio de pago: Movimientos muestra la flecha para volver.</summary>
    public bool FromAnalytics { get; set; }

    /// <summary>Fechas de la búsqueda; al abrir y al cerrar la lupa vuelven al mes en curso hasta hoy.</summary>
    public SearchDateRange SearchRange { get; set; } = SearchDateRange.MonthToDate(DateOnly.FromDateTime(DateTime.Today));

    /// <summary>"Todo el historial": la búsqueda ignora <see cref="SearchRange"/> (que se conserva para cuando se apague).</summary>
    public bool SearchWholeHistory { get; set; }

    /// <summary>Con texto, categoría o medio de pago la lista ignora el periodo y usa el rango de la búsqueda o todo el historial.</summary>
    public bool IsSearching => SearchOpen
        && (!string.IsNullOrWhiteSpace(SearchText) || SearchCategoryId is not null || SearchPaymentMethodId is not null);

    /// <summary>
    /// Abre la búsqueda con lo que se tocó en Análisis: el periodo completo (del primer al último día), el tipo y la
    /// categoría o el medio de pago. Al tener filtro, la lista busca sola al abrir Movimientos.
    /// </summary>
    public void OpenFromAnalytics(Period period, TransactionType type, int? categoryId, int? paymentMethodId)
    {
        SearchOpen = true;
        SearchText = string.Empty;
        SearchCategoryId = categoryId;
        SearchPaymentMethodId = paymentMethodId;
        SearchRange = SearchDateRange.FromPeriod(period);
        SearchWholeHistory = false;
        Type = type;
        FromAnalytics = true;
    }

    /// <summary>Al abrir y al cerrar la lupa: fechas del mes en curso hasta hoy (si la app quedó abierta de un mes a otro, se sugiere el nuevo).</summary>
    public void ResetSearch(bool open)
    {
        SearchOpen = open;
        SearchRange = SearchDateRange.MonthToDate(DateOnly.FromDateTime(DateTime.Today));
        SearchWholeHistory = false;
        FromAnalytics = false;
        if (!open)
        {
            SearchText = string.Empty;
            SearchCategoryId = null;
            SearchPaymentMethodId = null;
        }
    }
}
