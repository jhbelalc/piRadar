using PiRadar.Core.Domain;
using PiRadar.Data.Repositories;

namespace PiRadar.Data.Tests.Repositories;

public class SettingsRepositoryTests
{
    [Fact]
    public async Task BaseCurrency_DefaultsToSeed_AndCanChange()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new SettingsRepository(temp.Host);

        Assert.Equal(Currency.COP, await repo.GetBaseCurrencyAsync());
        await repo.SetBaseCurrencyAsync(Currency.USD);
        Assert.Equal(Currency.USD, await repo.GetBaseCurrencyAsync());
        Assert.Equal("USD", await repo.GetAsync(AppSettingKeys.BaseCurrency));
    }

    [Fact]
    public async Task BaseCurrency_InvalidStoredValue_FallsBackToCop()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new SettingsRepository(temp.Host);

        await repo.SetAsync(AppSettingKeys.BaseCurrency, "1");

        Assert.Equal(Currency.COP, await repo.GetBaseCurrencyAsync());
    }

    [Fact]
    public async Task LastBackupAt_IsNullUntilSet_ThenRoundTripsAsUtc()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new SettingsRepository(temp.Host);
        var when = new DateTime(2026, 9, 24, 3, 15, 30, 123, DateTimeKind.Utc);

        Assert.Null(await repo.GetLastBackupAtAsync());
        await repo.SetLastBackupAtAsync(when);

        var loaded = await repo.GetLastBackupAtAsync();
        Assert.Equal(when, loaded);
        Assert.Equal(DateTimeKind.Utc, loaded!.Value.Kind);
    }

    [Fact]
    public async Task LastBackupAt_ConvertsLocalToUtc()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new SettingsRepository(temp.Host);
        var local = new DateTime(2026, 9, 23, 22, 0, 0, DateTimeKind.Local);

        await repo.SetLastBackupAtAsync(local);

        Assert.Equal(local.ToUniversalTime(), await repo.GetLastBackupAtAsync());
    }

    [Fact]
    public async Task GetUnknownKey_ReturnsNull()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new SettingsRepository(temp.Host);

        Assert.Null(await repo.GetAsync("missing"));
    }
}
