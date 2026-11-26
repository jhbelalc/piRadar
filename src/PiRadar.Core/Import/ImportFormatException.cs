namespace PiRadar.Core.Import;

/// <summary>El archivo a importar no se puede leer (formato inválido o fuera de los límites). El mensaje es para el usuario.</summary>
public sealed class ImportFormatException(string message, Exception? inner = null) : Exception(message, inner);
