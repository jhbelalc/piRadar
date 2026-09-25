using PiRadar.Core.Receipts;

namespace PiRadar.Core.Tests.Receipts;

public class DateParserTests
{
    [Theory]
    [InlineData("FECHA: 14/09/2026 HORA: 18:42", 2026, 9, 14)]
    [InlineData("2026-09-21 17:36:41.045 0056 044 0138", 2026, 9, 21)]
    [InlineData("F.Expedicion:2026-09-2117:36:41.045", 2026, 9, 21)]
    [InlineData("Fecha : 2026/9/6 Hora: 17:34:36", 2026, 9, 6)]
    [InlineData("20/09/26 18:28", 2026, 9, 20)]
    [InlineData("21/SEP/2026 17:35 0056 44 0138", 2026, 9, 21)]
    [InlineData("24/9/2026 18:47", 2026, 9, 24)]
    [InlineData("9/16/2026 16:38 6102 008 0220", 2026, 9, 16)]
    [InlineData("Your order of September 11, 2026 (Order ID 112-8735779)", 2026, 9, 11)]
    [InlineData("RESOLUCION No. 0002968 DEL 5 DE ABRIL DE 2010", 2010, 4, 5)]
    [InlineData("Resolución 000200 27 DIC 2024", 2024, 12, 27)]
    [InlineData("Diciembre 07 de 2026", 2026, 12, 7)]
    [InlineData("Fecha genera: 20/U9/2026 Hora 18:47:25", 2026, 9, 20)]
    public void FindDates_ReadsCommonFormats(string row, int year, int month, int day)
    {
        var dates = DateParser.FindDates(row);

        Assert.Equal(new DateOnly(year, month, day), Assert.Single(dates).Date);
    }

    [Fact]
    public void FindDates_PrefersDayFirst_UnlessMonthFirstRequested()
    {
        Assert.Equal(new DateOnly(2026, 9, 6), DateParser.FindDates("06/09/2026")[0].Date);
        Assert.Equal(new DateOnly(2026, 6, 9), DateParser.FindDates("06/09/2026", preferMonthFirst: true)[0].Date);
    }

    [Fact]
    public void FindDates_FindsSeveralInOneRow()
    {
        var dates = DateParser.FindDates("54538 A 2000000 de 16/02/2026-16/02/2027");

        Assert.Equal([new DateOnly(2026, 2, 16), new DateOnly(2027, 2, 16)], dates.Select(d => d.Date));
    }

    [Theory]
    [InlineData("32/01/2026")]
    [InlineData("13/13/2026")]
    [InlineData("TEL 6017458617")]
    public void FindDates_IgnoresInvalid(string row)
    {
        Assert.Empty(DateParser.FindDates(row));
    }

    [Theory]
    [InlineData("HORA: 18:42", 18, 42, 0)]
    [InlineData("Fecha expedición: 16/09/2026 5:22:42 p.m.", 17, 22, 42)]
    [InlineData("18/9/2026, 12:55:13 p. m.", 12, 55, 13)]
    [InlineData("HORA DE GENERACION:04:07:18 PM", 16, 7, 18)]
    [InlineData("12:10 a.m.", 0, 10, 0)]
    [InlineData("Fecha expedi cion: 16/09/2026 5:22:42 D,m.", 17, 22, 42)]
    [InlineData("2026-09-14 16:50:36.390 4781", 16, 50, 36)]
    [InlineData("FECHA: 18/09/2026 HORA:9:46:09 01302", 9, 46, 9)]
    public void FindTimes_ReadsCommonFormats(string row, int hour, int minute, int second)
    {
        Assert.Equal(new TimeOnly(hour, minute, second), Assert.Single(DateParser.FindTimes(row)).Time);
    }
}
