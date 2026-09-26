using PiRadar.Core.Domain;
using PiRadar.Core.Receipts;

namespace PiRadar.App.Services;

/// <summary>Recibo leído que espera confirmación en el formulario del movimiento.</summary>
public sealed record PendingScan(ReceiptImage Image, string OcrEngine, ReceiptParseResult Parsed);

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
