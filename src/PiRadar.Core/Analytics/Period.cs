using PiRadar.Core.Domain;

namespace PiRadar.Core.Analytics;

public enum PeriodGranularity
{
    Day = 1,
    Week = 2,
    Month = 3,
    Year = 4,
}

/// <summary>
/// Día, semana (lunes a domingo en español, ISO; domingo a sábado en inglés), mes o año en hora local. <see cref="Start"/> es inclusivo y
/// <see cref="End"/> exclusivo, como <see cref="Domain.TransactionFilter"/>.
/// </summary>
public readonly record struct Period
{
    private Period(PeriodGranularity granularity, DateTime start)
    {
        Granularity = granularity;
        Start = start;
    }

    public PeriodGranularity Granularity { get; }

    public DateTime Start { get; }

    public DateTime End => Granularity switch
    {
        PeriodGranularity.Day => Start.AddDays(1),
        PeriodGranularity.Week => Start.AddDays(7),
        PeriodGranularity.Month => Start.AddMonths(1),
        PeriodGranularity.Year => Start.AddYears(1),
        _ => throw new ArgumentOutOfRangeException(nameof(Granularity), Granularity, null),
    };

    /// <summary>El periodo de la granularidad dada que contiene <paramref name="date"/>.</summary>
    public static Period Containing(DateTime date, PeriodGranularity granularity)
    {
        var day = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        var start = granularity switch
        {
            PeriodGranularity.Day => day,
            PeriodGranularity.Week => day.AddDays(-(((int)day.DayOfWeek - (int)AppLocale.FirstDayOfWeek + 7) % 7)),
            PeriodGranularity.Month => new DateTime(day.Year, day.Month, 1),
            PeriodGranularity.Year => new DateTime(day.Year, 1, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(granularity), granularity, null),
        };
        return new Period(granularity, start);
    }

    public Period Next() => new(Granularity, End);

    public Period Previous() => Granularity switch
    {
        PeriodGranularity.Day => new(Granularity, Start.AddDays(-1)),
        PeriodGranularity.Week => new(Granularity, Start.AddDays(-7)),
        PeriodGranularity.Month => new(Granularity, Start.AddMonths(-1)),
        _ => new(Granularity, Start.AddYears(-1)),
    };

    public bool Contains(DateTime date) => date >= Start && date < End;

    /// <summary>
    /// Etiqueta: "jueves 25 sep 2026", "22 – 28 sep 2026", "Septiembre 2026", "2026"; en inglés
    /// "Friday Sep 25, 2026", "Sep 20 – 26, 2026", "September 2026".
    /// </summary>
    public string Label => Granularity switch
    {
        PeriodGranularity.Day => AppLocale.IsEnglish
            ? $"{DateLabels.DayName(Start.DayOfWeek)}, {DateLabels.Short(Start)}"
            : $"{DateLabels.DayName(Start.DayOfWeek)} {DateLabels.Short(Start)}",
        PeriodGranularity.Week => WeekLabel(),
        PeriodGranularity.Month => $"{Capitalize(DateLabels.MonthName(Start.Month))} {Start.Year}",
        _ => Start.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    private string WeekLabel()
    {
        var last = End.AddDays(-1);
        if (Start.Year != last.Year)
        {
            return $"{DateLabels.Short(Start)} – {DateLabels.Short(last)}";
        }

        if (AppLocale.IsEnglish)
        {
            return Start.Month == last.Month
                ? $"{DateLabels.MonthAbbreviation(Start.Month)} {Start.Day} – {last.Day}, {last.Year}"
                : $"{DateLabels.MonthAbbreviation(Start.Month)} {Start.Day} – {DateLabels.Short(last)}";
        }

        return Start.Month == last.Month
            ? $"{Start.Day} – {DateLabels.Short(last)}"
            : $"{Start.Day} {DateLabels.MonthAbbreviation(Start.Month)} – {DateLabels.Short(last)}";
    }

    private static string Capitalize(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
