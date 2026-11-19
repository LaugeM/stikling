using System.Xml.Linq;
using Stikling.Core.Guides;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

/// <summary>
/// The pages on other sites are structure in Core and text in Stikling.Web's Resources/OutsidePages.resx.
/// These keep the two in step and keep the list to what docs/dev/outside-links.md says.
/// </summary>
public class OutsidePagesTests
{
    private static Dictionary<string, string> Read(string file)
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Stikling.slnx")))
            folder = folder.Parent;
        var path = Path.Combine(folder?.FullName ?? throw new InvalidOperationException("Stikling.slnx not found."), "src", "Stikling.Web", "Resources", file);
        return XDocument.Load(path).Root!.Elements("data").ToDictionary(d => d.Attribute("name")!.Value, d => d.Element("value")!.Value);
    }

    public static TheoryData<string> Ids => new(OutsidePages.All.Select(p => p.Id));

    [Fact]
    public void Ids_are_unique_and_so_are_the_addresses()
    {
        Assert.Equal(OutsidePages.All.Count, OutsidePages.All.Select(p => p.Id).Distinct().Count());
        var urls = OutsidePages.All.SelectMany(p => new[] { p.Url, p.DanishUrl }).Where(u => u is not null).ToList();
        Assert.Equal(urls.Count, urls.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Every_address_is_https(string id)
    {
        var page = OutsidePages.Find(id)!;
        Assert.StartsWith("https://", page.Url);
        Assert.True(Uri.TryCreate(page.Url, UriKind.Absolute, out _));
        if (page.DanishUrl is not null)
        {
            Assert.StartsWith("https://", page.DanishUrl);
            Assert.True(Uri.TryCreate(page.DanishUrl, UriKind.Absolute, out _));
        }
        Assert.False(string.IsNullOrWhiteSpace(page.Site));
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Every_page_has_a_title_and_a_line_in_English_and_Danish(string id)
    {
        var page = OutsidePages.Find(id)!;
        var english = Read("OutsidePages.resx");
        var danish = Read("OutsidePages.da.resx");
        foreach (var key in new[] { page.TitleKey, page.LineKey })
        {
            Assert.True(english.TryGetValue(key, out var en) && en.Length > 0, $"{key} has no English");
            Assert.True(danish.TryGetValue(key, out var da) && da.Length > 0, $"{key} has no Danish");
        }
    }

    [Fact]
    public void The_two_files_have_the_same_keys_and_none_is_unused()
    {
        var english = Read("OutsidePages.resx");
        var danish = Read("OutsidePages.da.resx");
        Assert.Equal(english.Keys.Order(), danish.Keys.Order());

        var used = OutsidePages.All.SelectMany(p => new[] { p.TitleKey, p.LineKey }).ToHashSet();
        var unused = english.Keys.Where(k => !used.Contains(k) && !Chrome.Contains(k)).ToList();
        Assert.Empty(unused);
    }

    /// <summary>The headings and words around the pages, which the app asks for by name.</summary>
    private static readonly string[] Chrome =
        ["PageHeading", "Intro", "GroupPests", "GroupPlantGroups", "GroupPropagating", "GroupGrowingMedia", "InEnglish", "InDanish", "ReadMore"];

    [Fact]
    public void The_Danish_address_is_only_used_in_Danish()
    {
        foreach (var page in OutsidePages.All.Where(p => p.DanishUrl is not null))
        {
            Assert.Equal(page.Url, page.UrlFor("en"));
            Assert.Equal(page.Site, page.SiteFor("en"));
            Assert.False(page.IsDanishPage("en"));
            Assert.Equal(page.DanishUrl, page.UrlFor("da"));
            Assert.True(page.IsDanishPage("da"));
        }
    }

    [Fact]
    public void A_page_with_no_Danish_address_opens_the_English_one_in_Danish()
    {
        foreach (var page in OutsidePages.All.Where(p => p.DanishUrl is null))
        {
            Assert.Equal(page.Url, page.UrlFor("da"));
            Assert.False(page.IsDanishPage("da"));
        }
    }

    [Theory]
    [InlineData(Pest.Thrips)]
    [InlineData(Pest.SpiderMites)]
    [InlineData(Pest.FungusGnats)]
    [InlineData(Pest.Mealybugs)]
    [InlineData(Pest.Scale)]
    [InlineData(Pest.Aphids)]
    [InlineData(Pest.Whitefly)]
    [InlineData(Pest.Other)]
    public void Every_pest_a_case_can_be_for_has_one_page(Pest pest)
    {
        Assert.Single(OutsidePages.ForPest(pest));
    }

    [Fact]
    public void Every_pest_is_covered()
    {
        Assert.All(Enum.GetValues<Pest>(), pest => Assert.NotEmpty(OutsidePages.ForPest(pest)));
    }

    [Fact]
    public void Every_page_for_a_plant_group_names_a_guide_that_exists_and_every_group_has_a_page()
    {
        var topics = OutsidePages.ForKind(OutsideKind.PlantGroup).Select(p => p.Topic).ToHashSet();
        foreach (var guide in GuideBook.All.Where(g => g.Group == GuideBook.PlantGroups))
            Assert.Contains(guide.Id, topics);
        Assert.All(topics, t => Assert.NotNull(GuideBook.Find(t)));
    }

    [Theory]
    [InlineData("Monstera", "Araceae", "aroids")]
    [InlineData("Hoya", null, "hoyas")]
    [InlineData("hoya", null, "hoyas")]
    [InlineData("Tillandsia", "Bromeliaceae", "air-plants")]
    [InlineData("Dionaea", "Droseraceae", "venus-flytrap")]
    [InlineData("Phalaenopsis", "Orchidaceae", "moth-orchids")]
    public void A_plant_gets_the_pages_for_its_genus_first(string genus, string? family, string first)
    {
        Assert.Equal(first, OutsidePages.ForPlant(genus, family)[0].Id);
    }

    [Fact]
    public void A_plant_in_a_group_also_gets_the_page_for_the_whole_group()
    {
        Assert.Equal(["air-plants", "bromeliads"], OutsidePages.ForPlant("Tillandsia", "Bromeliaceae").Select(p => p.Id));
        Assert.Equal(["bromeliads"], OutsidePages.ForPlant("Guzmania", "Bromeliaceae").Select(p => p.Id));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("Ficus", "Moraceae")]
    [InlineData("Pilea", null)]
    public void A_plant_in_no_group_gets_no_pages_and_nothing_fails(string? genus, string? family)
    {
        Assert.Empty(OutsidePages.ForPlant(genus, family));
    }

    [Fact]
    public void A_plant_gets_pages_exactly_when_it_gets_a_guide()
    {
        foreach (var (genus, family) in new (string?, string?)[] { ("Monstera", "Araceae"), ("Begonia", null), ("Ficus", null), ("Aloe", null), ("Goeppertia", null) })
            Assert.Equal(GuideBook.ForPlant(genus, family) is not null, OutsidePages.ForPlant(genus, family).Count > 0);
    }

    [Fact]
    public void Cuttings_and_soil_each_have_a_page()
    {
        Assert.NotEmpty(OutsidePages.ForKind(OutsideKind.Cuttings));
        Assert.NotEmpty(OutsidePages.ForKind(OutsideKind.Soil));
    }
}
