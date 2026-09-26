using System.Security.Cryptography;
using System.Text;
using PiRadar.Core.Backup;

namespace PiRadar.Core.Tests.Backup;

public class Pbkdf2Sha256Tests
{
    // RFC 7914, sección 11 (PBKDF2-HMAC-SHA256).
    [Theory]
    [InlineData("passwd", "salt", 1, "55ac046e56e3089fec1691c22544b605f94185216dde0465e68b9d57c20dacbc49ca9cccf179b645991664b39d77ef317c71b845b1e30bd509112041d3a19783")]
    [InlineData("Password", "NaCl", 80000, "4ddcd8f60b98be21830cee5ef22701f9641a4418d04c0414aeff08876b34ab56a1d425a1225833549adb841b51c9b3176a272bdebba1d078478f62b397f33c8d")]
    public void MatchesRfcVectors(string password, string salt, int iterations, string expectedHex)
    {
        var key = Pbkdf2Sha256.DeriveKey(Encoding.ASCII.GetBytes(password), Encoding.ASCII.GetBytes(salt), iterations, 64);

        Assert.Equal(expectedHex, Convert.ToHexStringLower(key));
    }

    [Theory]
    [InlineData(0, 0, 1, 32)]
    [InlineData(8, 16, 2, 32)]
    [InlineData(20, 16, 1000, 32)]
    [InlineData(64, 16, 3, 32)]      // clave de un bloque exacto
    [InlineData(65, 16, 3, 32)]      // clave más larga que un bloque: se usa su hash
    [InlineData(200, 16, 5, 32)]
    [InlineData(12, 40, 7, 32)]      // salt + contador ocupan casi un bloque: relleno en un bloque aparte
    [InlineData(12, 60, 7, 32)]
    [InlineData(12, 130, 7, 32)]     // mensaje de más de dos bloques
    [InlineData(12, 16, 4, 1)]
    [InlineData(12, 16, 4, 33)]      // dos bloques de salida, el segundo recortado
    [InlineData(12, 16, 4, 100)]
    public void MatchesDotNetImplementation(int passwordLength, int saltLength, int iterations, int length)
    {
        var random = new Random(passwordLength * 1000 + saltLength);
        var password = new byte[passwordLength];
        var salt = new byte[saltLength];
        random.NextBytes(password);
        random.NextBytes(salt);

        var expected = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, length);

        Assert.Equal(expected, Pbkdf2Sha256.DeriveKey(password, salt, iterations, length));
    }
}
