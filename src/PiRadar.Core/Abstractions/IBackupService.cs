namespace PiRadar.Core.Abstractions;

/// <summary>Qué trae un respaldo, para mostrarlo antes de confirmar la restauración.</summary>
public sealed record BackupSummary(
    DateTime CreatedAt,
    int TransactionCount,
    int ReceiptCount,
    DateTime? FirstTransaction,
    DateTime? LastTransaction);

/// <summary>Respaldo ya descifrado y validado, listo para restaurar.</summary>
public abstract class BackupContents
{
    public abstract BackupSummary Summary { get; }
}

/// <summary>Respaldo cifrado con contraseña (<c>.piradar</c>) de todos los datos, incluidas las fotos de recibos.</summary>
public interface IBackupService
{
    /// <summary>Todas las tablas en un archivo cifrado con <paramref name="password"/>.</summary>
    Task<byte[]> ExportAsync(string password, CancellationToken cancellationToken = default);

    /// <summary>Descifra y valida el archivo; si es de una versión de esquema anterior, lo migra.</summary>
    /// <exception cref="Backup.BackupException">Contraseña incorrecta, archivo dañado o de una versión más nueva.</exception>
    Task<BackupContents> OpenAsync(byte[] file, string password, CancellationToken cancellationToken = default);

    /// <summary>Reemplaza todos los datos por los del respaldo, en una sola transacción.</summary>
    Task RestoreAsync(BackupContents contents, CancellationToken cancellationToken = default);
}
