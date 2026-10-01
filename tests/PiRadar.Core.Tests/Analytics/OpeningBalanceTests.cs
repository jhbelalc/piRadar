using PiRadar.Core.Analytics;
using PiRadar.Core.Domain;

namespace PiRadar.Core.Tests.Analytics;

public class OpeningBalanceTests
{
    private static Transaction New(TransactionType type, long minor, Currency currency, DateTime at) =>
        new() { Type = type, Amount = new Money(minor, currency), CategoryId = 1, OccurredAt = at };

    private static readonly DateTime Sep = new(2026, 9, 15);
    private static readonly DateTime Aug = new(2026, 8, 20);

    [Fact]
    public void ByCurrency_IsIncomeMinusExpensesOfEverythingBefore()
    {
        var before = new[]
        {
            New(TransactionType.Income, 6_800_000, Currency.COP, Aug),
            New(TransactionType.Expense, 1_850_000, Currency.COP, Aug),
            New(TransactionType.Expense, 5_397_400, Currency.COP, Sep),
            New(TransactionType.Income, 500_00, Currency.USD, Sep),
        };

        var openings = OpeningBalance.ByCurrency(before);

        Assert.Collection(openings,
            cop =>
            {
                Assert.Equal(new Money(-447_400, Currency.COP), cop.Amount);
                Assert.Equal(3, cop.TransactionCount);
            },
            usd => Assert.Equal(new Money(500_00, Currency.USD), usd.Amount));
    }

    [Fact]
    public void ByCurrency_IsEmptyWithoutPreviousTransactions() => Assert.Empty(OpeningBalance.ByCurrency([]));

    [Fact]
    public void Closing_balance_of_a_month_is_the_opening_of_the_next()
    {
        // Septiembre: ingresos 100, gastos 40 => cierra en 60 (sin saldo previo).
        var september = new[]
        {
            New(TransactionType.Income, 100_00, Currency.USD, Sep),
            New(TransactionType.Expense, 40_00, Currency.USD, Sep),
        };
        var septemberTotals = CurrencyTotals.ByCurrency(september, OpeningBalance.ByCurrency([])).Single();
        Assert.False(septemberTotals.HasOpening);
        Assert.Equal(new Money(60_00, Currency.USD), septemberTotals.Balance);

        // Octubre arranca con lo que quedó y suma lo suyo (gasto de 10): cierra en 50.
        var october = new[] { New(TransactionType.Expense, 10_00, Currency.USD, new DateTime(2026, 10, 3)) };
        var octoberTotals = CurrencyTotals.ByCurrency(october, OpeningBalance.ByCurrency(september)).Single();
        Assert.True(octoberTotals.HasOpening);
        Assert.Equal(septemberTotals.Balance, octoberTotals.Opening);
        Assert.Equal(new Money(10_00, Currency.USD), octoberTotals.Expenses);
        Assert.Equal(Money.Zero(Currency.USD), octoberTotals.Income);
        Assert.Equal(new Money(50_00, Currency.USD), octoberTotals.Balance);

        // Noviembre sin movimientos: sigue mostrando el saldo heredado (no sale en ceros).
        var novemberTotals = CurrencyTotals.ByCurrency(
            [], OpeningBalance.ByCurrency(september.Concat(october))).Single();
        Assert.Equal(new Money(50_00, Currency.USD), novemberTotals.Opening);
        Assert.Equal(new Money(50_00, Currency.USD), novemberTotals.Balance);
        Assert.Equal(Money.Zero(Currency.USD), novemberTotals.Expenses);
    }

    [Fact]
    public void Opening_does_not_count_as_income_or_expense()
    {
        var totals = CurrencyTotals.ByCurrency(
            [New(TransactionType.Income, 200_000, Currency.COP, Sep)],
            [new OpeningBalance(new Money(1_000_000, Currency.COP), 5)]).Single();

        Assert.Equal(new Money(200_000, Currency.COP), totals.Income);
        Assert.Equal(Money.Zero(Currency.COP), totals.Expenses);
        Assert.Equal(new Money(1_200_000, Currency.COP), totals.Balance);
    }

    [Fact]
    public void Negative_opening_reduces_the_balance()
    {
        var totals = CurrencyTotals.ByCurrency(
            [New(TransactionType.Income, 300_000, Currency.COP, Sep)],
            [new OpeningBalance(new Money(-120_000, Currency.COP), 2)]).Single();

        Assert.Equal(new Money(180_000, Currency.COP), totals.Balance);
    }

    [Fact]
    public void Currencies_only_in_the_opening_still_show_up_in_order()
    {
        var totals = CurrencyTotals.ByCurrency(
            [New(TransactionType.Expense, 5_000, Currency.COP, Sep)],
            [new OpeningBalance(new Money(75_00, Currency.USD), 1)]);

        Assert.Collection(totals,
            cop => Assert.False(cop.HasOpening),
            usd =>
            {
                Assert.Equal(Currency.USD, usd.Currency);
                Assert.Equal(new Money(75_00, Currency.USD), usd.Balance);
                Assert.True(usd.HasOpening);
            });
    }

    [Fact]
    public void An_opening_without_transactions_is_ignored()
    {
        Assert.Empty(CurrencyTotals.ByCurrency([], [new OpeningBalance(Money.Zero(Currency.COP), 0)]));
    }

    [Fact]
    public void Balance_without_opening_is_unchanged()
    {
        var totals = CurrencyTotals.ByCurrency(
            [New(TransactionType.Income, 100, Currency.USD, Sep), New(TransactionType.Expense, 30, Currency.USD, Sep)]).Single();

        Assert.False(totals.HasOpening);
        Assert.Equal(new Money(70, Currency.USD), totals.Balance);
    }
}
