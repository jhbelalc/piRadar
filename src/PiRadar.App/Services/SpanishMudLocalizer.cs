using Microsoft.Extensions.Localization;
using MudBlazor;

namespace PiRadar.App.Services;

/// <summary>Textos internos de MudBlazor en español (botones de diálogos, selectores, etc.).</summary>
public sealed class SpanishMudLocalizer : MudLocalizer
{
    private static readonly Dictionary<string, string> Texts = new()
    {
        ["MudAlert_Close"] = "Cerrar",
        ["MudBaseDatePicker_NextMonth"] = "Mes siguiente",
        ["MudBaseDatePicker_NextYear"] = "Año siguiente",
        ["MudBaseDatePicker_Open"] = "Abrir calendario",
        ["MudBaseDatePicker_PrevMonth"] = "Mes anterior",
        ["MudBaseDatePicker_PrevYear"] = "Año anterior",
        ["MudChip_Close"] = "Quitar",
        ["MudDialog_Close"] = "Cerrar",
        ["MudFormComponent_Required"] = "Obligatorio",
        ["MudInput_Clear"] = "Borrar",
        ["MudInput_Decrement"] = "Disminuir",
        ["MudInput_Increment"] = "Aumentar",
        ["MudSelect_SelectAll"] = "Seleccionar todo",
        ["MudSnackbar_Close"] = "Cerrar",
        ["MudTabs_ScrollLeft"] = "Desplazar a la izquierda",
        ["MudTabs_ScrollRight"] = "Desplazar a la derecha",
        ["MudTimePicker_Open"] = "Abrir reloj",
    };

    public override LocalizedString this[string key] =>
        Texts.TryGetValue(key, out var text) ? new LocalizedString(key, text) : base[key];
}
