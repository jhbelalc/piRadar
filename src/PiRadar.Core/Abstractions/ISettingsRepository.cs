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

    /// <summary>Título de la pantalla de inicio; <see cref="HomeTitle.Default"/> si no se ha cambiado.</summary>
    Task<string> GetHomeTitleAsync();

    /// <summary>
    /// Guarda el título normalizado (<see cref="HomeTitle.Normalize"/>). Vacío o igual al predeterminado vuelve al
    /// predeterminado. Devuelve el título que queda.
    /// </summary>
    Task<string> SetHomeTitleAsync(string? title);

    /// <summary>Idioma elegido por el usuario, o <c>null</c> para seguir el idioma del dispositivo.</summary>
    Task<AppLanguage?> GetLanguageAsync();

    /// <summary><c>null</c> vuelve a seguir el idioma del dispositivo.</summary>
    Task SetLanguageAsync(AppLanguage? language);
}
