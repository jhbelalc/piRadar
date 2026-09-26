#if DEBUG
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PiRadar.App.Services;

/// <summary>
/// SOLO DEBUG: ejecuta <c>AppDataDirectory/ui-script.txt</c> al abrir la app, para tomar capturas en el
/// simulador/emulador sin tocar la pantalla (en este Mac los toques de adb no son fiables y simctl no toca).
/// Una orden por línea: <c>go /ruta</c>, <c>click selector-css</c>, <c>theme Dark|Light|System</c>,
/// <c>wait ms</c>, <c>js expresión</c> (agrega el resultado a <c>ui-out.txt</c>). El archivo se copia con simctl/adb y se borra después de leerlo.
/// </summary>
public static class UiScript
{
    public static async Task RunIfRequestedAsync(NavigationManager navigation, IJSRuntime js, ThemeState theme)
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, "ui-script.txt");
        if (!File.Exists(path))
        {
            return;
        }

        var lines = await File.ReadAllLinesAsync(path);
        File.Delete(path);
        foreach (var line in lines.Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')))
        {
            var (command, argument) = line.IndexOf(' ') is var i and > 0 ? (line[..i], line[(i + 1)..]) : (line, "");
            switch (command)
            {
                case "go":
                    navigation.NavigateTo(argument);
                    await Task.Delay(800);
                    break;
                case "click":
                    await js.InvokeVoidAsync("eval", $"document.querySelector({System.Text.Json.JsonSerializer.Serialize(argument)})?.click()");
                    await Task.Delay(600);
                    break;
                case "theme":
                    await theme.SetAsync(Enum.Parse<ThemePreference>(argument));
                    break;
                case "js":
                    var result = await js.InvokeAsync<string>("eval", $"String({argument})");
                    await File.AppendAllTextAsync(Path.Combine(FileSystem.AppDataDirectory, "ui-out.txt"), result + Environment.NewLine);
                    break;
                case "wait":
                    await Task.Delay(int.Parse(argument, System.Globalization.CultureInfo.InvariantCulture));
                    break;
            }
        }
    }
}
#endif
