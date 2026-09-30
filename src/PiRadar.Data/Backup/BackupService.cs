using PiRadar.Core.Domain;
using System.IO.Compression;
using System.Text.Json;
using PiRadar.Core.Abstractions;
using PiRadar.Core.Backup;
using PiRadar.Data.Migrations;
using PiRadar.Data.Repositories;
using SQLite;

namespace PiRadar.Data.Backup;

/// <summary>
/// Exporta todas las tablas a JSON, comprimido con GZip y cifrado con <see cref="BackupCrypto"/>. Al abrir un respaldo
/// lo carga en una BD en memoria con el esquema de su versión, le aplica las mismas migraciones que a la BD real y
/// valida las claves foráneas; así un respaldo viejo queda con el esquema actual antes de tocar los datos del usuario.
/// </summary>
internal sealed class BackupService(DatabaseHost host, IClock clock) : IBackupService
{
    /// <summary>Iteraciones de PBKDF2; los tests usan menos para no tardar.</summary>
    internal int Iterations { get; init; } = BackupCrypto.DefaultIterations;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<byte[]> ExportAsync(string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var document = new BackupDocument { CreatedAt = clock.UtcNow };

        // Una sola transacción de lectura: todas las tablas del mismo instante.
        await host.Database.Connection.RunInTransactionAsync(db =>
        {
            document.SchemaVersion = Migrator.GetVersion(db);
            document.Tables = TableDump.DumpAll(db);
        });

        // PBKDF2 con 600.000 iteraciones tarda: fuera del hilo que llama.
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return BackupCrypto.Encrypt(Compress(document), password, document.SchemaVersion, Iterations);
        }, cancellationToken);
    }

    public Task<BackupContents> OpenAsync(byte[] file, string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        return Task.Run<BackupContents>(() =>
        {
            var header = BackupCrypto.ReadHeader(file);
            if (header.SchemaVersion > Migrator.LatestVersion)
            {
                throw new BackupException(AppLocale.Pick("El respaldo se hizo con una versión más nueva de piRadar. Actualiza la app para restaurarlo.", "This backup was made with a newer version of piRadar. Update the app to restore it."));
            }

            var body = BackupCrypto.Decrypt(file, password);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var document = Decompress(body);
                // La versión de la cabecera está autenticada por el cifrado; la del JSON debe coincidir.
                if (document.App != BackupDocument.AppName || document.SchemaVersion != header.SchemaVersion || document.SchemaVersion < 1)
                {
                    throw new FormatException("Cabecera y contenido no coinciden.");
                }

                return Upgrade(document);
            }
            catch (Exception ex) when (ex is JsonException or FormatException or SQLiteException or InvalidDataException)
            {
                throw new BackupException(AppLocale.Pick("El archivo de respaldo está dañado.", "The backup file is damaged."), ex);
            }
        }, cancellationToken);
    }

    public async Task RestoreAsync(BackupContents contents, CancellationToken cancellationToken = default)
    {
        if (contents is not Contents { } opened)
        {
            throw new ArgumentException("El respaldo no se abrió con este servicio.", nameof(contents));
        }

        await host.Database.Connection.RunInTransactionAsync(db =>
        {
            if (Migrator.GetVersion(db) != opened.SchemaVersion)
            {
                throw new InvalidOperationException("El esquema de la BD cambió después de abrir el respaldo.");
            }

            // Las claves foráneas se validan al confirmar, no fila por fila (Category apunta a sí misma).
            db.Execute("PRAGMA defer_foreign_keys = ON");
            TableDump.DeleteAll(db);
            TableDump.Load(db, opened.Tables);

            // Los datos quedan tal como estaban al crear ese respaldo: esa es la fecha del último respaldo. (El archivo
            // trae la del respaldo anterior, porque la fecha se guarda después de exportar.)
            db.Execute(
                """INSERT OR REPLACE INTO "AppSetting" ("Key", "Value") VALUES (?, ?)""",
                AppSettingKeys.LastBackupAt,
                SettingsRepository.FormatBackupDate(opened.Summary.CreatedAt));
        });
    }

    /// <summary>Recrea el esquema del respaldo en memoria, carga sus filas y migra a la versión actual.</summary>
    private static Contents Upgrade(BackupDocument document)
    {
        using var db = new SQLiteConnection(new SQLiteConnectionString(":memory:", storeDateTimeAsTicks: true));
        db.Execute("PRAGMA foreign_keys = ON");

        db.RunInTransaction(() =>
        {
            Migrator.MigrateTo(db, document.SchemaVersion);
            db.Execute("PRAGMA defer_foreign_keys = ON");
            TableDump.DeleteAll(db); // datos semilla de las migraciones
            TableDump.Load(db, document.Tables);
        });

        // Si faltan filas referenciadas (p. ej. una categoría), el commit anterior ya habría fallado; esto cubre el resto.
        if (db.ExecuteScalar<int>("SELECT count(*) FROM pragma_foreign_key_check") > 0)
        {
            throw new FormatException("Referencias rotas en el respaldo.");
        }

        db.RunInTransaction(() => Migrator.Migrate(db));

        var (count, first, last) = (
            db.ExecuteScalar<int>("SELECT count(*) FROM \"Transactions\""),
            db.ExecuteScalar<long?>("SELECT min(\"OccurredAt\") FROM \"Transactions\""),
            db.ExecuteScalar<long?>("SELECT max(\"OccurredAt\") FROM \"Transactions\""));
        var summary = new BackupSummary(
            DateTime.SpecifyKind(document.CreatedAt, DateTimeKind.Utc),
            count,
            db.ExecuteScalar<int>("SELECT count(*) FROM \"Receipt\""),
            first is { } f ? new DateTime(f, DateTimeKind.Local) : null,
            last is { } l ? new DateTime(l, DateTimeKind.Local) : null);

        return new Contents(summary, Migrator.GetVersion(db), TableDump.DumpAll(db));
    }

    private static byte[] Compress(BackupDocument document)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            JsonSerializer.Serialize(gzip, document, Json);
        }

        return output.ToArray();
    }

    private static BackupDocument Decompress(byte[] body)
    {
        using var gzip = new GZipStream(new MemoryStream(body), CompressionMode.Decompress);
        return JsonSerializer.Deserialize<BackupDocument>(gzip, Json) ?? throw new FormatException("Respaldo vacío.");
    }

    private sealed class Contents(BackupSummary summary, int schemaVersion, Dictionary<string, BackupTable> tables) : BackupContents
    {
        public override BackupSummary Summary { get; } = summary;

        public int SchemaVersion { get; } = schemaVersion;

        public Dictionary<string, BackupTable> Tables { get; } = tables;
    }
}
