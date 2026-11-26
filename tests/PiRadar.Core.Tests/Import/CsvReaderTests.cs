using System.Text;
using PiRadar.Core.Domain;
using PiRadar.Core.Import;

namespace PiRadar.Core.Tests.Import;

public class CsvReaderTests
{
    private static SheetData Read(string text, Encoding? enc = null) =>
        CsvReader.Read(new MemoryStream((enc ?? new UTF8Encoding(false)).GetBytes(text)));

    private static string[] Texts(SheetData s, int row) => s.Rows[row].Select(c => c.Text ?? "").ToArray();

    [Theory]
    [InlineData("a;b;c\n1;2;3")]
    [InlineData("a,b,c\n1,2,3")]
    [InlineData("a\tb\tc\n1\t2\t3")]
    [InlineData("a;b;c\r\n1;2;3\r\n")]
    [InlineData("a;b;c\r1;2;3\r")]
    public void DetectsSeparatorAndLineBreaks(string text)
    {
        var s = Read(text);
        Assert.Equal("CSV", s.Name);
        Assert.Equal(2, s.Rows.Count);
        Assert.Equal(3, s.ColumnCount);
        Assert.Equal(["a", "b", "c"], Texts(s, 0));
        Assert.Equal(["1", "2", "3"], Texts(s, 1));
        Assert.All(s.Rows[1], c => Assert.Equal(CellKind.Text, c.Kind));
    }

    [Fact]
    public void SeparatorTie_OrNone_UsesComma()
    {
        // empate: coma ⇒ "a;b" y "c"
        Assert.Equal(["a;b", "c"], Texts(Read("a;b,c"), 0));
        Assert.Equal(1, Read("hola\nmundo").ColumnCount);
    }

    [Fact]
    public void Quotes_EscapedQuotes_AndNewlinesInside()
    {
        var s = Read("d;e\n\"x;y\";\"di \"\"hola\"\"\"\n\"línea1\nlínea2\";fin\n");
        Assert.Equal(3, s.Rows.Count);
        Assert.Equal(["x;y", "di \"hola\""], Texts(s, 1));
        Assert.Equal(["línea1\nlínea2", "fin"], Texts(s, 2));
    }

    [Fact]
    public void QuotedSeparatorInFirstLine_DoesNotCount()
    {
        var s = Read("\"a;b;c;d\",x\n1,2\n");
        Assert.Equal(["a;b;c;d", "x"], Texts(s, 0));
    }

    [Fact]
    public void EmptyCells_AreEmpty_SpacesKept_RowsPadded_TrailingTrimmed()
    {
        var s = Read("a;b;c\n x ;;\n1\n\n2\n;\n\n");
        Assert.Equal(5, s.Rows.Count); // fila vacía intermedia conservada, finales recortadas
        Assert.Equal(3, s.ColumnCount);
        Assert.Equal(" x ", s.Rows[1][0].Text);
        Assert.True(s.Rows[1][1].IsEmpty);
        Assert.Equal("1", s.Rows[2][0].Text);
        Assert.True(s.Rows[2][2].IsEmpty);
        Assert.All(s.Rows[3], c => Assert.True(c.IsEmpty));
    }

    [Fact]
    public void Utf8Bom_IsSkipped()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("Descripción;Monto\nCafé;5")).ToArray();
        var s = CsvReader.Read(new MemoryStream(bytes));
        Assert.Equal("Descripción", s.Rows[0][0].Text);
        Assert.Equal("Café", s.Rows[1][0].Text);
    }

    [Fact]
    public void Utf8WithoutBom_IsDetected()
    {
        Assert.Equal("Educación €", Read("Educación €;1").Rows[0][0].Text);
    }

    [Fact]
    public void Windows1252_IsDecoded()
    {
        // "Educación;€ 5" en Windows-1252: ó = 0xF3, € = 0x80.
        var bytes = new byte[] { (byte)'E', (byte)'d', (byte)'u', (byte)'c', (byte)'a', (byte)'c', (byte)'i', 0xF3, (byte)'n', (byte)';', 0x80, (byte)' ', (byte)'5', 0x96, 0x93, 0x94 };
        var s = CsvReader.Read(new MemoryStream(bytes));
        Assert.Equal("Educación", s.Rows[0][0].Text);
        Assert.Equal("€ 5–“”", s.Rows[0][1].Text);
    }

    [Fact]
    public void Limits_Throw()
    {
        var limits = ImportLimitSet.Default with { MaxDataRows = 2, MaxColumns = 3, MaxFileBytes = 1000 };
        var rows = string.Join("\n", Enumerable.Range(0, 60).Select(i => "a"));
        Assert.Throws<ImportFormatException>(() => CsvReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(rows)), limits));
        Assert.Throws<ImportFormatException>(() => CsvReader.Read(new MemoryStream(Encoding.UTF8.GetBytes("a,b,c,d")), limits));
        Assert.Throws<ImportFormatException>(() => CsvReader.Read(new MemoryStream(new byte[2000]), limits));
        Assert.Equal(3, CsvReader.Read(new MemoryStream(Encoding.UTF8.GetBytes("a,b,c,,,")), limits).ColumnCount);

        using var _ = AppLocale.Use(AppLanguage.English);
        var ex = Assert.Throws<ImportFormatException>(() => CsvReader.Read(new MemoryStream(Encoding.UTF8.GetBytes("a,b,c,d")), limits));
        Assert.Contains("columns", ex.Message);
    }

    [Fact]
    public void ColumnLetter_AndDisplayString()
    {
        Assert.Equal("A", SheetData.ColumnLetter(0));
        Assert.Equal("Z", SheetData.ColumnLetter(25));
        Assert.Equal("AA", SheetData.ColumnLetter(26));
        Assert.Equal("AZ", SheetData.ColumnLetter(51));
        Assert.Equal("BA", SheetData.ColumnLetter(52));
        Assert.Equal("1200.35", CellValue.FromNumber(1200.35m).ToDisplayString());
        Assert.Equal("2026-08-01", CellValue.FromDate(new DateTime(2026, 8, 1)).ToDisplayString());
        Assert.Equal("", CellValue.Empty.ToDisplayString());
    }
}
