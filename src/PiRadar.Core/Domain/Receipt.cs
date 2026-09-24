namespace PiRadar.Core.Domain;

/// <summary>Resultado del OCR de un recibo y los valores que el parser propuso.</summary>
public sealed class Receipt
{
    public Guid Id { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CapturedAt { get; set; }

    public required string OcrText { get; set; }
    public Money? ParsedTotal { get; set; }
    public DateTime? ParsedDate { get; set; }
    public string? ParsedMerchant { get; set; }

    /// <summary>NIT del comercio.</summary>
    public string? ParsedTaxId { get; set; }

    /// <summary>Motor que produjo el texto (p. ej. <c>Plugin.Maui.OCR/Vision</c>).</summary>
    public required string OcrEngine { get; set; }
}

/// <summary>Imagen reducida del recibo, guardada como BLOB dentro de la BD cifrada.</summary>
public sealed class ReceiptImage
{
    public Guid ReceiptId { get; set; }
    public required byte[] Jpeg { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
