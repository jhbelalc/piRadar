using PiRadar.Core.Receipts;
using PluginOcr = Plugin.Maui.OCR.IOcrService;

namespace PiRadar.App.Services;

/// <summary>Adaptador de Plugin.Maui.OCR a <see cref="Core.Abstractions.IOcrService"/>.</summary>
public sealed class PluginOcrService(PluginOcr ocr) : Core.Abstractions.IOcrService
{
    private readonly SemaphoreSlim _init = new(1, 1);
    private bool _initialized;

    public async Task<OcrPage> RecognizeAsync(byte[] image, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var upright = await Task.Run(() => ImageOrientation.Normalize(image), cancellationToken);

        // tryHard: en iOS el modo rápido parte palabras y montos ("$82. 150").
        // Desde un hilo de fondo: el plugin decodifica la imagen al llamarlo y en el hilo de la interfaz eso
        // congela la app (ANR en Android con fotos de 12 MP).
        var result = await Task.Run(() => ocr.RecognizeTextAsync(upright, tryHard: true, cancellationToken), cancellationToken);
        if (!result.Success)
        {
            return new OcrPage([], Engine);
        }

        var elements = result.Elements
            .Where(e => !string.IsNullOrWhiteSpace(e.Text))
            .Select(e => new OcrElement(e.Text, e.X, e.Y, e.Width, e.Height))
            .ToList();
        return new OcrPage(elements, Engine);
    }

    private static string Engine => $"Plugin.Maui.OCR/{DeviceInfo.Platform}";

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _init.WaitAsync(cancellationToken);
        try
        {
            if (!_initialized)
            {
                await ocr.InitAsync(cancellationToken);
                _initialized = true;
            }
        }
        finally
        {
            _init.Release();
        }
    }
}
