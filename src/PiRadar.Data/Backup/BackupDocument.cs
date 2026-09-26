namespace PiRadar.Data.Backup;

/// <summary>Cuerpo JSON del respaldo, antes de comprimir y cifrar.</summary>
internal sealed class BackupDocument
{
    public const string AppName = "piRadar";

    public string App { get; set; } = AppName;

    public int SchemaVersion { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Todas las tablas por nombre, tal como están en la BD.</summary>
    public Dictionary<string, BackupTable> Tables { get; set; } = [];
}

/// <summary>
/// Columnas y filas de una tabla. Cada valor es <c>null</c>, número (entero o real) o texto; los BLOB (fotos de
/// recibos) van en base64 y se reconocen al importar por el tipo declarado de la columna (<c>blob</c>).
/// </summary>
internal sealed class BackupTable
{
    public List<string> Columns { get; set; } = [];

    public List<object?[]> Rows { get; set; } = [];
}
