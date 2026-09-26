using System.Text;
using PiRadar.Core.Backup;

namespace PiRadar.Core.Tests.Backup;

public class BackupCryptoTests
{
    // Pocas iteraciones para que los tests sean rápidos; el valor real se prueba aparte.
    private const int FastIterations = 1_000;
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"tables":{"Transactions":[]}}""");

    [Fact]
    public void RoundTrip_WithSamePassword()
    {
        var file = BackupCrypto.Encrypt(Body, "clave segura", schemaVersion: 3, FastIterations);

        Assert.Equal(new BackupHeader(1, 3, FastIterations), BackupCrypto.ReadHeader(file));
        Assert.Equal(Body, BackupCrypto.Decrypt(file, "clave segura"));
    }

    [Fact]
    public void Encrypt_UsesDefaultIterations_AndNeverRepeatsOutput()
    {
        var a = BackupCrypto.Encrypt(Body, "clave segura", 1);
        var b = BackupCrypto.Encrypt(Body, "clave segura", 1);

        Assert.Equal(600_000, BackupCrypto.ReadHeader(a).Iterations);
        Assert.NotEqual(a, b); // salt y nonce aleatorios
        Assert.DoesNotContain("tables", Encoding.UTF8.GetString(a), StringComparison.Ordinal);
    }

    [Fact]
    public void Decrypt_WrongPassword_Fails()
    {
        var file = BackupCrypto.Encrypt(Body, "clave segura", 1, FastIterations);

        var ex = Assert.Throws<BackupException>(() => BackupCrypto.Decrypt(file, "clave Segura"));
        Assert.Equal("Contraseña incorrecta o archivo dañado.", ex.Message);
    }

    [Theory]
    [InlineData(10)]  // versión de esquema en la cabecera
    [InlineData(20)]  // salt
    [InlineData(40)]  // cuerpo cifrado
    [InlineData(-1)]  // tag
    public void Decrypt_TamperedFile_Fails(int offset)
    {
        var file = BackupCrypto.Encrypt(Body, "clave segura", 1, FastIterations);
        file[offset < 0 ? file.Length + offset : offset] ^= 0x01;

        Assert.Throws<BackupException>(() => BackupCrypto.Decrypt(file, "clave segura"));
    }

    [Fact]
    public void Decrypt_AcceptsPasswordInEitherUnicodeForm()
    {
        var composed = "contraseña".Normalize(NormalizationForm.FormC);
        var decomposed = "contraseña".Normalize(NormalizationForm.FormD);
        var file = BackupCrypto.Encrypt(Body, composed, 1, FastIterations);

        Assert.Equal(Body, BackupCrypto.Decrypt(file, decomposed));
    }

    [Fact]
    public void ReadHeader_RejectsOtherFiles()
    {
        var ex = Assert.Throws<BackupException>(() => BackupCrypto.ReadHeader("%PDF-1.7 no es un respaldo de nada"u8));

        Assert.Equal("El archivo no es un respaldo de piRadar.", ex.Message);
        Assert.Throws<BackupException>(() => BackupCrypto.ReadHeader("PIRADAR"u8));
    }

    [Fact]
    public void EmptyBody_IsNeverEncryptedNorDecrypted()
    {
        // En Android, AES-GCM con cuerpo vacío tumba la app: ni se crea ni se acepta un archivo así.
        Assert.Throws<ArgumentException>(() => BackupCrypto.Encrypt([], "clave segura", 1, FastIterations));

        var file = BackupCrypto.Encrypt([0x42], "clave segura", 1, FastIterations);
        var withoutBody = file[..^17].Concat(file[^16..]).ToArray(); // cabecera + tag, sin el byte cifrado

        Assert.Throws<BackupException>(() => BackupCrypto.Decrypt(withoutBody, "clave segura"));
    }

    [Fact]
    public void ReadHeader_RejectsNewerFormat()
    {
        var file = BackupCrypto.Encrypt(Body, "clave segura", 1, FastIterations);
        file[7] = BackupCrypto.FormatVersion + 1;

        Assert.Contains("versión más nueva", Assert.Throws<BackupException>(() => BackupCrypto.ReadHeader(file)).Message);
    }

    [Fact]
    public void ReadHeader_RejectsAbsurdIterations()
    {
        var file = BackupCrypto.Encrypt(Body, "clave segura", 1, FastIterations);
        BitConverter.GetBytes(int.MaxValue).CopyTo(file, 12);

        Assert.Throws<BackupException>(() => BackupCrypto.ReadHeader(file));
    }

    [Theory]
    [InlineData(null, null, "La contraseña debe tener al menos 8 caracteres.")]
    [InlineData("corta", "corta", "La contraseña debe tener al menos 8 caracteres.")]
    [InlineData("suficiente", "suficientE", "Las contraseñas no coinciden.")]
    [InlineData("suficiente", "suficiente", null)]
    public void ValidateNewPassword(string? password, string? confirmation, string? expected)
    {
        Assert.Equal(expected, BackupCrypto.ValidateNewPassword(password, confirmation));
    }
}
