using System.Globalization;
using System.Text;

namespace PiRadar.Core.Receipts;

/// <summary>Normaliza texto de OCR para comparar palabras clave sin depender de tildes, mayúsculas ni espacios.</summary>
internal static class TextNormalizer
{
    /// <summary>Mayúsculas sin tildes: "Electrónica" → "ELECTRONICA", "İ" → "I".</summary>
    public static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c switch
                {
                    'ı' => 'I',
                    'ß' => 'S',
                    _ => char.ToUpperInvariant(c),
                });
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Solo letras, sin espacios ni signos y con los dígitos confundibles pasados a letra ("T0TAL A PA GAR" → "TOTALAPAGAR").
    /// Sirve para buscar palabras clave; no para leer números.
    /// </summary>
    public static string Compact(string text)
    {
        var folded = Fold(text);
        var sb = new StringBuilder(folded.Length);
        foreach (var c in folded)
        {
            var letter = c switch
            {
                '0' => 'O',
                '1' => 'I',
                '5' => 'S',
                _ => c,
            };
            if (letter is >= 'A' and <= 'Z')
            {
                sb.Append(letter);
            }
        }

        return sb.ToString();
    }

    /// <summary>¿Contiene <paramref name="text"/> alguna de las palabras clave (en forma compacta)?</summary>
    public static bool ContainsAny(string compactText, params ReadOnlySpan<string> compactKeywords)
    {
        foreach (var keyword in compactKeywords)
        {
            if (compactText.Contains(keyword, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Como <see cref="ContainsAny"/>, pero tolera una letra de diferencia en palabras clave de 6+ letras
    /// ("RECEIP" por "RECEIPT", "CULNTANOS" por "CUENTANOS"). Ambos textos en forma compacta.
    /// </summary>
    public static bool ContainsApprox(string compactText, params ReadOnlySpan<string> compactKeywords)
    {
        foreach (var keyword in compactKeywords)
        {
            if (compactText.Contains(keyword, StringComparison.Ordinal))
            {
                return true;
            }

            if (keyword.Length < 6)
            {
                continue;
            }

            for (var length = keyword.Length - 1; length <= keyword.Length + 1; length++)
            {
                for (var start = 0; start + length <= compactText.Length; start++)
                {
                    if (EditDistanceAtMostOne(compactText.AsSpan(start, length), keyword))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>¿Se puede pasar de <paramref name="a"/> a <paramref name="b"/> con a lo sumo una edición?</summary>
    public static bool EditDistanceAtMostOne(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        if (Math.Abs(a.Length - b.Length) > 1)
        {
            return false;
        }

        var i = 0;
        while (i < a.Length && i < b.Length && a[i] == b[i])
        {
            i++;
        }

        if (i == a.Length || i == b.Length)
        {
            return true;
        }

        return a.Length == b.Length ? a[(i + 1)..].SequenceEqual(b[(i + 1)..])
            : a.Length > b.Length ? a[(i + 1)..].SequenceEqual(b[i..])
            : a[i..].SequenceEqual(b[(i + 1)..]);
    }
}
