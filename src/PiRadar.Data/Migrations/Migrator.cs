using SQLite;

namespace PiRadar.Data.Migrations;

internal static class Migrator
{
    /// <summary>Todas las migraciones en orden. Agregar las nuevas al final.</summary>
    public static readonly IReadOnlyList<IMigration> All = [new Migration001_Initial()];

    public static int LatestVersion => All[^1].Version;

    public static int GetVersion(SQLiteConnection db) => db.ExecuteScalar<int>("PRAGMA user_version");

    /// <summary>Aplica en una transacción cada migración pendiente y actualiza <c>user_version</c>.</summary>
    public static void Migrate(SQLiteConnection db) => MigrateTo(db, LatestVersion);

    /// <summary>
    /// Como <see cref="Migrate"/>, pero se detiene en <paramref name="version"/>. El respaldo lo usa para recrear el
    /// esquema con el que se exportó y después migrar sus datos igual que una BD.
    /// </summary>
    public static void MigrateTo(SQLiteConnection db, int version)
    {
        var current = GetVersion(db);
        if (current > LatestVersion)
        {
            throw new InvalidOperationException(
                $"La base de datos tiene la versión de esquema {current}, más nueva que la que soporta la app ({LatestVersion}).");
        }

        foreach (var migration in All.Where(m => m.Version > current && m.Version <= version))
        {
            db.RunInTransaction(() =>
            {
                migration.Apply(db);
                db.Execute($"PRAGMA user_version = {migration.Version}");
            });
        }
    }
}
