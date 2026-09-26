using System.Globalization;
using PiRadar.Core.Domain;

namespace PiRadar.App.Services;

/// <summary>Textos cortos para ejes de gráficos. Solo para mostrar: los montos reales siguen siendo <c>Money</c>.</summary>
public static class ChartFormat
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>"$ 1,2 M", "$ 450 mil", "US$ 12".</summary>
    public static string Compact(double value, Currency currency)
    {
        var symbol = currency.Symbol();
        var abs = Math.Abs(value);
        return abs switch
        {
            >= 1_000_000 => $"{symbol} {(value / 1_000_000).ToString("0.#", Colombia)} M",
            >= 1_000 => $"{symbol} {(value / 1_000).ToString("0", Colombia)} mil",
            _ => $"{symbol} {value.ToString("0", Colombia)}",
        };
    }
}
