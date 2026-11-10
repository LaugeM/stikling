using Stikling.Core.PlantFacts;

namespace Stikling.Core.Tests;

public class PlantFactsParsingTests
{
    private const string Page = """
        {
          "type": "standard",
          "title": "Monstera deliciosa",
          "description": "Species of plant",
          "extract": "Monstera deliciosa is a species of flowering plant native to tropical forests of southern Mexico.",
          "content_urls": { "desktop": { "page": "https://en.wikipedia.org/wiki/Monstera_deliciosa" } }
        }
        """;

    [Fact]
    public void A_normal_page_gives_its_text_title_and_address()
    {
        var summary = WikipediaParser.Parse(Page, "en", genusOnly: false);

        Assert.NotNull(summary);
        Assert.StartsWith("Monstera deliciosa is a species", summary.Text);
        Assert.Equal("Monstera deliciosa", summary.Title);
        Assert.Equal("en", summary.Language);
        Assert.Equal("https://en.wikipedia.org/wiki/Monstera_deliciosa", summary.PageUrl);
    }

    [Fact]
    public void The_text_is_kept_as_it_is_sent_and_never_made_into_markup()
    {
        var json = Page.Replace("Monstera deliciosa is a species", "<b>Bold</b> &amp; <script>x()</script>");

        var summary = WikipediaParser.Parse(json, "en", genusOnly: false);

        Assert.NotNull(summary);
        Assert.Contains("<script>", summary.Text);
    }

    [Fact]
    public void A_disambiguation_page_is_not_found()
    {
        var json = Page.Replace("\"standard\"", "\"disambiguation\"");

        Assert.Null(WikipediaParser.Parse(json, "en", genusOnly: false));
    }

    [Fact]
    public void A_genus_page_that_is_not_about_a_plant_is_not_found()
    {
        const string place = """
            {
              "type": "standard",
              "title": "Ficus",
              "description": "Village in Romania",
              "extract": "Ficus is a village in the north of the country.",
              "content_urls": { "desktop": { "page": "https://en.wikipedia.org/wiki/Ficus_(village)" } }
            }
            """;

        Assert.Null(WikipediaParser.Parse(place, "en", genusOnly: true));
    }

    [Theory]
    [InlineData("en", "Genus of plants", "Ficus is a thing.")]
    [InlineData("en", "Something", "Ficus is a genus of about 850 species.")]
    [InlineData("da", "Plantesalægt", "Ficus er noget.")]
    [InlineData("da", "Noget", "Ficus er en slægt af træer.")]
    public void A_genus_page_that_says_genus_or_plant_is_accepted(string language, string description, string extract)
    {
        var json = $$"""
            {
              "type": "standard", "title": "Ficus", "description": "{{description}}", "extract": "{{extract}}",
              "content_urls": { "desktop": { "page": "https://{{language}}.wikipedia.org/wiki/Ficus" } }
            }
            """;

        Assert.NotNull(WikipediaParser.Parse(json, language, genusOnly: true));
    }

    [Fact]
    public void The_plant_check_is_only_for_a_genus_on_its_own()
    {
        var json = Page.Replace("Species of plant", "A thing").Replace("flowering plant", "kind of vine");

        Assert.NotNull(WikipediaParser.Parse(json, "en", genusOnly: false));
        Assert.Null(WikipediaParser.Parse(json, "en", genusOnly: true));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"title\":\"A\",\"extract\":\"B\"}")]
    [InlineData("{\"title\":\"A\",\"extract\":\"B\",\"content_urls\":{\"desktop\":{\"page\":\"https://evil.example/x\"}}}")]
    [InlineData("{\"title\":\"A\",\"extract\":\"B\",\"content_urls\":{\"desktop\":{\"page\":\"javascript:alert(1)\"}}}")]
    public void A_page_that_cant_be_read_or_has_no_wikipedia_address_is_not_found(string json) =>
        Assert.Null(WikipediaParser.Parse(json, "en", genusOnly: false));

    [Fact]
    public void Trefle_facts_are_read_when_filled_in()
    {
        const string json = """
            {
              "data": {
                "scientific_name": "Monstera deliciosa",
                "edible": true,
                "specifications": { "maximum_height": { "cm": 2000 }, "growth_habit": "Vine", "toxicity": "high" },
                "growth": { "bloom_months": ["jan", "mar", "feb", "mar"], "light": 8, "soil_humidity": 7 },
                "sources": [
                  { "name": "Plants of the World Online", "url": "https://powo.science.kew.org/", "last_update": "2020" },
                  { "name": "Local note", "url": null },
                  { "name": "  " }
                ]
              }
            }
            """;

        var facts = TrefleParser.Parse(json);

        Assert.NotNull(facts);
        Assert.Equal(2000, facts.MaxHeightCm);
        Assert.Equal("Vine", facts.GrowthHabit);
        Assert.Equal([1, 2, 3], facts.BloomMonths);
        Assert.Equal(
            [new FactSource("Plants of the World Online", "https://powo.science.kew.org/"), new FactSource("Local note", null)],
            facts.Sources);
    }

    [Fact]
    public void Only_the_facts_that_are_not_about_care_exist_on_the_result()
    {
        var names = typeof(TrefleFacts).GetProperties().Select(p => p.Name).Order().ToArray();

        Assert.Equal(["BloomMonths", "GrowthHabit", "MaxHeightCm", "Sources"], names);
    }

    [Theory]
    [InlineData("{\"data\":{}}")]
    [InlineData("{\"data\":{\"specifications\":{},\"growth\":{\"bloom_months\":[]}}}")]
    [InlineData("{\"data\":{\"specifications\":{\"maximum_height\":{\"cm\":null},\"growth_habit\":null},\"growth\":{\"bloom_months\":null}}}")]
    [InlineData("{\"data\":{\"sources\":[{\"name\":\"X\"}]}}")]
    [InlineData("{\"data\":{\"specifications\":{\"growth_habit\":\"Graminoid\"}}}")]
    [InlineData("{\"data\":[]}")]
    [InlineData("[]")]
    [InlineData("not json")]
    public void Trefle_with_nothing_to_show_gives_nothing(string json) => Assert.Null(TrefleParser.Parse(json));

    [Fact]
    public void Missing_or_unreadable_fields_are_left_out_without_failing()
    {
        const string json = """
            {
              "data": {
                "specifications": { "maximum_height": { "cm": "tall" }, "growth_habit": "Forb/herb" },
                "growth": { "bloom_months": ["xyz", 5, "Dec", null] },
                "sources": "none"
              }
            }
            """;

        var facts = TrefleParser.Parse(json);

        Assert.NotNull(facts);
        Assert.Null(facts.MaxHeightCm);
        Assert.Equal("Forb/herb", facts.GrowthHabit);
        Assert.Equal([12], facts.BloomMonths);
        Assert.Empty(facts.Sources);
    }

    [Theory]
    [InlineData("Tree", "Tree")]
    [InlineData("shrub", "Shrub")]
    [InlineData("Subshrub", "Subshrub")]
    [InlineData("Vine", "Vine")]
    [InlineData("forb/herb", "Forb/herb")]
    public void The_growth_habits_that_are_shown_are_the_usda_ones(string given, string expected)
    {
        var facts = TrefleParser.Parse("{\"data\":{\"specifications\":{\"growth_habit\":\"" + given + "\"}}}");

        Assert.Equal(expected, facts?.GrowthHabit);
    }

    [Fact]
    public void Bloom_months_come_from_the_first_three_letters_in_any_case()
    {
        var facts = TrefleParser.Parse("""{"data":{"growth":{"bloom_months":["Sep","OCT","november","apr"]}}}""");

        Assert.Equal([4, 9, 10, 11], facts?.BloomMonths);
    }

    [Theory]
    [InlineData("Monstera", "deliciosa", "Monstera_deliciosa")]
    [InlineData("Monstera", null, "Monstera")]
    [InlineData("Pelargonium", "x hortorum", "Pelargonium_x_hortorum")]
    public void Wikipedia_titles_use_underscores(string genus, string? species, string title) =>
        Assert.Equal(title, PlantFactNames.WikipediaTitle(genus, species));

    [Theory]
    [InlineData("Monstera", "deliciosa", "monstera-deliciosa")]
    [InlineData("Pelargonium", "×hortorum", "pelargonium-hortorum")]
    [InlineData("Zamioculcas", "zamiifolia  ", "zamioculcas-zamiifolia")]
    public void Trefle_slugs_are_lower_case_with_hyphens(string genus, string species, string slug) =>
        Assert.Equal(slug, PlantFactNames.TrefleSlug(genus, species));

    [Theory]
    [InlineData("Monstera", true)]
    [InlineData("monstera deliciosa", true)]
    [InlineData("Pelargonium ×hortorum", true)]
    [InlineData("Ficus-x", true)]
    [InlineData("Æbletræ", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    [InlineData("deliciosa var. borsigiana", false)]
    [InlineData("Monstera1", false)]
    [InlineData("Monstera/deliciosa", false)]
    [InlineData("Mon<b>stera", false)]
    [InlineData("Monstera?x=1", false)]
    public void Names_are_letters_hyphens_times_signs_and_spaces(string? name, bool valid) =>
        Assert.Equal(valid, PlantFactNames.IsValid(name));

    [Fact]
    public void Names_are_at_most_60_characters()
    {
        Assert.True(PlantFactNames.IsValid(new string('a', 60)));
        Assert.False(PlantFactNames.IsValid(new string('a', 61)));
    }
}
