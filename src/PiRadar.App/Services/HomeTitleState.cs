using PiRadar.Core.Abstractions;
using PiRadar.Core.Domain;

namespace PiRadar.App.Services;

/// <summary>Título de la pantalla de inicio (Ajustes). El menú lateral se suscribe a <see cref="Changed"/>.</summary>
public sealed class HomeTitleState(ISettingsRepository settings)
{
    public string Title { get; private set; } = HomeTitle.Default;

    public event Action? Changed;

    /// <summary>Lee el título guardado; solo con la BD abierta (también tras restaurar un respaldo).</summary>
    public async Task LoadAsync() => Apply(await settings.GetHomeTitleAsync());

    /// <summary>Guarda el título (vacío vuelve al predeterminado) y devuelve el que quedó.</summary>
    public async Task<string> SetAsync(string? title)
    {
        Apply(await settings.SetHomeTitleAsync(title));
        return Title;
    }

    private void Apply(string title)
    {
        if (title == Title)
        {
            return;
        }

        Title = title;
        Changed?.Invoke();
    }
}
