using PiRadar.Core.Abstractions;
using PiRadar.Core.Domain;
using PiRadar.Core.Export;

namespace PiRadar.App.Services;

/// <summary>Escribe movimientos como CSV en la caché y abre el menú de compartir. Lo usan Movimientos y Respaldo.</summary>
public sealed class CsvExportService(ICategoryRepository categories, IPaymentMethodRepository paymentMethods)
{
    /// <summary>
    /// Exporta <paramref name="transactions"/> (con todas las categorías y medios de pago, archivados incluidos).
    /// El archivo queda en la caché mientras se comparte y <see cref="DeleteOldFiles"/> lo borra después.
    /// </summary>
    public async Task ExportAsync(IReadOnlyList<Transaction> transactions)
    {
        var allCategories = await categories.GetAllAsync(includeArchived: true);
        var allMethods = await paymentMethods.GetAllAsync(includeArchived: true);
        var path = Path.Combine(FileSystem.CacheDirectory, TransactionCsvExporter.FileName(DateTime.Now));

        await Task.Run(() =>
        {
            using var stream = File.Create(path);
            TransactionCsvExporter.Write(stream, transactions, allCategories, allMethods);
        });

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = AppLocale.Pick("Movimientos de piRadar", "piRadar transactions"),
            File = new ShareFile(path, "text/csv"),
        });
    }

    /// <summary>Borra de la caché los CSV de más de una hora (la app que lo recibió puede seguir leyéndolo un rato).</summary>
    public static void DeleteOldFiles()
    {
        foreach (var old in Directory.EnumerateFiles(FileSystem.CacheDirectory, "piradar-*.csv"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(old) < DateTime.UtcNow.AddHours(-1))
                {
                    File.Delete(old);
                }
            }
            catch (IOException)
            {
                // Otra app puede estar leyéndolo todavía; se intenta la próxima vez.
            }
        }
    }
}
