using PiRadar.Core.Domain;

namespace PiRadar.Core.Abstractions;

/// <summary>Lectura de recibos. Se crean y borran junto con su movimiento (<see cref="ITransactionRepository"/>).</summary>
public interface IReceiptRepository
{
    Task<Receipt?> GetAsync(Guid id);

    /// <summary>La imagen se lee aparte para no cargar BLOBs al listar.</summary>
    Task<ReceiptImage?> GetImageAsync(Guid receiptId);

    /// <summary>Reemplaza la foto de un recibo guardado (p. ej. recortada); el recibo y su movimiento no cambian.</summary>
    /// <exception cref="KeyNotFoundException">El recibo no tiene foto guardada.</exception>
    Task ReplaceImageAsync(Guid receiptId, ReceiptImage image);
}
