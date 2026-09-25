namespace PiRadar.Core.Receipts;

/// <summary>NIT colombiano (DIAN): número base y dígito de verificación.</summary>
public static class TaxId
{
    private static readonly int[] Weights = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71];

    /// <summary>Dígito de verificación DIAN (módulo 11) de un NIT sin DV.</summary>
    public static int CheckDigit(string baseNumber)
    {
        if (baseNumber.Length is 0 or > 15 || !baseNumber.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("El NIT debe tener entre 1 y 15 dígitos.", nameof(baseNumber));
        }

        var sum = 0;
        for (var i = 0; i < baseNumber.Length; i++)
        {
            sum += (baseNumber[^(i + 1)] - '0') * Weights[i];
        }

        var r = sum % 11;
        return r > 1 ? 11 - r : r;
    }

    /// <summary>Formato normalizado <c>900319753-3</c>.</summary>
    public static string Format(string baseNumber, int checkDigit) => $"{baseNumber}-{checkDigit}";
}
