using MudBlazor;

namespace PiRadar.App.Services;

/// <summary>Tema MudBlazor de piRadar. Fuente del sistema: la app no descarga nada (funciona sin red).</summary>
public static class AppTheme
{
    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#00695C",
            Secondary = "#F9A825",
            Tertiary = "#3949AB",
            Success = "#2E7D32",
            Error = "#C62828",
            AppbarBackground = "#00695C",
            Background = "#F6F7F8",
            DrawerBackground = "#FFFFFF",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#4DB6AC",
            PrimaryContrastText = "#0B2A27",
            Secondary = "#FFCA28",
            Tertiary = "#7986CB",
            Success = "#66BB6A",
            Error = "#EF5350",
            AppbarBackground = "#1E2528",
            Background = "#121416",
            Surface = "#1C2023",
            DrawerBackground = "#1C2023",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["-apple-system", "BlinkMacSystemFont", "Roboto", "Segoe UI", "Helvetica Neue", "Arial", "sans-serif"],
            },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px",
            DrawerWidthLeft = "232px",
        },
    };
}
