using PiRadar.Core.Domain;

namespace PiRadar.Core.Receipts;

/// <summary>Valores que el parser <b>propone</b> para un recibo. El usuario siempre confirma antes de guardar.</summary>
public sealed record ReceiptParseResult
{
    public Money? Total { get; init; }

    /// <summary>El total cuadra con otra parte del recibo (medio de pago, o subtotal + impuestos).</summary>
    public bool TotalConfirmed { get; init; }

    /// <summary>Hasta 3 montos probables, el más probable primero, para que la UI ofrezca alternativas.</summary>
    public IReadOnlyList<Money> TotalCandidates { get; init; } = [];

    public Currency Currency { get; init; } = Currency.COP;

    /// <summary>Fecha local de la compra; incluye la hora si <see cref="HasTime"/>.</summary>
    public DateTime? Date { get; init; }

    public bool HasTime { get; init; }

    /// <summary>Nombre comercial (marca) si se encontró; si no, la razón social sin sufijo societario.</summary>
    public string? Merchant { get; init; }

    /// <summary>Marca y razón social, en ese orden, sin repetidos.</summary>
    public IReadOnlyList<string> MerchantCandidates { get; init; } = [];

    /// <summary>NIT normalizado <c>900319753-3</c>.</summary>
    public string? TaxId { get; init; }

    /// <summary>El dígito de verificación venía impreso y cuadra. <c>false</c> si se calculó o no cuadra.</summary>
    public bool TaxIdVerified { get; init; }

    /// <summary>Filas reconstruidas en orden de lectura; se guardan como <c>Receipt.OcrText</c>.</summary>
    public IReadOnlyList<string> Rows { get; init; } = [];
}
