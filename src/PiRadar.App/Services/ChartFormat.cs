using System.Globalization;
using PiRadar.Core.Domain;

namespace PiRadar.App.Services;

/// <summary>Textos cortos para ejes de gráficos. Solo para mostrar: los montos reales siguen siendo <c>Money</c>.</summary>
public static class ChartFormat
{
    /// <summary>"$ 1,2 M", "$ 450 mil", "US$ 12" en español; "$1.2M", "$450K", "$12" en inglés.</summary>
    public static string Compact(double value, Currency currency)
    {
        var symbol = currency.Symbol();
        var culture = AppLocale.Culture;
        var abs = Math.Abs(value);
        if (AppLocale.IsEnglish)
        {
            return abs switch
            {
                >= 1_000_000 => $"{symbol}{(value / 1_000_000).ToString("0.#", culture)}M",
                >= 1_000 => $"{symbol}{(value / 1_000).ToString("0", culture)}K",
                _ => $"{symbol}{value.ToString("0", culture)}",
            };
        }

        return abs switch
        {
            >= 1_000_000 => $"{symbol} {(value / 1_000_000).ToString("0.#", culture)} M",
            >= 1_000 => $"{symbol} {(value / 1_000).ToString("0", culture)} mil",
            _ => $"{symbol} {value.ToString("0", culture)}",
        };
    }
}
