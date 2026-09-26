namespace PiRadar.Core.Images;

public static class ImageSize
{
    /// <summary>Lado mayor de la imagen del recibo que se guarda en la BD (la foto completa solo va al OCR).</summary>
    public const int ReceiptMaxSide = 1600;

    /// <summary>Escala conservando la proporción para que el lado mayor no pase de <paramref name="maxSide"/>. Nunca agranda.</summary>
    public static (int Width, int Height) FitWithin(int width, int height, int maxSide)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSide);

        var longest = Math.Max(width, height);
        if (longest <= maxSide)
        {
            return (width, height);
        }

        var scale = (double)maxSide / longest;
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }
}
