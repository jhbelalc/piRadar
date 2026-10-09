using System.Text.RegularExpressions;
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
}
