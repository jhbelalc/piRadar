using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;
using PiRadar.Core.Domain;

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
    // Propiedades calculadas (no campos): la etiqueta depende del idioma vigente.
    public static NavItem Home => new(AppLocale.Pick("Inicio", "Home"), Icons.Material.Rounded.Home, "");
    public static NavItem Transactions => new(
        AppLocale.Pick("Movimientos", "Transactions"), Icons.Material.Rounded.ReceiptLong, "transactions");
    public static NavItem NewTransaction => new(AppLocale.Pick("Nuevo", "New"), Icons.Material.Rounded.Add, "transactions/new");
    public static NavItem Analytics => new(AppLocale.Pick("Análisis", "Analytics"), Icons.Material.Rounded.Insights, "analytics");
    public static NavItem More => new(AppLocale.Pick("Más", "More"), Icons.Material.Rounded.Menu, "more");

    /// <summary>Páginas que en el teléfono cuelgan de "Más".</summary>
    public static IReadOnlyList<NavItem> MoreItems =>
    [
        new(AppLocale.Pick("Categorías", "Categories"), Icons.Material.Rounded.Category, "categories"),
        new(AppLocale.Pick("Medios de pago", "Payment methods"), Icons.Material.Rounded.CreditCard, "payment-methods"),
        new(AppLocale.Pick("Tasas de cambio", "Exchange rates"), Icons.Material.Rounded.CurrencyExchange, "exchange-rates"),
        new(AppLocale.Pick("Respaldo", "Backup"), Icons.Material.Rounded.CloudUpload, "backup"),
        new(AppLocale.Pick("Ajustes", "Settings"), Icons.Material.Rounded.Settings, "settings"),
        new(AppLocale.Pick("Acerca de", "About"), Icons.Material.Rounded.Info, "about"),
    ];
}
