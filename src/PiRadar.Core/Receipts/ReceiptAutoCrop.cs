using PiRadar.Core.Images;

namespace PiRadar.Core.Receipts;

/// <summary>
/// Marco automático para recortar la foto del recibo: los bloques de texto del OCR que forman el recibo, más un margen.
/// El texto suelto y alejado (unas pocas palabras de otro papel al lado, un letrero) no estira el marco. Solo propone;
/// el usuario lo ajusta.
/// </summary>
public static class ReceiptAutoCrop
{
    /// <summary>
    /// Distancia máxima entre recuadros del mismo bloque, en alturas de línea. Amplia a propósito: en los recibos reales
    /// hay secciones separadas por logos o espacios en blanco de varias líneas y montos lejos de su etiqueta. Ante la
    /// duda se incluye de más: el usuario achica el marco, pero un total cortado se propondría mal.
    /// </summary>
    private const double MaxGapX = 20, MaxGapY = 15;

    /// <summary>Un bloque con menos caracteres que esta fracción del más grande se considera texto suelto y se ignora.</summary>
    private const double MinBlockShare = 0.25;

    /// <summary>Margen alrededor del texto, en alturas de línea.</summary>
    private const double Margin = 2;

    /// <param name="elements">Recuadros del OCR en píxeles de la foto orientada.</param>
    public static CropRect Suggest(IReadOnlyList<OcrElement> elements, int width, int height)
    {
        var boxes = elements.Where(e => e.Width > 0 && e.Height > 0 && !string.IsNullOrWhiteSpace(e.Text)).ToList();
        if (boxes.Count == 0 || width <= 0 || height <= 0)
        {
            return CropRect.Full;
        }

        // Altura típica de línea: la mediana del lado corto (el texto vertical tiene el recuadro de lado).
        var lineHeight = Math.Max(1, boxes.Select(b => Math.Min(b.Width, b.Height)).Order().ElementAt(boxes.Count / 2));

        var block = ReceiptBlocks(boxes, lineHeight);
        var margin = Margin * lineHeight;
        return new CropRect(
            (block.Min(b => b.X) - margin) / width,
            (block.Min(b => b.Y) - margin) / height,
            (block.Max(b => b.X + b.Width) + margin) / width,
            (block.Max(b => b.Y + b.Height) + margin) / height).Normalize();
    }

    /// <summary>Solo el texto cuyo centro queda dentro del marco (para proponer total, fecha y comercio).</summary>
    public static OcrPage CropTo(this OcrPage page, CropRect crop, int width, int height)
    {
        if (crop.IsFull)
        {
            return page;
        }

        var (x, y, w, h) = crop.ToPixels(width, height);
        var inside = page.Elements.Where(e =>
        {
            var cx = e.X + e.Width / 2;
            var cy = e.Y + e.Height / 2;
            return cx >= x && cx <= x + w && cy >= y && cy <= y + h;
        }).ToList();
        return page with { Elements = inside };
    }

    /// <summary>
    /// Agrupa los recuadros cercanos (unión por cercanía) y devuelve los grupos grandes: el de más caracteres y los que
    /// tengan al menos <see cref="MinBlockShare"/> de los suyos (un recibo con un espacio grande queda en dos grupos).
    /// </summary>
    private static List<OcrElement> ReceiptBlocks(List<OcrElement> boxes, double lineHeight)
    {
        var parent = Enumerable.Range(0, boxes.Count).ToArray();
        int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);

        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                if (Gap(boxes[i].X, boxes[i].Width, boxes[j].X, boxes[j].Width) <= MaxGapX * lineHeight
                    && Gap(boxes[i].Y, boxes[i].Height, boxes[j].Y, boxes[j].Height) <= MaxGapY * lineHeight)
                {
                    parent[Find(i)] = Find(j);
                }
            }
        }

        var groups = Enumerable.Range(0, boxes.Count)
            .GroupBy(Find)
            .Select(g => (Boxes: g.Select(i => boxes[i]).ToList(), Chars: g.Sum(i => boxes[i].Text.Length)))
            .ToList();
        var largest = groups.Max(g => g.Chars);
        return groups.Where(g => g.Chars >= MinBlockShare * largest).SelectMany(g => g.Boxes).ToList();
    }

    /// <summary>Espacio entre dos tramos en un eje; 0 si se solapan.</summary>
    private static double Gap(double start1, double length1, double start2, double length2) =>
        Math.Max(0, Math.Max(start1, start2) - Math.Min(start1 + length1, start2 + length2));
}
