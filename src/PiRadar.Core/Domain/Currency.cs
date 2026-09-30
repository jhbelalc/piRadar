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

    /// <summary>Símbolo según el idioma: en español el peso es "$" y el dólar "US$"; en inglés el dólar es "$" y el peso "COP $".</summary>
    public static string Symbol(this Currency currency) => currency switch
    {
        Currency.COP => AppLocale.IsEnglish ? "COP $" : "$",
        Currency.USD => AppLocale.IsEnglish ? "$" : "US$",
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
