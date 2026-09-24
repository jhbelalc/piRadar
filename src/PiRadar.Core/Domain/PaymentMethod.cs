namespace PiRadar.Core.Domain;

public sealed class PaymentMethod
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public PaymentMethodKind Kind { get; set; }

    /// <summary>Nombre de ícono Material; la UI lo traduce a MudBlazor.</summary>
    public required string Icon { get; set; }

    /// <summary>Últimos 4 dígitos de la tarjeta o cuenta, solo para identificarla.</summary>
    public string? LastFourDigits { get; set; }

    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
    public bool IsSystem { get; set; }
}
