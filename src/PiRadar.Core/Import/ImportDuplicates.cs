using PiRadar.Core.Domain;

namespace PiRadar.Core.Import;

/// <summary>Detecta filas del archivo que ya existen como movimientos guardados.</summary>
public static class ImportDuplicates
{
    public sealed record DuplicateKey(DateOnly Day, TransactionType Type, long AmountMinor, Currency Currency, string Text)
    {
        public static DuplicateKey From(ImportRow row) => new(
            DateOnly.FromDateTime(row.OccurredAt), row.Type, row.Amount.AmountMinor, row.Amount.Currency,
            TextOf(row.Description, row.Merchant));

        public static DuplicateKey From(Transaction t) => new(
            DateOnly.FromDateTime(t.OccurredAt), t.Type, t.Amount.AmountMinor, t.Amount.Currency,
            TextOf(t.Description, t.Merchant));

        private static string TextOf(string? description, string? merchant) =>
            ImportText.Key(string.IsNullOrWhiteSpace(description) ? merchant : description);
    }

    /// <summary>
    /// Números de fila que duplican movimientos ya guardados, por conteo: con 2 filas iguales y 1 guardado solo se
    /// marca la primera. Repetidas dentro del archivo sin equivalente guardado no se marcan.
    /// </summary>
    public static IReadOnlySet<int> Find(IEnumerable<ImportRow> rows, IEnumerable<Transaction> existing)
    {
        var available = new Dictionary<DuplicateKey, int>();
        foreach (var t in existing)
        {
            var k = DuplicateKey.From(t);
            available[k] = available.GetValueOrDefault(k) + 1;
        }

        var result = new HashSet<int>();
        foreach (var row in rows)
        {
            var k = DuplicateKey.From(row);
            if (available.TryGetValue(k, out var n) && n > 0)
            {
                available[k] = n - 1;
                result.Add(row.RowNumber);
            }
        }
        return result;
    }
}
