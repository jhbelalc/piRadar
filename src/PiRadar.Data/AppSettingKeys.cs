namespace PiRadar.Data;

public static class AppSettingKeys
{
    /// <summary>Moneda de la vista consolidada (<c>COP</c> o <c>USD</c>).</summary>
    public const string BaseCurrency = "BaseCurrency";

    /// <summary>Fecha UTC (ISO 8601) del último respaldo exportado.</summary>
    public const string LastBackupAt = "LastBackupAt";

    /// <summary>Tema de la interfaz: <c>System</c>, <c>Light</c> o <c>Dark</c>.</summary>
    public const string Theme = "Theme";

    /// <summary>Título de la pantalla de inicio elegido por el usuario; sin fila, <c>HomeTitle.Default</c>.</summary>
    public const string HomeTitle = "HomeTitle";

    /// <summary>Idioma elegido: <c>es</c> o <c>en</c>; sin fila, el idioma del dispositivo.</summary>
    public const string Language = "Language";
}
