using PiRadar.App.Services;
using PiRadar.Core.Analytics;
using PiRadar.Core.Domain;

namespace PiRadar.App.Components.Shared;

/// <summary>Fila de <see cref="ShareList"/>: ícono Material, color <c>#RRGGBB</c>, nombre, total y proporción (0–1).</summary>
public sealed record ShareRow(string Icon, string Color, string Name, Money Total, decimal Share)
{
    /// <summary>Filas de categorías; las que ya no existen salen como "Sin categoría" ("Uncategorized").</summary>
    public static IReadOnlyList<ShareRow> FromCategories(IEnumerable<CategoryShare> shares, IReadOnlyDictionary<int, Category> categories) =>
        shares.Select(s => categories.GetValueOrDefault(s.CategoryId) is { } c
                ? new ShareRow(c.Icon, c.Color, c.Name, s.Total, s.Share)
                : new ShareRow(IconCatalog.Fallback, ShareList.OtherColor, AppLocale.Pick("Sin categoría", "Uncategorized"), s.Total, s.Share))
            .ToList();
}
