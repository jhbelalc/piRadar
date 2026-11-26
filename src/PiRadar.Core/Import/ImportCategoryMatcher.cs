using PiRadar.Core.Domain;

namespace PiRadar.Core.Import;

/// <summary>Propone con qué categoría o medio de pago existente se une cada nombre del archivo.</summary>
public static class ImportCategoryMatcher
{
    /// <summary>Categoría existente no archivada con el mismo nombre y tipo (la principal primero); null = crear nueva.</summary>
    public static IReadOnlyDictionary<ImportName, int?> Propose(IEnumerable<ImportName> names, IEnumerable<Category> existing)
    {
        var list = existing.Where(c => !c.IsArchived).OrderBy(c => c.ParentId is null ? 0 : 1).ToList();
        var result = new Dictionary<ImportName, int?>();
        foreach (var n in names)
        {
            var key = ImportText.Key(n.Name);
            result[n] = list.FirstOrDefault(c => c.Kind == n.Type && ImportText.Key(c.Name) == key)?.Id;
        }
        return result;
    }

    /// <summary>Medio de pago existente no archivado con el mismo nombre; null = crear nuevo.</summary>
    public static IReadOnlyDictionary<ImportName, int?> ProposePaymentMethods(
        IEnumerable<ImportName> names, IEnumerable<PaymentMethod> existing)
    {
        var list = existing.Where(p => !p.IsArchived).ToList();
        var result = new Dictionary<ImportName, int?>();
        foreach (var n in names)
        {
            var key = ImportText.Key(n.Name);
            result[n] = list.FirstOrDefault(p => ImportText.Key(p.Name) == key)?.Id;
        }
        return result;
    }

    /// <summary>Subcategoría existente no archivada bajo <paramref name="parentId"/> con ese nombre y tipo.</summary>
    public static int? FindSubcategory(string name, int parentId, TransactionType type, IEnumerable<Category> existing)
    {
        var key = ImportText.Key(name);
        return existing.FirstOrDefault(c => !c.IsArchived && c.ParentId == parentId && c.Kind == type
            && ImportText.Key(c.Name) == key)?.Id;
    }

    /// <summary>
    /// Categoría para filas sin categoría: la principal "Otros gastos"/"Otros ingresos" (o "Other expenses"/"Other income");
    /// si no existe, la última principal del tipo por orden; null si no hay ninguna.
    /// </summary>
    public static int? SuggestFallback(IEnumerable<Category> categories, TransactionType type)
    {
        var candidates = categories.Where(c => !c.IsArchived && c.ParentId is null && c.Kind == type).ToList();
        var keys = type == TransactionType.Expense
            ? new[] { "OTROS GASTOS", "OTHER EXPENSES" }
            : new[] { "OTROS INGRESOS", "OTHER INCOME" };
        var named = candidates.FirstOrDefault(c => keys.Contains(ImportText.Key(c.Name)));
        if (named is not null) return named.Id;
        return candidates.OrderBy(c => c.SortOrder).ThenBy(c => c.Id).LastOrDefault()?.Id;
    }
}
