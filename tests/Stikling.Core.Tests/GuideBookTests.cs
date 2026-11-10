using System.Xml.Linq;
using Stikling.Core.Guides;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

/// <summary>
/// The guides are structure in Core and text in Stikling.Web's Resources/Guides.resx. These keep the two in step,
/// and keep every guide written from sources, as docs/dev/guides.md says.
/// </summary>
public class GuideBookTests
{
    private static string ResourcesFolder()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Stikling.slnx")))
            folder = folder.Parent;
        return Path.Combine(folder?.FullName ?? throw new InvalidOperationException("Stikling.slnx not found."), "src", "Stikling.Web", "Resources");
    }

    private static Dictionary<string, string> Read(string file) =>
        XDocument.Load(Path.Combine(ResourcesFolder(), file)).Root!
            .Elements("data")
            .ToDictionary(d => d.Attribute("name")!.Value, d => d.Element("value")!.Value);

    public static TheoryData<string> GuideIds => new(GuideBook.All.Select(g => g.Id));

    [Fact]
    public void Ids_are_unique()
    {
        Assert.Equal(GuideBook.All.Count, GuideBook.All.Select(g => g.Id).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(GuideIds))]
    public void Every_guide_is_written_from_sources_that_open_in_the_browser(string id)
    {
        var guide = GuideBook.Find(id)!;
        Assert.NotEmpty(guide.Sources);
        Assert.Equal(guide.Sources.Count, guide.Sources.Select(s => s.Id).Distinct().Count());
        Assert.All(guide.Sources, s =>
        {
            Assert.StartsWith("https://", s.Url);
            Assert.False(string.IsNullOrWhiteSpace(s.Title));
            Assert.False(string.IsNullOrWhiteSpace(s.Publisher));
        });
    }

    [Theory]
    [MemberData(nameof(GuideIds))]
    public void Every_text_a_guide_uses_has_English_and_Danish(string id)
    {
        var english = Read("Guides.resx");
        var danish = Read("Guides.da.resx");
        foreach (var key in GuideBook.Find(id)!.Keys.Distinct())
        {
            Assert.True(english.TryGetValue(key, out var en) && en.Length > 0, $"{key} has no English");
            Assert.True(danish.TryGetValue(key, out var da) && da.Length > 0, $"{key} has no Danish");
        }
    }

    [Fact]
    public void Every_text_in_the_resource_file_is_used_or_is_a_shared_label()
    {
        var used = GuideBook.All.SelectMany(g => g.Keys).ToHashSet();
        var unused = Read("Guides.resx").Keys.Where(k => !used.Contains(k)).ToList();

        // The texts that are not part of a guide's structure: the list, the page around a guide and the links to it
        string[] shared =
        [
            "PageTitle", "Title", "Intro", "GroupPropagating", "GroupGrowingMedia", "BackToGuides", "BackTo", "BackToHelp",
            "LinkAll", "SearchGroup", "SuppliesRowDetail", "WrittenFrom", "WhereThisComesFrom", "InStikling",
            "TrackHeading", "TrackStart", "TrackMoving", "TrackMovingLink", "TrackFromList"
        ];
        var stray = unused.Where(k => !shared.Contains(k)).ToList();
        Assert.True(stray.Count == 0, "Not used by any guide: " + string.Join(", ", stray));
    }

    [Fact]
    public void Guides_with_thin_sources_say_so_under_the_first_paragraph()
    {
        Assert.NotNull(GuideBook.Find(GuideBook.SemiHydro)!.WhereKey);
        Assert.NotNull(GuideBook.Find(GuideBook.Wick)!.WhereKey);
    }

    [Fact]
    public void Links_between_guides_and_the_groups_on_the_list_exist()
    {
        foreach (var guide in GuideBook.All)
        {
            Assert.Contains(guide.Group, new[] { GuideBook.Propagating, GuideBook.GrowingMedia });
            Assert.True(Read("Guides.resx").ContainsKey("Group" + guide.Group));
        }

        Assert.NotNull(GuideBook.Find(GuideBook.Wick));
    }

    [Theory]
    [InlineData(GuideBook.Cutting, GrowingMedium.Water, "water")]
    [InlineData(GuideBook.Cutting, GrowingMedium.Soil, "mix")]
    [InlineData(GuideBook.Cutting, GrowingMedium.Perlite, "mix")]
    [InlineData(GuideBook.Cutting, GrowingMedium.Sphagnum, "sphagnum")]
    [InlineData(GuideBook.Wick, GrowingMedium.Leca, "semi-hydro")]
    [InlineData(GuideBook.Wick, GrowingMedium.Pon, "semi-hydro")]
    [InlineData(GuideBook.Wick, GrowingMedium.Soil, "soil")]
    public void A_guide_opens_on_the_method_that_fits_the_medium(string guide, GrowingMedium medium, string expected)
    {
        var method = GuideBook.StartMethod(guide, medium);
        Assert.Equal(expected, method);
        Assert.Contains(GuideBook.Find(guide)!.Blocks.SelectMany(Methods), m => m.Id == method);
    }

    [Fact]
    public void Where_nothing_fits_the_first_method_is_shown()
    {
        Assert.Null(GuideBook.StartMethod(GuideBook.Mixing, GrowingMedium.Soil));
        Assert.Null(GuideBook.StartMethod(GuideBook.SemiHydro, GrowingMedium.Leca));
    }

    private static IEnumerable<GuideMethod> Methods(GuideBlock block) => block switch
    {
        GuideMethods methods => methods.Methods,
        GuideSection section => section.Blocks.SelectMany(Methods),
        _ => []
    };
}
