#if DEBUG
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PiRadar.App.Services;

/// <summary>
/// SOLO DEBUG: ejecuta <c>AppDataDirectory/ui-script.txt</c> al abrir la app, para tomar capturas en el
/// simulador/emulador sin tocar la pantalla (en este Mac los toques de adb no son fiables y simctl no toca).
/// Una orden por línea: <c>go /ruta</c>, <c>click selector-css</c>, <c>theme Dark|Light|System</c>,
/// <c>wait ms</c>, <c>js expresión</c> (agrega el resultado a <c>ui-out.txt</c>),
/// <c>fill Etiqueta=valor</c> (campo de texto) y <c>pick Etiqueta=texto</c> (opción de un MudSelect). El archivo se copia con simctl/adb y se borra después de leerlo.
/// </summary>
public static class UiScript
{
    // Busca el campo por el texto de su etiqueta MudBlazor (sin el "*" de obligatorio).
    private const string Helpers = """
        window.__ui = window.__ui || {
          field(label) {
            const l = [...document.querySelectorAll('label.mud-input-label')]
              .find(x => x.textContent.replace('*', '').trim() === label);
            return l ? l.closest('.mud-input-control') : null;
          },
          fill(label, value) {
            const c = this.field(label); if (!c) return 'no field ' + label;
            const i = c.querySelector('input, textarea');
            i.focus(); i.value = value;
            i.dispatchEvent(new Event('input', { bubbles: true }));
            i.dispatchEvent(new Event('change', { bubbles: true }));
            i.blur();
            return 'filled ' + label;
          },
          async pick(label, text) {
            const c = this.field(label); if (!c) return 'no field ' + label;
            const input = c.querySelector('input');
            for (const type of ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click']) {
              input.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
            }
            let item = null;
            for (let i = 0; i < 20 && !item; i++) {
              await new Promise(r => setTimeout(r, 200));
              item = [...document.querySelectorAll('.mud-popover-open .mud-list-item')]
                .find(x => x.textContent.trim().startsWith(text));
            }
            if (!item) return 'no option ' + text;
            item.click();
            return 'picked ' + text;
          },
        };
        """;

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
            try
            {
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
                    case "fill" or "pick":
                        var (label, value) = argument.IndexOf('=') is var e and > 0
                            ? (argument[..e], argument[(e + 1)..])
                            : (argument, "");
                        await js.InvokeVoidAsync("eval", Helpers);
                        var call = $"window.__ui.{command}({Json(label)}, {Json(value)})";
                        var outcome = await js.InvokeAsync<string>("eval", call);
                        await File.AppendAllTextAsync(Path.Combine(FileSystem.AppDataDirectory, "ui-out.txt"), outcome + Environment.NewLine);
                        await Task.Delay(400);
                        break;
                    case "wait":
                        await Task.Delay(int.Parse(argument, System.Globalization.CultureInfo.InvariantCulture));
                        break;
                }
            }
            catch (Exception ex)
            {
                // Un paso que falla (p. ej. un selector que aún no existe) no debe tumbar la página.
                await File.AppendAllTextAsync(Path.Combine(FileSystem.AppDataDirectory, "ui-out.txt"),
                    $"ERROR en '{line}': {ex.GetType().Name}: {ex.Message}{Environment.NewLine}");
            }
        }
    }

    private static string Json(string value) => System.Text.Json.JsonSerializer.Serialize(value);
}
#endif
