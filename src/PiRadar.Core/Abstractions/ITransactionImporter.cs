using PiRadar.Core.Import;

namespace PiRadar.Core.Abstractions;

/// <summary>Guarda importaciones de Excel/CSV y las deshace por lote.</summary>
public interface ITransactionImporter
{
    /// <summary>Crea las categorías y medios que falten y guarda todos los movimientos, o nada si algo falla.</summary>
    /// <exception cref="Domain.DomainValidationException">El plan no es válido.</exception>
    Task<ImportResult> ImportAsync(ImportPlan plan);

    /// <summary>El lote más reciente que aún existe, o null.</summary>
    Task<ImportBatchInfo?> GetLastImportAsync();

    /// <summary>
    /// Borra los movimientos del lote <paramref name="batchId"/> y el lote (las categorías y medios creados se quedan); devuelve cuántos borró.
    /// Solo si ese lote sigue siendo el último.
    /// </summary>
    /// <exception cref="Domain.DomainValidationException">El último lote ya no es <paramref name="batchId"/>; no se borra nada.</exception>
    Task<int> UndoLastImportAsync(Guid batchId);
}
