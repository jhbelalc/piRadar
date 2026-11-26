using System.IO.Compression;
using System.Text;

namespace PiRadar.Core.Tests.Import;

/// <summary>Arma un .xlsx en memoria a partir de XML escrito a mano para probar casos raros.</summary>
internal static class XlsxBuilder
{
    public const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static string Sheet(string rowsXml) =>
        $"""<?xml version="1.0" encoding="UTF-8"?><worksheet xmlns="{Main}"><sheetData>{rowsXml}</sheetData></worksheet>""";

    public static string SharedStrings(params string[] siXml) =>
        $"""<?xml version="1.0" encoding="UTF-8"?><sst xmlns="{Main}">{string.Concat(siXml)}</sst>""";

    /// <summary>numFmts propios (id, código) y la lista de numFmtId de cellXfs, en orden.</summary>
    public static string Styles(IEnumerable<(int Id, string Code)> numFmts, params int[] xfNumFmtIds)
    {
        var fmts = string.Concat(numFmts.Select(f => $"""<numFmt numFmtId="{f.Id}" formatCode="{System.Security.SecurityElement.Escape(f.Code)}"/>"""));
        var xfs = string.Concat(xfNumFmtIds.Select(id => $"""<xf numFmtId="{id}" fontId="0" fillId="0" borderId="0"/>"""));
        return $"""<?xml version="1.0" encoding="UTF-8"?><styleSheet xmlns="{Main}"><numFmts count="1">{fmts}</numFmts><cellStyleXfs count="1"><xf numFmtId="14"/></cellStyleXfs><cellXfs count="{xfNumFmtIds.Length}">{xfs}</cellXfs></styleSheet>""";
    }

    public static byte[] Build(string sheetXml, string? sharedStrings = null, string? styles = null, bool date1904 = false) =>
        Build([("Hoja1", sheetXml)], sharedStrings, styles, date1904);

    public static byte[] Build(IReadOnlyList<(string Name, string Xml)> sheets, string? sharedStrings = null,
        string? styles = null, bool date1904 = false, bool absoluteTargets = false)
    {
        var sheetTags = string.Concat(sheets.Select((s, i) => $"""<sheet name="{s.Name}" sheetId="{i + 1}" r:id="rId{i + 1}"/>"""));
        var workbook = $"""<?xml version="1.0"?><workbook xmlns="{Main}" xmlns:r="{Rel}"><workbookPr{(date1904 ? " date1904=\"1\"" : "")}/><sheets>{sheetTags}</sheets></workbook>""";
        var relTags = string.Concat(sheets.Select((_, i) =>
            $"""<Relationship Id="rId{i + 1}" Type="x" Target="{(absoluteTargets ? "/xl/" : "")}worksheets/sheet{i + 1}.xml"/>"""));
        var rels = $"""<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">{relTags}</Relationships>""";

        var parts = new List<(string, string)> { ("xl/workbook.xml", workbook), ("xl/_rels/workbook.xml.rels", rels) };
        for (var i = 0; i < sheets.Count; i++) parts.Add(($"xl/worksheets/sheet{i + 1}.xml", sheets[i].Xml));
        if (sharedStrings is not null) parts.Add(("xl/sharedStrings.xml", sharedStrings));
        if (styles is not null) parts.Add(("xl/styles.xml", styles));
        return Zip(parts.ToArray());
    }

    public static byte[] Zip(params (string Path, string Content)[] parts)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (path, content) in parts)
            {
                using var w = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
                w.Write(content);
            }
        return ms.ToArray();
    }
}
