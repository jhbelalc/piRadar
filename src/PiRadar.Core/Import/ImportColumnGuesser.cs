namespace PiRadar.Core.Import;

/// <summary>Propone qué campo es cada columna, por el nombre del encabezado y luego por el contenido.</summary>
public static class ImportColumnGuesser
{
    private const int SampleRows = 200;
    private const double MinShare = 0.8;

    private sealed record Synonyms(ImportField Field, string[] Words, string[] ExactOnly);

    // El orden es el de prioridad en la pasada "contiene palabra".
    private static readonly Synonyms[] Table =
    [
        new(ImportField.Date, ["FECHA", "DIA", "DATE", "DAY", "FECHA MOVIMIENTO", "TRANSACTION DATE"], []),
        new(ImportField.Time, ["HORA", "TIME"], []),
        new(ImportField.Subcategory, ["SUBCATEGORIA", "SUB CATEGORIA", "SUBCATEGORY", "SUBTIPO", "SUBGRUPO"], []),
        new(ImportField.Category, ["CATEGORIA", "RUBRO", "CATEGORY"], ["TIPO DE GASTO", "TIPO DE INGRESO", "CLASE", "CLASIFICACION", "GRUPO"]),
        new(ImportField.PaymentMethod, ["MEDIO DE PAGO", "MEDIO", "FORMA DE PAGO", "METODO DE PAGO", "CUENTA", "TARJETA",
            "PAYMENT METHOD", "PAYMENT", "ACCOUNT", "CARD", "METHOD"], []),
        new(ImportField.Currency, ["MONEDA", "DIVISA", "CURRENCY"], []),
        new(ImportField.Description, ["DESCRIPCION", "CONCEPTO", "DETALLE", "DESCRIPTION", "MEMO", "DETAILS", "CONCEPT"], []),
        new(ImportField.Merchant, ["COMERCIO", "ESTABLECIMIENTO", "TIENDA", "LUGAR", "PROVEEDOR", "MERCHANT", "PAYEE", "STORE", "VENDOR"], []),
        new(ImportField.Notes, ["NOTAS", "NOTA", "OBSERVACIONES", "COMENTARIOS", "NOTES", "NOTE", "COMMENTS"], []),
        new(ImportField.Type, ["TIPO", "MOVIMIENTO", "TIPO DE MOVIMIENTO", "TYPE", "TRANSACTION TYPE", "KIND"], []),
        new(ImportField.Amount, ["VALOR", "MONTO", "IMPORTE", "CANTIDAD", "AMOUNT", "TOTAL", "VALUE", "SUMA"], []),
        new(ImportField.Income, ["INGRESO", "INGRESOS", "ENTRADA", "ENTRADAS", "ABONO", "ABONOS", "INCOME", "DEPOSIT", "DEPOSITS", "INFLOW"],
            ["CREDITO", "CREDIT"]),
        new(ImportField.Expense, ["GASTO", "GASTOS", "EGRESO", "EGRESOS", "SALIDA", "SALIDAS", "CARGO", "CARGOS", "EXPENSE", "EXPENSES",
            "SPENDING", "WITHDRAWAL", "OUTFLOW"], ["DEBITO", "DEBIT"]),
    ];

    public static ImportMapping Guess(SheetData sheet, int? headerRow)
    {
        var columns = new ImportField[sheet.ColumnCount];
        var keys = new string[sheet.ColumnCount];
        for (var c = 0; c < keys.Length; c++)
            keys[c] = headerRow is { } h && h < sheet.Rows.Count ? ImportText.Key(sheet.Rows[h][c].ToDisplayString()) : "";

        bool Assigned(ImportField f) => Array.IndexOf(columns, f) >= 0;

        // 1. Coincidencia exacta.
        foreach (var s in Table)
        {
            for (var c = 0; c < columns.Length && !Assigned(s.Field); c++)
            {
                if (columns[c] != ImportField.Ignore || keys[c].Length == 0) continue;
                if (s.Words.Contains(keys[c]) || s.ExactOnly.Contains(keys[c])) columns[c] = s.Field;
            }
        }

        // 2. La palabra aparece completa dentro del encabezado.
        foreach (var s in Table)
        {
            for (var c = 0; c < columns.Length && !Assigned(s.Field); c++)
            {
                if (columns[c] != ImportField.Ignore || keys[c].Length == 0) continue;
                if (s.Words.Any(w => ImportText.ContainsWords(keys[c], w))) columns[c] = s.Field;
            }
        }

        // Monto único y ingreso/gasto separados no conviven: gana la separación.
        if (Assigned(ImportField.Amount) && (Assigned(ImportField.Income) || Assigned(ImportField.Expense)))
            columns[Array.IndexOf(columns, ImportField.Amount)] = ImportField.Ignore;

        // 3. Por contenido.
        var first = headerRow is { } hr ? hr + 1 : 0;
        var last = Math.Min(sheet.Rows.Count, first + SampleRows);

        double Share(int col, Func<CellValue, bool> test)
        {
            int total = 0, ok = 0;
            for (var r = first; r < last; r++)
            {
                var cell = sheet.Rows[r][col];
                if (IsBlank(cell)) continue;
                total++;
                if (test(cell)) ok++;
            }
            return total == 0 ? 0 : (double)ok / total;
        }

        int? FirstFree(Func<CellValue, bool> test)
        {
            for (var c = 0; c < columns.Length; c++)
                if (columns[c] == ImportField.Ignore && Share(c, test) >= MinShare) return c;
            return null;
        }

        if (!Assigned(ImportField.Date) && FirstFree(IsDateLike) is { } dc) columns[dc] = ImportField.Date;

        var hasAmount = Assigned(ImportField.Amount) || Assigned(ImportField.Income) || Assigned(ImportField.Expense);
        if (!hasAmount && FirstFree(IsNumberLike) is { } ac) columns[ac] = ImportField.Amount;

        if (!Assigned(ImportField.Description)
            && FirstFree(cell => cell.Kind == CellKind.Text && !IsNumberLike(cell) && !IsDateLike(cell)) is { } tc)
            columns[tc] = ImportField.Description;

        return new ImportMapping(headerRow, columns);
    }

    private static bool IsBlank(CellValue c) => c.IsEmpty || (c.Kind == CellKind.Text && (string.IsNullOrWhiteSpace(c.Text) || ImportRowParser.IsDashOnly(c.Text)));

    private static bool IsDateLike(CellValue c) => c.Kind switch
    {
        CellKind.Date => true,
        CellKind.Text => ImportRowParser.TryParseDateText(c.Text!, DateOrder.DayFirst, out _, out _)
            || ImportRowParser.TryParseDateText(c.Text!, DateOrder.MonthFirst, out _, out _),
        _ => false,
    };

    private static bool IsNumberLike(CellValue c) => c.Kind switch
    {
        CellKind.Number => true,
        CellKind.Text => ImportRowParser.ParseAmount(c.Text!, out _, out _),
        _ => false,
    };
}
