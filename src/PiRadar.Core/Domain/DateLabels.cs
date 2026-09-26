using System.Globalization;

namespace PiRadar.Core.Domain;

/// <summary>
/// Fechas en español para la UI. Nombres fijos porque ICU cambia entre plataformas ("sept." en unas, "sep" en otras).
/// </summary>
public static class DateLabels
{
    private static readonly string[] MonthNames =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
    ];

    private static readonly string[] MonthAbbreviations =
        ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    private static readonly string[] DayNames =
        ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    public static string MonthName(int month) => MonthNames[month - 1];

    public static string MonthAbbreviation(int month) => MonthAbbreviations[month - 1];

    public static string DayName(DayOfWeek day) => DayNames[(int)day];

    /// <summary>"25 sep 2026".</summary>
    public static string Short(DateTime date) =>
        $"{date.Day} {MonthAbbreviation(date.Month)} {date.Year.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Encabezado de un día en listas: "Hoy", "Ayer", "jueves 24 sep" o, de otro año, "lunes 29 dic 2025".</summary>
    public static string Day(DateTime date, DateTime today)
    {
        var day = date.Date;
        if (day == today.Date)
        {
            return "Hoy";
        }

        if (day == today.Date.AddDays(-1))
        {
            return "Ayer";
        }

        var text = $"{DayName(day.DayOfWeek)} {day.Day} {MonthAbbreviation(day.Month)}";
        return day.Year == today.Year ? text : $"{text} {day.Year.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>Hora en formato de 24 horas: "18:42".</summary>
    public static string Time(DateTime date) => date.ToString("HH:mm", CultureInfo.InvariantCulture);
}
