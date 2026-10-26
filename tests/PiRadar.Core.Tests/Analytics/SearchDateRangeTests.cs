using PiRadar.Core.Analytics;

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
}
