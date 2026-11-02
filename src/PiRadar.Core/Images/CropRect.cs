namespace PiRadar.Core.Images;

/// <summary>
/// Marco de recorte normalizado (0–1) sobre la foto ya orientada: independiente del tamaño en píxeles, así sirve igual
/// para la foto completa del OCR y para la de ~1600 px que se guarda.
/// </summary>
public sealed record CropRect(double Left, double Top, double Right, double Bottom)
{
    /// <summary>Lado mínimo del marco (fracción de la foto): uno más chico no deja nada legible.</summary>
    public const double MinSide = 0.1;

    public static CropRect Full { get; } = new(0, 0, 1, 1);

    public double Width => Right - Left;
    public double Height => Bottom - Top;

    public bool IsFull => Left <= 0 && Top <= 0 && Right >= 1 && Bottom >= 1;

    /// <summary>Ordena los lados, los deja dentro de la foto y agranda el marco hasta <see cref="MinSide"/> si hace falta.</summary>
    public CropRect Normalize()
    {
        var (left, right) = Side(Math.Min(Left, Right), Math.Max(Left, Right));
        var (top, bottom) = Side(Math.Min(Top, Bottom), Math.Max(Top, Bottom));
        return new CropRect(left, top, right, bottom);
    }

    private static (double Start, double End) Side(double start, double end)
    {
        start = Math.Clamp(start, 0, 1);
        end = Math.Clamp(end, 0, 1);
        if (end - start >= MinSide)
        {
            return (start, end);
        }

        // Crece alrededor del centro y, si se sale por un borde, se corre hacia adentro.
        var center = (start + end) / 2;
        start = Math.Clamp(center - MinSide / 2, 0, 1 - MinSide);
        return (start, start + MinSide);
    }

    /// <summary>El marco en píxeles de una foto de <paramref name="width"/> × <paramref name="height"/> (al menos 1 px).</summary>
    public (int X, int Y, int Width, int Height) ToPixels(int width, int height)
    {
        var n = Normalize();
        var x = Math.Clamp((int)Math.Round(n.Left * width), 0, width - 1);
        var y = Math.Clamp((int)Math.Round(n.Top * height), 0, height - 1);
        var right = Math.Clamp((int)Math.Round(n.Right * width), x + 1, width);
        var bottom = Math.Clamp((int)Math.Round(n.Bottom * height), y + 1, height);
        return (x, y, right - x, bottom - y);
    }
}
