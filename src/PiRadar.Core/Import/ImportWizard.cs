using PiRadar.Core.Domain;

namespace PiRadar.Core.Import;

/// <summary>Lógica pura del asistente de importación (la pantalla solo la usa).</summary>
public static class ImportWizard
{
    /// <summary>Hasta <paramref name="count"/> valores no vacíos de la columna (después de los encabezados), recortados con "…".</summary>
    public static IReadOnlyList<string> Samples(SheetData sheet, int column, int? headerRow, int count = 3, int maxLength = 40)
    {
        var result = new List<string>(count);
        if (column < 0 || column >= sheet.ColumnCount) return result;
        for (var r = (headerRow ?? -1) + 1; r < sheet.Rows.Count && result.Count < count; r++)
        {
            var cell = sheet.Rows[r][column];
            // Los montos binarios de Excel (…9.3499999996) se muestran con 2 decimales como máximo.
            var text = cell.Kind == CellKind.Number && cell.Number is { } n
                ? Math.Round(n, 2, MidpointRounding.AwayFromZero).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                : ImportText.CleanCell(cell);
            if (string.IsNullOrEmpty(text)) continue;
            result.Add(text.Length > maxLength ? text[..(maxLength - 1)] + "…" : text);
        }
        return result;
    }

    /// <summary>Texto de encabezado de una columna, o null si no hay encabezado o está vacío.</summary>
    public static string? Header(SheetData sheet, int column, int? headerRow) =>
        headerRow is { } h && h >= 0 && h < sheet.Rows.Count && column < sheet.ColumnCount
            ? ImportText.CleanCell(sheet.Rows[h][column])
            : null;

    /// <summary>Filas que se importan: todas menos los posibles duplicados, salvo que se incluyan.</summary>
    public static IReadOnlyList<ImportRow> IncludedRows(ImportPreview preview, IReadOnlySet<int> duplicates, bool includeDuplicates) =>
        includeDuplicates || duplicates.Count == 0
            ? preview.Rows
            : preview.Rows.Where(r => !duplicates.Contains(r.RowNumber)).ToList();

    /// <summary>Qué tipos tienen filas sin categoría y necesitan una categoría de respaldo.</summary>
    public static (bool Expense, bool Income) FallbacksNeeded(IEnumerable<ImportRow> rows)
    {
        bool expense = false, income = false;
        foreach (var r in rows)
        {
            if (!string.IsNullOrWhiteSpace(r.Category)) continue;
            if (r.Type == TransactionType.Expense) expense = true;
            else income = true;
        }
        return (expense, income);
    }

    /// <summary>
    /// Plan de importación con las filas incluidas. Solo pasan las elecciones de categorías y medios que esas filas usan.
    /// </summary>
    public static ImportPlan BuildPlan(
        string? fileName,
        IReadOnlyList<ImportRow> includedRows,
        IEnumerable<CategoryChoice> categories,
        IEnumerable<PaymentMethodChoice> paymentMethods,
        int? fallbackExpenseCategoryId,
        int? fallbackIncomeCategoryId)
    {
        var usedCategories = includedRows
            .Where(r => !string.IsNullOrWhiteSpace(r.Category))
            .Select(r => (ImportText.Key(r.Category), r.Type)).ToHashSet();
        var usedMethods = includedRows
            .Where(r => !string.IsNullOrWhiteSpace(r.PaymentMethod))
            .Select(r => ImportText.Key(r.PaymentMethod)).ToHashSet();
        var (needExpense, needIncome) = FallbacksNeeded(includedRows);

        return new ImportPlan(
            fileName,
            includedRows,
            categories.Where(c => usedCategories.Contains((ImportText.Key(c.Name), c.Type))).ToList(),
            paymentMethods.Where(p => usedMethods.Contains(ImportText.Key(p.Name))).ToList(),
            needExpense ? fallbackExpenseCategoryId : null,
            needIncome ? fallbackIncomeCategoryId : null);
    }

    /// <summary>Rango [desde, hasta) de fechas de las filas, para buscar duplicados guardados; null si no hay filas.</summary>
    public static (DateTime From, DateTime To)? DateRange(IReadOnlyList<ImportRow> rows)
    {
        if (rows.Count == 0) return null;
        var min = rows.Min(r => r.OccurredAt).Date;
        var max = rows.Max(r => r.OccurredAt).Date;
        return (min, max.AddDays(1));
    }

    /// <summary>Subcategorías que cuelgan de la categoría del archivo <paramref name="name"/> (compara sin tildes ni mayúsculas).</summary>
    public static IReadOnlyList<ImportSubName> SubcategoriesOf(ImportPreview preview, ImportName name) =>
        preview.Subcategories.Where(s => s.Type == name.Type && SameName(s.Parent, name.Name)).ToList();

    /// <summary>Mismo nombre sin contar tildes, mayúsculas ni signos (como compara el importador).</summary>
    public static bool SameName(string? a, string? b) => ImportText.Key(a) == ImportText.Key(b);

    /// <summary>Totales por moneda (COP primero) de las filas que se van a importar.</summary>
    public static IReadOnlyList<CurrencySummary> Summarize(IEnumerable<ImportRow> rows) =>
        rows.GroupBy(r => r.Amount.Currency).OrderBy(g => g.Key).Select(g =>
        {
            var c = g.Key;
            var income = g.Where(r => r.Type == TransactionType.Income).Sum(r => r.Amount.AmountMinor);
            var expense = g.Where(r => r.Type == TransactionType.Expense).Sum(r => r.Amount.AmountMinor);
            return new CurrencySummary(c, g.Count(), new Money(income, c), new Money(expense, c),
                g.Min(r => r.OccurredAt), g.Max(r => r.OccurredAt));
        }).ToList();
}
