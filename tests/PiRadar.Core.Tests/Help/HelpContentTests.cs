using System.Text.RegularExpressions;
using PiRadar.Core.Domain;
using PiRadar.Core.Help;

namespace PiRadar.Core.Tests.Help;

public class HelpContentTests
{
    private static readonly string[] OtherPlatforms = ["android", "ios", "iphone", "ipad", "google", "apple", "play store", "app store"];

    private static IEnumerable<string> AllTexts() =>
        HelpContent.Sections.SelectMany(s => new[] { s.Title.Es, s.Title.En }
            .Concat(s.Articles.SelectMany(a => new[] { a.Question.Es, a.Question.En }
                .Concat(a.Steps.SelectMany(t => new[] { t.Es, t.En }))
                .Concat(a.Tip is { } tip ? [tip.Es, tip.En] : []))));

    [Fact]
    public void Ids_are_unique_and_url_safe()
    {
        var ids = HelpContent.Sections.Select(s => s.Id).Concat(HelpContent.Sections.SelectMany(s => s.Articles.Select(a => a.Id))).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.Matches("^[a-z0-9-]+$", id));
    }

    [Fact]
    public void Every_text_exists_in_both_languages()
    {
        Assert.NotEmpty(HelpContent.Sections);
        Assert.All(HelpContent.Sections, s =>
        {
            Assert.NotEmpty(s.Articles);
            Assert.All(s.Articles, a => Assert.NotEmpty(a.Steps));
        });
        Assert.All(AllTexts(), t => Assert.False(string.IsNullOrWhiteSpace(t)));
    }

    [Fact]
    public void Spanish_and_english_use_the_same_number_of_bold_button_names()
    {
        var pairs = HelpContent.Sections.SelectMany(s => s.Articles).SelectMany(a => a.Steps.Concat(a.Tip is { } t ? [t] : []));
        Assert.All(pairs, t => Assert.Equal(t.Es.Split("**").Length, t.En.Split("**").Length));
    }

    [Fact]
    public void Content_does_not_name_other_platforms_or_stores()
    {
        // La ficha de la App Store no puede hablar de otras plataformas (guideline 2.3.10) y la web de ayuda se enlaza desde ella.
        Assert.All(AllTexts(), t => Assert.DoesNotContain(OtherPlatforms, p => Regex.IsMatch(t, $@"\b{Regex.Escape(p)}\b", RegexOptions.IgnoreCase)));
    }

    [Fact]
    public void Markup_escapes_html_and_formats_bold_and_code()
    {
        Assert.Equal("Toca <strong>Guardar</strong> &lt;ya&gt; y abre <code>.piradar</code>", HelpMarkup.ToHtml("Toca **Guardar** <ya> y abre `.piradar`"));
    }

    [Fact]
    public void Every_contextual_help_topic_exists()
    {
        Assert.All(HelpTopics.All.SelectMany(t => t), id => Assert.NotNull(HelpContent.FindArticle(id)));
        Assert.All(HelpTopics.All, topics => Assert.Equal(topics.Length, topics.Distinct().Count()));
    }

    [Fact]
    public void Filter_article_explains_the_search_dates()
    {
        // Los nombres en negrita deben ser los de la pantalla Movimientos.
        var steps = HelpContent.FindArticle("filtrar-movimientos")!.Steps;
        Assert.Contains(steps, t => t.Es.Contains("**Desde**") && t.Es.Contains("**Hasta**") && t.Es.Contains("**Todo el historial**"));
        Assert.Contains(steps, t => t.En.Contains("**From**") && t.En.Contains("**To**") && t.En.Contains("**Whole history**"));
        Assert.DoesNotContain(steps, t => t.Es.Contains("todo tu historial") || t.En.Contains("searches your whole history"));
        // Las fechas solo cuentan con texto o categoría: si la ayuda no lo dice, parecen no funcionar.
        Assert.Contains(steps, t => t.Es.Contains("Al abrirla") && t.Es.Contains("escribes algo o eliges una categoría"));
        Assert.Contains(steps, t => t.En.Contains("When you open it") && t.En.Contains("type something or choose a category"));
    }
}
