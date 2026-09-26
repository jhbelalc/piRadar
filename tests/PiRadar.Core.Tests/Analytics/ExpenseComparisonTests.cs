using PiRadar.Core.Analytics;
using PiRadar.Core.Domain;

namespace PiRadar.Core.Tests.Analytics;

public class ExpenseComparisonTests
{
    private static readonly DateTime Now = new(2026, 9, 26, 10, 0, 0);
    private static readonly Period September = Period.Containing(Now, PeriodGranularity.Month);

    [Fact]
    public void PreviousSpan_CoversSameDaysOfPreviousMonth()
    {
        Assert.Equal((new DateTime(2026, 8, 1), new DateTime(2026, 8, 27)), ExpenseComparison.PreviousSpan(September, Now));
    }

    [Fact]
    public void PreviousSpan_IsCappedAtShorterPreviousMonth()
    {
        var march = Period.Containing(new DateTime(2026, 3, 31), PeriodGranularity.Month);

        Assert.Equal((new DateTime(2026, 2, 1), new DateTime(2026, 3, 1)),
            ExpenseComparison.PreviousSpan(march, new DateTime(2026, 3, 31, 12, 0, 0)));
    }

    [Fact]
    public void PreviousSpan_OfFinishedPeriod_IsWholePreviousPeriod()
    {
        var august = September.Previous();

        Assert.Equal((new DateTime(2026, 7, 1), new DateTime(2026, 8, 1)), ExpenseComparison.PreviousSpan(august, Now));
    }

    [Fact]
    public void ByCurrency_ComparesOnlyTheEquivalentSpan()
    {
        Transaction[] transactions =
        [
            Expense(new DateTime(2026, 9, 3), 50000),
            Expense(new DateTime(2026, 9, 26, 9, 0, 0), 25000),
            Expense(new DateTime(2026, 8, 10), 60000),
            Expense(new DateTime(2026, 8, 26, 23, 59, 0), 40000),
            Expense(new DateTime(2026, 8, 27), 900000),        // fuera del tramo: después del 26 de agosto
            Expense(new DateTime(2026, 9, 5), 1250, Currency.USD),
            new Transaction { Type = TransactionType.Income, Amount = new Money(3000000, Currency.COP), OccurredAt = new DateTime(2026, 9, 1) },
        ];

        var comparisons = ExpenseComparison.ByCurrency(transactions, September, Now);

        Assert.Collection(comparisons,
            cop =>
            {
                Assert.Equal(new Money(75000, Currency.COP), cop.Current);
                Assert.Equal(new Money(100000, Currency.COP), cop.Previous);
                Assert.Equal(-0.25m, cop.Change);
            },
            usd =>
            {
                Assert.Equal(new Money(1250, Currency.USD), usd.Current);
                Assert.True(usd.Previous.IsZero);
                Assert.Null(usd.Change);
            });
    }

    [Fact]
    public void ByCurrency_IsEmptyWithoutExpenses()
    {
        Assert.Empty(ExpenseComparison.ByCurrency([], September, Now));
    }

    private static Transaction Expense(DateTime occurredAt, long minor, Currency currency = Currency.COP) =>
        new() { Type = TransactionType.Expense, Amount = new Money(minor, currency), CategoryId = 1, OccurredAt = occurredAt };
}
