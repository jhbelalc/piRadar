using System.Text.Json;

namespace PiRadar.Core.Import;

/// <summary>Asignación de columnas recordada para un archivo con los mismos encabezados.</summary>
public sealed record SavedImportMapping(string Fingerprint, IReadOnlyList<ImportField> Columns, DateOrder DateOrder);

/// <summary>Serializa y lee el valor del ajuste <c>ImportMapping</c> (una sola asignación recordada).</summary>
public static class ImportMappingSetting
{
    public const string Key = "ImportMapping";

    public static string Serialize(SavedImportMapping saved) => JsonSerializer.Serialize(new Dto
    {
        F = saved.Fingerprint,
        C = saved.Columns.Select(c => (int)c).ToArray(),
        D = (int)saved.DateOrder,
    });

    /// <summary>Lee el valor guardado; cualquier cosa inválida da null.</summary>
    public static SavedImportMapping? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            var dto = JsonSerializer.Deserialize<Dto>(value);
            if (dto?.F is not { Length: > 0 } fingerprint || dto.C is not { Length: > 0 and <= ImportLimits.MaxColumns } columns) return null;
            if (!Enum.IsDefined((DateOrder)dto.D)) return null;
            var fields = new ImportField[columns.Length];
            for (var i = 0; i < columns.Length; i++)
            {
                if (!Enum.IsDefined((ImportField)columns[i])) return null;
                fields[i] = (ImportField)columns[i];
            }
            return new SavedImportMapping(fingerprint, fields, (DateOrder)dto.D);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// La asignación guardada aplicada a esta hoja, o null si no corresponde (otros encabezados u otro número de columnas).
    /// </summary>
    public static ImportMapping? Apply(SavedImportMapping? saved, ImportMapping detected, SheetData sheet)
    {
        if (saved is null || detected.Fingerprint(sheet) is not { } fingerprint) return null;
        if (saved.Fingerprint != fingerprint || saved.Columns.Count != sheet.ColumnCount) return null;
        return detected with { Columns = saved.Columns };
    }

    private sealed class Dto
    {
        public string? F { get; set; }
        public int[]? C { get; set; }
        public int D { get; set; }
    }
}
