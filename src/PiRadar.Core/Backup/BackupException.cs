namespace PiRadar.Core.Backup;

/// <summary>El archivo no es un respaldo válido, la contraseña no corresponde o no se puede restaurar. El mensaje es para el usuario.</summary>
public sealed class BackupException(string message, Exception? inner = null) : Exception(message, inner);
