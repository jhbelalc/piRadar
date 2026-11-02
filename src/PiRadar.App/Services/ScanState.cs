using PiRadar.Core.Domain;
using PiRadar.Core.Images;
using PiRadar.Core.Receipts;

namespace PiRadar.App.Services;

/// <summary>
/// Recibo leído que espera confirmación en el formulario del movimiento. <paramref name="Image"/> es la foto recortada
/// que se guardará; <paramref name="FullImage"/> y <paramref name="Crop"/> permiten ajustar el recorte antes de guardar
/// (incluso agrandarlo hasta la foto completa).
/// </summary>
public sealed record PendingScan(ReceiptImage Image, string OcrEngine, ReceiptParseResult Parsed, ReceiptImage FullImage, CropRect Crop);

/// <summary>Pasa el recibo leído de <c>ScanReceipt</c> a <c>TransactionEdit</c>. Solo en memoria; nunca a disco.</summary>
public sealed class ScanState
{
    public PendingScan? Pending { get; set; }

    /// <summary>Entrega el recibo pendiente una sola vez.</summary>
    public PendingScan? Take()
    {
        var pending = Pending;
        Pending = null;
        return pending;
    }
}
