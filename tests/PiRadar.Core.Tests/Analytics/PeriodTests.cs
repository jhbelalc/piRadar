using PiRadar.Core.Analytics;

namespace PiRadar.Core.Tests.Analytics;

public class PeriodTests
{
    private static readonly DateTime Thursday = new(2026, 9, 24, 21, 30, 0);

    [Theory]
    [InlineData(PeriodGranularity.Day, "2026-09-24", "2026-09-25")]
    [InlineData(PeriodGranularity.Week, "2026-09-21", "2026-09-28")]
    [InlineData(PeriodGranularity.Month, "2026-09-01", "2026-10-01")]
    [InlineData(PeriodGranularity.Year, "2026-01-01", "2027-01-01")]
    public void Containing_ReturnsBoundaries(PeriodGranularity granularity, string start, string end)
    {
        var period = Period.Containing(Thursday, granularity);

        Assert.Equal(DateTime.Parse(start), period.Start);
        Assert.Equal(DateTime.Parse(end), period.End);
        Assert.True(period.Contains(Thursday));
        Assert.False(period.Contains(period.End));
    }

    [Fact]
    public void Week_StartsOnMondayEvenFromSunday()
    {
        var period = Period.Containing(new DateTime(2026, 9, 27), PeriodGranularity.Week);

        Assert.Equal(new DateTime(2026, 9, 21), period.Start);
    }

    [Fact]
    public void NextAndPrevious_AreInverse()
    {
        var month = Period.Containing(new DateTime(2026, 1, 31), PeriodGranularity.Month);

        Assert.Equal(new DateTime(2026, 2, 1), month.Next().Start);
        Assert.Equal(new DateTime(2025, 12, 1), month.Previous().Start);
        Assert.Equal(month, month.Next().Previous());
        Assert.Equal(new DateTime(2026, 9, 14), Period.Containing(Thursday, PeriodGranularity.Week).Previous().Start);
    }

    [Theory]
    [InlineData(PeriodGranularity.Day, "2026-09-24", "jueves 24 sep 2026")]
    [InlineData(PeriodGranularity.Week, "2026-09-24", "21 – 27 sep 2026")]
    [InlineData(PeriodGranularity.Week, "2026-10-01", "28 sep – 4 oct 2026")]
    [InlineData(PeriodGranularity.Week, "2026-12-31", "28 dic 2026 – 3 ene 2027")]
    [InlineData(PeriodGranularity.Month, "2026-09-24", "Septiembre 2026")]
    [InlineData(PeriodGranularity.Year, "2026-09-24", "2026")]
    public void Label_IsSpanish(PeriodGranularity granularity, string date, string expected)
    {
        Assert.Equal(expected, Period.Containing(DateTime.Parse(date), granularity).Label);
    }
}
