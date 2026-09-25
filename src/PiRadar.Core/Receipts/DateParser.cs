using System.Text.RegularExpressions;

namespace PiRadar.Core.Receipts;

/// <summary>Fecha (y hora opcional) encontrada en una fila.</summary>
internal readonly record struct DateToken(DateOnly Date, int Start, int Length)
{
    public int End => Start + Length;
}

internal readonly record struct TimeToken(TimeOnly Time, int Start);

/// <summary>
/// Fechas de recibos colombianos y de EE. UU.: "14/09/2026", "2026-09-14", "2026/9/6", "20/09/26", "21/SEP/2026",
/// "5 DE ABRIL DE 2010", "September 11, 2026". El día va primero salvo que no sea posible o se pida mes primero.
/// </summary>
internal static partial class DateParser
{
    private static readonly Dictionary<string, int> Months = new(StringComparer.Ordinal)
    {
        ["ENE"] = 1, ["ENERO"] = 1, ["JAN"] = 1, ["JANUARY"] = 1,
        ["FEB"] = 2, ["FEBRERO"] = 2, ["FEBRUARY"] = 2,
        ["MAR"] = 3, ["MARZO"] = 3, ["MARCH"] = 3,
        ["ABR"] = 4, ["ABRIL"] = 4, ["APR"] = 4, ["APRIL"] = 4,
        ["MAY"] = 5, ["MAYO"] = 5,
        ["JUN"] = 6, ["JUNIO"] = 6, ["JUNE"] = 6,
        ["JUL"] = 7, ["JULIO"] = 7, ["JULY"] = 7,
        ["AGO"] = 8, ["AGOSTO"] = 8, ["AUG"] = 8, ["AUGUST"] = 8,
        ["SEP"] = 9, ["SEPT"] = 9, ["SET"] = 9, ["SEPTIEMBRE"] = 9, ["SETIEMBRE"] = 9, ["SEPTEMBER"] = 9,
        ["OCT"] = 10, ["OCTUBRE"] = 10, ["OCTOBER"] = 10,
        ["NOV"] = 11, ["NOVIEMBRE"] = 11, ["NOVEMBER"] = 11,
        ["DIC"] = 12, ["DICIEMBRE"] = 12, ["DEC"] = 12, ["DECEMBER"] = 12,
    };

    public static IReadOnlyList<DateToken> FindDates(string row, bool preferMonthFirst = false)
    {
        // "20/U9/2026", "2O26": O y U leídas en lugar de 0 entre dígitos o separadores de fecha.
        var text = LetterZero().Replace(TextNormalizer.Fold(row), "0");
        var result = new List<DateToken>();
        var used = new bool[text.Length];

        void Add(Match m, int year, int month, int day)
        {
            if (m.Index < used.Length && used[m.Index])
            {
                return;
            }

            if (year < 100)
            {
                year += 2000;
            }

            if (year is >= 2000 and <= 2099 && month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month))
            {
                result.Add(new DateToken(new DateOnly(year, month, day), m.Index, m.Length));
                Array.Fill(used, true, m.Index, m.Length);
            }
        }

        foreach (Match m in YearFirst().Matches(text))
        {
            Add(m, Int(m, "y"), Int(m, "m"), Int(m, "d"));
        }

        foreach (Match m in Numeric().Matches(text))
        {
            var (a, b, year) = (Int(m, "a"), Int(m, "b"), Int(m, "y"));
            // Día primero en Colombia; mes primero si el día no es válido (PriceSmart imprime "9/16/2026") o si se pide.
            var monthFirst = a <= 12 && (b > 12 || (preferMonthFirst && b <= 12));
            Add(m, year, monthFirst ? a : b, monthFirst ? b : a);
        }

        foreach (Match m in DayMonthName().Matches(text))
        {
            if (Months.TryGetValue(m.Groups["mon"].Value, out var month))
            {
                Add(m, Int(m, "y"), month, Int(m, "d"));
            }
        }

        foreach (Match m in MonthNameDay().Matches(text))
        {
            if (Months.TryGetValue(m.Groups["mon"].Value, out var month))
            {
                Add(m, Int(m, "y"), month, Int(m, "d"));
            }
        }

        return result.OrderBy(t => t.Start).ToList();
    }

    /// <summary>Horas "18:42", "5:22:42 p.m.", "12:55:13 p. m.", "04:07:18 PM", "17:36:41.045".</summary>
    public static IReadOnlyList<TimeToken> FindTimes(string row)
    {
        var text = TextNormalizer.Fold(row);
        var result = new List<TimeToken>();
        foreach (Match m in Time().Matches(text))
        {
            var hour = Int(m, "h");
            var minute = Int(m, "min");
            var second = m.Groups["s"].Success ? Int(m, "s") : 0;
            if (m.Groups["ap"].Success && hour is >= 1 and <= 12)
            {
                // ML Kit lee a veces "p.m." como "D.m." o "D,m.".
                var pm = m.Groups["ap"].Value is "P" or "D";
                hour = hour % 12 + (pm ? 12 : 0);
            }

            if (hour <= 23 && minute <= 59 && second <= 59)
            {
                result.Add(new TimeToken(new TimeOnly(hour, minute, second), m.Index));
            }
        }

        return result;
    }

    [GeneratedRegex(@"(?<=[\d/.-])[OU](?=\d)|(?<=\d)[OU](?=[\d/.-])")]
    private static partial Regex LetterZero();

    private static int Int(Match m, string group) => int.Parse(m.Groups[group].ValueSpan);

    // "2026-09-21", "2026/9/6"; admite la hora pegada ("2026-09-2117:36:41").
    [GeneratedRegex(@"(?<!\d)(?<y>20\d{2})\s?[-/.]\s?(?<m>\d{1,2})\s?[-/.]\s?(?<d>\d{1,2})")]
    private static partial Regex YearFirst();

    // "14/09/2026", "20/09/26", "9/16/2026", "27 06 2026" no (demasiado ambiguo sin separador).
    [GeneratedRegex(@"(?<!\d)(?<a>\d{1,2})\s?[-/.]\s?(?<b>\d{1,2})\s?[-/.]\s?(?<y>20\d{2}|\d{2})(?!\d)")]
    private static partial Regex Numeric();

    // "21/SEP/2026", "14 SEP 2026", "5 DE ABRIL DE 2010", "27 DIC 2024".
    [GeneratedRegex(@"(?<!\d)(?<d>\d{1,2})\s?(?:[-/.]|\s)\s?(?:DE\s)?(?<mon>[A-Z]{3,10})\.?\s?(?:[-/.]|\s)\s?(?:DE\s|DEL\s)?(?<y>20\d{2}|\d{2})(?!\d)")]
    private static partial Regex DayMonthName();

    // "SEPTEMBER 11, 2026", "DICIEMBRE 07 DE 2026".
    [GeneratedRegex(@"(?<![A-Z])(?<mon>[A-Z]{3,10})\.?\s(?<d>\d{1,2}),?\s(?:DE\s)?(?<y>20\d{2})(?!\d)")]
    private static partial Regex MonthNameDay();

    [GeneratedRegex(@"(?<!\d)(?<!\d\s?:\s?)(?<h>\d{1,2})\s?:\s?(?<min>\d{2})(?:\s?:\s?(?<s>\d{2}))?(?:[.,]\d{1,3})?(?:\s?(?<ap>[APD])[.,]?\s?M\b\.?)?(?![\d:])")]
    private static partial Regex Time();
}
