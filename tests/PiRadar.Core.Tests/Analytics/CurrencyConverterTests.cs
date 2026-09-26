using PiRadar.Core.Analytics;
using PiRadar.Core.Domain;

namespace PiRadar.Core.Tests.Analytics;

public class CurrencyConverterTests
{
    private static readonly CurrencyConverter Converter = new(
    [
        Rate(Currency.USD, Currency.COP, 4000m, new DateTime(2026, 9, 1)),
        Rate(Currency.USD, Currency.COP, 3900m, new DateTime(2026, 9, 15)),
    ]);

    [Fact]
    public void Convert_UsesLatestRateOnOrBeforeDate()
    {
        Assert.Equal(new Money(200000, Currency.COP), Converter.Convert(new Money(5000, Currency.USD), Currency.COP, new DateTime(2026, 9, 14, 23, 0, 0)));
        Assert.Equal(new Money(195000, Currency.COP), Converter.Convert(new Money(5000, Currency.USD), Currency.COP, new DateTime(2026, 9, 15, 8, 0, 0)));
    }

    [Fact]
    public void Convert_RoundsToTargetScale()
    {
        // US$ 49,99 × 3.900 = 194.961; COP 82.150 / 3.900 = US$ 21,064… → 21,06.
        Assert.Equal(new Money(194961, Currency.COP), Converter.Convert(new Money(4999, Currency.USD), Currency.COP, new DateTime(2026, 9, 20)));
        Assert.Equal(new Money(2106, Currency.USD), Converter.Convert(new Money(82150, Currency.COP), Currency.USD, new DateTime(2026, 9, 20)));
    }

    [Fact]
    public void Convert_WithoutRateForThatDate_IsNull()
    {
        Assert.Null(Converter.Convert(new Money(100, Currency.USD), Currency.COP, new DateTime(2026, 8, 31)));
    }

    [Fact]
    public void Convert_SameCurrency_IsUnchanged()
    {
        Assert.Equal(new Money(100, Currency.USD), new CurrencyConverter([]).Convert(new Money(100, Currency.USD), Currency.USD, DateTime.Today));
    }

    [Fact]
    public void RateAt_PrefersNewestOfDirectAndInverse()
    {
        var converter = new CurrencyConverter(
        [
            Rate(Currency.USD, Currency.COP, 4000m, new DateTime(2026, 9, 1)),
            Rate(Currency.COP, Currency.USD, 0.00025m, new DateTime(2026, 9, 10)), // 1 / 4.000
            Rate(Currency.USD, Currency.COP, 3800m, new DateTime(2026, 9, 10)),
        ]);

        Assert.Equal(4000m, converter.RateAt(Currency.USD, Currency.COP, new DateTime(2026, 9, 5)));
        Assert.Equal(3800m, converter.RateAt(Currency.USD, Currency.COP, new DateTime(2026, 9, 12)));
        Assert.Equal(0.00025m, converter.RateAt(Currency.COP, Currency.USD, new DateTime(2026, 9, 12)));
    }

    [Fact]
    public void Consolidate_SeparatesTransactionsWithoutRate()
    {
        Transaction[] transactions =
        [
            New(new Money(82150, Currency.COP), new DateTime(2026, 9, 20)),
            New(new Money(5000, Currency.USD), new DateTime(2026, 9, 20)),
            New(new Money(1000, Currency.USD), new DateTime(2026, 8, 20)),
        ];

        var result = Converter.Consolidate(transactions, Currency.COP);

        Assert.Equal([new Money(82150, Currency.COP), new Money(195000, Currency.COP)], result.Converted.Select(t => t.Amount));
        Assert.Equal(new Money(1000, Currency.USD), Assert.Single(result.Missing).Amount);
        Assert.Equal(new Money(5000, Currency.USD), transactions[1].Amount); // no modifica los originales
    }

    private static ExchangeRate Rate(Currency from, Currency to, decimal rate, DateTime date) =>
        new() { From = from, To = to, Rate = rate, EffectiveDate = date };

    private static Transaction New(Money amount, DateTime occurredAt) =>
        new() { Type = TransactionType.Expense, Amount = amount, CategoryId = 1, OccurredAt = occurredAt };
}
