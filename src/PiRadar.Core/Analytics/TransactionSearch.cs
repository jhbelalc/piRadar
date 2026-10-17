using System.Globalization;
using PiRadar.Core.Domain;

namespace PiRadar.Core.Analytics;

/// <summary>
/// Búsqueda de movimientos por palabra clave y categoría. Se hace en memoria porque el <c>LIKE</c> de SQLite solo
/// ignora mayúsculas ASCII ("Éxito" ≠ "éxito"); aquí no importan mayúsculas ni tildes.
/// </summary>
public static class TransactionSearch
{
    private static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;
    private const CompareOptions Loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreWidth;

    /// <summary>Palabras de la búsqueda: separadas por espacios; todas deben aparecer.</summary>
    public static string[] Tokens(string? query) =>
        (query ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Ids de la categoría y de sus subcategorías (elegir un padre incluye a sus hijas).</summary>
    public static HashSet<int> CategoryAndChildren(int categoryId, IEnumerable<Category> categories)
    {
        var ids = new HashSet<int> { categoryId };
        foreach (var category in categories)
        {
            if (category.ParentId == categoryId)
            {
                ids.Add(category.Id);
            }
        }

        return ids;
    }

    /// <summary>
    /// ¿Cumple el movimiento la búsqueda? Cada palabra debe aparecer en el comercio, la descripción, las notas, la
    /// categoría o el medio de pago; una palabra numérica también coincide con el monto exacto ("25958", "25.958", "12,50").
    /// Sin palabras y sin categoría, todo coincide.
    /// </summary>
    public static bool Matches(Transaction t, string? categoryName, string? paymentMethodName,
        IReadOnlyCollection<string> tokens, ISet<int>? categoryIds = null)
    {
        if (categoryIds is not null && !categoryIds.Contains(t.CategoryId))
        {
            return false;
        }

        foreach (var token in tokens)
        {
            if (!Contains(t.Merchant, token) && !Contains(t.Description, token) && !Contains(t.Notes, token)
                && !Contains(categoryName, token) && !Contains(paymentMethodName, token) && !MatchesAmount(t.Amount, token))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Contains(string? text, string token) =>
        !string.IsNullOrEmpty(text) && Compare.IndexOf(text, token, Loose) >= 0;

    /// <summary>Solo dígitos y separadores: se compara con el monto en unidades menores o en unidades enteras.</summary>
    private static bool MatchesAmount(Money amount, string token)
    {
        if (token.Any(c => !char.IsDigit(c) && c is not ('.' or ',')) || !token.Any(char.IsDigit))
        {
            return false;
        }

        var digits = new string(token.Where(char.IsDigit).ToArray()).TrimStart('0');
        var minor = Math.Abs(amount.AmountMinor);
        var whole = minor / (long)Math.Pow(10, amount.Currency.Scale());
        var hasDecimals = token.IndexOfAny(['.', ',']) is var i && i >= 0 && token.Length - i - 1 is 1 or 2
            && amount.Currency.Scale() > 0;

        // "12,50" (decimales) ⇒ unidades menores; "25.958" / "25958" ⇒ unidades enteras (o menores si coincide tal cual).
        if (hasDecimals)
        {
            var scale = amount.Currency.Scale();
            var frac = token[(token.LastIndexOfAny(['.', ',']) + 1)..].PadRight(scale, '0');
            var intPart = new string(token[..token.LastIndexOfAny(['.', ','])].Where(char.IsDigit).ToArray());
            var combined = (intPart + frac).TrimStart('0');
            return combined == minor.ToString(CultureInfo.InvariantCulture).TrimStart('0');
        }

        return digits == whole.ToString(CultureInfo.InvariantCulture).TrimStart('0')
            || (amount.Currency.Scale() == 0 && digits == minor.ToString(CultureInfo.InvariantCulture).TrimStart('0'));
    }
}
