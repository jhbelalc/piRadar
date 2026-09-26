using System.Globalization;

namespace PiRadar.Core.Receipts;

public static class MerchantSuggestions
{
    private static readonly StringComparer Comparer = StringComparer.Create(
        CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);

    /// <summary>
    /// Nombres de comercio a ofrecer: primero el que el usuario eligió la última vez para ese NIT, luego la marca y la
    /// razón social del parser. Sin repetidos (sin distinguir mayúsculas ni tildes).
    /// </summary>
    public static IReadOnlyList<string> Combine(string? remembered, IEnumerable<string> parsed) =>
        new[] { remembered }.Concat(parsed)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m!.Trim())
            .Distinct(Comparer)
            .ToList();
}
