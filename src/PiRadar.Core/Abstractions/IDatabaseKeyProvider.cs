namespace PiRadar.Core.Abstractions;

/// <summary>Entrega la clave de 32 bytes de la BD; la genera y guarda en almacenamiento seguro la primera vez.</summary>
public interface IDatabaseKeyProvider
{
    Task<byte[]> GetOrCreateKeyAsync(CancellationToken cancellationToken = default);
}
