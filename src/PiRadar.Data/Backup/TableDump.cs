using System.Text.Json;
using SQLite;

namespace PiRadar.Data.Backup;

/// <summary>
/// Lee y escribe tablas completas sin depender de las clases de sqlite-net, para que un respaldo refleje el esquema
/// real de su versión (igual que las migraciones, que usan SQL explícito).
/// </summary>
internal static class TableDump
{
    public static IReadOnlyList<string> TableNames(SQLiteConnection db) =>
        db.QueryScalars<string>("SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name");

    /// <summary>Columnas de la tabla con su tipo declarado en minúsculas (<c>integer</c>, <c>varchar</c>, <c>blob</c>…).</summary>
    public static IReadOnlyDictionary<string, string> Columns(SQLiteConnection db, string table) =>
        db.Query<ColumnRow>($"SELECT name AS Name, lower(type) AS Type FROM pragma_table_info({QuoteLiteral(table)})")
            .ToDictionary(c => c.Name, c => c.Type, StringComparer.Ordinal);

    public static Dictionary<string, BackupTable> DumpAll(SQLiteConnection db) =>
        TableNames(db).ToDictionary(t => t, t => Dump(db, t), StringComparer.Ordinal);

    public static BackupTable Dump(SQLiteConnection db, string table)
    {
        var result = new BackupTable();
        var statement = SQLite3.Prepare2(db.Handle, $"SELECT * FROM {Quote(table)}");
        try
        {
            var count = SQLite3.ColumnCount(statement);
            for (var i = 0; i < count; i++)
            {
                result.Columns.Add(SQLite3.ColumnName16(statement, i));
            }

            while (SQLite3.Step(statement) == SQLite3.Result.Row)
            {
                var row = new object?[count];
                for (var i = 0; i < count; i++)
                {
                    row[i] = SQLite3.ColumnType(statement, i) switch
                    {
                        SQLite3.ColType.Integer => SQLite3.ColumnInt64(statement, i),
                        SQLite3.ColType.Float => SQLite3.ColumnDouble(statement, i),
                        SQLite3.ColType.Text => SQLite3.ColumnString(statement, i),
                        SQLite3.ColType.Blob => Convert.ToBase64String(SQLite3.ColumnByteArray(statement, i)),
                        _ => null,
                    };
                }

                result.Rows.Add(row);
            }
        }
        finally
        {
            SQLite3.Finalize(statement);
        }

        return result;
    }

    /// <summary>Borra el contenido de todas las tablas de <paramref name="db"/>.</summary>
    public static void DeleteAll(SQLiteConnection db)
    {
        foreach (var table in TableNames(db))
        {
            db.Execute($"DELETE FROM {Quote(table)}");
        }
    }

    /// <summary>
    /// Inserta las filas. Las tablas y columnas deben existir en <paramref name="db"/>: los nombres vienen del archivo
    /// y se validan antes de armar el SQL.
    /// </summary>
    /// <exception cref="FormatException">Tabla o columna desconocida, o un valor que no corresponde.</exception>
    public static void Load(SQLiteConnection db, IReadOnlyDictionary<string, BackupTable> tables)
    {
        var existing = TableNames(db).ToHashSet(StringComparer.Ordinal);
        foreach (var (table, data) in tables)
        {
            if (!existing.Contains(table))
            {
                throw new FormatException($"Tabla desconocida en el respaldo: {table}.");
            }

            var columns = Columns(db, table);
            if (data.Columns.Count == 0 || data.Columns.Any(c => !columns.ContainsKey(c)) || data.Columns.Distinct().Count() != data.Columns.Count)
            {
                throw new FormatException($"Columnas no válidas en la tabla {table}.");
            }

            var types = data.Columns.Select(c => columns[c]).ToArray();
            var sql = $"INSERT INTO {Quote(table)} ({string.Join(", ", data.Columns.Select(Quote))}) " +
                $"VALUES ({string.Join(", ", data.Columns.Select(_ => "?"))})";
            foreach (var row in data.Rows)
            {
                if (row.Length != types.Length)
                {
                    throw new FormatException($"Fila incompleta en la tabla {table}.");
                }

                db.Execute(sql, row.Select((value, i) => ToSqlValue(value, types[i])).ToArray());
            }
        }
    }

    private static object? ToSqlValue(object? value, string declaredType)
    {
        // Del JSON llegan JsonElement; de un volcado en memoria, valores ya convertidos.
        var plain = value switch
        {
            null or JsonElement { ValueKind: JsonValueKind.Null } => null,
            JsonElement { ValueKind: JsonValueKind.Number } n => n.TryGetInt64(out var l) ? l : n.GetDouble(),
            JsonElement { ValueKind: JsonValueKind.String } s => s.GetString(),
            long or double or string => value,
            _ => throw new FormatException("Valor no válido en el respaldo."),
        };

        return plain is string text && declaredType == "blob" ? Convert.FromBase64String(text) : plain;
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static string QuoteLiteral(string text) => "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'";

    private sealed class ColumnRow
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
    }
}
