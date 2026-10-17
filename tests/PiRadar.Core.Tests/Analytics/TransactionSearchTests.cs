using PiRadar.Core.Analytics;
using PiRadar.Core.Domain;

namespace PiRadar.Core.Tests.Analytics;

public class TransactionSearchTests
{
    private static Transaction Tx(string? merchant = null, string? description = null, string? notes = null,
        long amount = 25958, Currency currency = Currency.COP, int categoryId = 1) => new()
    {
        Merchant = merchant,
        Description = description,
        Notes = notes,
        Amount = new Money(amount, currency),
        CategoryId = categoryId,
        OccurredAt = new DateTime(2026, 10, 1),
    };

    private static bool Match(Transaction t, string query, string? category = null, string? method = null, ISet<int>? ids = null) =>
        TransactionSearch.Matches(t, category, method, TransactionSearch.Tokens(query), ids);

    [Fact]
    public void Empty_query_matches_everything() => Assert.True(Match(Tx(), "  "));

    [Theory]
    [InlineData("exito")]
    [InlineData("ÉXITO")]
    [InlineData("éxi")]
    public void Ignores_case_and_accents(string query) => Assert.True(Match(Tx(merchant: "Éxito Laureles"), query));

    [Fact]
    public void Searches_description_notes_category_and_payment_method()
    {
        var t = Tx(description: "pizza familiar", notes: "cumpleaños");
        Assert.True(Match(t, "pizza"));
        Assert.True(Match(t, "cumpleanos"));
        Assert.True(Match(t, "restaurantes", category: "Restaurantes"));
        Assert.True(Match(t, "nequi", method: "Nequi"));
        Assert.False(Match(t, "hamburguesa"));
    }

    [Fact]
    public void All_words_must_appear()
    {
        var t = Tx(merchant: "Éxito", description: "mercado semanal");
        Assert.True(Match(t, "mercado exito"));
        Assert.False(Match(t, "mercado olimpica"));
    }

    [Theory]
    [InlineData("25958")]
    [InlineData("25.958")]
    [InlineData("25,958")]
    public void Matches_exact_cop_amount(string query) => Assert.True(Match(Tx(), query));

    [Fact]
    public void Does_not_match_a_different_amount() => Assert.False(Match(Tx(), "25959"));

    [Theory]
    [InlineData("12,50")]
    [InlineData("12.50")]
    public void Matches_usd_amount_with_decimals(string query) => Assert.True(Match(Tx(amount: 1250, currency: Currency.USD), query));

    [Fact]
    public void Matches_usd_whole_units() => Assert.True(Match(Tx(amount: 1250, currency: Currency.USD), "12"));

    [Fact]
    public void Category_filter_includes_children()
    {
        var categories = new[]
        {
            new Category { Id = 1, Name = "Comida", Icon = "x", Color = "#000000" },
            new Category { Id = 2, Name = "Restaurantes", Icon = "x", Color = "#000000", ParentId = 1 },
            new Category { Id = 3, Name = "Transporte", Icon = "x", Color = "#000000" },
        };
        var ids = TransactionSearch.CategoryAndChildren(1, categories);
        Assert.True(Match(Tx(categoryId: 2), "", ids: ids));
        Assert.True(Match(Tx(categoryId: 1), "", ids: ids));
        Assert.False(Match(Tx(categoryId: 3), "", ids: ids));
    }
}
