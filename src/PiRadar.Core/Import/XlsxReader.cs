using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using PiRadar.Core.Domain;

namespace PiRadar.Core.Import;

/// <summary>Lee archivos .xlsx (Open XML) en streaming con <see cref="ZipArchive"/> y <see cref="XmlReader"/>, sin cargar toda la hoja en memoria.</summary>
public static class XlsxReader
{
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private sealed record SheetRef(string Name, string RelId);
    private sealed record Workbook(List<SheetRef> Sheets, bool Date1904, Dictionary<string, string> Targets);

    /// <summary>Nombres de las hojas, en el orden del libro.</summary>
    public static IReadOnlyList<string> GetSheetNames(Stream xlsx) => GetSheetNames(xlsx, ImportLimitSet.Default);

    /// <summary>Lee una hoja por nombre; <paramref name="sheetName"/> null = la primera.</summary>
    public static SheetData Read(Stream xlsx, string? sheetName = null) => Read(xlsx, sheetName, ImportLimitSet.Default);

    internal static IReadOnlyList<string> GetSheetNames(Stream xlsx, ImportLimitSet limits)
    {
        using var archive = Open(xlsx, limits);
        return Guard(() => ReadWorkbook(archive, limits).Sheets.Select(s => s.Name).ToList());
    }

    internal static SheetData Read(Stream xlsx, string? sheetName, ImportLimitSet limits)
    {
        using var archive = Open(xlsx, limits);
        return Guard(() =>
        {
            var book = ReadWorkbook(archive, limits);
            var sheet = sheetName is null
                ? book.Sheets.FirstOrDefault()
                : book.Sheets.FirstOrDefault(s => s.Name == sheetName)
                  ?? book.Sheets.FirstOrDefault(s => string.Equals(s.Name, sheetName, StringComparison.OrdinalIgnoreCase));
            if (sheet is null)
                throw new ImportFormatException(sheetName is null
                    ? AppLocale.Pick("El libro no tiene hojas.", "The workbook has no sheets.")
                    : AppLocale.Pick($"No se encontró la hoja \"{sheetName}\".", $"The sheet \"{sheetName}\" was not found."));
            if (!book.Targets.TryGetValue(sheet.RelId, out var path)) throw Invalid();

            var shared = ReadSharedStrings(archive, limits);
            var dateStyles = ReadDateStyles(archive, limits);
            return ReadSheet(archive, path, sheet.Name, shared, dateStyles, book.Date1904, limits);
        });
    }

    private static ImportFormatException Invalid(Exception? inner = null) => new(
        AppLocale.Pick("El archivo no es un Excel (.xlsx) válido.", "This file is not a valid Excel (.xlsx) file."), inner);

    private static T Guard<T>(Func<T> action)
    {
        try { return action(); }
        catch (XmlException ex) { throw Invalid(ex); }
        catch (InvalidDataException ex) { throw Invalid(ex); }
        catch (ArgumentException ex) { throw Invalid(ex); }
        catch (OverflowException ex) { throw Invalid(ex); }
    }

    private static ZipArchive Open(Stream xlsx, ImportLimitSet limits)
    {
        var ms = ImportIO.ReadLimited(xlsx, limits.MaxFileBytes);
        if (ms.Length >= 4 && ms.GetBuffer()[0] == 0xD0 && ms.GetBuffer()[1] == 0xCF && ms.GetBuffer()[2] == 0x11 && ms.GetBuffer()[3] == 0xE0)
            throw new ImportFormatException(AppLocale.Pick(
                "Este es un Excel antiguo (.xls). Ábrelo en Excel y guárdalo como .xlsx.",
                "This is an old Excel file (.xls). Open it in Excel and save it as .xlsx."));
        try { return new ZipArchive(ms, ZipArchiveMode.Read); }
        catch (InvalidDataException ex) { throw Invalid(ex); }
    }

    private static XmlReader? OpenPart(ZipArchive archive, string path, ImportLimitSet limits, bool required)
    {
        var entry = archive.GetEntry(path)
                    ?? archive.Entries.FirstOrDefault(e => string.Equals(e.FullName.Replace('\\', '/'), path, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            if (required) throw Invalid();
            return null;
        }
        var stream = new LimitedReadStream(entry.Open(), limits.MaxPartUncompressedBytes);
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, CloseInput = true };
        return XmlReader.Create(stream, settings);
    }

    private static Workbook ReadWorkbook(ZipArchive archive, ImportLimitSet limits)
    {
        var sheets = new List<SheetRef>();
        var date1904 = false;
        using (var r = OpenPart(archive, "xl/workbook.xml", limits, required: true)!)
        {
            while (r.Read())
            {
                if (r.NodeType != XmlNodeType.Element) continue;
                if (r.LocalName == "workbookPr")
                {
                    var d = r.GetAttribute("date1904");
                    date1904 = d is "1" or "true";
                }
                else if (r.LocalName == "sheet")
                {
                    var name = r.GetAttribute("name");
                    var id = r.GetAttribute("id", RelNs);
                    if (name is not null && id is not null) sheets.Add(new SheetRef(name, id));
                }
            }
        }

        var targets = new Dictionary<string, string>();
        using (var r = OpenPart(archive, "xl/_rels/workbook.xml.rels", limits, required: true)!)
        {
            while (r.Read())
            {
                if (r.NodeType != XmlNodeType.Element || r.LocalName != "Relationship") continue;
                var id = r.GetAttribute("Id");
                var target = r.GetAttribute("Target");
                if (id is not null && target is not null) targets[id] = ResolveTarget(target);
            }
        }
        return new Workbook(sheets, date1904, targets);
    }

    private static string ResolveTarget(string target)
    {
        var parts = new List<string>();
        var raw = target.Replace('\\', '/');
        if (!raw.StartsWith('/')) parts.Add("xl");
        foreach (var seg in raw.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (seg == ".") continue;
            if (seg == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
            else parts.Add(seg);
        }
        return string.Join('/', parts);
    }

    private static string TextOf(XElement el) =>
        string.Concat(el.Descendants().Where(d => d.Name.LocalName == "t"
                                                  && !d.Ancestors().Any(a => a.Name.LocalName == "rPh")).Select(t => t.Value));

    private static List<string> ReadSharedStrings(ZipArchive archive, ImportLimitSet limits)
    {
        var list = new List<string>();
        using var r = OpenPart(archive, "xl/sharedStrings.xml", limits, required: false);
        if (r is null) return list;
        r.MoveToContent();
        while (!r.EOF)
        {
            if (r.NodeType == XmlNodeType.Element && r.LocalName == "si")
                list.Add(TextOf((XElement)XNode.ReadFrom(r)));
            else
                r.Read();
        }
        return list;
    }

    private static bool[] ReadDateStyles(ZipArchive archive, ImportLimitSet limits)
    {
        using var r = OpenPart(archive, "xl/styles.xml", limits, required: false);
        if (r is null) return [];
        var formats = new Dictionary<int, string>();
        var xfIds = new List<int>();
        var inXfs = false;
        while (r.Read())
        {
            if (r.NodeType == XmlNodeType.Element)
            {
                switch (r.LocalName)
                {
                    case "numFmt":
                        if (int.TryParse(r.GetAttribute("numFmtId"), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                            && r.GetAttribute("formatCode") is { } code)
                            formats[id] = code;
                        break;
                    case "cellXfs":
                        inXfs = !r.IsEmptyElement;
                        break;
                    case "xf" when inXfs:
                        xfIds.Add(int.TryParse(r.GetAttribute("numFmtId"), NumberStyles.None, CultureInfo.InvariantCulture, out var f) ? f : 0);
                        break;
                }
            }
            else if (r.NodeType == XmlNodeType.EndElement && r.LocalName == "cellXfs")
                inXfs = false;
        }
        return xfIds.Select(id => IsDateFormat(id, formats.GetValueOrDefault(id))).ToArray();
    }

    /// <summary>¿El formato numérico (id integrado o código propio) representa una fecha u hora?</summary>
    internal static bool IsDateFormat(int numFmtId, string? formatCode)
    {
        if (numFmtId is >= 14 and <= 22 or >= 45 and <= 47) return true;
        return formatCode is not null && IsDateFormatCode(formatCode);
    }

    internal static bool IsDateFormatCode(string code)
    {
        for (var i = 0; i < code.Length; i++)
        {
            var c = code[i];
            switch (c)
            {
                case '"':
                    i = code.IndexOf('"', i + 1);
                    if (i < 0) return false;
                    break;
                case '\\' or '_' or '*':
                    i++;
                    break;
                case '[':
                    var end = code.IndexOf(']', i + 1);
                    if (end < 0) return false;
                    var inner = code.Substring(i + 1, end - i - 1);
                    if (inner.ToLowerInvariant() is "h" or "hh" or "m" or "mm" or "s" or "ss") return true;
                    i = end;
                    break;
                default:
                    if (char.ToLowerInvariant(c) is 'd' or 'm' or 'y' or 'h' or 's') return true;
                    break;
            }
        }
        return false;
    }

    private static SheetData ReadSheet(ZipArchive archive, string path, string name, List<string> shared,
        bool[] dateStyles, bool date1904, ImportLimitSet limits)
    {
        var rows = new List<List<CellValue>?>();
        using var r = OpenPart(archive, path, limits, required: true)!;
        var rowNum = 0;
        var colNum = -1;
        while (r.Read())
        {
            if (r.NodeType != XmlNodeType.Element) continue;
            if (r.LocalName == "row")
            {
                rowNum = int.TryParse(r.GetAttribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out var rn) ? rn : rowNum + 1;
                colNum = -1;
            }
            else if (r.LocalName == "c")
            {
                var col = ParseColumn(r.GetAttribute("r")) ?? colNum + 1;
                colNum = col;
                var type = r.GetAttribute("t");
                var style = int.TryParse(r.GetAttribute("s"), NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? s : 0;
                string? v = null;
                XElement? inline = null;
                using (var sub = r.ReadSubtree())
                {
                    sub.Read();
                    while (!sub.EOF)
                    {
                        if (sub.NodeType == XmlNodeType.Element && sub.LocalName == "v") v = sub.ReadElementContentAsString();
                        else if (sub.NodeType == XmlNodeType.Element && sub.LocalName == "is") inline = (XElement)XNode.ReadFrom(sub);
                        else sub.Read();
                    }
                }

                var value = ToValue(type, v, inline, style, shared, dateStyles, date1904);
                if (value.IsEmpty) continue;
                if (col >= limits.MaxColumns) throw ImportIO.TooManyColumns(limits);
                if (rowNum < 1 || rowNum > limits.MaxRows) throw ImportIO.TooManyRows(limits);

                while (rows.Count < rowNum) rows.Add(null);
                var row = rows[rowNum - 1] ??= [];
                while (row.Count <= col) row.Add(CellValue.Empty);
                row[col] = value;
            }
        }
        return SheetData.Normalize(name, rows);
    }

    private static int? ParseColumn(string? reference)
    {
        if (string.IsNullOrEmpty(reference)) return null;
        const int TooBig = 16384; // más allá de XFD: columna inválida, cae en el límite de columnas
        var n = 0;
        var i = 0;
        for (; i < reference.Length && char.IsAsciiLetter(reference[i]); i++)
        {
            if (i >= 3) return TooBig;
            n = n * 26 + (char.ToUpperInvariant(reference[i]) - 'A' + 1);
        }
        if (i == 0) return null;
        return n - 1 > 16383 ? TooBig : n - 1;
    }

    private static CellValue ToValue(string? type, string? v, XElement? inline, int style, List<string> shared,
        bool[] dateStyles, bool date1904)
    {
        switch (type)
        {
            case "inlineStr":
                return CellValue.FromText(inline is null ? v : TextOf(inline));
            case "s":
                return int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var idx) && idx < shared.Count
                    ? CellValue.FromText(shared[idx])
                    : CellValue.Empty;
            case "str":
                return CellValue.FromText(v);
            case "b":
                return v is null ? CellValue.Empty : CellValue.FromBoolean(v is "1" or "true");
            case "e":
                return CellValue.Empty;
        }

        if (string.IsNullOrWhiteSpace(v)
            || !decimal.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return CellValue.Empty;

        if (style >= 0 && style < dateStyles.Length && dateStyles[style] && number is >= 1m and <= 2958465m)
            return FromSerial(number, date1904) is { } date ? CellValue.FromDate(date) : CellValue.FromNumber(number);
        return CellValue.FromNumber(number);
    }

    /// <summary>Serial de Excel a fecha. Base 1900 con el error del 29-feb-1900: el serial 60 (día inexistente) se lleva al 28-feb.</summary>
    internal static DateTime? FromSerial(decimal serial, bool date1904)
    {
        var days = (int)Math.Floor(serial);
        var seconds = (double)Math.Round((serial - days) * 86400m);
        DateTime date;
        if (date1904) date = new DateTime(1904, 1, 1).AddDays(days);
        else if (days >= 61) date = new DateTime(1899, 12, 30).AddDays(days);
        else if (days == 60) date = new DateTime(1900, 2, 28);
        else date = new DateTime(1899, 12, 31).AddDays(days);
        try { return date.AddSeconds(seconds); }
        catch (ArgumentOutOfRangeException) { return null; }
    }
}
