using PiRadar.Core.Domain;

namespace PiRadar.Core.Abstractions;

/// <summary>Preferencias de la app guardadas en la BD.</summary>
public interface ISettingsRepository
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);

    /// <summary>Moneda de la vista consolidada; COP si no está definida.</summary>
    Task<Currency> GetBaseCurrencyAsync();

    Task SetBaseCurrencyAsync(Currency currency);

    /// <summary>Fecha UTC del último respaldo exportado, o <c>null</c> si nunca se ha hecho.</summary>
    Task<DateTime?> GetLastBackupAtAsync();

    Task SetLastBackupAtAsync(DateTime utc);
}
