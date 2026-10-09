namespace PiRadar.Core.Help;

/// <summary>Artículos de <see cref="HelpContent"/> que abre el botón "?" de cada pantalla (ayuda contextual, dentro de la app).</summary>
public static class HelpTopics
{
    public static readonly string[] Scan = ["escanear", "mejor-foto", "permiso-camara", "total-incorrecto"];
    public static readonly string[] Transaction = ["registrar-movimiento", "total-incorrecto", "editar-borrar"];
    public static readonly string[] Transactions = ["filtrar-movimientos", "editar-borrar", "saldo-inicial"];
    public static readonly string[] Analytics = ["ver-analisis", "saldo-inicial", "todo-en-moneda-base"];
    public static readonly string[] Backup = ["crear-respaldo", "restaurar-respaldo", "recordatorio-respaldo", "olvide-contrasena"];
    public static readonly string[] Categories = ["crear-categoria", "archivar"];
    public static readonly string[] PaymentMethods = ["medios-de-pago", "archivar"];
    public static readonly string[] ExchangeRates = ["tasas", "todo-en-moneda-base"];
    public static readonly string[] Settings = ["idioma-tema", "donde-estan-mis-datos"];

    public static IReadOnlyList<string[]> All =>
        [Scan, Transaction, Transactions, Analytics, Backup, Categories, PaymentMethods, ExchangeRates, Settings];
}
