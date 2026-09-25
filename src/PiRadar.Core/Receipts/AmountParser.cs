using System.Globalization;
using System.Text.RegularExpressions;

namespace PiRadar.Core.Receipts;

/// <summary>Monto encontrado en una fila. <see cref="Start"/> es la posición en el texto de la fila.</summary>
internal readonly record struct AmountToken(decimal Value, int Start, int Length, bool HasCurrencySymbol, bool HasDecimals);

/// <summary>
/// Encuentra montos en una fila de texto. Acepta miles con punto, coma o espacio ("63.500", "47,700", "6 250"),
/// decimales de 1–2 dígitos ("1.234,56", "8,580.00") y sin separador ("50900").
/// Regla: un separador seguido de exactamente 3 dígitos es de miles; seguido de 1–2 dígitos al final, decimal.
/// </summary>
internal static partial class AmountParser
{
    /// <summary>Más dígitos que esto sin separadores es un teléfono, código o consecutivo, no un monto.</summary>
    private const int MaxPlainDigits = 7;

    public static IReadOnlyList<AmountToken> Find(string row)
    {
        var text = FixLetterO(row);
        var result = new List<AmountToken>();
        foreach (Match m in Amount().Matches(text))
        {
            var number = m.Groups["num"].Value;
            if (TryParseNumber(number, out var value, out var hasDecimals))
            {
                result.Add(new AmountToken(value, m.Index, m.Length, m.Groups["cur"].Success, hasDecimals));
            }
        }

        return result;
    }

    /// <summary>Interpreta un número suelto ("82.150", "1.234,56"). Devuelve <c>false</c> si no parece un monto.</summary>
    public static bool TryParseNumber(string number, out decimal value, out bool hasDecimals)
    {
        value = 0;
        hasDecimals = false;
        var compact = number.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (compact.Length == 0 || !char.IsAsciiDigit(compact[0]) || !char.IsAsciiDigit(compact[^1]))
        {
            return false;
        }

        var lastSeparator = compact.LastIndexOfAny(['.', ',']);
        string integerPart, fraction = string.Empty;

        // Separadores mezclados ("14,900.000", un cero de más del OCR en "14,900.00"): el último es el decimal.
        var mixed = lastSeparator > 0 && compact[..lastSeparator].IndexOf(compact[lastSeparator] == '.' ? ',' : '.') >= 0;
        if (mixed)
        {
            integerPart = compact[..lastSeparator];
            fraction = compact[(lastSeparator + 1)..].TrimEnd('0');
            fraction = fraction.Length > 2 ? fraction[..2] : fraction;
            hasDecimals = true;
        }
        else if (lastSeparator >= 0 && compact.Length - lastSeparator - 1 is 1 or 2)
        {
            integerPart = compact[..lastSeparator];
            fraction = compact[(lastSeparator + 1)..];
            hasDecimals = true;
        }
        else
        {
            integerPart = compact;
        }

        var hadGroupSeparators = integerPart.IndexOfAny(['.', ',']) >= 0 || number.Contains(' ', StringComparison.Ordinal);
        var digits = integerPart.Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal);
        if (digits.Length == 0 || digits.Length > 12 || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        // Códigos, teléfonos y consecutivos: muchos dígitos sin separadores, o ceros a la izquierda.
        if (!hadGroupSeparators && (digits.Length > MaxPlainDigits || (digits.Length > 1 && digits[0] == '0')))
        {
            return false;
        }

        value = decimal.Parse(digits, CultureInfo.InvariantCulture);
        if (fraction.Length > 0)
        {
            value += decimal.Parse(fraction, CultureInfo.InvariantCulture) / (fraction.Length == 1 ? 10m : 100m);
        }

        return true;
    }

    /// <summary>Corrige la O leída en lugar de 0 dentro de números: "8O.15O" → "80.150".</summary>
    private static string FixLetterO(string row)
    {
        // Se repite porque cada reemplazo habilita el siguiente ("28.OOO" → "28.0OO" → "28.00O" → "28.000").
        for (var i = 0; i < 12; i++)
        {
            var fixedRow = LetterOInNumber().Replace(row, "0");
            if (fixedRow == row)
            {
                break;
            }

            row = fixedRow;
        }

        return row;
    }

    // Separador de miles: punto o coma (con un espacio opcional tras él, "$82. 150") o un espacio.
    // No debe empezar pegado a letras o dígitos (códigos como "RJ49175108"), ni terminar pegado a letras o a otro número.
    [GeneratedRegex("""
        (?<![\p{L}\d.,])
        (?<cur>US\$|U\$S|\$|COP\b|USD\b)?\s?
        (?<num>\d{1,3}(?:(?:[.,]\ ?|\ )\d{3})+(?:[.,]\d{1,2})?|\d+(?:[.,]\d{1,2})?)
        (?![\p{L}\d]|[.,]\d)
        """, RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase)]
    private static partial Regex Amount();

    [GeneratedRegex(@"(?<=\d[.,]?)[Oo](?=[\d.,\sOo]|$)|(?<=[\s$]|^)[Oo](?=[.,]?\d)")]
    private static partial Regex LetterOInNumber();
}
