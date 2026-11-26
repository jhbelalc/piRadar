using PiRadar.Core.Import;

namespace PiRadar.Core.Tests.Import;

/// <summary>Arma hojas en memoria: string ⇒ texto, decimal/int ⇒ número, DateTime ⇒ fecha, null ⇒ vacía.</summary>
internal static class ImportTestSheets
{
    public static SheetData Sheet(params object?[][] rows)
    {
        var raw = rows.Select(r => (List<CellValue>?)r.Select(Cell).ToList()).ToList();
        return SheetData.Normalize("Hoja1", raw);
    }

    private static CellValue Cell(object? o) => o switch
    {
        null => CellValue.Empty,
        string s => CellValue.FromText(s),
        decimal d => CellValue.FromNumber(d),
        int i => CellValue.FromNumber(i),
        DateTime dt => CellValue.FromDate(dt),
        _ => throw new ArgumentException("tipo"),
    };

    public static ImportMapping Map(int? header, params ImportField[] cols) => new(header, cols);
}
