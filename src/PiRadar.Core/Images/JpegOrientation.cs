using System.Buffers.Binary;

namespace PiRadar.Core.Images;

/// <summary>Lee la etiqueta EXIF <c>Orientation</c> (0x0112) de un JPEG sin decodificar la imagen.</summary>
public static class JpegOrientation
{
    /// <summary>
    /// Grados en sentido horario que hay que rotar los píxeles para ver la imagen derecha: 0, 90, 180 o 270.
    /// Devuelve 0 si no es JPEG, no tiene EXIF o la orientación incluye espejo (no ocurre en fotos de cámara).
    /// </summary>
    public static int ReadRotation(ReadOnlySpan<byte> jpeg) => Read(jpeg) switch
    {
        3 => 180,
        6 => 90,
        8 => 270,
        _ => 0,
    };

    /// <summary>Valor EXIF crudo (1–8), o 1 si no hay.</summary>
    public static int Read(ReadOnlySpan<byte> jpeg)
    {
        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
        {
            return 1;
        }

        var pos = 2;
        while (pos + 4 <= jpeg.Length && jpeg[pos] == 0xFF)
        {
            var marker = jpeg[pos + 1];
            if (marker is 0xD9 or 0xDA)
            {
                break; // fin de imagen o inicio de datos: ya no hay metadatos
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(jpeg[(pos + 2)..]);
            if (length < 2 || pos + 2 + length > jpeg.Length)
            {
                break;
            }

            var segment = jpeg.Slice(pos + 4, length - 2);
            if (marker == 0xE1 && segment.StartsWith("Exif\0\0"u8))
            {
                return ReadTiff(segment[6..]);
            }

            pos += 2 + length;
        }

        return 1;
    }

    private static int ReadTiff(ReadOnlySpan<byte> tiff)
    {
        if (tiff.Length < 8)
        {
            return 1;
        }

        var little = tiff[0] == 'I' && tiff[1] == 'I';
        if (!little && !(tiff[0] == 'M' && tiff[1] == 'M'))
        {
            return 1;
        }

        var ifd = U32(tiff, 4, little);
        if (ifd + 2 > tiff.Length)
        {
            return 1;
        }

        var count = U16(tiff, (int)ifd, little);
        for (var i = 0; i < count; i++)
        {
            var entry = (int)ifd + 2 + i * 12;
            if (entry + 12 > tiff.Length)
            {
                break;
            }

            if (U16(tiff, entry, little) == 0x0112)
            {
                var value = U16(tiff, entry + 8, little);
                return value is >= 1 and <= 8 ? value : 1;
            }
        }

        return 1;
    }

    private static ushort U16(ReadOnlySpan<byte> data, int at, bool little) =>
        little ? BinaryPrimitives.ReadUInt16LittleEndian(data[at..]) : BinaryPrimitives.ReadUInt16BigEndian(data[at..]);

    private static uint U32(ReadOnlySpan<byte> data, int at, bool little) =>
        little ? BinaryPrimitives.ReadUInt32LittleEndian(data[at..]) : BinaryPrimitives.ReadUInt32BigEndian(data[at..]);
}
