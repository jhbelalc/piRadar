using MudBlazor;

namespace PiRadar.App.Services;

/// <summary>
/// Íconos que se pueden elegir para categorías y medios de pago. La BD guarda el nombre Material
/// (<c>shopping_cart</c>), que es estable; aquí se traduce al SVG de MudBlazor.
/// Nunca quitar un nombre: puede estar guardado en la BD o en un respaldo.
/// </summary>
public static class IconCatalog
{
    public const string Fallback = "category";

    private static readonly (string Name, string Svg)[] Entries =
    [
        ("shopping_cart", Icons.Material.Filled.ShoppingCart),
        ("restaurant", Icons.Material.Filled.Restaurant),
        ("local_cafe", Icons.Material.Filled.LocalCafe),
        ("local_bar", Icons.Material.Filled.LocalBar),
        ("directions_bus", Icons.Material.Filled.DirectionsBus),
        ("directions_car", Icons.Material.Filled.DirectionsCar),
        ("local_taxi", Icons.Material.Filled.LocalTaxi),
        ("two_wheeler", Icons.Material.Filled.TwoWheeler),
        ("local_gas_station", Icons.Material.Filled.LocalGasStation),
        ("local_parking", Icons.Material.Filled.LocalParking),
        ("flight", Icons.Material.Filled.Flight),
        ("receipt_long", Icons.Material.Filled.ReceiptLong),
        ("electric_bolt", Icons.Material.Filled.ElectricBolt),
        ("water_drop", Icons.Material.Filled.WaterDrop),
        ("wifi", Icons.Material.Filled.Wifi),
        ("phone_iphone", Icons.Material.Filled.PhoneIphone),
        ("home", Icons.Material.Filled.Home),
        ("build", Icons.Material.Filled.Build),
        ("local_hospital", Icons.Material.Filled.LocalHospital),
        ("local_pharmacy", Icons.Material.Filled.LocalPharmacy),
        ("fitness_center", Icons.Material.Filled.FitnessCenter),
        ("spa", Icons.Material.Filled.Spa),
        ("school", Icons.Material.Filled.School),
        ("menu_book", Icons.Material.Filled.MenuBook),
        ("child_care", Icons.Material.Filled.ChildCare),
        ("pets", Icons.Material.Filled.Pets),
        ("movie", Icons.Material.Filled.Movie),
        ("sports_esports", Icons.Material.Filled.SportsEsports),
        ("celebration", Icons.Material.Filled.Celebration),
        ("card_giftcard", Icons.Material.Filled.CardGiftcard),
        ("volunteer_activism", Icons.Material.Filled.VolunteerActivism),
        ("checkroom", Icons.Material.Filled.Checkroom),
        ("shopping_bag", Icons.Material.Filled.ShoppingBag),
        ("store", Icons.Material.Filled.Store),
        ("devices", Icons.Material.Filled.Devices),
        ("sell", Icons.Material.Filled.Sell),
        ("payments", Icons.Material.Filled.Payments),
        ("attach_money", Icons.Material.Filled.AttachMoney),
        ("savings", Icons.Material.Filled.Savings),
        ("trending_up", Icons.Material.Filled.TrendingUp),
        ("work", Icons.Material.Filled.Work),
        ("account_balance", Icons.Material.Filled.AccountBalance),
        ("account_balance_wallet", Icons.Material.Filled.AccountBalanceWallet),
        ("credit_card", Icons.Material.Filled.CreditCard),
        ("smartphone", Icons.Material.Filled.Smartphone),
        ("add_circle", Icons.Material.Filled.AddCircle),
        ("more_horiz", Icons.Material.Filled.MoreHoriz),
        ("category", Icons.Material.Filled.Category),
    ];

    private static readonly Dictionary<string, string> ByName = Entries.ToDictionary(e => e.Name, e => e.Svg);

    public static IReadOnlyList<string> Names { get; } = Entries.Select(e => e.Name).ToList();

    /// <summary>SVG del ícono; uno genérico si el nombre no está en el catálogo.</summary>
    public static string Svg(string? name) =>
        name is not null && ByName.TryGetValue(name, out var svg) ? svg : ByName[Fallback];

    /// <summary>Colores sugeridos para categorías (los de la semilla y algunos más).</summary>
    public static IReadOnlyList<string> Colors { get; } =
    [
        "#E53935", "#D81B60", "#8E24AA", "#5E35B1", "#3949AB", "#1E88E5", "#0277BD", "#00838F",
        "#00897B", "#2E7D32", "#43A047", "#9E9D24", "#F9A825", "#FB8C00", "#F4511E", "#6D4C41",
        "#795548", "#546E7A", "#757575", "#616161",
    ];
}
