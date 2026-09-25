#if DEBUG
using System.Diagnostics;
using System.Text.Json;
using PiRadar.Core.Abstractions;

namespace PiRadar.App.Services;

/// <summary>
/// SOLO DEBUG: corre el OCR sobre cada JPEG de <c>AppDataDirectory/ocr-in</c> y guarda el <c>OcrPage</c> como JSON en
/// <c>ocr-out</c>, para generar fixtures de <c>ReceiptParser</c>. Los archivos se copian con simctl/adb.
/// </summary>
public static class OcrLab
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static async Task<string?> RunIfRequestedAsync(IOcrService ocr)
    {
        var input = Path.Combine(FileSystem.AppDataDirectory, "ocr-in");
        if (!Directory.Exists(input))
        {
            return null;
        }

        var output = Path.Combine(FileSystem.AppDataDirectory, "ocr-out");
        Directory.CreateDirectory(output);
        var log = new List<string>();
        foreach (var file in Directory.GetFiles(input, "*.jpg").Order())
        {
            var name = Path.GetFileNameWithoutExtension(file);
            try
            {
                var bytes = await File.ReadAllBytesAsync(file);
                var sw = Stopwatch.StartNew();
                var page = await ocr.RecognizeAsync(bytes);
                sw.Stop();
                await File.WriteAllTextAsync(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(page, Json));
                log.Add($"{name}\t{sw.ElapsedMilliseconds} ms\t{page.Elements.Count} elementos\trotacion EXIF {ImageOrientation.ExifRotation(bytes)}");
            }
            catch (Exception ex)
            {
                log.Add($"{name}\tERROR {ex.GetType().Name}: {ex.Message}");
            }
        }

        log.Add("DONE");
        await File.WriteAllLinesAsync(Path.Combine(output, "_log.txt"), log);
        return string.Join('\n', log);
    }
}
#endif
