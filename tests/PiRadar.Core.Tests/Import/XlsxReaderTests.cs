using PiRadar.Core.Domain;
using PiRadar.Core.Import;

namespace PiRadar.Core.Tests.Import;

public class XlsxReaderTests
{
    private const string AccountingFmt = """_-* #,##0_-;\-* #,##0_-;_-* \-??_-;_-@_-""";

    private static SheetData Read(byte[] file, string? sheet = null) => XlsxReader.Read(new MemoryStream(file), sheet);

    private static SheetData ReadSingle(string rows, string? sst = null, string? styles = null, bool date1904 = false) =>
        Read(XlsxBuilder.Build(XlsxBuilder.Sheet(rows), sst, styles, date1904));

    // Estilos: 0 General, 1 = 14, 2 = 164 dd/mm/yyyy, 3 = 165 dd/mm/yy, 4 = 167 contable, 5 = 43, 6 = 22 (fecha y hora)
    private static readonly string FullStyles = XlsxBuilder.Styles(
        [(164, "dd/mm/yyyy"), (165, "dd/mm/yy"), (167, AccountingFmt)], 0, 14, 164, 165, 167, 43, 22);

    [Fact]
    public void MonthFixture_ReadsDatesAmountsAndEmptyColumns()
    {
        var sst = XlsxBuilder.SharedStrings(
            "<si><t>Descripción</t></si>", "<si><t>Fecha</t></si>", "<si><t>Ingreso</t></si>", "<si><t>Gasto</t></si>",
            "<si><t>Categoria</t></si>", "<si><t>Medio de pago</t></si>", "<si><t>Moneda</t></si>",
            "<si><t>Mercado </t></si>", "<si><t>Educación</t></si>", "<si><t>Salario</t></si>");
        var rows =
            """
            <row r="1"><c r="A1" t="s"><v>0</v></c><c r="B1" t="s"><v>1</v></c><c r="C1" t="s"><v>2</v></c><c r="D1" t="s"><v>3</v></c><c r="E1" t="s"><v>4</v></c><c r="F1" t="s"><v>5</v></c><c r="G1" t="s"><v>6</v></c></row>
            <row r="2"><c r="A2" t="s"><v>9</v></c><c r="B2" s="1"><v>46235</v></c><c r="C2" s="4"><v>3456789.3499999996</v></c><c r="E2" t="s"><v>9</v></c></row>
            <row r="3"><c r="A3" t="s"><v>7</v></c><c r="B3" s="2"><v>46250</v></c><c r="D3" s="5"><v>45000.5</v></c><c r="E3" t="s"><v>8</v></c></row>
            <row r="4"><c r="A4" t="s"><v>7</v></c><c r="B4" s="3"><v>46265</v></c><c r="D4" s="4"><f>1000.35+200</f><v>1200.3499999999999</v></c></row>
            <row r="5"><c r="A5" s="4"/><c r="B5" s="1"/></row>
            <row r="6"><c r="C6" s="4"/></row>
            """;
        var sheet = ReadSingle(rows, sst, FullStyles);

        Assert.Equal(7, sheet.ColumnCount);
        Assert.Equal(4, sheet.Rows.Count); // filas 5 y 6 recortadas
        Assert.All(sheet.Rows, r => Assert.Equal(7, r.Count));
        Assert.Equal("Descripción", sheet.Rows[0][0].Text);
        Assert.Equal("Mercado ", sheet.Rows[2][0].Text); // el espacio final se conserva
        Assert.Equal("Educación", sheet.Rows[2][4].Text);
        Assert.Equal(new DateTime(2026, 8, 1), sheet.Rows[1][1].Date);
        Assert.Equal(new DateTime(2026, 8, 16), sheet.Rows[2][1].Date);
        Assert.Equal(new DateTime(2026, 8, 31), sheet.Rows[3][1].Date);
        Assert.Equal(CellKind.Date, sheet.Rows[1][1].Kind);
        Assert.Equal(3456789.3499999996m, sheet.Rows[1][2].Number);
        Assert.Equal(45000.5m, sheet.Rows[2][3].Number);
        Assert.Equal(1200.3499999999999m, sheet.Rows[3][3].Number);
        Assert.True(sheet.Rows[1][3].IsEmpty);
        Assert.All(sheet.Rows, r => { Assert.True(r[5].IsEmpty || r == sheet.Rows[0]); });
        Assert.True(sheet.Rows[2][5].IsEmpty);
        Assert.True(sheet.Rows[3][6].IsEmpty);
    }

    [Theory]
    [InlineData("60", 1900, 2, 28)]
    [InlineData("61", 1900, 3, 1)]
    [InlineData("1", 1900, 1, 1)]
    [InlineData("59", 1900, 2, 28)]
    public void Serial1900_HandlesLeapYearBug(string serial, int y, int m, int d)
    {
        var sheet = ReadSingle($"""<row r="1"><c r="A1" s="1"><v>{serial}</v></c></row>""", null, FullStyles);
        Assert.Equal(new DateTime(y, m, d), sheet.Rows[0][0].Date);
    }

    [Fact]
    public void Serial_WithTime_RoundsToSecond()
    {
        var sheet = ReadSingle("""<row r="1"><c r="A1" s="6"><v>46235.5</v></c><c r="B1" s="6"><v>46235.999999</v></c></row>""", null, FullStyles);
        Assert.Equal(new DateTime(2026, 8, 1, 12, 0, 0), sheet.Rows[0][0].Date);
        Assert.Equal(new DateTime(2026, 8, 2), sheet.Rows[0][1].Date);
        Assert.Equal("2026-08-01 12:00:00", sheet.Rows[0][0].ToDisplayString());
    }

    [Fact]
    public void Date1904_UsesOtherBase()
    {
        var sheet = ReadSingle("""<row r="1"><c r="A1" s="1"><v>1</v></c></row>""", null, FullStyles, date1904: true);
        Assert.Equal(new DateTime(1904, 1, 2), sheet.Rows[0][0].Date);
    }

    [Fact]
    public void SerialOutOfRange_StaysNumber()
    {
        var sheet = ReadSingle("""<row r="1"><c r="A1" s="1"><v>0.5</v></c><c r="B1" s="1"><v>99999999</v></c></row>""", null, FullStyles);
        Assert.Equal(CellKind.Number, sheet.Rows[0][0].Kind);
        Assert.Equal(CellKind.Number, sheet.Rows[0][1].Kind);
    }

    [Fact]
    public void CellTypes_InlineStr_Str_Boolean_Error()
    {
        var sheet = ReadSingle(
            """
            <row r="1"><c r="A1" t="inlineStr"><is><t>hola</t></is></c><c r="B1" t="str"><v>texto</v></c><c r="C1" t="b"><v>1</v></c><c r="D1" t="b"><v>0</v></c><c r="E1" t="e"><v>#DIV/0!</v></c><c r="F1" t="inlineStr"><is><r><t>ri</t></r><r><t>ch</t></r></is></c></row>
            """);
        var r = sheet.Rows[0];
        Assert.Equal("hola", r[0].Text);
        Assert.Equal("texto", r[1].Text);
        Assert.Equal(true, r[2].Boolean);
        Assert.Equal(false, r[3].Boolean);
        Assert.True(r[4].IsEmpty);
        Assert.Equal("rich", r[5].Text);
    }

    [Fact]
    public void SharedString_RichTextIgnoresPhonetic()
    {
        var sst = XlsxBuilder.SharedStrings("<si><r><t>Ca</t></r><r><t xml:space=\"preserve\">fé </t></r><rPh sb=\"0\" eb=\"1\"><t>カフェ</t></rPh></si>");
        var sheet = ReadSingle("""<row r="1"><c r="A1" t="s"><v>0</v></c></row>""", sst);
        Assert.Equal("Café ", sheet.Rows[0][0].Text);
    }

    [Fact]
    public void Cells_WithoutReferences_AdvanceSequentially()
    {
        var sheet = ReadSingle("""<row><c t="inlineStr"><is><t>a</t></is></c><c t="inlineStr"><is><t>b</t></is></c></row><row><c><v>3</v></c></row>""");
        Assert.Equal("a", sheet.Rows[0][0].Text);
        Assert.Equal("b", sheet.Rows[0][1].Text);
        Assert.Equal(3m, sheet.Rows[1][0].Number);
    }

    [Fact]
    public void SkippedCells_AndRows_KeepPositions()
    {
        var sheet = ReadSingle("""<row r="2"><c r="A2"><v>1</v></c><c r="C2"><v>2</v></c></row><row r="4"><c r="AA4"><v>9</v></c></row>""");
        Assert.Equal(4, sheet.Rows.Count);
        Assert.Equal(27, sheet.ColumnCount);
        Assert.True(sheet.Rows[0].All(c => c.IsEmpty)); // fila 1 vacía intermedia conservada
        Assert.Equal(1m, sheet.Rows[1][0].Number);
        Assert.True(sheet.Rows[1][1].IsEmpty);
        Assert.Equal(2m, sheet.Rows[1][2].Number);
        Assert.Equal(9m, sheet.Rows[3][26].Number);
    }

    [Fact]
    public void Numbers_ExponentialAndLarge()
    {
        var sheet = ReadSingle("""<row r="1"><c r="A1"><v>1.5E+3</v></c><c r="B1"><v>123456789012345</v></c><c r="C1"><v>1E+300</v></c><c r="D1"><v>-0.1</v></c></row>""");
        Assert.Equal(1500m, sheet.Rows[0][0].Number);
        Assert.Equal(123456789012345m, sheet.Rows[0][1].Number);
        Assert.True(sheet.Rows[0][2].IsEmpty);
        Assert.Equal(-0.1m, sheet.Rows[0][3].Number);
    }

    [Fact]
    public void Formula_WithoutCachedValue_IsEmpty()
    {
        var sheet = ReadSingle("""<row r="1"><c r="A1"><f>1+1</f></c><c r="B1"><v>5</v></c></row>""");
        Assert.True(sheet.Rows[0][0].IsEmpty);
        Assert.Equal(5m, sheet.Rows[0][1].Number);
    }

    [Fact]
    public void AbsoluteTargets_AndMultipleSheets()
    {
        var file = XlsxBuilder.Build(
            [("Uno", XlsxBuilder.Sheet("""<row r="1"><c r="A1"><v>1</v></c></row>""")),
             ("Dos", XlsxBuilder.Sheet("""<row r="1"><c r="A1"><v>2</v></c></row>"""))],
            absoluteTargets: true);

        Assert.Equal(["Uno", "Dos"], XlsxReader.GetSheetNames(new MemoryStream(file)));
        Assert.Equal(1m, Read(file).Rows[0][0].Number);
        var second = Read(file, "Dos");
        Assert.Equal("Dos", second.Name);
        Assert.Equal(2m, second.Rows[0][0].Number);
    }

    [Fact]
    public void MissingSheet_Throws()
    {
        var file = XlsxBuilder.Build(XlsxBuilder.Sheet(""));
        var ex = Assert.Throws<ImportFormatException>(() => Read(file, "Nada"));
        Assert.Contains("Nada", ex.Message);
    }

    [Fact]
    public void InvalidZip_AndMissingWorkbook_Throw()
    {
        var bad = Assert.Throws<ImportFormatException>(() => Read([1, 2, 3, 4, 5, 6, 7, 8]));
        Assert.Equal("El archivo no es un Excel (.xlsx) válido.", bad.Message);
        Assert.Throws<ImportFormatException>(() => Read(XlsxBuilder.Zip(("otro.txt", "x"))));
        Assert.Throws<ImportFormatException>(() => Read([]));
    }

    [Fact]
    public void OldXls_HasSpecificMessage()
    {
        var ex = Assert.Throws<ImportFormatException>(() => Read([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]));
        Assert.Contains(".xls", ex.Message);
        using var _ = AppLocale.Use(AppLanguage.English);
        var en = Assert.Throws<ImportFormatException>(() => Read([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]));
        Assert.Equal("This is an old Excel file (.xls). Open it in Excel and save it as .xlsx.", en.Message);
    }

    [Fact]
    public void English_Messages()
    {
        using var _ = AppLocale.Use(AppLanguage.English);
        var ex = Assert.Throws<ImportFormatException>(() => Read([1, 2, 3, 4, 5, 6, 7, 8]));
        Assert.Equal("This file is not a valid Excel (.xlsx) file.", ex.Message);
    }

    [Fact]
    public void Dtd_IsRejected()
    {
        var evil = """<?xml version="1.0"?><!DOCTYPE x [<!ENTITY a "boom">]><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData/></worksheet>""";
        Assert.Throws<ImportFormatException>(() => Read(XlsxBuilder.Build(evil)));
    }

    [Fact]
    public void DecompressionBomb_IsRejected()
    {
        var big = XlsxBuilder.Sheet(string.Concat(Enumerable.Repeat("""<row r="1"><c r="A1"><v>1</v></c></row>""", 5_000)));
        var file = XlsxBuilder.Build(big);
        var limits = ImportLimitSet.Default with { MaxPartUncompressedBytes = 10_000 };
        Assert.Throws<ImportFormatException>(() => XlsxReader.Read(new MemoryStream(file), null, limits));
    }

    [Fact]
    public void FileTooLarge_IsRejected_SeekableOrNot()
    {
        var file = XlsxBuilder.Build(XlsxBuilder.Sheet(""));
        var limits = ImportLimitSet.Default with { MaxFileBytes = 100 };
        Assert.Throws<ImportFormatException>(() => XlsxReader.Read(new MemoryStream(file), null, limits));
        Assert.Throws<ImportFormatException>(() => XlsxReader.Read(new ForwardOnlyStream(file), null, limits));
    }

    [Fact]
    public void TooManyRows_AndColumns_Throw()
    {
        var limits = ImportLimitSet.Default with { MaxDataRows = 2, MaxColumns = 3 };
        var rows = string.Concat(Enumerable.Range(1, 60).Select(i => $"""<row r="{i}"><c r="A{i}"><v>1</v></c></row>"""));
        Assert.Throws<ImportFormatException>(() => XlsxReader.Read(new MemoryStream(XlsxBuilder.Build(XlsxBuilder.Sheet(rows))), null, limits));

        var cols = XlsxBuilder.Build(XlsxBuilder.Sheet("""<row r="1"><c r="A1"><v>1</v></c><c r="D1"><v>1</v></c></row>"""));
        Assert.Throws<ImportFormatException>(() => XlsxReader.Read(new MemoryStream(cols), null, limits));

        // Celdas vacías con estilo más allá del límite no cuentan.
        var ok = XlsxBuilder.Build(XlsxBuilder.Sheet("""<row r="1"><c r="A1"><v>1</v></c><c r="Z1" s="1"/></row><row r="9999"><c r="A9999" s="1"/></row>"""));
        Assert.Single(XlsxReader.Read(new MemoryStream(ok), null, limits).Rows);
    }

    [Fact]
    public void HugeColumnReference_ThrowsColumnsError()
    {
        var file = XlsxBuilder.Build(XlsxBuilder.Sheet("<row r=\"1\"><c r=\"ZZZZZZZZZ1\"><v>1</v></c></row>"));
        var ex = Assert.Throws<ImportFormatException>(() => Read(file));
        Assert.Contains("columnas", ex.Message);
        var empty = XlsxBuilder.Build(XlsxBuilder.Sheet("<row r=\"1\"><c r=\"ZZZZZZZZZ1\" s=\"1\"/><c r=\"A1\"><v>1</v></c></row>"));
        Assert.Single(Read(empty).Rows);
    }

    [Fact]
    public void SerialAtMaxDate_NeverThrows()
    {
        var near = ReadSingle("<row r=\"1\"><c r=\"A1\" s=\"1\"><v>2958465</v></c></row>", null, FullStyles);
        Assert.Equal(CellKind.Date, near.Rows[0][0].Kind);
        var over = ReadSingle("<row r=\"1\"><c r=\"A1\" s=\"1\"><v>2958465.9999</v></c></row>", null, FullStyles);
        Assert.Equal(CellKind.Number, over.Rows[0][0].Kind);
    }

    [Theory]
    [InlineData(14, null, true)]
    [InlineData(22, null, true)]
    [InlineData(45, null, true)]
    [InlineData(43, null, false)]
    [InlineData(44, null, false)]
    [InlineData(0, null, false)]
    [InlineData(167, AccountingFmt, false)]
    [InlineData(164, "dd/mm/yyyy", true)]
    [InlineData(164, "d-mmm-yy", true)]
    [InlineData(164, "[$-409]m/d/yy h:mm AM/PM;@", true)]
    [InlineData(164, "yyyy-mm-dd hh:mm", true)]
    [InlineData(164, "[h]:mm:ss", true)]
    [InlineData(164, "\"Día\" 0", false)]
    [InlineData(164, "0.00", false)]
    [InlineData(164, "General", false)]
    [InlineData(164, "[Red]0.00", false)]
    [InlineData(164, "\\d0", false)]
    [InlineData(164, "$#,##0.00_);[Red]($#,##0.00)", false)]
    public void DateFormatDetection(int id, string? code, bool expected) =>
        Assert.Equal(expected, XlsxReader.IsDateFormat(id, code));

    private sealed class ForwardOnlyStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }


    [Fact]
    public void TooManyCells_ThrowsWhileReading()
    {
        var limits = ImportLimitSet.Default with { MaxCells = 20 };
        var rows = string.Concat(Enumerable.Range(1, 10).Select(i =>
            $"""<row r="{i}"><c r="A{i}"><v>1</v></c><c r="D{i}"><v>1</v></c></row>"""));
        var ex = Assert.Throws<ImportFormatException>(() =>
            XlsxReader.Read(new MemoryStream(XlsxBuilder.Build(XlsxBuilder.Sheet(rows))), null, limits));
        Assert.Contains("celdas", ex.Message);
        using var _ = AppLocale.Use(AppLanguage.English);
        Assert.Contains("too many cells", Assert.Throws<ImportFormatException>(() =>
            XlsxReader.Read(new MemoryStream(XlsxBuilder.Build(XlsxBuilder.Sheet(rows))), null, limits)).Message);
    }

    [Fact]
    public void RealWorldShape_50000Rows_12Columns_IsAccepted()
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 1; i <= 50_000; i++)
        {
            sb.Append($"<row r=\"{i}\">");
            for (var c = 0; c < 12; c++) sb.Append($"<c r=\"{SheetData.ColumnLetter(c)}{i}\"><v>1</v></c>");
            sb.Append("</row>");
        }
        var sheet = Read(XlsxBuilder.Build(XlsxBuilder.Sheet(sb.ToString())));
        Assert.Equal(50_000, sheet.Rows.Count);
        Assert.Equal(12, sheet.ColumnCount);
    }

    [Fact]
    public void TooManySharedStrings_AndChars_Throw()
    {
        var sst = XlsxBuilder.SharedStrings("<si><t>a</t></si>", "<si><t>b</t></si>", "<si><t>c</t></si>");
        var file = XlsxBuilder.Build(XlsxBuilder.Sheet("""<row r="1"><c r="A1" t="s"><v>0</v></c></row>"""), sst);
        Assert.Throws<ImportFormatException>(() =>
            XlsxReader.Read(new MemoryStream(file), null, ImportLimitSet.Default with { MaxSharedStrings = 2 }));
        Assert.Throws<ImportFormatException>(() =>
            XlsxReader.Read(new MemoryStream(file), null, ImportLimitSet.Default with { MaxSharedStringChars = 2 }));
        Assert.Single(XlsxReader.Read(new MemoryStream(file), null, ImportLimitSet.Default with { MaxSharedStrings = 3, MaxSharedStringChars = 3 }).Rows);
    }

    [Fact]
    public void OutOfRangeSerial_WithDate1904_DoesNotInvalidateTheWorkbook()
    {
        Assert.Null(XlsxReader.FromSerial(2958465m, true));
        var sheet = ReadSingle("""<row r="1"><c r="A1" s="1"><v>2958465</v></c><c r="B1"><v>5</v></c></row>""", null, FullStyles, date1904: true);
        Assert.Equal(5m, sheet.Rows[0][1].Number);
        Assert.True(sheet.Date1904);
    }
}
