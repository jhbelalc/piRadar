using System.Globalization;

namespace PiRadar.Core.Import;

/// <summary>Tipo de contenido de una celda leída de un archivo de importación.</summary>
public enum CellKind
{
    Empty = 0,
    Text = 1,
    Number = 2,
    Date = 3,
    Boolean = 4,
}

/// <summary>Valor inmutable de una celda. Los números son <see cref="decimal"/> (nunca double); las fechas llegan con Kind Unspecified.</summary>
public readonly record struct CellValue(
    CellKind Kind,
    string? Text = null,
    decimal? Number = null,
    DateTime? Date = null,
    bool? Boolean = null)
{
    /// <summary>Celda vacía.</summary>
    public static CellValue Empty => default;

    public bool IsEmpty => Kind == CellKind.Empty;

    /// <summary>Texto; una cadena vacía se considera celda vacía. No recorta espacios.</summary>
    public static CellValue FromText(string? text) =>
        string.IsNullOrEmpty(text) ? Empty : new CellValue(CellKind.Text, Text: text);

    public static CellValue FromNumber(decimal number) => new(CellKind.Number, Number: number);

    public static CellValue FromDate(DateTime date) =>
        new(CellKind.Date, Date: DateTime.SpecifyKind(date, DateTimeKind.Unspecified));

    public static CellValue FromBoolean(bool value) => new(CellKind.Boolean, Boolean: value);

    /// <summary>Texto invariante para mostrar ejemplos al usuario (no depende del idioma).</summary>
    public string ToDisplayString() => Kind switch
    {
        CellKind.Text => Text ?? "",
        CellKind.Number => (Number ?? 0m).ToString(CultureInfo.InvariantCulture),
        CellKind.Date => Date is { } d
            ? d.TimeOfDay == TimeSpan.Zero
                ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : "",
        CellKind.Boolean => Boolean == true ? "true" : "false",
        _ => "",
    };
}
