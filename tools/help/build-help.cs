#:project ../../src/PiRadar.Core/PiRadar.Core.csproj
#:property PublishAot=false

// Genera las páginas web de ayuda (español e inglés) desde HelpContent, la misma fuente de la pantalla Ayuda de la app.
// Uso: dotnet run tools/help/build-help.cs -- [carpeta-salida]   (por defecto store/help)
// Luego subir ayuda.html y help.html a https://www.belvicto.com/piRadar/ (publicación manual).
using System.Net;
using System.Text;
using PiRadar.Core.Domain;
using PiRadar.Core.Help;

var outDir = args.Length > 0 ? args[0] : "store/help";
Directory.CreateDirectory(outDir);
var today = DateTime.Today;

File.WriteAllText(Path.Combine(outDir, "ayuda.html"), Render(AppLanguage.Spanish, today), new UTF8Encoding(false));
File.WriteAllText(Path.Combine(outDir, "help.html"), Render(AppLanguage.English, today), new UTF8Encoding(false));
Console.WriteLine($"Escrito en {Path.GetFullPath(outDir)}: ayuda.html, help.html");

static string Rich(string text) => HelpMarkup.ToHtml(text);

static string Render(AppLanguage lang, DateTime today)
{
    var es = lang == AppLanguage.Spanish;
    var culture = AppLocale.For(lang);
    var title = es ? "Ayuda de piRadar" : "piRadar Help";
    var intro = es
        ? "Guía rápida para usar piRadar: registrar movimientos, escanear recibos, entender tus análisis y cuidar tus datos. Toca una pregunta del índice para ir directo a ella."
        : "A quick guide to using piRadar: adding transactions, scanning receipts, understanding your analytics and taking care of your data. Tap a question in the contents to jump straight to it.";
    var sb = new StringBuilder();
    sb.Append($$"""
<!DOCTYPE html>
<html lang="{{(es ? "es" : "en")}}">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>{{title}}</title>
    <meta name="description" content="{{WebUtility.HtmlEncode(intro)}}" />
    <style>
      :root {
        --bg: #f6f8fb; --card: #ffffff; --text: #1f2937; --muted: #4b5563; --border: #e5e7eb;
        --accent: #00796b; --accent-soft: #e0f2f1; --code: #f3f4f6;
      }
      @media (prefers-color-scheme: dark) {
        :root {
          --bg: #0f1a19; --card: #16241f; --text: #e5eceb; --muted: #aab8b5; --border: #26403a;
          --accent: #4db6ac; --accent-soft: #1d3a35; --code: #1d2f2b;
        }
      }
      * { box-sizing: border-box; }
      body { margin: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
        background: var(--bg); color: var(--text); line-height: 1.7; }
      .container { max-width: 900px; margin: 32px auto; padding: 0 20px 40px; }
      .card { background: var(--card); border: 1px solid var(--border); border-radius: 18px;
        box-shadow: 0 8px 24px rgba(15, 23, 42, 0.04); padding: 32px 28px; }
      h1, h2, h3 { line-height: 1.3; margin-top: 0; scroll-margin-top: 16px; }
      h1 { font-size: clamp(2rem, 4vw, 2.6rem); margin-bottom: 8px; }
      h2 { font-size: 1.45rem; margin-top: 36px; margin-bottom: 12px; padding-top: 14px; border-top: 1px solid var(--border); }
      h3 { font-size: 1.1rem; margin-top: 24px; margin-bottom: 8px; }
      p { margin: 0 0 16px; color: var(--muted); }
      ol, ul { margin: 0 0 16px 20px; padding-left: 10px; color: var(--muted); }
      li { margin-bottom: 8px; }
      strong { color: var(--text); }
      code { background: var(--code); border-radius: 6px; padding: 1px 6px; font-size: 0.92em; }
      .meta { display: inline-block; margin: 8px 0 20px; padding: 8px 12px; border-radius: 999px;
        background: var(--accent-soft); color: var(--accent); font-size: 0.92rem; font-weight: 600; }
      .tip { background: var(--accent-soft); border-radius: 12px; padding: 10px 14px; color: var(--text); margin: 0 0 16px; }
      .toc { background: var(--accent-soft); border-radius: 14px; padding: 16px 20px; margin: 8px 0 8px; }
      .toc ul { list-style: none; margin: 0; padding: 0; }
      .toc li { margin: 0; }
      .toc .sec { font-weight: 700; margin-top: 12px; color: var(--text); }
      .toc .sec:first-child { margin-top: 0; }
      .toc ul ul { margin-left: 16px; }
      .toc ul ul li { margin-bottom: 2px; font-weight: 400; }
      a { color: var(--accent); text-decoration: none; }
      a:hover { text-decoration: underline; }
      .lang { float: right; font-size: 0.95rem; }
      @media (max-width: 600px) { .card { padding: 24px 18px; } .lang { float: none; display: block; margin-bottom: 8px; } }
    </style>
  </head>
  <body>
    <main class="container">
      <article class="card">

""");
    sb.Append($"        <a class=\"lang\" href=\"{(es ? "help.html" : "ayuda.html")}\" hreflang=\"{(es ? "en" : "es")}\">{(es ? "English" : "Español")}</a>\n");
    sb.Append($"        <h1>{title}</h1>\n");
    var date = es ? today.ToString("d 'de' MMMM 'de' yyyy", culture) : today.ToString("MMMM d, yyyy", culture);
    sb.Append($"        <div class=\"meta\">{(es ? "Actualizado" : "Updated")}: {date}</div>\n");

    sb.Append($"        <p>{WebUtility.HtmlEncode(intro)}</p>\n");
    sb.Append($"        <nav class=\"toc\" aria-label=\"{(es ? "Contenido" : "Contents")}\">\n          <ul>\n");
    foreach (var s in HelpContent.Sections)
    {
        sb.Append($"            <li class=\"sec\"><a href=\"#{s.Id}\">{Rich(s.Title.In(lang))}</a>\n              <ul>\n");
        foreach (var a in s.Articles)
        {
            sb.Append($"                <li><a href=\"#{a.Id}\">{Rich(a.Question.In(lang))}</a></li>\n");
        }

        sb.Append("              </ul>\n            </li>\n");
    }

    sb.Append("          </ul>\n        </nav>\n");
    foreach (var s in HelpContent.Sections)
    {
        sb.Append($"\n        <h2 id=\"{s.Id}\">{Rich(s.Title.In(lang))}</h2>\n");
        foreach (var a in s.Articles)
        {
            sb.Append($"        <h3 id=\"{a.Id}\">{Rich(a.Question.In(lang))}</h3>\n        <ol>\n");
            foreach (var step in a.Steps)
            {
                sb.Append($"          <li>{Rich(step.In(lang))}</li>\n");
            }

            sb.Append("        </ol>\n");
            if (a.Tip is { } tip)
            {
                sb.Append($"        <p class=\"tip\">{Rich(tip.In(lang))}</p>\n");
            }
        }
    }

    sb.Append($"\n        <h2>{(es ? "Contacto" : "Contact")}</h2>\n");
    sb.Append($"        <p>{(es ? "¿Sigues con dudas? Escríbenos a" : "Still have questions? Write to")} <a href=\"mailto:{HelpContent.ContactEmail}\">{HelpContent.ContactEmail}</a>.</p>\n");
    sb.Append("      </article>\n    </main>\n  </body>\n</html>\n");
    return sb.ToString();
}
