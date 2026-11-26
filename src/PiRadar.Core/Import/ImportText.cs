using System.Text;
using PiRadar.Core.Receipts;

namespace PiRadar.Core.Import;

/// <summary>Normalización de textos de archivos de importación.</summary>
internal static class ImportText
{
    /// <summary>Mayúsculas sin tildes, solo letras y dígitos separados por un espacio: "Categoría " ⇒ "CATEGORIA".</summary>
    public static string Key(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var folded = TextNormalizer.Fold(text);
        var sb = new StringBuilder(folded.Length);
        var pendingSpace = false;
        foreach (var c in folded)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (pendingSpace && sb.Length > 0) sb.Append(' ');
                pendingSpace = false;
                sb.Append(c);
            }
            else pendingSpace = true;
        }
        return sb.ToString();
    }

    /// <summary>Recorta y colapsa espacios internos; null si queda vacío.</summary>
    public static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var sb = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c) || c == ' ') pendingSpace = true;
            else
            {
                if (pendingSpace && sb.Length > 0) sb.Append(' ');
                pendingSpace = false;
                sb.Append(c);
            }
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    /// <summary>Texto de una celda para guardar (números y fechas en forma invariante).</summary>
    public static string? CleanCell(CellValue cell) => Clean(cell.ToDisplayString());

    /// <summary>true si <paramref name="key"/> contiene <paramref name="phrase"/> como palabras completas.</summary>
    public static bool ContainsWords(string key, string phrase) =>
        (" " + key + " ").Contains(" " + phrase + " ", StringComparison.Ordinal);
}
