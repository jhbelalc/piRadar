using System.Security.Cryptography;

namespace PiRadar.Data.Tests;

/// <summary>Ruta y clave aleatorias para una BD de prueba; borra los archivos al terminar.</summary>
internal sealed class TempDatabase : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), $"piradar-test-{Guid.NewGuid():N}.db3");

    public byte[] Key { get; } = RandomNumberGenerator.GetBytes(32);

    public Task<PiRadarDatabase> OpenAsync() => PiRadarDatabase.OpenAsync(Path, Key);

    public void Dispose() => PiRadarDatabase.DeleteFiles(Path);
}
