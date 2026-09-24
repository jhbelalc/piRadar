namespace PiRadar.Core.Abstractions;

/// <summary>Almacenamiento seguro (Keychain/Keystore) de la clave de 32 bytes de la BD.</summary>
public interface IDatabaseKeyProvider
{
    /// <summary>Devuelve la clave guardada, o <c>null</c> si no existe.</summary>
    Task<byte[]?> TryGetKeyAsync(CancellationToken cancellationToken = default);

    Task SaveKeyAsync(byte[] key, CancellationToken cancellationToken = default);

    Task DeleteKeyAsync(CancellationToken cancellationToken = default);
}
