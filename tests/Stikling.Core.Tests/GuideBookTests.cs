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
            "PageTitle", "Title", "Intro", "GroupPropagating", "GroupGrowingMedia", "GroupPests", "PhotoCredit", "PhotoCropped", "BackToGuides", "BackTo", "BackToHelp",
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
            Assert.Contains(guide.Group, new[] { GuideBook.Propagating, GuideBook.GrowingMedia, GuideBook.Pests });
            Assert.True(Read("Guides.resx").ContainsKey("Group" + guide.Group));
        }

        Assert.NotNull(GuideBook.Find(GuideBook.Wick));
        Assert.All(GuideBook.All.SelectMany(g => g.Blocks.SelectMany(Seen)).OfType<GuideSeeAlso>(), s => Assert.NotNull(GuideBook.Find(s.GuideId)));
    }

    [Fact]
    public void The_list_shows_the_guides_in_order_under_Propagating_Growing_media_and_Pests()
    {
        Assert.Equal(
            [GuideBook.Cutting, GuideBook.SemiHydro, GuideBook.Wick, GuideBook.Mixing, GuideBook.CheckingForPests, GuideBook.Thrips,
             GuideBook.SpiderMites, GuideBook.FungusGnats, GuideBook.Mealybugs, GuideBook.Scale, GuideBook.Aphids, GuideBook.Whitefly],
            GuideBook.All.Select(g => g.Id));
        Assert.Equal([GuideBook.Propagating, GuideBook.GrowingMedia, GuideBook.Pests], GuideBook.All.Select(g => g.Group).Distinct());
    }

    [Theory]
    [InlineData(Pest.Thrips, GuideBook.Thrips)]
    [InlineData(Pest.SpiderMites, GuideBook.SpiderMites)]
    [InlineData(Pest.FungusGnats, GuideBook.FungusGnats)]
    [InlineData(Pest.Mealybugs, GuideBook.Mealybugs)]
    [InlineData(Pest.Scale, GuideBook.Scale)]
    [InlineData(Pest.Aphids, GuideBook.Aphids)]
    [InlineData(Pest.Whitefly, GuideBook.Whitefly)]
    [InlineData(Pest.Other, GuideBook.CheckingForPests)]
    public void A_pest_opens_its_own_guide(Pest pest, string expected)
    {
        Assert.Equal(expected, GuideBook.For(pest));
    }

    [Fact]
    public void Every_pest_has_a_guide_that_exists()
    {
        foreach (var pest in Enum.GetValues<Pest>())
            Assert.NotNull(GuideBook.Find(GuideBook.For(pest)));
    }

    [Theory]
    [InlineData("CC0", "https://creativecommons.org/publicdomain/zero/1.0/")]
    [InlineData("CC BY 2.0", "https://creativecommons.org/licenses/by/2.0/")]
    [InlineData("CC BY-SA 4.0", "https://creativecommons.org/licenses/by-sa/4.0/")]
    [InlineData("CC BY 3.0 US", "https://creativecommons.org/licenses/by/3.0/us/")]
    public void A_photo_credit_links_to_its_licence(string licence, string url) =>
        Assert.Equal(url, GuidePhoto.LicenceUrlFor(licence));

    [Fact]
    public void Every_photo_has_a_licence_with_a_page()
    {
        var photos = GuideBook.All.SelectMany(g => g.Blocks.SelectMany(Seen)).OfType<GuidePhotos>().SelectMany(p => p.Photos);
        foreach (var photo in photos)
            Assert.StartsWith("https://creativecommons.org/", photo.LicenceUrl);
    }

    [Fact]
    public void Every_photo_is_a_file_of_the_size_it_says_with_a_credit_that_links_to_Wikimedia_Commons()
    {
        var folder = Path.Combine(ResourcesFolder(), "..", "wwwroot", "img", "guides");
        var photos = GuideBook.All.SelectMany(g => g.Blocks.SelectMany(Seen)).OfType<GuidePhotos>().SelectMany(p => p.Photos).ToList();
        Assert.Equal(12, photos.Count);
        Assert.Equal(photos.Count, photos.Select(p => p.File).Distinct().Count());
        foreach (var photo in photos)
        {
            var path = Path.Combine(folder, photo.File);
            Assert.True(File.Exists(path), $"{photo.File} is missing");
            Assert.Equal((photo.Width, photo.Height), WebpSize(File.ReadAllBytes(path)));
            Assert.False(string.IsNullOrWhiteSpace(photo.Author));
            Assert.False(string.IsNullOrWhiteSpace(photo.Licence));
            Assert.StartsWith("https://commons.wikimedia.org/", photo.SourceUrl);
            Assert.False(photo.Cropped);
        }
    }

    [Fact]
    public void Every_row_of_a_table_of_pests_points_at_a_guide_that_exists()
    {
        var tables = GuideBook.All.SelectMany(g => g.Blocks.SelectMany(Seen)).OfType<GuideLinkTable>().ToList();
        Assert.NotEmpty(tables);
        Assert.All(tables.SelectMany(t => t.Rows), row => Assert.NotNull(GuideBook.Find(row.GuideId)));
    }

    [Fact]
    public void Checking_for_pests_has_a_row_for_each_pest_guide()
    {
        var table = GuideBook.Find(GuideBook.CheckingForPests)!.Blocks.SelectMany(Seen).OfType<GuideLinkTable>().Single();
        Assert.Equal(
            GuideBook.All.Where(g => g.Group == GuideBook.Pests && g.Id != GuideBook.CheckingForPests).Select(g => g.Id),
            table.Rows.Select(r => r.GuideId));
    }

    /// <summary>Width and height from the header of a WebP file, whether it is lossy, lossless or extended.</summary>
    private static (int, int) WebpSize(byte[] b)
    {
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(b, 0, 4));
        Assert.Equal("WEBP", System.Text.Encoding.ASCII.GetString(b, 8, 4));
        return System.Text.Encoding.ASCII.GetString(b, 12, 4) switch
        {
            "VP8X" => (1 + (b[24] | b[25] << 8 | b[26] << 16), 1 + (b[27] | b[28] << 8 | b[29] << 16)),
            "VP8L" => (1 + ((b[21] | b[22] << 8) & 0x3FFF), 1 + (((b[22] | b[23] << 8 | b[24] << 16) >> 6) & 0x3FFF)),
            "VP8 " => ((b[26] | b[27] << 8) & 0x3FFF, (b[28] | b[29] << 8) & 0x3FFF),
            var other => throw new InvalidOperationException($"Unknown WebP chunk {other}")
        };
    }

    private static IEnumerable<GuideBlock> Seen(GuideBlock block) => block switch
    {
        GuideSection section => section.Blocks.SelectMany(Seen).Prepend(block),
        GuideMethods methods => methods.Methods.SelectMany(m => m.Blocks).SelectMany(Seen).Prepend(block),
        _ => [block]
    };

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
