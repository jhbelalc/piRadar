namespace PiRadar.Core.Domain;

/// <summary>Busca las categorías que trae la app cuando hace falta una por defecto.</summary>
public static class DefaultCategories
{
    /// <summary>Ícono de "Otros gastos" / "Other expenses" en la semilla.</summary>
    private const string OtherExpenseIcon = "more_horiz";

    private static readonly string[] OtherExpenseNames = ["Otros gastos", "Other expenses"];

    /// <summary>
    /// La categoría "Otros gastos" (de sistema, de gastos, principal y sin archivar), para usarla cuando el escaneo de un
    /// recibo no detecta la categoría. Se reconoce por el ícono de la semilla, así sigue encontrándose aunque el usuario
    /// la haya renombrado; si no, por su nombre en español o inglés. <c>null</c> si no existe (la borraron o la archivaron).
    /// </summary>
    public static Category? FindOtherExpense(IEnumerable<Category> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);
        var candidates = categories
            .Where(c => c is { Kind: TransactionType.Expense, IsSystem: true, IsArchived: false, ParentId: null })
            .ToList();
        return candidates.FirstOrDefault(c => c.Icon == OtherExpenseIcon)
            ?? candidates.FirstOrDefault(c => OtherExpenseNames.Contains(c.Name, StringComparer.CurrentCultureIgnoreCase));
    }
}
