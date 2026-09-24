namespace PiRadar.Data;

/// <summary>La BD existe pero no se puede leer con la clave dada (clave distinta o archivo dañado).</summary>
public sealed class DatabaseUnreadableException(string message, Exception innerException)
    : Exception(message, innerException);
