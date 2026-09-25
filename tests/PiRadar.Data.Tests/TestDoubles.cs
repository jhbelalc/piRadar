using PiRadar.Core.Abstractions;

namespace PiRadar.Data.Tests;

internal sealed class InMemoryKeyProvider : IDatabaseKeyProvider
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

internal sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 9, 20, 15, 0, 0, DateTimeKind.Utc);

    public void Advance(TimeSpan by) => UtcNow += by;
}

/// <summary><see cref="DatabaseHost"/> abierto sobre una BD temporal cifrada, para probar repositorios.</summary>
internal sealed class TempHost : IAsyncDisposable
{
    private readonly TempDatabase _temp = new();

    private TempHost() => Host = new DatabaseHost(new InMemoryKeyProvider(), _temp.Path);

    public DatabaseHost Host { get; }

    public FakeClock Clock { get; } = new();

    public static async Task<TempHost> OpenAsync()
    {
        var temp = new TempHost();
        await temp.Host.OpenAsync();
        return temp;
    }

    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        _temp.Dispose();
    }
}
