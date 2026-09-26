namespace PiRadar.Core.Analytics;

public enum PeriodGranularity
{
    Day = 1,
    Week = 2,
    Month = 3,
    Year = 4,
}

/// <summary>
/// Día, semana (lunes a domingo, ISO), mes o año en hora local. <see cref="Start"/> es inclusivo y
/// <see cref="End"/> exclusivo, como <see cref="Domain.TransactionFilter"/>.
/// </summary>
public readonly record struct Period
{
    // Nombres fijos: ICU cambia entre plataformas ("sept." en unas, "sep" en otras).
    private static readonly string[] MonthNames =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
    ];

    private static readonly string[] MonthAbbreviations =
        ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    private static readonly string[] DayNames =
        ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

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
            PeriodGranularity.Week => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
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

    /// <summary>Etiqueta en español: "jueves 25 sep 2026", "22 – 28 sep 2026", "Septiembre 2026", "2026".</summary>
    public string Label => Granularity switch
    {
        PeriodGranularity.Day => $"{DayNames[(int)Start.DayOfWeek]} {Start.Day} {Abbrev(Start)} {Start.Year}",
        PeriodGranularity.Week => WeekLabel(),
        PeriodGranularity.Month => $"{Capitalize(MonthNames[Start.Month - 1])} {Start.Year}",
        _ => Start.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    private string WeekLabel()
    {
        var last = End.AddDays(-1);
        if (Start.Year != last.Year)
        {
            return $"{Start.Day} {Abbrev(Start)} {Start.Year} – {last.Day} {Abbrev(last)} {last.Year}";
        }

        return Start.Month == last.Month
            ? $"{Start.Day} – {last.Day} {Abbrev(last)} {last.Year}"
            : $"{Start.Day} {Abbrev(Start)} – {last.Day} {Abbrev(last)} {last.Year}";
    }

    private static string Abbrev(DateTime date) => MonthAbbreviations[date.Month - 1];

    private static string Capitalize(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
