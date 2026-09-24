using PiRadar.Core.Abstractions;
using PiRadar.Data.Records;

namespace PiRadar.Data.Tests;

public class DatabaseHostTests
{
    [Fact]
    public async Task FirstOpen_CreatesAndSavesKey_ThenOpens()
    {
        using var temp = new TempDatabase();
        var keys = new InMemoryKeyProvider();
        await using var host = new DatabaseHost(keys, temp.Path);

        Assert.Equal(DatabaseStatus.Opened, await host.OpenAsync(TestContext.Current.CancellationToken));
        Assert.NotNull(keys.Key);
        Assert.Equal(32, keys.Key.Length);
        Assert.True(File.Exists(temp.Path));
    }

    [Fact]
    public async Task Reopen_ReusesSavedKey_AndKeepsData()
    {
        using var temp = new TempDatabase();
        var keys = new InMemoryKeyProvider();
        await using (var first = new DatabaseHost(keys, temp.Path))
        {
            await first.OpenAsync(TestContext.Current.CancellationToken);
            await first.Database.Connection.InsertOrReplaceAsync(new AppSettingRecord { Key = "probe", Value = "ok" });
        }

        var savedKey = keys.Key;
        await using var second = new DatabaseHost(keys, temp.Path);

        Assert.Equal(DatabaseStatus.Opened, await second.OpenAsync(TestContext.Current.CancellationToken));
        Assert.Same(savedKey, keys.Key);
        Assert.Equal("ok", (await second.Database.Connection.GetAsync<AppSettingRecord>("probe")).Value);
    }

    [Fact]
    public async Task ExistingFileWithoutKey_ReportsKeyMissing_AndDoesNotCreateKey()
    {
        using var temp = new TempDatabase();
        var keys = new InMemoryKeyProvider();
        await using (var first = new DatabaseHost(keys, temp.Path))
        {
            await first.OpenAsync(TestContext.Current.CancellationToken);
        }

        keys.Key = null;
        await using var host = new DatabaseHost(keys, temp.Path);

        Assert.Equal(DatabaseStatus.KeyMissing, await host.OpenAsync(TestContext.Current.CancellationToken));
        Assert.Null(keys.Key);
        Assert.Throws<InvalidOperationException>(() => host.Database);
    }

    [Fact]
    public async Task WrongKey_ReportsUnreadable()
    {
        using var temp = new TempDatabase();
        var keys = new InMemoryKeyProvider();
        await using (var first = new DatabaseHost(keys, temp.Path))
        {
            await first.OpenAsync(TestContext.Current.CancellationToken);
        }

        keys.Key = new byte[32];
        await using var host = new DatabaseHost(keys, temp.Path);

        Assert.Equal(DatabaseStatus.Unreadable, await host.OpenAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reset_DeletesDataAndKey_AndStartsFresh()
    {
        using var temp = new TempDatabase();
        var keys = new InMemoryKeyProvider();
        await using (var first = new DatabaseHost(keys, temp.Path))
        {
            await first.OpenAsync(TestContext.Current.CancellationToken);
            await first.Database.Connection.InsertOrReplaceAsync(new AppSettingRecord { Key = "probe", Value = "ok" });
        }

        keys.Key = null;
        await using var host = new DatabaseHost(keys, temp.Path);
        Assert.Equal(DatabaseStatus.KeyMissing, await host.OpenAsync(TestContext.Current.CancellationToken));

        Assert.Equal(DatabaseStatus.Opened, await host.ResetAsync(TestContext.Current.CancellationToken));
        Assert.NotNull(keys.Key);
        Assert.Null(await host.Database.Connection.FindAsync<AppSettingRecord>("probe"));
        Assert.Equal(1, keys.DeleteCalls);
    }

    [Fact]
    public async Task ConcurrentOpens_OpenOnce()
    {
        using var temp = new TempDatabase();
        var keys = new InMemoryKeyProvider();
        await using var host = new DatabaseHost(keys, temp.Path);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => host.OpenAsync()));

        Assert.All(results, s => Assert.Equal(DatabaseStatus.Opened, s));
        Assert.Equal(1, keys.SaveCalls);
    }

    private sealed class InMemoryKeyProvider : IDatabaseKeyProvider
    {
        public byte[]? Key { get; set; }
        public int SaveCalls { get; private set; }
        public int DeleteCalls { get; private set; }

        public Task<byte[]?> TryGetKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(Key);

        public Task SaveKeyAsync(byte[] key, CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            Key = key;
            return Task.CompletedTask;
        }

        public Task DeleteKeyAsync(CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            Key = null;
            return Task.CompletedTask;
        }
    }
}
