using PiRadar.Core.Domain;
using PiRadar.Core.Images;

namespace PiRadar.App.Services;

/// <summary>
/// Versión del recibo que se guarda en la BD: orientada, con el lado mayor en ~1600 px y JPEG al 80 %.
/// La foto completa solo se usa para el OCR (en Android acierta más totales con la imagen completa).
/// </summary>
public static class ReceiptPhoto
{
    private const int JpegQuality = 80;

    public static ReceiptImage Prepare(byte[] jpeg)
    {
#if ANDROID
        return PrepareAndroid(jpeg);
#elif IOS
        return PrepareIos(jpeg);
#else
        throw new PlatformNotSupportedException();
#endif
    }

#if ANDROID
    private static ReceiptImage PrepareAndroid(byte[] jpeg)
    {
        // Primero solo las dimensiones, para decodificar reducida y no cargar 12 MP en memoria.
        var bounds = new Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
        Android.Graphics.BitmapFactory.DecodeByteArray(jpeg, 0, jpeg.Length, bounds);
        if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0)
        {
            throw new InvalidOperationException("No se pudo leer la imagen.");
        }

        var sample = 1;
        while (Math.Max(bounds.OutWidth, bounds.OutHeight) / (sample * 2) >= ImageSize.ReceiptMaxSide)
        {
            sample *= 2;
        }

        var options = new Android.Graphics.BitmapFactory.Options { InSampleSize = sample };
        using var decoded = Android.Graphics.BitmapFactory.DecodeByteArray(jpeg, 0, jpeg.Length, options)
            ?? throw new InvalidOperationException("No se pudo leer la imagen.");

        // ML Kit y BitmapFactory ignoran la etiqueta EXIF: se rota aquí, junto con el escalado.
        var degrees = ImageOrientation.ExifRotation(jpeg);
        var rotatedWidth = degrees is 90 or 270 ? decoded.Height : decoded.Width;
        var rotatedHeight = degrees is 90 or 270 ? decoded.Width : decoded.Height;
        var (width, height) = ImageSize.FitWithin(rotatedWidth, rotatedHeight, ImageSize.ReceiptMaxSide);
        var scale = (float)width / rotatedWidth;

        using var matrix = new Android.Graphics.Matrix();
        matrix.PostRotate(degrees);
        matrix.PostScale(scale, scale);
        using var result = Android.Graphics.Bitmap.CreateBitmap(decoded, 0, 0, decoded.Width, decoded.Height, matrix, true);
        using var output = new MemoryStream();
        result.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, JpegQuality, output);
        return new ReceiptImage { Jpeg = output.ToArray(), Width = result.Width, Height = result.Height };
    }
#endif

#if IOS
    private static ReceiptImage PrepareIos(byte[] jpeg)
    {
        // ImageIO y no UIKit: esto corre en un hilo de fondo y con UIKit ahí (UIGraphicsImageRenderer) la app
        // fallaba en Debug. La miniatura aplica la orientación EXIF y reduce sin decodificar los 12 MP.
        using var data = Foundation.NSData.FromArray(jpeg);
        using var source = ImageIO.CGImageSource.FromData(data) ?? throw new InvalidOperationException("No se pudo leer la imagen.");
        using var image = source.CreateThumbnail(0, new ImageIO.CGImageThumbnailOptions
        {
            MaxPixelSize = ImageSize.ReceiptMaxSide,
            CreateThumbnailFromImageAlways = true,
            CreateThumbnailWithTransform = true,
        }) ?? throw new InvalidOperationException("No se pudo leer la imagen.");

        using var output = new Foundation.NSMutableData();
        using (var destination = ImageIO.CGImageDestination.Create(output, "public.jpeg", 1)
            ?? throw new InvalidOperationException("No se pudo comprimir la imagen."))
        {
            destination.AddImage(image, new ImageIO.CGImageDestinationOptions { LossyCompressionQuality = JpegQuality / 100f });
            if (!destination.Close())
            {
                throw new InvalidOperationException("No se pudo comprimir la imagen.");
            }
        }

        return new ReceiptImage { Jpeg = output.ToArray(), Width = (int)image.Width, Height = (int)image.Height };
    }
#endif
}
