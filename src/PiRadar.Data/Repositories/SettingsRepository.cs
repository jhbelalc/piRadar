using System.Globalization;
using PiRadar.Core.Abstractions;
using PiRadar.Core.Domain;
using PiRadar.Data.Records;

namespace PiRadar.Data.Repositories;

internal sealed class SettingsRepository(DatabaseHost host) : RepositoryBase(host), ISettingsRepository
{
    public async Task<string?> GetAsync(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return (await Connection.FindAsync<AppSettingRecord>(key))?.Value;
    }

    public async Task SetAsync(string key, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);
        await Connection.InsertOrReplaceAsync(new AppSettingRecord { Key = key, Value = value });
    }

    public async Task<Currency> GetBaseCurrencyAsync()
    {
        var code = await GetAsync(AppSettingKeys.BaseCurrency);
        try
        {
            return code is null ? Currency.COP : CurrencyExtensions.ParseCode(code);
        }
        catch (FormatException)
        {
            return Currency.COP;
        }
    }

    public Task SetBaseCurrencyAsync(Currency currency) =>
        SetAsync(AppSettingKeys.BaseCurrency, currency.ToString());

    public async Task<DateTime?> GetLastBackupAtAsync()
    {
        var value = await GetAsync(AppSettingKeys.LastBackupAt);
        return DateTime.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc)
            ? utc
            : null;
    }

    public Task SetLastBackupAtAsync(DateTime utc) => SetAsync(AppSettingKeys.LastBackupAt, FormatBackupDate(utc));

    public async Task<string> GetHomeTitleAsync() =>
        HomeTitle.Normalize(await GetAsync(AppSettingKeys.HomeTitle)) ?? HomeTitle.Default;

    public async Task<string> SetHomeTitleAsync(string? title)
    {
        var normalized = HomeTitle.Normalize(title);
        if (normalized is null || normalized == HomeTitle.Default)
        {
            await Connection.DeleteAsync<AppSettingRecord>(AppSettingKeys.HomeTitle);
            return HomeTitle.Default;
        }

        await SetAsync(AppSettingKeys.HomeTitle, normalized);
        return normalized;
    }

    /// <summary>Formato ISO 8601 en UTC con que se guarda <see cref="AppSettingKeys.LastBackupAt"/>.</summary>
    internal static string FormatBackupDate(DateTime utc) =>
        RecordMapping.ToUtcForStorage(utc).ToString("O", CultureInfo.InvariantCulture);
}
