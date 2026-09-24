using System.Security.Cryptography;
using PiRadar.Core.Abstractions;

namespace PiRadar.Data;

public enum DatabaseStatus
{
    NotOpened,
    Opened,

    /// <summary>El archivo de datos existe pero no hay clave guardada (p. ej. se restauró el archivo sin el Keystore).</summary>
    KeyMissing,

    /// <summary>Hay clave pero no abre el archivo: clave distinta o archivo dañado.</summary>
    Unreadable,
}

/// <summary>
/// Dueño único de la BD de la app: obtiene o crea la clave, abre la BD y permite empezar de cero.
/// Registrar como singleton.
/// </summary>
public sealed class DatabaseHost(IDatabaseKeyProvider keys, string databasePath) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private PiRadarDatabase? _database;

    public DatabaseStatus Status { get; private set; } = DatabaseStatus.NotOpened;

    public string DatabasePath { get; } = databasePath;

    /// <summary>La BD abierta. Solo válida cuando <see cref="Status"/> es <see cref="DatabaseStatus.Opened"/>.</summary>
    public PiRadarDatabase Database =>
        _database ?? throw new InvalidOperationException($"La base de datos no está abierta (estado: {Status}).");

    /// <summary>Abre la BD si no está abierta. Idempotente y seguro ante llamadas concurrentes.</summary>
    public async Task<DatabaseStatus> OpenAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (Status == DatabaseStatus.Opened)
            {
                return Status;
            }

            var key = await keys.TryGetKeyAsync(cancellationToken);
            if (key is null)
            {
                if (File.Exists(DatabasePath))
                {
                    // No generar una clave nueva: dejaría los datos existentes ilegibles para siempre.
                    return Status = DatabaseStatus.KeyMissing;
                }

                // La clave se guarda antes de crear la BD para no quedar nunca con un archivo sin clave.
                key = RandomNumberGenerator.GetBytes(32);
                await keys.SaveKeyAsync(key, cancellationToken);
            }

            try
            {
                _database = await PiRadarDatabase.OpenAsync(DatabasePath, key);
                return Status = DatabaseStatus.Opened;
            }
            catch (DatabaseUnreadableException)
            {
                return Status = DatabaseStatus.Unreadable;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Borra la BD y su clave, y crea una nueva vacía. Se pierden todos los datos.</summary>
    public async Task<DatabaseStatus> ResetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await CloseCoreAsync();
            PiRadarDatabase.DeleteFiles(DatabasePath);
            await keys.DeleteKeyAsync(cancellationToken);
            Status = DatabaseStatus.NotOpened;
        }
        finally
        {
            _gate.Release();
        }

        return await OpenAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await CloseCoreAsync();
            Status = DatabaseStatus.NotOpened;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task CloseCoreAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
            _database = null;
        }
    }
}
