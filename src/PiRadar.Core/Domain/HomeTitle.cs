using System.Globalization;
using System.Text;

namespace PiRadar.Core.Domain;

/// <summary>
/// Título de la pantalla de inicio que el usuario puede cambiar ("Mis finanzas"); debajo siempre se ve "piRadar".
/// </summary>
public static class HomeTitle
{
    /// <summary>Título predeterminado en el idioma vigente.</summary>
    public static string Default => AppLocale.Pick("Tus cuentas protegidas", "Your money, protected");

    public const int MaxLength = 50;

    /// <summary>
    /// Sin espacios de más y con máximo <see cref="MaxLength"/> caracteres (sin partir tildes ni emojis).
    /// <c>null</c> si queda vacío, que significa usar <see cref="Default"/>.
    /// </summary>
    public static string? Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var collapsed = string.Join(' ', text.Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (collapsed.Length <= MaxLength)
        {
            return collapsed;
        }

        var cut = 0;
        var elements = StringInfo.GetTextElementEnumerator(collapsed);
        while (elements.MoveNext() && elements.ElementIndex + elements.GetTextElement().Length <= MaxLength)
        {
            cut = elements.ElementIndex + elements.GetTextElement().Length;
        }

        return collapsed[..cut].TrimEnd();
    }
}
