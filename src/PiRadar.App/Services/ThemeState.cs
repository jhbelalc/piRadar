using PiRadar.Core.Abstractions;
using PiRadar.Data;

namespace PiRadar.App.Services;

public enum ThemePreference
{
    System,
    Light,
    Dark,
}

/// <summary>Tema elegido en Ajustes. <c>MainLayout</c> se suscribe a <see cref="Changed"/>.</summary>
public sealed class ThemeState(ISettingsRepository settings)
{
    public ThemePreference Preference { get; private set; } = ThemePreference.System;

    public event Action? Changed;

    /// <summary>Lee la preferencia guardada; solo con la BD abierta.</summary>
    public async Task LoadAsync()
    {
        var value = await settings.GetAsync(AppSettingKeys.Theme);
        Apply(Parse(value));
    }

    public async Task SetAsync(ThemePreference preference)
    {
        await settings.SetAsync(AppSettingKeys.Theme, preference.ToString());
        Apply(preference);
    }

    private void Apply(ThemePreference preference)
    {
        if (preference == Preference)
        {
            return;
        }

        Preference = preference;

        // La parte nativa (fondo de las barras del sistema y color de sus íconos) sigue la misma preferencia.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Application.Current is { } app)
            {
                app.UserAppTheme = preference switch
                {
                    ThemePreference.Light => Microsoft.Maui.ApplicationModel.AppTheme.Light,
                    ThemePreference.Dark => Microsoft.Maui.ApplicationModel.AppTheme.Dark,
                    _ => Microsoft.Maui.ApplicationModel.AppTheme.Unspecified,
                };
#if ANDROID
                // Android no cambia solo el color de los íconos de la barra de estado y de gestos.
                if (Platform.CurrentActivity?.Window is { } window
                    && AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView) is { } controller)
                {
                    var light = app.RequestedTheme != Microsoft.Maui.ApplicationModel.AppTheme.Dark;
                    controller.AppearanceLightStatusBars = light;
                    controller.AppearanceLightNavigationBars = light;
                }
#endif
            }
        });
        Changed?.Invoke();
    }

    private static ThemePreference Parse(string? value) => value switch
    {
        nameof(ThemePreference.Light) => ThemePreference.Light,
        nameof(ThemePreference.Dark) => ThemePreference.Dark,
        _ => ThemePreference.System,
    };
}
