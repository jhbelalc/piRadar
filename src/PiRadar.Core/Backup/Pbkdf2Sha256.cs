using System.Buffers.Binary;
using System.Security.Cryptography;

namespace PiRadar.Core.Backup;

/// <summary>
/// PBKDF2-HMAC-SHA256 (RFC 8018) en código administrado; da exactamente lo mismo que
/// <see cref="Rfc2898DeriveBytes.Pbkdf2(byte[], byte[], int, HashAlgorithmName, int)"/>. En Android esa función pasa
/// cada HMAC por Java (≈ 15 s para 600.000 iteraciones en el emulador, en Release). Aquí cada iteración son dos
/// compresiones SHA-256 sobre palabras de 32 bits con el relleno ya puesto, y con punteros para que el JIT de Mono no
/// compruebe límites en cada acceso.
/// </summary>
internal static unsafe class Pbkdf2Sha256
{
    private const int BlockSize = 64;
    private const int HashSize = 32;

    private static readonly uint[] K =
    [
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
    ];

    private static readonly uint[] InitialState =
        [0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19];

    public static byte[] DeriveKey(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt, int iterations, int length)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);

        var result = new byte[length];
        var message = new byte[salt.Length + 4];
        salt.CopyTo(message);

        // Todo el estado sensible en la pila; se borra al final.
        var key = stackalloc byte[BlockSize];
        var inner = stackalloc uint[8];
        var outer = stackalloc uint[8];
        var state = stackalloc uint[8];
        var u = stackalloc uint[8];
        var t = stackalloc uint[8];
        var block = stackalloc uint[16];
        var w = stackalloc uint[64];
        var bytes = stackalloc byte[HashSize];

        try
        {
            fixed (uint* k = K)
            {
                // Estados SHA-256 después de procesar clave ⊕ ipad y clave ⊕ opad: se reutilizan en cada HMAC.
                var keySpan = new Span<byte>(key, BlockSize);
                keySpan.Clear();
                if (password.Length > BlockSize)
                {
                    SHA256.HashData(password, keySpan);
                }
                else
                {
                    password.CopyTo(keySpan);
                }

                PadState(key, 0x36, inner, block, w, k);
                PadState(key, 0x5c, outer, block, w, k);

                for (int index = 1, offset = 0; offset < length; index++, offset += HashSize)
                {
                    BinaryPrimitives.WriteInt32BigEndian(message.AsSpan(salt.Length), index);

                    // U1 = HMAC(salt || INT(i)): el mensaje interno puede ocupar varios bloques.
                    Copy(inner, state);
                    FinishInner(state, message, block, w, k);
                    OuterHash(outer, state, u, block, w, k);
                    Copy(u, t);

                    // U2..Uc = HMAC(U anterior): mensaje de 32 bytes, un bloque con relleno fijo en cada paso.
                    for (var i = 1; i < iterations; i++)
                    {
                        Copy(u, block);
                        SetFixedPadding(block);
                        Copy(inner, state);
                        Compress(state, block, w, k);
                        OuterHash(outer, state, u, block, w, k);
                        for (var j = 0; j < 8; j++)
                        {
                            t[j] ^= u[j];
                        }
                    }

                    for (var j = 0; j < 8; j++)
                    {
                        BinaryPrimitives.WriteUInt32BigEndian(new Span<byte>(bytes + j * 4, 4), t[j]);
                    }

                    new ReadOnlySpan<byte>(bytes, Math.Min(HashSize, length - offset)).CopyTo(result.AsSpan(offset));
                }
            }

            return result;
        }
        finally
        {
            new Span<byte>(key, BlockSize).Clear();
            new Span<uint>(inner, 8).Clear();
            new Span<uint>(outer, 8).Clear();
            new Span<uint>(state, 8).Clear();
            new Span<uint>(u, 8).Clear();
            new Span<uint>(t, 8).Clear();
            new Span<uint>(block, 16).Clear();
            new Span<uint>(w, 64).Clear();
            new Span<byte>(bytes, HashSize).Clear();
            CryptographicOperations.ZeroMemory(message);
        }
    }

    private static void Copy(uint* from, uint* to)
    {
        for (var i = 0; i < 8; i++)
        {
            to[i] = from[i];
        }
    }

    /// <summary>Relleno de un mensaje de 32 bytes tras un bloque de clave: longitud total (64 + 32) × 8 bits.</summary>
    private static void SetFixedPadding(uint* block)
    {
        block[8] = 0x80000000;
        for (var i = 9; i < 15; i++)
        {
            block[i] = 0;
        }

        block[15] = (BlockSize + HashSize) * 8;
    }

    /// <summary>Hash externo del HMAC: <paramref name="output"/> = SHA-256(opad-state, digest interno en <paramref name="innerDigest"/>).</summary>
    private static void OuterHash(uint* outer, uint* innerDigest, uint* output, uint* block, uint* w, uint* k)
    {
        Copy(innerDigest, block);
        SetFixedPadding(block);
        Copy(outer, output);
        Compress(output, block, w, k);
    }

    private static void PadState(byte* key, byte pad, uint* state, uint* block, uint* w, uint* k)
    {
        for (var i = 0; i < 16; i++)
        {
            block[i] = ((uint)(key[i * 4] ^ pad) << 24) | ((uint)(key[i * 4 + 1] ^ pad) << 16)
                | ((uint)(key[i * 4 + 2] ^ pad) << 8) | (uint)(key[i * 4 + 3] ^ pad);
        }

        for (var i = 0; i < 8; i++)
        {
            state[i] = InitialState[i];
        }

        Compress(state, block, w, k);
    }

    /// <summary>Procesa <paramref name="data"/> y el relleno sobre un estado que ya procesó un bloque (la clave ⊕ ipad).</summary>
    private static void FinishInner(uint* state, ReadOnlySpan<byte> data, uint* block, uint* w, uint* k)
    {
        var total = (ulong)(BlockSize + data.Length);
        Span<byte> tail = stackalloc byte[BlockSize * 2];
        while (data.Length >= BlockSize)
        {
            LoadBlock(data, block);
            Compress(state, block, w, k);
            data = data[BlockSize..];
        }

        tail.Clear();
        data.CopyTo(tail);
        tail[data.Length] = 0x80;
        var blocks = data.Length >= BlockSize - 8 ? 2 : 1;
        BinaryPrimitives.WriteUInt64BigEndian(tail[(blocks * BlockSize - 8)..], total * 8);
        for (var b = 0; b < blocks; b++)
        {
            LoadBlock(tail[(b * BlockSize)..], block);
            Compress(state, block, w, k);
        }

        tail.Clear();
    }

    private static void LoadBlock(ReadOnlySpan<byte> bytes, uint* block)
    {
        for (var i = 0; i < 16; i++)
        {
            block[i] = BinaryPrimitives.ReadUInt32BigEndian(bytes[(i * 4)..]);
        }
    }

    private static void Compress(uint* state, uint* block, uint* w, uint* k)
    {
        for (var i = 0; i < 16; i++)
        {
            w[i] = block[i];
        }

        for (var i = 16; i < 64; i++)
        {
            uint x = w[i - 15], y = w[i - 2];
            var s0 = ((x >> 7) | (x << 25)) ^ ((x >> 18) | (x << 14)) ^ (x >> 3);
            var s1 = ((y >> 17) | (y << 15)) ^ ((y >> 19) | (y << 13)) ^ (y >> 10);
            w[i] = w[i - 16] + s0 + w[i - 7] + s1;
        }

        uint a = state[0], b = state[1], c = state[2], d = state[3], e = state[4], f = state[5], g = state[6], h = state[7];
        for (var i = 0; i < 64; i++)
        {
            var t1 = h + (((e >> 6) | (e << 26)) ^ ((e >> 11) | (e << 21)) ^ ((e >> 25) | (e << 7)))
                + ((e & f) ^ (~e & g)) + k[i] + w[i];
            var t2 = (((a >> 2) | (a << 30)) ^ ((a >> 13) | (a << 19)) ^ ((a >> 22) | (a << 10)))
                + ((a & b) ^ (a & c) ^ (b & c));
            h = g;
            g = f;
            f = e;
            e = d + t1;
            d = c;
            c = b;
            b = a;
            a = t1 + t2;
        }

        state[0] += a;
        state[1] += b;
        state[2] += c;
        state[3] += d;
        state[4] += e;
        state[5] += f;
        state[6] += g;
        state[7] += h;
    }
}
