using MudBlazor;
using PiRadar.Core.Domain;

namespace PiRadar.App.Services;

/// <summary>Abre un enlace (correo, navegador) en otra app del teléfono; piRadar en sí no usa internet.</summary>
public sealed class LinkOpener(ISnackbar snackbar)
{
    public async Task OpenAsync(string uri)
    {
        try
        {
            if (!await Launcher.Default.TryOpenAsync(new Uri(uri)))
            {
                snackbar.Add(AppLocale.Pick("No hay una app en este teléfono para abrir el enlace.", "There is no app on this phone to open the link."), Severity.Warning);
            }
        }
        catch (Exception)
        {
            snackbar.Add(AppLocale.Pick("No se pudo abrir el enlace.", "The link could not be opened."), Severity.Warning);
        }
    }
}
