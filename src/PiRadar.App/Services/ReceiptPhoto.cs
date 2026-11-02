using PiRadar.Core.Domain;
using PiRadar.Core.Images;

namespace PiRadar.App.Services;

/// <summary>Foto lista para guardar y el tamaño de la original ya orientada (el espacio de las coordenadas del OCR).</summary>
public sealed record PreparedPhoto(ReceiptImage Image, int SourceWidth, int SourceHeight);

/// <summary>
/// Versión del recibo que se guarda en la BD: orientada, con el lado mayor en ~1600 px y JPEG al 80 %.
/// La foto completa solo se usa para el OCR (en Android acierta más totales con la imagen completa).
/// </summary>
public static class ReceiptPhoto
{
    private const int JpegQuality = 80;

    public static PreparedPhoto Prepare(byte[] jpeg)
    {
#if ANDROID
        return PrepareAndroid(jpeg);
#elif IOS
        return PrepareIos(jpeg);
#else
        throw new PlatformNotSupportedException();
#endif
    }

    /// <summary>Recorta una foto guardada (ya orientada, ~1600 px) al marco; con el marco completo la devuelve igual.</summary>
    public static ReceiptImage Crop(ReceiptImage image, CropRect crop)
    {
        if (crop.IsFull)
        {
            return image;
        }

#if ANDROID
        return CropAndroid(image, crop);
#elif IOS
        return CropIos(image, crop);
#else
        throw new PlatformNotSupportedException();
#endif
    }

#if ANDROID
    private static PreparedPhoto PrepareAndroid(byte[] jpeg)
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
        var image = new ReceiptImage { Jpeg = output.ToArray(), Width = result.Width, Height = result.Height };
        return degrees is 90 or 270
            ? new PreparedPhoto(image, bounds.OutHeight, bounds.OutWidth)
            : new PreparedPhoto(image, bounds.OutWidth, bounds.OutHeight);
    }

    private static ReceiptImage CropAndroid(ReceiptImage image, CropRect crop)
    {
        using var decoded = Android.Graphics.BitmapFactory.DecodeByteArray(image.Jpeg, 0, image.Jpeg.Length)
            ?? throw new InvalidOperationException("No se pudo leer la imagen.");
        var (x, y, width, height) = crop.ToPixels(decoded.Width, decoded.Height);
        using var result = Android.Graphics.Bitmap.CreateBitmap(decoded, x, y, width, height);
        using var output = new MemoryStream();
        result.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, JpegQuality, output);
        return new ReceiptImage { Jpeg = output.ToArray(), Width = result.Width, Height = result.Height };
    }
#endif

#if IOS
    private static PreparedPhoto PrepareIos(byte[] jpeg)
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

        // Tamaño de la original orientada: EXIF 5–8 la gira 90°.
        var properties = source.GetProperties(0);
        var (width, height) = (properties.PixelWidth ?? (int)image.Width, properties.PixelHeight ?? (int)image.Height);
        if ((int?)properties.Orientation is >= 5)
        {
            (width, height) = (height, width);
        }

        return new PreparedPhoto(EncodeIos(image), width, height);
    }

    private static ReceiptImage CropIos(ReceiptImage image, CropRect crop)
    {
        // La foto guardada ya está orientada y sin etiqueta EXIF: se recorta tal cual.
        using var data = Foundation.NSData.FromArray(image.Jpeg);
        using var source = ImageIO.CGImageSource.FromData(data) ?? throw new InvalidOperationException("No se pudo leer la imagen.");
        using var decoded = source.CreateImage(0, new ImageIO.CGImageOptions()) ?? throw new InvalidOperationException("No se pudo leer la imagen.");
        var (x, y, width, height) = crop.ToPixels((int)decoded.Width, (int)decoded.Height);
        using var cropped = decoded.WithImageInRect(new CoreGraphics.CGRect(x, y, width, height))
            ?? throw new InvalidOperationException("No se pudo recortar la imagen.");
        return EncodeIos(cropped);
    }

    private static ReceiptImage EncodeIos(CoreGraphics.CGImage image)
    {
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
