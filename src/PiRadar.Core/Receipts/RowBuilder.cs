namespace PiRadar.Core.Receipts;

/// <summary>Trozo de texto con un solo recuadro: una palabra (Android) o un tramo de línea (iOS).</summary>
public sealed record OcrChunk(string Text, double Left, double Top, double Right, double Bottom)
{
    public double Height => Bottom - Top;
    public double CenterY => (Top + Bottom) / 2;
}

/// <summary>Fila de texto en orden de lectura, con sus trozos ordenados de izquierda a derecha.</summary>
public sealed record ReceiptRow(IReadOnlyList<OcrChunk> Chunks)
{
    public string Text { get; } = string.Join(' ', Chunks.Select(c => c.Text));
    public double Top => Chunks.Min(c => c.Top);
    public double Bottom => Chunks.Max(c => c.Bottom);
    public double CenterY => Chunks.Average(c => c.CenterY);
    public double Height => Chunks.Select(c => c.Height).Order().ElementAt(Chunks.Count / 2);

    public override string ToString() => Text;
}

/// <summary>
/// Reconstruye filas a partir de los elementos del OCR, que llegan por columnas o bloques y no en orden de lectura
/// (la etiqueta "TOTAL A PAGAR" y su monto llegan separados).
/// </summary>
public static class RowBuilder
{
    /// <summary>Fracción mínima de la altura menor que dos trozos deben compartir verticalmente para ir en la misma fila.</summary>
    private const double MinVerticalOverlap = 0.5;

    public static IReadOnlyList<ReceiptRow> Build(IEnumerable<OcrElement> elements)
    {
        var chunks = ToChunks(elements).ToList();

        // El texto vertical (al borde de la factura: imprenta, resoluciones, avisos del comercio) nunca trae el total
        // ni los datos de la compra, y pegado a las filas las daña ("TOTAL $ 120.000 IMPRESO POR ... NIT ... TEL").
        // Si casi todo es vertical, la foto quedó de lado: entonces no se quita nada.
        var vertical = chunks.Where(IsVertical).ToList();
        if (vertical.Count * 2 < chunks.Count)
        {
            chunks = chunks.Except(vertical).ToList();
        }

        var rows = new List<List<OcrChunk>>();
        foreach (var chunk in chunks.OrderBy(c => c.CenterY).ThenBy(c => c.Left))
        {
            var best = rows
                .Select(row => (row, score: Fit(row, chunk)))
                .Where(x => x.score >= MinVerticalOverlap)
                .OrderByDescending(x => x.score)
                .Select(x => x.row)
                .FirstOrDefault();
            if (best is null)
            {
                rows.Add([chunk]);
            }
            else
            {
                best.Add(chunk);
            }
        }

        return rows
            .Select(r => new ReceiptRow(r.OrderBy(c => c.Left).ToList()))
            .OrderBy(r => r.CenterY)
            .ToList();
    }

    public static IReadOnlyList<ReceiptRow> Build(OcrPage page) => Build(page.Elements);

    /// <summary>
    /// En iOS todas las palabras de un tramo comparten recuadro: se unen en un trozo conservando su orden original.
    /// En Android cada palabra ya es un trozo.
    /// </summary>
    private static IEnumerable<OcrChunk> ToChunks(IEnumerable<OcrElement> elements)
    {
        OcrElement? current = null;
        var words = new List<string>();
        foreach (var e in elements)
        {
            if (current is not null && SameBox(current, e))
            {
                words.Add(e.Text.Trim());
                continue;
            }

            if (current is not null)
            {
                yield return ToChunk(current, words);
            }

            current = e;
            words = [e.Text.Trim()];
        }

        if (current is not null)
        {
            yield return ToChunk(current, words);
        }
    }

    /// <summary>
    /// Recuadro alto y angosto con varias letras: texto girado 90°. Una palabra horizontal de 3+ caracteres siempre es
    /// más ancha que alta; se exige el doble para no descartar palabras cortas mal recortadas.
    /// </summary>
    private static bool IsVertical(OcrChunk chunk) =>
        chunk.Text.Count(c => !char.IsWhiteSpace(c)) >= 3 && chunk.Height >= 2 * (chunk.Right - chunk.Left);

    private static bool SameBox(OcrElement a, OcrElement b) =>
        a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;

    private static OcrChunk ToChunk(OcrElement box, List<string> words) =>
        new(string.Join(' ', words.Where(w => w.Length > 0)), box.X, box.Y, box.X + box.Width, box.Y + box.Height);

    /// <summary>
    /// Qué tanto encaja el trozo en la fila, comparando con el trozo de la fila más cercano en X: así se toleran
    /// recibos inclinados, donde una misma línea sube o baja de izquierda a derecha. 0 si se solapa en X con un trozo
    /// de la fila (serían dos líneas distintas).
    /// </summary>
    private static double Fit(List<OcrChunk> row, OcrChunk chunk)
    {
        foreach (var other in row)
        {
            var horizontal = Math.Min(other.Right, chunk.Right) - Math.Max(other.Left, chunk.Left);
            if (horizontal > 0.3 * Math.Min(other.Right - other.Left, chunk.Right - chunk.Left))
            {
                return 0;
            }
        }

        var nearest = row.MinBy(o => Math.Max(0, Math.Max(o.Left - chunk.Right, chunk.Left - o.Right)))!;
        var vertical = Math.Min(nearest.Bottom, chunk.Bottom) - Math.Max(nearest.Top, chunk.Top);
        var minHeight = Math.Min(nearest.Height, chunk.Height);
        return minHeight <= 0 ? 0 : vertical / minHeight;
    }
}
