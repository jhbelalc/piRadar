using PiRadar.Core.Domain;

namespace PiRadar.Core.Abstractions;

/// <summary>Lectura de recibos. Se crean y borran junto con su movimiento (<see cref="ITransactionRepository"/>).</summary>
public interface IReceiptRepository
{
    Task<Receipt?> GetAsync(Guid id);

    /// <summary>La imagen se lee aparte para no cargar BLOBs al listar.</summary>
    Task<ReceiptImage?> GetImageAsync(Guid receiptId);
}
