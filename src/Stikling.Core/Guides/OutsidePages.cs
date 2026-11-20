using Stikling.Core.Models;

namespace Stikling.Core.Guides;

/// <summary>What an outside page is about, which decides where it is offered and which heading it is listed under.</summary>
public enum OutsideKind
{
    /// <summary>A pest. <see cref="OutsidePage.Topic"/> is the name of a <see cref="Pest"/>; Other is the general page.</summary>
    Pest,

    /// <summary>A plant group. <see cref="OutsidePage.Topic"/> is the id of the plant group guide.</summary>
    PlantGroup,

    /// <summary>Taking cuttings.</summary>
    Cuttings,

    /// <summary>Soil and potting mixes.</summary>
    Soil
}

/// <summary>
/// A page on another site that the app links to, see docs/dev/outside-links.md. The title and the line about it
/// are in Stikling.Web's Resources/OutsidePages.resx as {Id}_Title and {Id}_Line.
/// </summary>
/// <param name="Id">Fixed, never reused for another page.</param>
/// <param name="Topic">What the kind needs: a pest's name or a plant group guide's id, or null.</param>
/// <param name="Genera">For a plant group page, the genera it is for when it is about those and not the whole group. Empty for the whole group.</param>
/// <param name="Site">The name of the site, shown next to the link.</param>
/// <param name="Url">The English page, always https.</param>
/// <param name="DanishSite">The name of the site of the Danish page, or null when it is the same site.</param>
/// <param name="DanishUrl">A Danish page for the same thing, shown instead of the English one in Danish, or null.</param>
public sealed record OutsidePage(
    string Id,
    OutsideKind Kind,
    string? Topic,
    IReadOnlyList<string> Genera,
    string Site,
    string Url,
    string? DanishSite = null,
    string? DanishUrl = null)
{
    public string TitleKey => Id + "_Title";

    public string LineKey => Id + "_Line";

    /// <summary>The address to open: the Danish page when the language is Danish and there is one.</summary>
    public string UrlFor(string language) => IsDanishPage(language) ? DanishUrl! : Url;

    /// <summary>The name of the site that <see cref="UrlFor"/> opens.</summary>
    public string SiteFor(string language) => IsDanishPage(language) ? DanishSite ?? Site : Site;

    /// <summary>True when <see cref="UrlFor"/> opens a Danish page.</summary>
    public bool IsDanishPage(string language) =>
        DanishUrl is not null && string.Equals(language, "da", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The pages on other sites that the app links to, each one opened and read before it went on the list.
/// How they are picked and checked again is in docs/dev/outside-links.md.
/// </summary>
public static class OutsidePages
{
    private const string Rhs = "RHS";
    private const string Bolius = "Bolius";

    private static OutsidePage ForAPest(string id, Pest pest, string site, string url, string? daSite = null, string? daUrl = null) =>
        new(id, OutsideKind.Pest, pest.ToString(), [], site, url, daSite, daUrl);

    private static OutsidePage ForAGroup(string id, string guide, string site, string url, params string[] genera) =>
        new(id, OutsideKind.PlantGroup, guide, genera, site, url);

    public static IReadOnlyList<OutsidePage> All { get; } =
    [
        ForAPest("thrips", Pest.Thrips, Rhs, "https://www.rhs.org.uk/biodiversity/thrips"),
        ForAPest("spider-mites", Pest.SpiderMites, Rhs, "https://www.rhs.org.uk/biodiversity/glasshouse-red-spider-mite"),
        ForAPest("fungus-gnats", Pest.FungusGnats, Rhs, "https://www.rhs.org.uk/biodiversity/fungus-gnats"),
        ForAPest("mealybugs", Pest.Mealybugs, Rhs, "https://www.rhs.org.uk/biodiversity/mealybug"),
        ForAPest("scale", Pest.Scale, Rhs, "https://www.rhs.org.uk/biodiversity/scale-insects",
            Bolius, "https://www.bolius.dk/slip-af-med-skjoldlus-41666"),
        ForAPest("aphids", Pest.Aphids, Rhs, "https://www.rhs.org.uk/biodiversity/aphids"),
        ForAPest("whitefly", Pest.Whitefly, Rhs, "https://www.rhs.org.uk/biodiversity/glasshouse-whitefly"),
        ForAPest("houseplant-pests", Pest.Other, "UC IPM", "https://ipm.ucanr.edu/home-and-landscape/houseplant-problems/",
            Bolius, "https://www.bolius.dk/saadan-slipper-du-af-med-insekter-og-skadedyr-i-stueplanter-97772"),

        ForAGroup("aroids", GuideBook.Aroids, "Iowa State University Extension", "https://yardandgarden.extension.iastate.edu/article/2021/01/aroids"),
        ForAGroup("begonias", GuideBook.Begonias, Rhs, "https://www.rhs.org.uk/plants/begonias/houseplants"),
        ForAGroup("bromeliads", GuideBook.Bromeliads, Rhs, "https://www.rhs.org.uk/plants/bromeliads/growing-guide"),
        ForAGroup("air-plants", GuideBook.Bromeliads, Rhs, "https://www.rhs.org.uk/plants/types/houseplants/air-plants", "Tillandsia"),
        ForAGroup("cacti-succulents", GuideBook.Succulents, Rhs, "https://www.rhs.org.uk/plants/types/cacti-succulents/houseplants/growing-guide"),
        ForAGroup("carnivorous", GuideBook.Carnivorous, Rhs, "https://www.rhs.org.uk/plants/types/carnivorous/growing-guide"),
        ForAGroup("venus-flytrap", GuideBook.Carnivorous, "ICPS", "https://www.carnivorousplants.org/grow/guides/Dionaea", "Dionaea"),
        ForAGroup("ferns", GuideBook.Ferns, Rhs, "https://www.rhs.org.uk/plants/types/ferns/tender-ferns-houseplants"),
        ForAGroup("hoyas", GuideBook.Hoyas, Rhs, "https://www.rhs.org.uk/plants/hoya/how-to-grow"),
        ForAGroup("orchids", GuideBook.Orchids, Rhs, "https://www.rhs.org.uk/plants/types/houseplants/top-tips-for-growing-orchids-at-home"),
        ForAGroup("moth-orchids", GuideBook.Orchids, Rhs, "https://www.rhs.org.uk/plants/phalaenopsis/growing-guide", "Phalaenopsis"),
        ForAGroup("prayer-plants", GuideBook.PrayerPlants, Rhs, "https://www.rhs.org.uk/plants/calathea/growing-guide"),

        new("cuttings", OutsideKind.Cuttings, null, [], Rhs, "https://www.rhs.org.uk/plants/types/houseplants/how-to-take-cuttings-from-houseplants",
            Bolius, "https://www.bolius.dk/saadan-laver-du-stiklinger-88709"),
        new("compost-mixes", OutsideKind.Soil, null, [], Rhs, "https://www.rhs.org.uk/plants/types/houseplants/growing-media-houseplants"),
    ];

    public static OutsidePage? Find(string? id) => All.FirstOrDefault(p => p.Id == id);

    /// <summary>The page for a pest a case can be for, or none. A case for Other gets the general page.</summary>
    public static IReadOnlyList<OutsidePage> ForPest(Pest pest) =>
        All.Where(p => p.Kind == OutsideKind.Pest && p.Topic == pest.ToString()).ToList();

    /// <summary>
    /// The pages for a plant or a genus: the ones for its genus first, then the ones for its group as a whole.
    /// Decided the same way as <see cref="GuideBook.ForPlant"/>, and none when the plant is in no group.
    /// </summary>
    public static IReadOnlyList<OutsidePage> ForPlant(string? genus, string? family)
    {
        if (GuideBook.ForPlant(genus, family) is not { } group)
            return [];

        var name = genus?.Trim() ?? "";
        var inGroup = All.Where(p => p.Kind == OutsideKind.PlantGroup && p.Topic == group).ToList();
        return inGroup.Where(p => p.Genera.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Concat(inGroup.Where(p => p.Genera.Count == 0))
            .ToList();
    }

    public static IReadOnlyList<OutsidePage> ForKind(OutsideKind kind) => All.Where(p => p.Kind == kind).ToList();
}
