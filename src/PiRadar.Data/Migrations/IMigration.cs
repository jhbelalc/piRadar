using SQLite;

namespace PiRadar.Data.Migrations;

/// <summary>
/// Cambio de esquema versionado con <c>PRAGMA user_version</c>. Usar SQL explícito, nunca <c>CreateTable&lt;T&gt;</c>
/// ni inserts con records: una migración vieja no debe depender de la forma actual de las clases.
/// </summary>
internal interface IMigration
{
    int Version { get; }

    void Apply(SQLiteConnection db);
}
