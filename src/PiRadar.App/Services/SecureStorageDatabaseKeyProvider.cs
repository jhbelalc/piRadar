using PiRadar.Core.Abstractions;

namespace PiRadar.App.Services;

/// <summary>Guarda la clave de la BD en Keychain (iOS) / Keystore (Android) vía <see cref="SecureStorage"/>.</summary>
public sealed class SecureStorageDatabaseKeyProvider(ISecureStorage secureStorage) : IDatabaseKeyProvider
{
    private const string KeyName = "piradar.db.key.v1";

    public async Task<byte[]?> TryGetKeyAsync(CancellationToken cancellationToken = default)
    {
        var value = await secureStorage.GetAsync(KeyName);
        return value is null ? null : Convert.FromBase64String(value);
    }

    public Task SaveKeyAsync(byte[] key, CancellationToken cancellationToken = default) =>
        secureStorage.SetAsync(KeyName, Convert.ToBase64String(key));

    public Task DeleteKeyAsync(CancellationToken cancellationToken = default)
    {
        secureStorage.Remove(KeyName);
        return Task.CompletedTask;
    }
}
