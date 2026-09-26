using PiRadar.Core.Domain;

namespace PiRadar.Core.Tests;

public class DateLabelsTests
{
    private static readonly DateTime Today = new(2026, 9, 25, 9, 0, 0);

    [Theory]
    [InlineData("2026-09-25 23:59", "Hoy")]
    [InlineData("2026-09-24 00:00", "Ayer")]
    [InlineData("2026-09-23 12:00", "miércoles 23 sep")]
    [InlineData("2025-12-29 12:00", "lunes 29 dic 2025")]
    public void Day_UsesRelativeNamesAndOmitsCurrentYear(string date, string expected)
    {
        Assert.Equal(expected, DateLabels.Day(DateTime.Parse(date), Today));
    }

    [Fact]
    public void ShortAndTime_AreFixedFormats()
    {
        var date = new DateTime(2026, 9, 5, 7, 3, 0);
        Assert.Equal("5 sep 2026", DateLabels.Short(date));
        Assert.Equal("07:03", DateLabels.Time(date));
    }
}
