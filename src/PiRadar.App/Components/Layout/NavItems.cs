using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;

namespace PiRadar.App.Components.Layout;

/// <param name="Href">Ruta relativa a la base ("" es Inicio).</param>
/// <param name="Enabled">Falso mientras la página no exista (se activa en los pasos siguientes).</param>
public sealed record NavItem(string Label, string Icon, string Href, bool Enabled = true)
{
    public NavLinkMatch Match => Href.Length == 0 ? NavLinkMatch.All : NavLinkMatch.Prefix;
}

/// <summary>Destinos de la barra inferior (teléfono) y del menú lateral (tablet u horizontal).</summary>
public static class NavItems
{
    public static NavItem Home { get; } = new("Inicio", Icons.Material.Rounded.Home, "");
    public static NavItem Transactions { get; } = new("Movimientos", Icons.Material.Rounded.ReceiptLong, "transactions");
    public static NavItem NewTransaction { get; } = new("Nuevo", Icons.Material.Rounded.Add, "transactions/new");
    public static NavItem Analytics { get; } = new("Análisis", Icons.Material.Rounded.Insights, "analytics");
    public static NavItem More { get; } = new("Más", Icons.Material.Rounded.Menu, "more");

    /// <summary>Páginas que en el teléfono cuelgan de "Más".</summary>
    public static IReadOnlyList<NavItem> MoreItems { get; } =
    [
        new("Categorías", Icons.Material.Rounded.Category, "categories"),
        new("Medios de pago", Icons.Material.Rounded.CreditCard, "payment-methods"),
        new("Tasas de cambio", Icons.Material.Rounded.CurrencyExchange, "exchange-rates"),
        new("Respaldo", Icons.Material.Rounded.CloudUpload, "backup", Enabled: false),
        new("Ajustes", Icons.Material.Rounded.Settings, "settings"),
    ];
}
