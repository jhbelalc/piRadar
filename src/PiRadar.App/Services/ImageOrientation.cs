using PiRadar.Core.Images;

namespace PiRadar.App.Services;

/// <summary>
/// Aplica la rotación EXIF a los píxeles del JPEG. ML Kit (Android) ignora la etiqueta EXIF y lee de lado las fotos
/// de cámara (p. ej. <c>Orientation = 6</c> en fotos verticales); Vision (iOS) sí la respeta.
/// </summary>
public static class ImageOrientation
{
    /// <summary>Grados que hay que rotar según la etiqueta EXIF.</summary>
    public static int ExifRotation(byte[] jpeg) => JpegOrientation.ReadRotation(jpeg);

    public static byte[] Normalize(byte[] jpeg)
    {
#if ANDROID
        var degrees = ExifRotation(jpeg);
        if (degrees == 0)
        {
            return jpeg;
        }

        using var bitmap = Android.Graphics.BitmapFactory.DecodeByteArray(jpeg, 0, jpeg.Length)
            ?? throw new InvalidOperationException("No se pudo decodificar la imagen.");
        using var matrix = new Android.Graphics.Matrix();
        matrix.PostRotate(degrees);
        using var rotated = Android.Graphics.Bitmap.CreateBitmap(bitmap, 0, 0, bitmap.Width, bitmap.Height, matrix, true);
        using var output = new MemoryStream();
        rotated.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, 90, output);
        return output.ToArray();
#else
        return jpeg;
#endif
    }
}
