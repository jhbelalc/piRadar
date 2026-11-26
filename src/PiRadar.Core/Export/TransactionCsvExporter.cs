using System.Globalization;
using System.Text;
using PiRadar.Core.Domain;

namespace PiRadar.Core.Export;

/// <summary>
/// Escribe movimientos como CSV (UTF-8 con BOM, CRLF, RFC 4180). Español: <c>;</c> y coma decimal; inglés: <c>,</c> y punto.
/// Las celdas de texto que empiezan con <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, tabulador o CR llevan el prefijo <c>'</c>
/// para que una hoja de cálculo no las ejecute como fórmulas.
/// </summary>
public static class TransactionCsvExporter
{
    /// <summary>Escribe el CSV en <paramref name="output"/> sin cerrarlo. Categorías y medios incluyen los archivados.</summary>
    public static void Write(Stream output, IEnumerable<Transaction> transactions, IEnumerable<Category> categories,
        IEnumerable<PaymentMethod> paymentMethods)
    {
        var english = AppLocale.IsEnglish;
        var separator = english ? ',' : ';';
        var categoryById = categories.ToDictionary(c => c.Id);
        var methodById = paymentMethods.ToDictionary(p => p.Id);

        using var writer = new StreamWriter(output, new UTF8Encoding(true), 4096, leaveOpen: true) { NewLine = "\r\n" };

        string[] header = english
            ? ["Date", "Time", "Type", "Amount", "Currency", "Category", "Subcategory", "Payment method", "Merchant", "Description", "Notes"]
            : ["Fecha", "Hora", "Tipo", "Monto", "Moneda", "Categoría", "Subcategoría", "Medio de pago", "Comercio", "Descripción", "Notas"];
        WriteRow(writer, separator, header);

        var fields = new string[11];
        foreach (var tx in transactions.OrderBy(t => t.OccurredAt))
        {
            string category = "", subcategory = "";
            if (categoryById.TryGetValue(tx.CategoryId, out var c))
            {
                if (c.ParentId is { } parentId && categoryById.TryGetValue(parentId, out var parent))
                {
                    category = parent.Name;
                    subcategory = c.Name;
                }
                else category = c.Name;
            }
            var method = tx.PaymentMethodId is { } mid && methodById.TryGetValue(mid, out var m) ? m.Name : "";

            fields[0] = tx.OccurredAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            fields[1] = tx.OccurredAt.ToString("HH:mm", CultureInfo.InvariantCulture);
            fields[2] = tx.Type == TransactionType.Income ? AppLocale.Pick("Ingreso", "Income") : AppLocale.Pick("Gasto", "Expense");
            fields[3] = FormatAmount(tx.Amount, english);
            fields[4] = tx.Amount.Currency.ToString();
            fields[5] = Guard(category);
            fields[6] = Guard(subcategory);
            fields[7] = Guard(method);
            fields[8] = Guard(tx.Merchant);
            fields[9] = Guard(tx.Description);
            fields[10] = Guard(tx.Notes);
            WriteRow(writer, separator, fields);
        }
        writer.Flush();
    }

    /// <summary>Nombre de archivo sugerido, con la hora para que dos exportaciones del mismo día no choquen.</summary>
    public static string FileName(DateTime localNow) =>
        AppLocale.Pick("piradar-movimientos-", "piradar-transactions-")
        + localNow.ToString("yyyy-MM-dd-HHmm", CultureInfo.InvariantCulture) + ".csv";

    private static string FormatAmount(Money amount, bool english)
    {
        var scale = amount.Currency.Scale();
        var text = Math.Abs(amount.ToDecimal()).ToString("F" + scale, CultureInfo.InvariantCulture);
        return english ? text : text.Replace('.', ',');
    }

    private static string Guard(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + text : text;
    }

    private static void WriteRow(StreamWriter writer, char separator, IReadOnlyList<string> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0) writer.Write(separator);
            WriteField(writer, separator, fields[i]);
        }
        writer.Write("\r\n");
    }

    private static void WriteField(StreamWriter writer, char separator, string value)
    {
        var needsQuotes = value.Length > 0
            && (value.AsSpan().IndexOfAny([separator, '"', '\r', '\n']) >= 0 || char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]));
        if (!needsQuotes) { writer.Write(value); return; }
        writer.Write('"');
        writer.Write(value.Replace("\"", "\"\""));
        writer.Write('"');
    }
}
