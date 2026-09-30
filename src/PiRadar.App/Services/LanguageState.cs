using System.Globalization;
using PiRadar.Core.Abstractions;
using PiRadar.Core.Domain;

namespace PiRadar.App.Services;

/// <summary>
/// Idioma de la interfaz (Ajustes): "Sistema" sigue el del teléfono (inglés o español; cualquier otro cae en español),
/// o se fija en español o inglés. Aplica el idioma a <see cref="AppLocale"/> y a la cultura de los hilos.
/// </summary>
public sealed class LanguageState(ISettingsRepository settings, ISystemNameLocalizer systemNames)
{
    private static readonly AppLanguage Device = AppLocale.FromDevice(CultureInfo.CurrentUICulture);

    /// <summary>Lo elegido en Ajustes; <c>null</c> = seguir el idioma del teléfono.</summary>
    public AppLanguage? Preference { get; private set; }

    /// <summary>Aplica el idioma del teléfono; se llama al arrancar, antes de abrir la BD.</summary>
    public static void ApplyDeviceLanguage() => Apply(Device);

    /// <summary>Lee la preferencia guardada y aplica el idioma; solo con la BD abierta.</summary>
    public async Task LoadAsync()
    {
        Preference = await settings.GetLanguageAsync();
        Apply(Preference ?? Device);
    }

    /// <summary>
    /// Igual que <see cref="LoadAsync"/> pero además traduce los nombres de categorías y medios de pago que trae la
    /// app: tras restaurar un respaldo hecho en el otro idioma.
    /// </summary>
    public async Task ReloadAfterRestoreAsync()
    {
        await LoadAsync();
        await systemNames.ApplyAsync(AppLocale.Language);
    }

    /// <summary>Guarda la preferencia, aplica el idioma y traduce los nombres de sistema. La UI debe recargarse después.</summary>
    public async Task SetAsync(AppLanguage? preference)
    {
        await settings.SetLanguageAsync(preference);
        Preference = preference;
        var language = preference ?? Device;
        Apply(language);
        await systemNames.ApplyAsync(language);
    }

    private static void Apply(AppLanguage language)
    {
        AppLocale.Language = language;
        var culture = AppLocale.Culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
