using System.Text;
using PiRadar.Core.Domain;

namespace PiRadar.Core.Import;

/// <summary>Lee archivos CSV (RFC 4180) con separador <c>;</c>, <c>,</c> o tabulador detectado, en UTF-8 o Windows-1252. Todas las celdas son texto.</summary>
public static class CsvReader
{
    // Windows-1252 en 0x80–0x9F (0x81, 0x8D, 0x8F, 0x90 y 0x9D quedan como en Latin-1).
    private const string Cp1252High = "€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008dŽ\u008f\u0090‘’“”•–—˜™š›œ\u009džŸ";

    public static SheetData Read(Stream csv) => Read(csv, ImportLimitSet.Default);

    internal static SheetData Read(Stream csv, ImportLimitSet limits)
    {
        var bytes = ImportIO.ReadLimited(csv, limits.MaxFileBytes).ToArray();
        return Parse(Decode(bytes), limits);
    }

    private static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        try { return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes); }
        catch (DecoderFallbackException) { }

        var sb = new StringBuilder(bytes.Length);
        foreach (var b in bytes)
            sb.Append(b is >= 0x80 and <= 0x9F ? Cp1252High[b - 0x80] : (char)b);
        return sb.ToString();
    }

    private static char DetectSeparator(string text)
    {
        int semi = 0, comma = 0, tab = 0;
        var inQuotes = false;
        var hasContent = false;
        foreach (var c in text)
        {
            if (c == '"') { inQuotes = !inQuotes; hasContent = true; continue; }
            if (inQuotes) continue;
            if (c is '\n' or '\r')
            {
                if (hasContent) break;
                continue;
            }
            if (!char.IsWhiteSpace(c)) hasContent = true;
            if (c == ';') semi++;
            else if (c == ',') comma++;
            else if (c == '\t') tab++;
        }
        var max = Math.Max(semi, Math.Max(comma, tab));
        if (max == 0) return ',';
        var winners = (semi == max ? 1 : 0) + (comma == max ? 1 : 0) + (tab == max ? 1 : 0);
        if (winners > 1) return ',';
        return semi == max ? ';' : comma == max ? ',' : '\t';
    }

    private static SheetData Parse(string text, ImportLimitSet limits)
    {
        var sep = DetectSeparator(text);
        var rows = new List<List<CellValue>?>();
        var row = new List<CellValue>();
        var field = new StringBuilder();
        var inQuotes = false;
        var wasQuoted = false;
        var pending = false; // hay algo en la fila actual (campo, comillas o separador)

        void EndField()
        {
            var value = CellValue.FromText(field.ToString());
            if (row.Count >= limits.MaxColumns)
            {
                if (!value.IsEmpty) throw ImportIO.TooManyColumns(limits);
            }
            else row.Add(value);
            field.Clear();
            wasQuoted = false;
        }

        void EndRow()
        {
            EndField();
            if (row.TrueForAll(c => c.IsEmpty))
                rows.Add(null);
            else
            {
                if (rows.Count >= limits.MaxRows) throw ImportIO.TooManyRows(limits);
                rows.Add(row);
            }
            row = [];
            pending = false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else if (c == '"' && field.Length == 0 && !wasQuoted)
            {
                inQuotes = true;
                wasQuoted = true;
                pending = true;
            }
            else if (c == sep)
            {
                EndField();
                pending = true;
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                EndRow();
            }
            else
            {
                field.Append(c);
                pending = true;
            }
        }
        if (pending || field.Length > 0) EndRow();
        return SheetData.Normalize("CSV", rows);
    }
}
