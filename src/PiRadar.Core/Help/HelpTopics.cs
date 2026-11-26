namespace PiRadar.Core.Help;

/// <summary>Artículos de <see cref="HelpContent"/> que abre el botón "?" de cada pantalla (ayuda contextual, dentro de la app).</summary>
public static class HelpTopics
{
    public static readonly string[] Scan = ["escanear", "mejor-foto", "permiso-camara", "total-incorrecto"];
    public static readonly string[] Transaction = ["registrar-movimiento", "total-incorrecto", "editar-borrar"];
    public static readonly string[] Transactions = ["filtrar-movimientos", "editar-borrar", "saldo-inicial", "exportar-csv"];
    public static readonly string[] Analytics = ["ver-analisis", "detalle-analisis", "saldo-inicial", "todo-en-moneda-base"];
    public static readonly string[] Backup = ["crear-respaldo", "restaurar-respaldo", "recordatorio-respaldo", "olvide-contrasena", "deshacer-importacion", "exportar-csv"];
    public static readonly string[] Import = ["importar-movimientos", "importar-avisos", "deshacer-importacion"];
    public static readonly string[] Categories = ["crear-categoria", "archivar"];
    public static readonly string[] PaymentMethods = ["medios-de-pago", "archivar"];
    public static readonly string[] ExchangeRates = ["tasas", "todo-en-moneda-base"];
    public static readonly string[] Settings = ["idioma-tema", "donde-estan-mis-datos"];

    public static IReadOnlyList<string[]> All =>
        [Scan, Transaction, Transactions, Analytics, Backup, Import, Categories, PaymentMethods, ExchangeRates, Settings];
}
