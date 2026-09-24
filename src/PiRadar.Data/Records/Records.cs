using PiRadar.Core.Domain;
using SQLite;

namespace PiRadar.Data.Records;

// Clases tabla de sqlite-net. El esquema real lo definen las migraciones (Migrations/);
// estos nombres de tabla y columna deben coincidir con el SQL de las migraciones.
// Fechas: sqlite-net las guarda como ticks sin Kind; el mapeo a dominio restaura UTC/local.

[Table("Category")]
internal sealed class CategoryRecord
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TransactionType Kind { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
    public bool IsSystem { get; set; }
}

[Table("PaymentMethod")]
internal sealed class PaymentMethodRecord
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PaymentMethodKind Kind { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string? LastFourDigits { get; set; }
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
    public bool IsSystem { get; set; }
}

// TRANSACTION es palabra reservada en SQLite.
[Table("Transactions")]
internal sealed class TransactionRecord
{
    [PrimaryKey] public Guid Id { get; set; }
    public TransactionType Type { get; set; }
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public int CategoryId { get; set; }
    public int? PaymentMethodId { get; set; }
    public string? Merchant { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public Guid? ReceiptId { get; set; }
    public TransactionSource Source { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[Table("Receipt")]
internal sealed class ReceiptRecord
{
    [PrimaryKey] public Guid Id { get; set; }
    public DateTime CapturedAt { get; set; }
    public string OcrText { get; set; } = string.Empty;
    public long? ParsedTotalMinor { get; set; }
    public string? ParsedCurrency { get; set; }
    public DateTime? ParsedDate { get; set; }
    public string? ParsedMerchant { get; set; }
    public string? ParsedTaxId { get; set; }
    public string OcrEngine { get; set; } = string.Empty;
}

[Table("ReceiptImage")]
internal sealed class ReceiptImageRecord
{
    [PrimaryKey] public Guid ReceiptId { get; set; }
    public byte[] Jpeg { get; set; } = [];
    public int Width { get; set; }
    public int Height { get; set; }
}

[Table("ExchangeRate")]
internal sealed class ExchangeRateRecord
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;
    public long RateMicros { get; set; }
    public DateTime EffectiveDate { get; set; }
}

[Table("AppSetting")]
internal sealed class AppSettingRecord
{
    [PrimaryKey] public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
