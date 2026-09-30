namespace PiRadar.Core.Domain;

/// <summary>
/// Monto en unidades menores de su moneda (COP escala 0, USD escala 2). Nunca usar double/float para dinero.
/// </summary>
public readonly record struct Money(long AmountMinor, Currency Currency)
{
    public static Money Zero(Currency currency) => new(0, currency);

    /// <summary>Crea un monto a partir de un valor decimal; falla si tiene más decimales de los que admite la moneda.</summary>
    public static Money FromDecimal(decimal amount, Currency currency)
    {
        var minor = amount * Pow10(currency.Scale());
        if (minor != decimal.Truncate(minor))
        {
            throw new ArgumentException(
                AppLocale.Pick(
                    $"{amount} tiene más de {currency.Scale()} decimales para {currency}.",
                    $"{amount} has more than {currency.Scale()} decimals for {currency}."),
                nameof(amount));
        }

        return new Money(checked((long)minor), currency);
    }

    public decimal ToDecimal() => AmountMinor / Pow10(Currency.Scale());

    public bool IsZero => AmountMinor == 0;

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left with { AmountMinor = checked(left.AmountMinor + right.AmountMinor) };
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left with { AmountMinor = checked(left.AmountMinor - right.AmountMinor) };
    }

    public static Money operator -(Money money) => money with { AmountMinor = checked(-money.AmountMinor) };

    /// <summary>
    /// Formato del idioma vigente. Español: <c>$ 1.234.567</c> (COP) y <c>US$ 12,50</c> (USD). Inglés:
    /// <c>$12.50</c> (USD) y <c>COP $1,234,567</c> (COP), con el signo delante del símbolo.
    /// </summary>
    public override string ToString()
    {
        var scale = Currency.Scale();
        var number = Math.Abs(ToDecimal()).ToString("N" + scale, AppLocale.Culture);
        var sign = AmountMinor < 0 ? "-" : string.Empty;
        var separator = AppLocale.IsEnglish ? string.Empty : " ";
        return $"{sign}{Currency.Symbol()}{separator}{number}";
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException(AppLocale.Pick(
                $"No se pueden operar montos en {left.Currency} y {right.Currency}.",
                $"Cannot combine amounts in {left.Currency} and {right.Currency}."));
        }
    }

    private static decimal Pow10(int exponent) => exponent switch
    {
        0 => 1m,
        2 => 100m,
        _ => (decimal)Math.Pow(10, exponent),
    };
}
