using PiRadar.Core.Analytics;
using PiRadar.Core.Domain;

namespace PiRadar.Core.Tests.Analytics;

public class SearchDateRangeTests
{
    [Fact]
    public void MonthToDate_StartsOnTheFirst()
    {
        var r = SearchDateRange.MonthToDate(new DateOnly(2026, 10, 26));

        Assert.Equal(new DateOnly(2026, 10, 1), r.From);
        Assert.Equal(new DateOnly(2026, 10, 26), r.To);
    }

    [Fact]
    public void MonthToDate_OnTheFirst_IsASingleDay()
    {
        var r = SearchDateRange.MonthToDate(new DateOnly(2026, 11, 1));

        Assert.Equal(new DateOnly(2026, 11, 1), r.From);
        Assert.Equal(new DateOnly(2026, 11, 1), r.To);
    }

    [Fact]
    public void WithFrom_AfterTo_MovesToToTheSameDay()
    {
        var r = new SearchDateRange(new(2026, 10, 1), new(2026, 10, 10)).WithFrom(new(2026, 10, 15));

        Assert.Equal(new SearchDateRange(new(2026, 10, 15), new(2026, 10, 15)), r);
    }

    [Fact]
    public void WithTo_BeforeFrom_MovesFromToTheSameDay()
    {
        var r = new SearchDateRange(new(2026, 10, 10), new(2026, 10, 20)).WithTo(new(2026, 10, 5));

        Assert.Equal(new SearchDateRange(new(2026, 10, 5), new(2026, 10, 5)), r);
    }

    [Fact]
    public void WithFromAndWithTo_InOrder_KeepTheOtherEnd()
    {
        var r = new SearchDateRange(new(2026, 10, 1), new(2026, 10, 20));

        Assert.Equal(new SearchDateRange(new(2026, 9, 15), new(2026, 10, 20)), r.WithFrom(new(2026, 9, 15)));
        Assert.Equal(new SearchDateRange(new(2026, 10, 1), new(2026, 10, 5)), r.WithTo(new(2026, 10, 5)));
    }

    [Fact]
    public void Bounds_IncludeTheWholeLastDay()
    {
        var r = new SearchDateRange(new(2026, 3, 15), new(2026, 4, 10));

        Assert.Equal(new DateTime(2026, 3, 15, 0, 0, 0), r.Start);
        Assert.Equal(new DateTime(2026, 4, 11, 0, 0, 0), r.EndExclusive);
        Assert.True(new DateTime(2026, 4, 10, 23, 59, 0) < r.EndExclusive);
    }

    [Fact]
    public void Bounds_AcrossYears()
    {
        var r = new SearchDateRange(new(2025, 12, 20), new(2026, 1, 10));

        Assert.Equal(new DateTime(2025, 12, 20), r.Start);
        Assert.Equal(new DateTime(2026, 1, 11), r.EndExclusive);
    }

    [Fact]
    public void FromPeriod_Day_IsThatDay()
    {
        var r = SearchDateRange.FromPeriod(Period.Containing(new DateTime(2026, 11, 12, 15, 30, 0), PeriodGranularity.Day));

        Assert.Equal(new SearchDateRange(new(2026, 11, 12), new(2026, 11, 12)), r);
    }

    [Fact]
    public void FromPeriod_Week_Spanish_MondayToSunday()
    {
        using var _ = AppLocale.Use(AppLanguage.Spanish);
        var r = SearchDateRange.FromPeriod(Period.Containing(new DateTime(2026, 11, 12), PeriodGranularity.Week));

        Assert.Equal(new SearchDateRange(new(2026, 11, 9), new(2026, 11, 15)), r);
    }

    [Fact]
    public void FromPeriod_Week_English_SundayToSaturday()
    {
        using var _ = AppLocale.Use(AppLanguage.English);
        var r = SearchDateRange.FromPeriod(Period.Containing(new DateTime(2026, 11, 12), PeriodGranularity.Week));

        Assert.Equal(new SearchDateRange(new(2026, 11, 8), new(2026, 11, 14)), r);
    }

    [Fact]
    public void FromPeriod_Week_AcrossYears()
    {
        using var _ = AppLocale.Use(AppLanguage.Spanish);
        var r = SearchDateRange.FromPeriod(Period.Containing(new DateTime(2026, 12, 31), PeriodGranularity.Week));

        Assert.Equal(new SearchDateRange(new(2026, 12, 28), new(2027, 1, 3)), r);
    }

    [Theory]
    [InlineData(2026, 11, 30)]
    [InlineData(2026, 12, 31)]
    [InlineData(2026, 2, 28)]
    [InlineData(2028, 2, 29)]
    public void FromPeriod_Month_FirstToLastDay(int year, int month, int lastDay)
    {
        var r = SearchDateRange.FromPeriod(Period.Containing(new DateTime(year, month, 10), PeriodGranularity.Month));

        Assert.Equal(new SearchDateRange(new(year, month, 1), new(year, month, lastDay)), r);
    }

    [Fact]
    public void FromPeriod_Year_JanuaryFirstToDecember31()
    {
        var r = SearchDateRange.FromPeriod(Period.Containing(new DateTime(2026, 6, 15), PeriodGranularity.Year));

        Assert.Equal(new SearchDateRange(new(2026, 1, 1), new(2026, 12, 31)), r);
    }
}
