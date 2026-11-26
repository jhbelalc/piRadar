namespace PiRadar.Core.Import;

/// <summary>Asignación de columnas del archivo a campos del movimiento.</summary>
/// <param name="HeaderRow">Índice de la fila de encabezados, o null si no hay.</param>
/// <param name="Columns">Un campo por columna de la hoja.</param>
public sealed record ImportMapping(int? HeaderRow, IReadOnlyList<ImportField> Columns)
{
    /// <summary>Columna asignada al campo, o null.</summary>
    public int? ColumnOf(ImportField field)
    {
        for (var i = 0; i < Columns.Count; i++)
            if (Columns[i] == field) return i;
        return null;
    }

    public AmountMode AmountMode
    {
        get
        {
            var amount = ColumnOf(ImportField.Amount) is not null;
            var type = ColumnOf(ImportField.Type) is not null;
            var split = ColumnOf(ImportField.Income) is not null || ColumnOf(ImportField.Expense) is not null;
            if (amount && type) return AmountMode.AmountWithType;
            if (amount) return AmountMode.Signed;
            return split ? AmountMode.IncomeExpense : AmountMode.None;
        }
    }

    /// <summary>Hay fecha y una forma de obtener tipo y monto.</summary>
    public bool IsComplete => ColumnOf(ImportField.Date) is not null && AmountMode != AmountMode.None;

    /// <summary>
    /// Nuevo mapping con <paramref name="field"/> en <paramref name="column"/>. Un campo vive en una sola columna
    /// (la anterior queda Ignore) y Amount no convive con Income/Expense.
    /// </summary>
    public ImportMapping With(int column, ImportField field)
    {
        var list = Columns.ToArray();
        if (field != ImportField.Ignore)
        {
            for (var i = 0; i < list.Length; i++)
            {
                if (i == column) continue;
                var clash = list[i] == field
                    || (field == ImportField.Amount && list[i] is ImportField.Income or ImportField.Expense)
                    || (field is ImportField.Income or ImportField.Expense && list[i] == ImportField.Amount);
                if (clash) list[i] = ImportField.Ignore;
            }
        }
        list[column] = field;
        return this with { Columns = list };
    }

    /// <summary>Huella de los encabezados (para recordar la asignación); null si no hay fila de encabezados.</summary>
    public string? Fingerprint(SheetData sheet)
    {
        if (HeaderRow is not { } h || h < 0 || h >= sheet.Rows.Count) return null;
        return string.Join("|", sheet.Rows[h].Select(c => ImportText.Key(c.ToDisplayString())));
    }
}
