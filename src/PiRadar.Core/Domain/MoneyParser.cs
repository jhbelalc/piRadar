using System.Globalization;

namespace PiRadar.Core.Domain;

/// <summary>
/// Interpreta montos escritos por el usuario en formato colombiano ("1.234.567", "12,50"). Tolera el punto como
/// decimal porque el teclado numérico de Android a veces solo ofrece "." ("12.50"). Regla, igual que en los
/// recibos: un separador seguido de exactamente 3 dígitos es de miles; si no, es decimal. Con separadores
/// mezclados, el último es el decimal.
/// </summary>
public static class MoneyParser
{
    /// <summary>Más dígitos enteros que esto ya no cabe en <c>long</c> con centavos (y no es un monto real).</summary>
    private const int MaxIntegerDigits = 13;

    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>
    /// Convierte el texto a <see cref="Money"/>. Devuelve <c>false</c> con un mensaje en español si está vacío,
    /// no es un número o tiene más decimales de los que admite la moneda.
    /// </summary>
    public static bool TryParse(string? text, Currency currency, out Money money, out string? error)
    {
        money = Money.Zero(currency);
        var cleaned = StripCurrency(text);
        if (cleaned.Length == 0)
        {
            error = "Ingresa un monto.";
            return false;
        }

        if (!TryParseDecimal(cleaned, out var value))
        {
            error = "El monto no es válido.";
            return false;
        }

        var scale = currency.Scale();
        if (decimal.Round(value, scale) != value)
        {
            error = scale == 0 ? "Los pesos no llevan centavos." : $"Máximo {scale} decimales.";
            return false;
        }

        money = Money.FromDecimal(decimal.Round(value, scale), currency);
        error = null;
        return true;
    }

    /// <summary>Interpreta un número no negativo con separadores colombianos o ingleses.</summary>
    public static bool TryParseDecimal(string? text, out decimal value)
    {
        value = 0;
        var compact = (text ?? string.Empty).Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (compact.Length == 0 || !compact.All(c => char.IsAsciiDigit(c) || c is '.' or ','))
        {
            return false;
        }

        var lastSeparator = compact.LastIndexOfAny(['.', ',']);
        string integerPart = compact, fraction = string.Empty;
        if (lastSeparator >= 0)
        {
            var separator = compact[lastSeparator];
            var other = separator == '.' ? ',' : '.';
            var mixed = compact.IndexOf(other) >= 0;
            var single = compact.IndexOf(separator) == lastSeparator;
            var tail = compact[(lastSeparator + 1)..];
            var head = compact[..lastSeparator];

            // "0,500", ",5" o "4150,257" no pueden ser miles: el grupo entero sería "0", vacío o de más de 3 dígitos.
            var thousands = !mixed && tail.Length == 3 && head.Length is > 0 and <= 3 && head != "0";
            if (mixed || (single && !thousands))
            {
                if (mixed && head.IndexOf(separator) >= 0)
                {
                    return false;
                }

                integerPart = head.Length == 0 ? "0" : head;
                fraction = tail;
                if (fraction.Length == 0)
                {
                    return false;
                }
            }
        }

        if (!TryParseGroupedInteger(integerPart, out var integerDigits))
        {
            return false;
        }

        var normalized = fraction.Length == 0 ? integerDigits : $"{integerDigits}.{fraction}";
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Texto para un campo de edición: sin símbolo, con miles y los decimales de la moneda.</summary>
    public static string FormatForInput(Money money) =>
        money.ToDecimal().ToString("N" + money.Currency.Scale(), Colombia);

    /// <summary>Texto para editar un número decimal (p. ej. una tasa), sin ceros de más a la derecha.</summary>
    public static string FormatDecimal(decimal value, int minDecimals = 0)
    {
        var text = value.ToString("#,##0." + new string('0', minDecimals) + new string('#', 28 - minDecimals), Colombia);
        return text.TrimEnd(',');
    }

    // "1.234.567": el primer grupo tiene 1–3 dígitos y los demás exactamente 3; un solo tipo de separador.
    private static bool TryParseGroupedInteger(string text, out string digits)
    {
        digits = string.Empty;
        var separators = text.Where(c => c is '.' or ',').Distinct().ToList();
        if (separators.Count > 1)
        {
            return false;
        }

        var groups = separators.Count == 0 ? [text] : text.Split(separators[0]);
        if (groups.Any(g => g.Length == 0)
            || (groups.Length > 1 && (groups[0].Length > 3 || groups.Skip(1).Any(g => g.Length != 3))))
        {
            return false;
        }

        digits = string.Concat(groups).TrimStart('0');
        if (digits.Length == 0)
        {
            digits = "0";
        }

        return digits.Length <= MaxIntegerDigits;
    }

    private static string StripCurrency(string? text)
    {
        var result = (text ?? string.Empty).Trim();
        foreach (var token in new[] { "US$", "USD", "COP", "$" })
        {
            result = result.Replace(token, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result.Trim();
    }
}
