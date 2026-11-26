namespace PiRadar.Core.Import;

/// <summary>Busca la fila de encabezados de una hoja.</summary>
public static class HeaderDetector
{
    private const int MaxScan = 20;

    /// <summary>
    /// Primera fila (entre las 20 primeras) con al menos 2 celdas de texto que no parecen datos, seguida de una fila
    /// con fechas, números o texto con dígitos. Null si no hay encabezado.
    /// </summary>
    public static int? Find(SheetData sheet)
    {
        var limit = Math.Min(MaxScan, sheet.Rows.Count);
        for (var i = 0; i < limit; i++)
        {
            var row = sheet.Rows[i];
            var texts = 0;
            var numbers = 0;
            var bad = false;
            foreach (var c in row)
            {
                switch (c.Kind)
                {
                    case CellKind.Text when string.IsNullOrWhiteSpace(c.Text): break;
                    case CellKind.Text:
                        if (LooksNumeric(c.Text!)) bad = true;
                        else texts++;
                        break;
                    case CellKind.Number: numbers++; break;
                    case CellKind.Date or CellKind.Boolean: bad = true; break;
                }
            }
            if (bad || texts < 2 || numbers > 1) continue;

            for (var j = i + 1; j < sheet.Rows.Count; j++)
            {
                var next = sheet.Rows[j];
                if (next.All(c => c.IsEmpty || (c.Kind == CellKind.Text && string.IsNullOrWhiteSpace(c.Text)))) continue;
                if (next.Any(c => c.Kind is CellKind.Date or CellKind.Number
                        || (c.Kind == CellKind.Text && c.Text!.Any(char.IsAsciiDigit))))
                    return i;
                break;
            }
        }
        return null;
    }

    /// <summary>Texto de solo dígitos y signos de fecha/monto ("03/08/2026", "$ 1.500").</summary>
    internal static bool LooksNumeric(string text)
    {
        var any = false;
        foreach (var c in text)
        {
            if (char.IsAsciiDigit(c)) any = true;
            else if (!(char.IsWhiteSpace(c) || "./-:,$+()%".Contains(c))) return false;
        }
        return any;
    }
}
