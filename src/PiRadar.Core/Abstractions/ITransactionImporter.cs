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

    /// <summary>Borra los movimientos del último lote y el lote (las categorías y medios creados se quedan); devuelve cuántos borró.</summary>
    Task<int> UndoLastImportAsync();
}
