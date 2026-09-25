using PiRadar.Core.Domain;
using PiRadar.Data.Repositories;

namespace PiRadar.Data.Tests.Repositories;

public class ExchangeRateRepositoryTests
{
    [Fact]
    public async Task Upsert_SameDay_ReplacesRate_AndIgnoresTime()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new ExchangeRateRepository(temp.Host);

        var first = await repo.UpsertAsync(Rate(4100.5m, new DateTime(2026, 9, 1, 8, 30, 0, DateTimeKind.Local)));
        var second = await repo.UpsertAsync(Rate(4150m, new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Local)));

        var all = await repo.GetAllAsync();
        Assert.Equal(first.Id, second.Id);
        var only = Assert.Single(all);
        Assert.Equal(4150m, only.Rate);
        Assert.Equal(new DateTime(2026, 9, 1), only.EffectiveDate);
        Assert.Equal(DateTimeKind.Local, only.EffectiveDate.Kind);
    }

    [Fact]
    public async Task GetAll_OrdersByPairThenNewestFirst()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new ExchangeRateRepository(temp.Host);
        await repo.UpsertAsync(Rate(4000m, new DateTime(2026, 8, 1)));
        await repo.UpsertAsync(Rate(4200m, new DateTime(2026, 9, 1)));
        var reverse = Rate(0.00025m, new DateTime(2026, 9, 1));
        (reverse.From, reverse.To) = (Currency.COP, Currency.USD);
        await repo.UpsertAsync(reverse);

        var all = await repo.GetAllAsync();

        Assert.Equal([0.00025m, 4200m, 4000m], all.Select(r => r.Rate));
    }

    [Fact]
    public async Task Upsert_RejectsSameCurrencyOrNonPositiveRate()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new ExchangeRateRepository(temp.Host);
        var same = Rate(1m, DateTime.Today);
        same.To = Currency.USD;

        await Assert.ThrowsAsync<DomainValidationException>(() => repo.UpsertAsync(same));
        await Assert.ThrowsAsync<DomainValidationException>(() => repo.UpsertAsync(Rate(0m, DateTime.Today)));
    }

    [Fact]
    public async Task Delete_RemovesRate()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new ExchangeRateRepository(temp.Host);
        var saved = await repo.UpsertAsync(Rate(4100m, DateTime.Today));

        Assert.True(await repo.DeleteAsync(saved.Id));
        Assert.False(await repo.DeleteAsync(saved.Id));
        Assert.Empty(await repo.GetAllAsync());
    }

    private static ExchangeRate Rate(decimal rate, DateTime date) =>
        new() { From = Currency.USD, To = Currency.COP, Rate = rate, EffectiveDate = date };
}
