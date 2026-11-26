namespace PiRadar.Core.Import;

/// <summary>
/// Hoja leída de un archivo. La fila i corresponde a la fila i+1 de Excel; todas las filas tienen
/// <see cref="ColumnCount"/> celdas y no hay filas vacías al final.
/// </summary>
public sealed class SheetData
{
    public SheetData(string name, IReadOnlyList<IReadOnlyList<CellValue>> rows, int columnCount)
    {
        Name = name;
        Rows = rows;
        ColumnCount = columnCount;
    }

    public string Name { get; }
    public IReadOnlyList<IReadOnlyList<CellValue>> Rows { get; }
    public int ColumnCount { get; }

    /// <summary>Letra de columna estilo Excel: 0 ⇒ A, 25 ⇒ Z, 26 ⇒ AA.</summary>
    public static string ColumnLetter(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        var s = "";
        for (var n = index + 1; n > 0; n = (n - 1) / 26)
            s = (char)('A' + (n - 1) % 26) + s;
        return s;
    }

    /// <summary>Rellena filas cortas con celdas vacías y recorta las filas vacías del final. Las filas nulas son filas vacías.</summary>
    internal static SheetData Normalize(string name, List<List<CellValue>?> raw)
    {
        var last = raw.Count - 1;
        while (last >= 0 && (raw[last] is null || raw[last]!.TrueForAll(c => c.IsEmpty))) last--;

        var columns = 0;
        for (var i = 0; i <= last; i++)
            if (raw[i] is { } r) columns = Math.Max(columns, r.Count);

        var rows = new List<IReadOnlyList<CellValue>>(last + 1);
        for (var i = 0; i <= last; i++)
        {
            var row = new CellValue[columns];
            if (raw[i] is { } r) r.CopyTo(row);
            rows.Add(row);
        }
        return new SheetData(name, rows, columns);
    }
}
