namespace PiRadar.Core.Domain;

/// <summary>Monedas soportadas. El nombre coincide con el código ISO 4217 y es lo que se guarda en la BD.</summary>
public enum Currency
{
    COP,
    USD,
}

public static class CurrencyExtensions
{
    /// <summary>Decimales que se guardan en <c>AmountMinor</c>: COP no usa centavos en la práctica.</summary>
    public static int Scale(this Currency currency) => currency switch
    {
        Currency.COP => 0,
        Currency.USD => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, null),
    };

    public static string Symbol(this Currency currency) => currency switch
    {
        Currency.COP => "$",
        Currency.USD => "US$",
        _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, null),
    };

    // Enum.TryParse no sirve: acepta valores numéricos como "1".
    public static Currency ParseCode(string code) => code switch
    {
        "COP" => Currency.COP,
        "USD" => Currency.USD,
        _ => throw new FormatException($"Código de moneda no soportado: '{code}'."),
    };
}
