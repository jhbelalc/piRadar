using System.Globalization;

namespace PiRadar.Core.Domain;

/// <summary>
/// Fechas para la UI en el idioma vigente (<see cref="AppLocale"/>). Nombres fijos porque ICU cambia entre
/// plataformas ("sept." en unas, "sep" en otras).
/// </summary>
public static class DateLabels
{
    private static readonly string[] MonthNamesEn =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    ];

    private static readonly string[] MonthAbbreviationsEn =
        ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    private static readonly string[] DayNamesEn =
        ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

    private static readonly string[] MonthNames =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
    ];

    private static readonly string[] MonthAbbreviations =
        ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    private static readonly string[] DayNames =
        ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    public static string MonthName(int month) => (AppLocale.IsEnglish ? MonthNamesEn : MonthNames)[month - 1];

    public static string MonthAbbreviation(int month) =>
        (AppLocale.IsEnglish ? MonthAbbreviationsEn : MonthAbbreviations)[month - 1];

    public static string DayName(DayOfWeek day) => (AppLocale.IsEnglish ? DayNamesEn : DayNames)[(int)day];

    /// <summary>"25 sep 2026" en español, "Sep 25, 2026" en inglés.</summary>
    public static string Short(DateTime date)
    {
        var year = date.Year.ToString(CultureInfo.InvariantCulture);
        return AppLocale.IsEnglish
            ? $"{MonthAbbreviation(date.Month)} {date.Day}, {year}"
            : $"{date.Day} {MonthAbbreviation(date.Month)} {year}";
    }

    /// <summary>
    /// Encabezado de un día en listas: "Hoy", "Ayer", "jueves 24 sep" o, de otro año, "lunes 29 dic 2025".
    /// En inglés: "Today", "Yesterday", "Thursday, Sep 24" o "Monday, Dec 29, 2025".
    /// </summary>
    public static string Day(DateTime date, DateTime today)
    {
        var day = date.Date;
        if (day == today.Date)
        {
            return AppLocale.Pick("Hoy", "Today");
        }

        if (day == today.Date.AddDays(-1))
        {
            return AppLocale.Pick("Ayer", "Yesterday");
        }

        var year = day.Year.ToString(CultureInfo.InvariantCulture);
        if (AppLocale.IsEnglish)
        {
            var english = $"{DayName(day.DayOfWeek)}, {MonthAbbreviation(day.Month)} {day.Day}";
            return day.Year == today.Year ? english : $"{english}, {year}";
        }

        var text = $"{DayName(day.DayOfWeek)} {day.Day} {MonthAbbreviation(day.Month)}";
        return day.Year == today.Year ? text : $"{text} {year}";
    }

    /// <summary>Hora: "18:42" (24 horas) en español, "6:42 PM" en inglés.</summary>
    public static string Time(DateTime date) => AppLocale.IsEnglish
        ? date.ToString("h:mm tt", CultureInfo.InvariantCulture)
        : date.ToString("HH:mm", CultureInfo.InvariantCulture);
}
