using PiRadar.Core.Domain;

namespace PiRadar.Data.Records;

internal static class RecordMapping
{
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime AsLocal(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Local);

    private static DateTime ToUtcForStorage(DateTime value) =>
        value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;

    private static DateTime ToLocalForStorage(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;

    public static Category ToDomain(this CategoryRecord r) => new()
    {
        Id = r.Id, Name = r.Name, Kind = r.Kind, Icon = r.Icon, Color = r.Color, ParentId = r.ParentId,
        SortOrder = r.SortOrder, IsArchived = r.IsArchived, IsSystem = r.IsSystem,
    };

    public static CategoryRecord ToRecord(this Category c) => new()
    {
        Id = c.Id, Name = c.Name, Kind = c.Kind, Icon = c.Icon, Color = c.Color, ParentId = c.ParentId,
        SortOrder = c.SortOrder, IsArchived = c.IsArchived, IsSystem = c.IsSystem,
    };

    public static PaymentMethod ToDomain(this PaymentMethodRecord r) => new()
    {
        Id = r.Id, Name = r.Name, Kind = r.Kind, Icon = r.Icon, LastFourDigits = r.LastFourDigits,
        SortOrder = r.SortOrder, IsArchived = r.IsArchived, IsSystem = r.IsSystem,
    };

    public static PaymentMethodRecord ToRecord(this PaymentMethod p) => new()
    {
        Id = p.Id, Name = p.Name, Kind = p.Kind, Icon = p.Icon, LastFourDigits = p.LastFourDigits,
        SortOrder = p.SortOrder, IsArchived = p.IsArchived, IsSystem = p.IsSystem,
    };

    public static Transaction ToDomain(this TransactionRecord r) => new()
    {
        Id = r.Id,
        Type = r.Type,
        Amount = new Money(r.AmountMinor, CurrencyExtensions.ParseCode(r.Currency)),
        OccurredAt = AsLocal(r.OccurredAt),
        CategoryId = r.CategoryId,
        PaymentMethodId = r.PaymentMethodId,
        Merchant = r.Merchant,
        Description = r.Description,
        Notes = r.Notes,
        ReceiptId = r.ReceiptId,
        Source = r.Source,
        CreatedAt = AsUtc(r.CreatedAt),
        UpdatedAt = AsUtc(r.UpdatedAt),
    };

    public static TransactionRecord ToRecord(this Transaction t) => new()
    {
        Id = t.Id,
        Type = t.Type,
        AmountMinor = t.Amount.AmountMinor,
        Currency = t.Amount.Currency.ToString(),
        OccurredAt = ToLocalForStorage(t.OccurredAt),
        CategoryId = t.CategoryId,
        PaymentMethodId = t.PaymentMethodId,
        Merchant = t.Merchant,
        Description = t.Description,
        Notes = t.Notes,
        ReceiptId = t.ReceiptId,
        Source = t.Source,
        CreatedAt = ToUtcForStorage(t.CreatedAt),
        UpdatedAt = ToUtcForStorage(t.UpdatedAt),
    };

    public static Receipt ToDomain(this ReceiptRecord r) => new()
    {
        Id = r.Id,
        CapturedAt = AsUtc(r.CapturedAt),
        OcrText = r.OcrText,
        ParsedTotal = r is { ParsedTotalMinor: { } minor, ParsedCurrency: { } code }
            ? new Money(minor, CurrencyExtensions.ParseCode(code))
            : null,
        ParsedDate = r.ParsedDate is { } date ? AsLocal(date) : null,
        ParsedMerchant = r.ParsedMerchant,
        ParsedTaxId = r.ParsedTaxId,
        OcrEngine = r.OcrEngine,
    };

    public static ReceiptRecord ToRecord(this Receipt r) => new()
    {
        Id = r.Id,
        CapturedAt = ToUtcForStorage(r.CapturedAt),
        OcrText = r.OcrText,
        ParsedTotalMinor = r.ParsedTotal?.AmountMinor,
        ParsedCurrency = r.ParsedTotal?.Currency.ToString(),
        ParsedDate = r.ParsedDate is { } date ? ToLocalForStorage(date) : null,
        ParsedMerchant = r.ParsedMerchant,
        ParsedTaxId = r.ParsedTaxId,
        OcrEngine = r.OcrEngine,
    };

    public static ReceiptImage ToDomain(this ReceiptImageRecord r) => new()
    {
        ReceiptId = r.ReceiptId, Jpeg = r.Jpeg, Width = r.Width, Height = r.Height,
    };

    public static ReceiptImageRecord ToRecord(this ReceiptImage i) => new()
    {
        ReceiptId = i.ReceiptId, Jpeg = i.Jpeg, Width = i.Width, Height = i.Height,
    };

    public static ExchangeRate ToDomain(this ExchangeRateRecord r) => new()
    {
        Id = r.Id,
        From = CurrencyExtensions.ParseCode(r.FromCurrency),
        To = CurrencyExtensions.ParseCode(r.ToCurrency),
        RateMicros = r.RateMicros,
        EffectiveDate = AsLocal(r.EffectiveDate),
    };

    public static ExchangeRateRecord ToRecord(this ExchangeRate e) => new()
    {
        Id = e.Id,
        FromCurrency = e.From.ToString(),
        ToCurrency = e.To.ToString(),
        RateMicros = e.RateMicros,
        EffectiveDate = ToLocalForStorage(e.EffectiveDate),
    };
}
