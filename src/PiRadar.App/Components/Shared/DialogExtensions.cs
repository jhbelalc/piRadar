using MudBlazor;

namespace PiRadar.App.Components.Shared;

public static class DialogExtensions
{
    /// <summary>Pregunta de sí/no; <c>true</c> solo si el usuario confirma.</summary>
    public static async Task<bool> ConfirmAsync(this IDialogService dialogs, string title, string message, string confirmText) =>
        await dialogs.ShowMessageBoxAsync(title, message, yesText: confirmText, cancelText: "Cancelar") == true;

    public static DialogOptions FormOptions { get; } = new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        CloseButton = true,
        BackdropClick = false,
    };
}
