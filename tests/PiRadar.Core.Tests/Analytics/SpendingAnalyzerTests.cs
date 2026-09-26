using PiRadar.Core.Analytics;
using PiRadar.Core.Domain;

namespace PiRadar.Core.Tests.Analytics;

public class SpendingAnalyzerTests
{
    private static readonly DateTime Today = new(2026, 9, 26);

    [Theory]
    [InlineData(PeriodGranularity.Day, 24, "0h")]
    [InlineData(PeriodGranularity.Week, 7, "lu")]
    [InlineData(PeriodGranularity.Month, 30, "1")]
    [InlineData(PeriodGranularity.Year, 12, "ene")]
    public void Buckets_CoverThePeriod(PeriodGranularity granularity, int count, string firstLabel)
    {
        var period = Period.Containing(Today, granularity);

        var buckets = SpendingAnalyzer.Buckets(period);

        Assert.Equal(count, buckets.Count);
        Assert.Equal(firstLabel, buckets[0].Label);
        Assert.Equal(period.Start, buckets[0].Start);
        Assert.Equal(period.End, buckets[^1].End);
        Assert.All(buckets.Zip(buckets.Skip(1)), p => Assert.Equal(p.First.End, p.Second.Start));
    }

    [Fact]
    public void Buckets_OfMonth_LabelOnlyEveryFiveDays()
    {
        var labels = SpendingAnalyzer.Buckets(Period.Containing(Today, PeriodGranularity.Month)).Select(b => b.Label).Where(l => l.Length > 0);

        Assert.Equal(["1", "5", "10", "15", "20", "25", "30"], labels);
    }

    [Fact]
    public void Series_SumsPerBucketAndCurrency()
    {
        var week = Period.Containing(Today, PeriodGranularity.Week); // 21–27 sep
        Transaction[] transactions =
        [
            New(TransactionType.Expense, 50000, new DateTime(2026, 9, 21, 8, 0, 0)),
            New(TransactionType.Expense, 25000, new DateTime(2026, 9, 21, 20, 0, 0)),
            New(TransactionType.Income, 3000000, new DateTime(2026, 9, 25)),
            New(TransactionType.Expense, 999, new DateTime(2026, 9, 21), Currency.USD),
            New(TransactionType.Expense, 70000, new DateTime(2026, 9, 28)), // semana siguiente
        ];

        var series = SpendingAnalyzer.Series(transactions, week, Currency.COP);

        Assert.Equal(7, series.Count);
        Assert.Equal(new Money(75000, Currency.COP), series[0].Expenses);
        Assert.Equal(new Money(3000000, Currency.COP), series[4].Income);
        Assert.Equal(75000, series.Sum(b => b.Expenses.AmountMinor));
    }

    [Fact]
    public void ByPaymentMethod_SortsDescending_WithoutMethodLast()
    {
        Transaction[] transactions =
        [
            New(TransactionType.Expense, 10000, Today, paymentMethodId: 1),
            New(TransactionType.Expense, 50000, Today, paymentMethodId: null),
            New(TransactionType.Expense, 40000, Today, paymentMethodId: 2),
            New(TransactionType.Income, 90000, Today, paymentMethodId: 1),
        ];

        var shares = SpendingAnalyzer.ByPaymentMethod(transactions, TransactionType.Expense, Currency.COP);

        Assert.Equal([2, 1, null], shares.Select(s => s.PaymentMethodId));
        Assert.Equal([0.4m, 0.1m, 0.5m], shares.Select(s => s.Share));
    }

    [Fact]
    public void DailyAverage_CountsElapsedDaysOfCurrentPeriod()
    {
        var september = Period.Containing(Today, PeriodGranularity.Month);

        Assert.Equal(26, SpendingAnalyzer.ElapsedDays(september, Today));
        Assert.Equal(31, SpendingAnalyzer.ElapsedDays(september.Previous(), Today));
        Assert.Equal(0, SpendingAnalyzer.ElapsedDays(september.Next(), Today));
        Assert.Equal(new Money(18358, Currency.COP), SpendingAnalyzer.DailyAverage(new Money(477300, Currency.COP), september, Today));
        Assert.Null(SpendingAnalyzer.DailyAverage(new Money(1, Currency.COP), september.Next(), Today));
    }

    private static Transaction New(TransactionType type, long minor, DateTime occurredAt, Currency currency = Currency.COP,
        int? paymentMethodId = null) =>
        new() { Type = type, Amount = new Money(minor, currency), CategoryId = 1, OccurredAt = occurredAt, PaymentMethodId = paymentMethodId };
}
