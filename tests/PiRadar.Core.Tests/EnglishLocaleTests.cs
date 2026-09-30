using PiRadar.Core.Analytics;
using PiRadar.Core.Domain;
using Xunit;

namespace PiRadar.Core.Tests;

public class EnglishLocaleTests
{
    private static IDisposable English() => AppLocale.Use(AppLanguage.English);

    [Fact]
    public void Default_language_is_spanish()
    {
        Assert.Equal(AppLanguage.Spanish, AppLocale.Language);
        Assert.Equal("$ 1.234.567", new Money(1_234_567, Currency.COP).ToString());
        Assert.Equal("US$ 12,50", new Money(1250, Currency.USD).ToString());
    }

    [Fact]
    public void Use_restores_the_previous_language()
    {
        using (English())
        {
            Assert.True(AppLocale.IsEnglish);
        }

        Assert.False(AppLocale.IsEnglish);
    }

    [Theory]
    [InlineData(1250, "$12.50")]
    [InlineData(123456, "$1,234.56")]
    [InlineData(-5, "-$0.05")]
    [InlineData(0, "$0.00")]
    public void Usd_formats_the_american_way(long minor, string expected)
    {
        using var english = English();
        Assert.Equal(expected, new Money(minor, Currency.USD).ToString());
    }

    [Fact]
    public void Cop_is_labelled_in_english()
    {
        using var english = English();
        Assert.Equal("COP $1,234,567", new Money(1_234_567, Currency.COP).ToString());
        Assert.Equal("-COP $500", new Money(-500, Currency.COP).ToString());
    }

    [Fact]
    public void Money_errors_are_in_english()
    {
        using var english = English();
        var ex = Assert.Throws<ArgumentException>(() => Money.FromDecimal(1.5m, Currency.COP));
        Assert.Contains("decimals", ex.Message);
    }

    [Fact]
    public void Parser_messages_are_in_english()
    {
        using var english = English();
        Assert.False(MoneyParser.TryParse("", Currency.USD, out _, out var empty));
        Assert.Equal("Enter an amount.", empty);
        Assert.False(MoneyParser.TryParse("abc", Currency.USD, out _, out var invalid));
        Assert.Equal("The amount is not valid.", invalid);
        Assert.False(MoneyParser.TryParse("12.3456", Currency.USD, out _, out var tooMany));
        Assert.Equal("At most 2 decimals.", tooMany);
        Assert.False(MoneyParser.TryParse("12.5", Currency.COP, out _, out var noCents));
        Assert.Equal("Colombian pesos have no cents.", noCents);
    }

    [Theory]
    [InlineData("1,234.56", 123456)]
    [InlineData("$12.50", 1250)]
    [InlineData("12", 1200)]
    [InlineData("1,000", 100000)]
    public void Parser_reads_american_input(string text, long minor)
    {
        using var english = English();
        Assert.True(MoneyParser.TryParse(text, Currency.USD, out var money, out var error), error);
        Assert.Equal(minor, money.AmountMinor);
    }

    [Fact]
    public void Input_text_uses_american_separators()
    {
        using var english = English();
        Assert.Equal("1,234.50", MoneyParser.FormatForInput(new Money(123450, Currency.USD)));
        Assert.Equal("4,150.25", MoneyParser.FormatDecimal(4150.25m));
        Assert.Equal("4,150", MoneyParser.FormatDecimal(4150m));
    }

    [Fact]
    public void Dates_are_in_english()
    {
        using var english = English();
        var today = new DateTime(2026, 9, 25);
        Assert.Equal("Sep 25, 2026", DateLabels.Short(today));
        Assert.Equal("Today", DateLabels.Day(today, today));
        Assert.Equal("Yesterday", DateLabels.Day(today.AddDays(-1), today));
        Assert.Equal("Thursday, Sep 24", DateLabels.Day(new DateTime(2026, 9, 24), new DateTime(2026, 9, 26)));
        Assert.Equal("Monday, Dec 29, 2025", DateLabels.Day(new DateTime(2025, 12, 29), today));
        Assert.Equal("September", DateLabels.MonthName(9));
        Assert.Equal("Wednesday", DateLabels.DayName(DayOfWeek.Wednesday));
    }

    [Fact]
    public void Time_uses_twelve_hours()
    {
        using var english = English();
        Assert.Equal("6:42 PM", DateLabels.Time(new DateTime(2026, 9, 25, 18, 42, 0)));
        Assert.Equal("12:05 AM", DateLabels.Time(new DateTime(2026, 9, 25, 0, 5, 0)));
    }

    [Fact]
    public void Week_starts_on_sunday()
    {
        using var english = English();
        // Friday 25 Sep 2026 -> Sunday 20 – Saturday 26.
        var week = Period.Containing(new DateTime(2026, 9, 25), PeriodGranularity.Week);
        Assert.Equal(new DateTime(2026, 9, 20), week.Start);
        Assert.Equal(new DateTime(2026, 9, 27), week.End);
        Assert.Equal("Sep 20 – 26, 2026", week.Label);
        // A Sunday belongs to the week that starts that day.
        Assert.Equal(new DateTime(2026, 9, 20), Period.Containing(new DateTime(2026, 9, 20), PeriodGranularity.Week).Start);
        Assert.Equal(new DateTime(2026, 9, 20), Period.Containing(new DateTime(2026, 9, 26), PeriodGranularity.Week).Start);
    }

    [Fact]
    public void Week_label_crossing_months_and_years()
    {
        using var english = English();
        var crossMonth = Period.Containing(new DateTime(2026, 9, 30), PeriodGranularity.Week);
        Assert.Equal("Sep 27 – Oct 3, 2026", crossMonth.Label);
        var crossYear = Period.Containing(new DateTime(2026, 12, 31), PeriodGranularity.Week);
        Assert.Equal("Dec 27, 2026 – Jan 2, 2027", crossYear.Label);
    }

    [Fact]
    public void Day_month_and_year_labels()
    {
        using var english = English();
        Assert.Equal("Friday, Sep 25, 2026", Period.Containing(new DateTime(2026, 9, 25), PeriodGranularity.Day).Label);
        Assert.Equal("September 2026", Period.Containing(new DateTime(2026, 9, 25), PeriodGranularity.Month).Label);
        Assert.Equal("2026", Period.Containing(new DateTime(2026, 9, 25), PeriodGranularity.Year).Label);
    }

    [Fact]
    public void Spanish_week_still_starts_on_monday()
    {
        var week = Period.Containing(new DateTime(2026, 9, 25), PeriodGranularity.Week);
        Assert.Equal(new DateTime(2026, 9, 21), week.Start);
    }

    [Theory]
    [InlineData("es", AppLanguage.Spanish)]
    [InlineData("es-CO", AppLanguage.Spanish)]
    [InlineData("fr", AppLanguage.Spanish)]
    [InlineData("en", AppLanguage.English)]
    [InlineData("en-US", AppLanguage.English)]
    [InlineData("en-GB", AppLanguage.English)]
    public void Device_language_maps_to_supported_language(string culture, AppLanguage expected) =>
        Assert.Equal(expected, AppLocale.FromDevice(System.Globalization.CultureInfo.GetCultureInfo(culture)));

    [Theory]
    [InlineData("es", AppLanguage.Spanish)]
    [InlineData("en", AppLanguage.English)]
    public void Setting_code_round_trips(string code, AppLanguage language)
    {
        Assert.Equal(language, AppLocale.ParseCode(code));
        Assert.Equal(code, AppLocale.Code(language));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("system")]
    [InlineData("1")]
    public void Unknown_setting_means_follow_device(string? code) => Assert.Null(AppLocale.ParseCode(code));
}
