using PiRadar.Data.Migrations;
using SQLite;

namespace PiRadar.Data;

public sealed record DatabaseInfo(
    int SchemaVersion,
    string Cipher,
    bool ForeignKeysEnabled,
    int CategoryCount,
    int PaymentMethodCount,
    int TransactionCount);

/// <summary>Abre la BD cifrada (sqlite3mc en modo SQLCipher 4), la migra y expone la conexión.</summary>
public sealed class PiRadarDatabase : IAsyncDisposable
{
    public const string FileName = "piradar.db3";

    private const SQLiteOpenFlags OpenFlags =
        SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex;

    static PiRadarDatabase() => SQLitePCL.Batteries_V2.Init();

    private PiRadarDatabase(SQLiteAsyncConnection connection) => Connection = connection;

    internal SQLiteAsyncConnection Connection { get; }

    /// <summary>
    /// Abre (o crea) la BD en <paramref name="path"/> con la clave de 32 bytes y aplica las migraciones pendientes.
    /// </summary>
    /// <exception cref="DatabaseUnreadableException">La clave no corresponde o el archivo está dañado.</exception>
    public static async Task<PiRadarDatabase> OpenAsync(string path, byte[] key)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (key is not { Length: 32 })
        {
            throw new ArgumentException("La clave debe tener 32 bytes.", nameof(key));
        }

        var connectionString = new SQLiteConnectionString(
            path,
            OpenFlags,
            storeDateTimeAsTicks: true,
            key: key,
            // sqlite3mc: el esquema de cifrado se configura antes de PRAGMA key.
            preKeyAction: db =>
            {
                db.ExecuteScalar<string>("PRAGMA cipher = 'sqlcipher'");
                db.ExecuteScalar<int>("PRAGMA legacy = 4");
            },
            postKeyAction: db => db.Execute("PRAGMA foreign_keys = ON"));

        var connection = new SQLiteAsyncConnection(connectionString);
        try
        {
            // SQLite no valida la clave hasta la primera lectura.
            await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM sqlite_master");
        }
        catch (SQLiteException ex)
        {
            await connection.CloseAsync();
            throw new DatabaseUnreadableException("No se pudo leer la base de datos: clave incorrecta o archivo dañado.", ex);
        }

        // Todas las migraciones pendientes son atómicas: cada una corre en un SAVEPOINT anidado.
        await connection.RunInTransactionAsync(Migrator.Migrate);

        return new PiRadarDatabase(connection);
    }

    /// <summary>Datos de diagnóstico, sin información del usuario.</summary>
    public async Task<DatabaseInfo> GetInfoAsync() => new(
        SchemaVersion: await Connection.ExecuteScalarAsync<int>("PRAGMA user_version"),
        Cipher: await Connection.ExecuteScalarAsync<string>("PRAGMA cipher"),
        ForeignKeysEnabled: await Connection.ExecuteScalarAsync<int>("PRAGMA foreign_keys") == 1,
        CategoryCount: await Connection.ExecuteScalarAsync<int>("SELECT count(*) FROM \"Category\""),
        PaymentMethodCount: await Connection.ExecuteScalarAsync<int>("SELECT count(*) FROM \"PaymentMethod\""),
        TransactionCount: await Connection.ExecuteScalarAsync<int>("SELECT count(*) FROM \"Transactions\""));

    /// <summary>Borra el archivo de BD y sus archivos auxiliares de SQLite. La BD debe estar cerrada.</summary>
    public static void DeleteFiles(string path)
    {
        foreach (var suffix in (string[])["", "-journal", "-wal", "-shm"])
        {
            File.Delete(path + suffix);
        }
    }

    public async ValueTask DisposeAsync() => await Connection.CloseAsync();
}
