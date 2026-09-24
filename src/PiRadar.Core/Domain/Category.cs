namespace PiRadar.Core.Domain;

public sealed class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }

    /// <summary>Tipo de movimiento al que aplica la categoría.</summary>
    public TransactionType Kind { get; set; }

    /// <summary>Nombre de ícono Material (p. ej. <c>shopping_cart</c>); la UI lo traduce a MudBlazor.</summary>
    public required string Icon { get; set; }

    /// <summary>Color en hex <c>#RRGGBB</c>.</summary>
    public required string Color { get; set; }

    public int? ParentId { get; set; }
    public int SortOrder { get; set; }

    /// <summary>Se archiva en vez de borrar cuando tiene movimientos.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Creada por la semilla inicial.</summary>
    public bool IsSystem { get; set; }
}
