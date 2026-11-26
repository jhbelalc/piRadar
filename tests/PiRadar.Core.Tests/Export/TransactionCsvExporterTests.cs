using System.Text;
using PiRadar.Core.Domain;
using PiRadar.Core.Export;

namespace PiRadar.Core.Tests.Export;

public class TransactionCsvExporterTests
{
    private static readonly Category Food = new() { Id = 1, Name = "Comida", Icon = "x", Color = "#000000", Kind = TransactionType.Expense };
    private static readonly Category Cafe = new() { Id = 2, Name = "Café", Icon = "x", Color = "#000000", Kind = TransactionType.Expense, ParentId = 1 };
    private static readonly Category Old = new() { Id = 3, Name = "Antigua", Icon = "x", Color = "#000000", Kind = TransactionType.Expense, IsArchived = true };
    private static readonly Category[] Categories = [Food, Cafe, Old];
    private static readonly PaymentMethod Card = new() { Id = 7, Name = "Tarjeta azul", Icon = "x", Kind = PaymentMethodKind.CreditCard };
    private static readonly PaymentMethod[] Methods = [Card];

    private static Transaction Tx(long minor = 1000, Currency cur = Currency.COP, int category = 1, int? method = null,
        TransactionType type = TransactionType.Expense, string? merchant = null, string? description = null, string? notes = null,
        DateTime? at = null) => new()
    {
        Type = type,
        Amount = new Money(minor, cur),
        OccurredAt = at ?? new DateTime(2026, 3, 5, 14, 7, 0, DateTimeKind.Local),
        CategoryId = category,
        PaymentMethodId = method,
        Merchant = merchant,
        Description = description,
        Notes = notes,
    };

    private static byte[] Export(params Transaction[] txs)
    {
        using var ms = new MemoryStream();
        TransactionCsvExporter.Write(ms, txs, Categories, Methods);
        return ms.ToArray();
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

    [Fact]
    public void Spanish_exact_bytes()
    {
        var bytes = Export(
            Tx(1234567, Currency.COP, 2, 7, merchant: "Tienda", description: "Compra", notes: "ok"),
            Tx(1250, Currency.USD, 3, type: TransactionType.Income, at: new DateTime(2026, 3, 6, 0, 5, 0)));

        const string expected =
            "Fecha;Hora;Tipo;Monto;Moneda;Categoría;Subcategoría;Medio de pago;Comercio;Descripción;Notas\r\n" +
            "2026-03-05;14:07;Gasto;1234567;COP;Comida;Café;Tarjeta azul;Tienda;Compra;ok\r\n" +
            "2026-03-06;00:05;Ingreso;12,50;USD;Antigua;;;;;\r\n";
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Equal(new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(expected)).ToArray(), bytes);
    }

    [Fact]
    public void English_format()
    {
        using var _ = AppLocale.Use(AppLanguage.English);
        var text = Text(Export(Tx(1250, Currency.USD, 1, type: TransactionType.Income, at: new DateTime(2026, 3, 5, 18, 30, 0))));

        Assert.Equal(
            "Date,Time,Type,Amount,Currency,Category,Subcategory,Payment method,Merchant,Description,Notes\r\n" +
            "2026-03-05,18:30,Income,12.50,USD,Comida,,,,,\r\n", text);
    }

    [Fact]
    public void Quotes_when_needed()
    {
        var text = Text(Export(Tx(merchant: "A;B", description: "dice \"hola\"", notes: "uno\r\ndos")));
        Assert.Contains(";\"A;B\";\"dice \"\"hola\"\"\";\"uno\r\ndos\"\r\n", text);

        var lead = Text(Export(Tx(description: " espacio", notes: "fin ")));
        Assert.EndsWith(";\" espacio\";\"fin \"\r\n", lead);

        using var _ = AppLocale.Use(AppLanguage.English);
        var comma = Text(Export(Tx(description: "a,b", notes: "a;b")));
        Assert.EndsWith(",\"a,b\",a;b\r\n", comma);
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+57", "'+57")]
    [InlineData("-x", "'-x")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("seguro=", "seguro=")]
    public void Formula_injection_is_prefixed(string value, string expected)
    {
        var text = Text(Export(Tx(merchant: value)));
        Assert.Contains(";" + expected + ";;", text);
    }

    [Fact]
    public void Tab_and_cr_prefixed_and_quoted()
    {
        var text = Text(Export(Tx(description: "\tx", notes: "\rx")));
        Assert.EndsWith(";'\tx;\"'\rx\"\r\n", text);
    }

    [Fact]
    public void Unknown_category_and_missing_method_are_empty_and_rows_sorted()
    {
        var text = Text(Export(
            Tx(500, category: 99, at: new DateTime(2026, 5, 1, 10, 0, 0)),
            Tx(100, at: new DateTime(2026, 1, 1, 9, 0, 0))));
        var lines = text.Split("\r\n");
        Assert.Equal("2026-01-01;09:00;Gasto;100;COP;Comida;;;;;", lines[1]);
        Assert.Equal("2026-05-01;10:00;Gasto;500;COP;;;;;;", lines[2]);
        Assert.Equal("", lines[3]);
    }

    [Fact]
    public void Zero_rows_writes_header_only()
    {
        var text = Text(Export());
        Assert.Equal("Fecha;Hora;Tipo;Monto;Moneda;Categoría;Subcategoría;Medio de pago;Comercio;Descripción;Notas\r\n", text);
    }

    [Fact]
    public void Does_not_close_the_stream_and_handles_many_rows()
    {
        var txs = Enumerable.Range(0, 50_000).Select(i => Tx(i + 1, at: new DateTime(2026, 1, 1).AddMinutes(i)));
        using var ms = new MemoryStream();
        TransactionCsvExporter.Write(ms, txs, Categories, Methods);
        Assert.True(ms.CanWrite);
        Assert.Equal(50_001, Text(ms.ToArray()).Split("\r\n").Length - 1);
    }

    [Fact]
    public void File_name_in_both_languages()
    {
        var now = new DateTime(2026, 3, 5, 14, 7, 0);
        Assert.Equal("piradar-movimientos-2026-03-05-1407.csv", TransactionCsvExporter.FileName(now));
        using var _ = AppLocale.Use(AppLanguage.English);
        Assert.Equal("piradar-transactions-2026-03-05-1407.csv", TransactionCsvExporter.FileName(now));
    }
}
