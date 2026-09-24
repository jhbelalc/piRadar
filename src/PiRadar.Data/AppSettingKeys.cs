namespace PiRadar.Data;

public static class AppSettingKeys
{
    /// <summary>Moneda de la vista consolidada (<c>COP</c> o <c>USD</c>).</summary>
    public const string BaseCurrency = "BaseCurrency";

    /// <summary>Fecha UTC (ISO 8601) del último respaldo exportado.</summary>
    public const string LastBackupAt = "LastBackupAt";
}
