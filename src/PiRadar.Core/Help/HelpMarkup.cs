using System.Net;
using System.Text.RegularExpressions;

namespace PiRadar.Core.Help;

/// <summary>Convierte el formato mínimo de <see cref="HelpContent"/> (<c>**negrita**</c> y <c>`código`</c>) a HTML seguro.</summary>
public static partial class HelpMarkup
{
    public static string ToHtml(string text)
    {
        var html = WebUtility.HtmlEncode(text);
        html = Bold().Replace(html, "<strong>$1</strong>");
        return Code().Replace(html, "<code>$1</code>");
    }

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex("`(.+?)`")]
    private static partial Regex Code();
}
