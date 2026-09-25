using PiRadar.Core.Images;

namespace PiRadar.Core.Tests.Images;

public class JpegOrientationTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(3, 180)]
    [InlineData(6, 90)]
    [InlineData(8, 270)]
    [InlineData(2, 0)] // espejo: no ocurre en fotos de cámara
    public void ReadRotation_BigEndianExif_AfterJfif(ushort orientation, int expected)
    {
        // Como las fotos del iPhone exportadas: APP0 (JFIF) antes del APP1 (Exif, "MM").
        var jpeg = Jpeg(Jfif(), Exif(orientation, littleEndian: false));

        Assert.Equal(expected, JpegOrientation.ReadRotation(jpeg));
    }

    [Fact]
    public void ReadRotation_LittleEndianExif()
    {
        Assert.Equal(90, JpegOrientation.ReadRotation(Jpeg(Exif(6, littleEndian: true))));
    }

    [Fact]
    public void Read_WithoutExif_IsNormal()
    {
        Assert.Equal(1, JpegOrientation.Read(Jpeg(Jfif())));
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47 })] // PNG
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE1, 0xFF, 0xFF })] // segmento truncado
    public void Read_InvalidInput_IsNormal(byte[] data)
    {
        Assert.Equal(1, JpegOrientation.Read(data));
    }

    private static byte[] Jpeg(params byte[][] segments) =>
        [0xFF, 0xD8, .. segments.SelectMany(s => s), 0xFF, 0xDA, 0x00, 0x02, 0xFF, 0xD9];

    private static byte[] Jfif() =>
        [0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 2, 1, 0, 0x48, 0, 0x48, 0, 0];

    private static byte[] Exif(ushort orientation, bool littleEndian)
    {
        byte[] U16(ushort v) => littleEndian ? [(byte)v, (byte)(v >> 8)] : [(byte)(v >> 8), (byte)v];
        byte[] U32(uint v) => littleEndian
            ? [(byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24)]
            : [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];

        byte[] tiff =
        [
            .. (littleEndian ? "II"u8 : "MM"u8).ToArray(), .. U16(42), .. U32(8),
            .. U16(1), // una entrada en el IFD0
            .. U16(0x0112), .. U16(3), .. U32(1), .. U16(orientation), 0, 0,
            .. U32(0),
        ];
        byte[] payload = [.. "Exif\0\0"u8.ToArray(), .. tiff];
        var length = (ushort)(payload.Length + 2);
        return [0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. payload];
    }
}
