using PiRadar.Core.Domain;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace PiRadar.Core.Backup;

/// <summary>Datos de la cabecera de un archivo <c>.piradar</c>, legibles sin la contraseña.</summary>
public sealed record BackupHeader(int FormatVersion, int SchemaVersion, int Iterations);

/// <summary>
/// Cifrado del respaldo <c>.piradar</c>, independiente de la clave de la BD para poder restaurarlo en otro dispositivo.
/// Formato: <c>PIRADAR</c> (7 B) + versión de formato (1 B) + versión de esquema (int32) + iteraciones (int32) +
/// salt (16 B) + nonce (12 B) + cuerpo cifrado con AES-256-GCM + tag (16 B). La clave sale de la contraseña con
/// PBKDF2-SHA256 y la cabecera va como datos asociados: si alguien la cambia, el tag deja de cuadrar.
/// </summary>
public static class BackupCrypto
{
    public const int FormatVersion = 1;
    public const int DefaultIterations = 600_000;
    public const int MinPasswordLength = 8;
    public const string FileExtension = ".piradar";

    private static readonly byte[] Magic = "PIRADAR"u8.ToArray();
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int HeaderSize = 7 + 1 + 4 + 4 + SaltSize + NonceSize;

    // Un archivo manipulado no debe poder dejar la app calculando PBKDF2 por horas.
    private const int MaxIterations = 10_000_000;

    /// <summary>Mensaje para el usuario si la contraseña no sirve; <c>null</c> si está bien.</summary>
    public static string? ValidateNewPassword(string? password, string? confirmation)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
        {
            return $"La contraseña debe tener al menos {MinPasswordLength} caracteres.";
        }

        return password == confirmation ? null : "Las contraseñas no coinciden.";
    }

    public static byte[] Encrypt(ReadOnlySpan<byte> plaintext, string password, int schemaVersion, int iterations = DefaultIterations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(iterations, MaxIterations);
        // AES-GCM nativo de Android aborta el proceso (SIGABRT en CipherUpdate) con un cuerpo vacío.
        if (plaintext.IsEmpty)
        {
            throw new ArgumentException("El cuerpo del respaldo no puede estar vacío.", nameof(plaintext));
        }

        EnsureSupported();

        var file = new byte[HeaderSize + plaintext.Length + TagSize];
        var header = file.AsSpan(0, HeaderSize);
        Magic.CopyTo(header);
        header[7] = FormatVersion;
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], schemaVersion);
        BinaryPrimitives.WriteInt32LittleEndian(header[12..], iterations);
        var salt = header.Slice(16, SaltSize);
        var nonce = header.Slice(16 + SaltSize, NonceSize);
        RandomNumberGenerator.Fill(salt);
        RandomNumberGenerator.Fill(nonce);

        var key = DeriveKey(password, salt, iterations);
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plaintext, file.AsSpan(HeaderSize, plaintext.Length), file.AsSpan(HeaderSize + plaintext.Length), header);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        return file;
    }

    /// <exception cref="BackupException">No es un archivo de piRadar o su formato es de una versión más nueva.</exception>
    public static BackupHeader ReadHeader(ReadOnlySpan<byte> file)
    {
        // Sin al menos un byte cifrado no es un respaldo (y AES-GCM de Android abortaría el proceso con cuerpo vacío).
        if (file.Length <= HeaderSize + TagSize || !file[..Magic.Length].SequenceEqual(Magic))
        {
            throw new BackupException(AppLocale.Pick("El archivo no es un respaldo de piRadar.", "This file is not a piRadar backup."));
        }

        var format = file[7];
        if (format > FormatVersion)
        {
            throw new BackupException(AppLocale.Pick("El respaldo se hizo con una versión más nueva de piRadar. Actualiza la app para restaurarlo.", "This backup was made with a newer version of piRadar. Update the app to restore it."));
        }

        var iterations = BinaryPrimitives.ReadInt32LittleEndian(file[12..]);
        if (format < 1 || iterations is < 1 or > MaxIterations)
        {
            throw new BackupException(AppLocale.Pick("El archivo de respaldo está dañado.", "The backup file is damaged."));
        }

        return new BackupHeader(format, BinaryPrimitives.ReadInt32LittleEndian(file[8..]), iterations);
    }

    /// <summary>El cuerpo descifrado.</summary>
    /// <exception cref="BackupException">Formato inválido, o contraseña incorrecta o archivo dañado.</exception>
    public static byte[] Decrypt(ReadOnlySpan<byte> file, string password)
    {
        var info = ReadHeader(file);
        EnsureSupported();

        var header = file[..HeaderSize];
        var ciphertext = file[HeaderSize..^TagSize];
        var plaintext = new byte[ciphertext.Length];
        var key = DeriveKey(password, header.Slice(16, SaltSize), info.Iterations);
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(header.Slice(16 + SaltSize, NonceSize), ciphertext, file[^TagSize..], plaintext, header);
        }
        catch (AuthenticationTagMismatchException ex)
        {
            throw new BackupException(AppLocale.Pick("Contraseña incorrecta o archivo dañado.", "Wrong password or damaged file."), ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        return plaintext;
    }

    // NFC: el mismo texto con tildes puede llegar compuesto o descompuesto según el teclado (iOS/Android).
    private static byte[] DeriveKey(string password, ReadOnlySpan<byte> salt, int iterations)
    {
        var bytes = Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormC));
        try
        {
            // Mismo resultado en ambos caminos (lo verifican los tests). En Android la función del sistema pasa cada
            // HMAC por Java y es mucho más lenta; en iOS es una sola llamada a CommonCrypto.
            return OperatingSystem.IsAndroid()
                ? Pbkdf2Sha256.DeriveKey(bytes, salt, iterations, KeySize)
                : Rfc2898DeriveBytes.Pbkdf2(bytes, salt, iterations, HashAlgorithmName.SHA256, KeySize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static void EnsureSupported()
    {
        if (!AesGcm.IsSupported)
        {
            throw new BackupException(AppLocale.Pick("Este dispositivo no soporta el cifrado del respaldo (AES-GCM).", "This device does not support backup encryption (AES-GCM)."));
        }
    }
}
