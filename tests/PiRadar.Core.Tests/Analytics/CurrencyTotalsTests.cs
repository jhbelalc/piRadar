using PiRadar.Core.Analytics;
using PiRadar.Core.Domain;

namespace PiRadar.Core.Tests.Analytics;

public class CurrencyTotalsTests
{
    [Fact]
    public void ByCurrency_SeparatesCurrenciesAndComputesBalance()
    {
        Transaction[] transactions =
        [
            New(TransactionType.Expense, 82150, Currency.COP),
            New(TransactionType.Income, 1250, Currency.USD),
            New(TransactionType.Expense, 17850, Currency.COP),
            New(TransactionType.Income, 3000000, Currency.COP),
        ];

        var totals = CurrencyTotals.ByCurrency(transactions);

        Assert.Collection(totals,
            cop =>
            {
                Assert.Equal(new Money(100000, Currency.COP), cop.Expenses);
                Assert.Equal(new Money(3000000, Currency.COP), cop.Income);
                Assert.Equal(new Money(2900000, Currency.COP), cop.Balance);
            },
            usd =>
            {
                Assert.Equal(Money.Zero(Currency.USD), usd.Expenses);
                Assert.Equal(new Money(1250, Currency.USD), usd.Balance);
            });
    }

    [Fact]
    public void ByCurrency_IsEmptyWithoutTransactions()
    {
        Assert.Empty(CurrencyTotals.ByCurrency([]));
    }

    private static Transaction New(TransactionType type, long minor, Currency currency) =>
        new() { Type = type, Amount = new Money(minor, currency), CategoryId = 1, OccurredAt = new DateTime(2026, 9, 1) };
}
