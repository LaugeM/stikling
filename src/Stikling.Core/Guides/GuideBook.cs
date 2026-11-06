using Stikling.Core.Models;

namespace Stikling.Core.Guides;

/// <summary>
/// The guides in the app, and the pages they are written from. The text is in Stikling.Web's Resources/Guides.resx.
/// docs/dev/guides.md is the checked text of each guide and where every part of it comes from, so a change
/// to a guide is a change there too.
/// </summary>
public static class GuideBook
{
    public const string Cutting = "taking-a-cutting";
    public const string SemiHydro = "semi-hydro";
    public const string Wick = "wick-watering";
    public const string Mixing = "mixing-soil";

    public const string Propagating = "Propagating";
    public const string GrowingMedia = "GrowingMedia";

    private static readonly GuideSource RhsCuttings = new("rhs-cuttings", "Hunt, How to take cuttings from houseplants", "Royal Horticultural Society", "https://www.rhs.org.uk/plants/types/houseplants/how-to-take-cuttings-from-houseplants");
    private static readonly GuideSource RhsMedia = new("rhs-media", "A guide to compost mixes for houseplants", "Royal Horticultural Society", "https://www.rhs.org.uk/plants/types/houseplants/growing-media-houseplants");
    private static readonly GuideSource RhsRepot = new("rhs-repot", "How to repot a plant", "Royal Horticultural Society", "https://www.rhs.org.uk/container-gardening/how-to-repot-a-plant");
    private static readonly GuideSource RhsCacti = new("rhs-cacti", "How to grow cacti and succulents", "Royal Horticultural Society", "https://www.rhs.org.uk/plants/types/cacti-succulents/houseplants/growing-guide");
    private static readonly GuideSource Illinois = new("illinois", "Cuttings", "University of Illinois Extension", "https://extension.illinois.edu/houseplants/cuttings");
    private static readonly GuideSource Unr = new("unr", "Robinson and Roberts, Propagating Houseplants, FS 98-92, 1998", "University of Nevada, Reno Extension", "https://extension.unr.edu/small-business-education/pub.aspx?PubID=3384");
    private static readonly GuideSource UfMix = new("uf-mix", "Homemade Potting Mix", "UF/IFAS Extension", "https://sfyl.ifas.ufl.edu/lawn-and-garden/homemade-potting-mix/");
    private static readonly GuideSource OkState = new("okstate", "Soilless Growing Mediums, HLA-6728", "Oklahoma State University Extension", "https://extension.okstate.edu/fact-sheets/soilless-growing-mediums");
    private static readonly GuideSource Lechuza = new("lechuza", "Planting and watering instructions", "LECHUZA, the maker of PON", "https://getpotted.com/upload/Lechuza%20Instructions.pdf");
    private static readonly GuideSource Compo = new("compo", "Hydrokultur für Zimmerpflanzen?", "COMPO, a maker of hydroculture fertiliser", "https://www.compo.de/ratgeber/pflanzenpflege/grundlagen/erde-kompost/hydrokultur");
    private static readonly GuideSource Cornell = new("cornell", "Fischer and Fox, Propagation of House Plants, 1969, reprinted 2025", "Cornell Cooperative Extension Suffolk County", "https://s3.amazonaws.com/assets.cce.cornell.edu/attachments/75540/Propagation_Of_Houseplants.pdf");
    private static readonly GuideSource Pavlis = new("pavlis", "Pavlis, Wick Watering: How It Works & Common Myths", "Garden Myths", "https://www.gardenmyths.com/wick-watering-how-it-works-common-myths/");
    private static readonly GuideSource Costa = new("costa", "Wick & Grow", "Costa Farms, the maker of a wick pot", "https://help.costafarms.com/en/knowledge/wick-grow");
    private static readonly GuideSource UfSphagnum = new("uf-sphagnum", "Hildinger, Rooting Spider Plants in Sphagnum Moss, 2024", "UF/IFAS Therapeutic Horticulture Activity Database", "https://hos.ifas.ufl.edu/media/hosifasufledu/therapeutic-horticulture-activities-database/pdfs/Rooting-Spider-Plants-in-Sphagnum.pdf");

    private static readonly string[] Ingredients =
        ["Multipurpose", "Peat", "Coir", "Loam", "Bark", "Perlite", "Pumice", "Grit", "Vermiculite", "Castings", "Charcoal", "SphagnumMoss"];

    private static GuideSection Section(string title, params GuideBlock[] blocks) => new(title, blocks);

    private static GuideList Steps(string prefix, int count) =>
        new(Enumerable.Range(1, count).Select(i => $"{prefix}Step{i}").ToArray(), Numbered: true);

    private static GuideList Items(params string[] keys) => new(keys);

    private static GuideMethod Method(string id, string titleKey, params GuideBlock[] blocks) => new(id, titleKey, blocks);

    public static IReadOnlyList<Guide> All { get; } =
    [
        new(Cutting, Propagating, "TitleCutting", "RowCutting", "Cutting_Lead", null, "LinkCutting",
        [
            Section("Cutting_WhenTitle", new GuideText("Cutting_When")),
            Section("YoullNeed", new GuideText("Cutting_Need")),
            Section("Steps", Steps("Cutting_", 6)),
            new GuideMethods("Cutting_RootIn",
            [
                Method("water", "Cutting_Water",
                    new GuideField("FieldWhy", "Cutting_WaterWhy"),
                    new GuideField("FieldSteps", "Cutting_WaterSteps"),
                    new GuideField("FieldHowLong", "Cutting_WaterHowLong"),
                    new GuideText("Cutting_WaterWinter"),
                    new GuideField("FieldWatch", "Cutting_WaterWatch")),
                Method("mix", "Cutting_Mix",
                    new GuideField("FieldWhy", "Cutting_MixWhy"),
                    new GuideField("FieldSteps", "Cutting_MixSteps"),
                    new GuideText("Cutting_MixKeep"),
                    new GuideField("FieldHowLong", "Cutting_MixHowLong"),
                    new GuideField("FieldWatch", "Cutting_MixWatch")),
                Method("sphagnum", "Cutting_Sphagnum",
                    new GuideField("FieldWhy", "Cutting_SphagnumWhy"),
                    new GuideField("FieldSteps", "Cutting_SphagnumSteps"),
                    new GuideField("FieldHowLong", "Cutting_SphagnumHowLong"),
                    new GuideText("Cutting_SphagnumGloves"),
                    new GuideField("FieldWatch", "Cutting_SphagnumWatch")),
            ]),
            Section("Cutting_PotTitle",
                new GuideText("Cutting_PotLead"),
                Items("Cutting_PotEarly", "Cutting_PotLater"),
                new GuideText("Cutting_PotHow")),
            Section("InStikling", new GuideText("Cutting_InApp")),
        ],
        [RhsCuttings, Illinois, Unr, Cornell, UfSphagnum]),

        new(SemiHydro, GrowingMedia, "TitleSemiHydro", "RowSemiHydro", "SemiHydro_Lead", "SemiHydro_Where", "LinkSemiHydro",
        [
            Section("SemiHydro_WhichTitle", new GuideText("SemiHydro_Which")),
            Section("YoullNeed", Items("SemiHydro_Need1", "SemiHydro_Need2", "SemiHydro_Need3", "SemiHydro_Need4")),
            Section("SemiHydro_ReadyTitle", new GuideText("SemiHydro_Ready")),
            Section("Steps", Steps("SemiHydro_", 4)),
            Section("SemiHydro_SetupTitle", new GuideSeeAlso("SemiHydro_Setup", Wick)),
            new GuideMethods("SemiHydro_HowMove",
            [
                Method("straight", "SemiHydro_Straight",
                    new GuideField("FieldWhy", "SemiHydro_StraightWhy"),
                    new GuideField("FieldHow", "SemiHydro_StraightHow"),
                    new GuideField("FieldWatch", "SemiHydro_StraightWatch")),
                Method("water-first", "SemiHydro_WaterFirst",
                    new GuideField("FieldWhy", "SemiHydro_WaterFirstWhy"),
                    new GuideField("FieldHow", "SemiHydro_WaterFirstHow"),
                    new GuideField("FieldWatch", "SemiHydro_WaterFirstWatch")),
                Method("lechuza", "SemiHydro_Lechuza",
                    new GuideField("FieldWhy", "SemiHydro_LechuzaWhy"),
                    new GuideField("FieldHow", "SemiHydro_LechuzaHow"),
                    new GuideField("FieldWatch", "SemiHydro_LechuzaWatch")),
            ]),
            Section("SemiHydro_LevelTitle",
                new GuideText("SemiHydro_LevelLead"),
                Items("SemiHydro_Level1", "SemiHydro_Level2", "SemiHydro_Level3"),
                new GuideText("SemiHydro_LevelNever")),
            Section("SemiHydro_SettlesTitle", Items("SemiHydro_Settle1", "SemiHydro_Settle2", "SemiHydro_Settle3", "SemiHydro_Settle4")),
            Section("InStikling", new GuideText("SemiHydro_InApp")),
            new GuideTrack(),
        ],
        [RhsMedia, OkState, Lechuza, Compo, RhsRepot]),

        new(Wick, GrowingMedia, "TitleWick", "RowWick", "Wick_Lead", "Wick_Where", "LinkWick",
        [
            Section("Wick_WetTitle", new GuideText("Wick_Wet")),
            Section("YoullNeed", Items("Wick_Need1", "Wick_Need2")),
            Section("Steps", Steps("Wick_", 5)),
            new GuideMethods("Wick_GrowsIn",
            [
                Method("soil", "Wick_Soil",
                    new GuideField("FieldWhy", "Wick_SoilWhy"),
                    new GuideField("FieldMix", "Wick_SoilMix"),
                    new GuideField("FieldWatch", "Wick_SoilWatch")),
                Method("semi-hydro", "Wick_SemiHydro",
                    new GuideField("FieldWhy", "Wick_SemiWhy"),
                    new GuideField("FieldMix", "Wick_SemiMix"),
                    new GuideField("FieldWatch", "Wick_SemiWatch")),
            ]),
            Section("Wick_CareTitle", Items("Wick_Care1", "Wick_Care2", "Wick_Care3", "Wick_Care4", "Wick_Care5")),
            Section("Wick_DisagreeTitle", Items("Wick_Disagree1", "Wick_Disagree2")),
            Section("InStikling", new GuideText("Wick_InApp")),
        ],
        [Pavlis, Costa, Cornell]),

        new(Mixing, GrowingMedia, "TitleMix", "RowMix", "Mix_Lead", null, "LinkMix",
        [
            new GuideText("Mix_Water"),
            Section("Mix_InTitle",
                new GuideTable(Ingredients.Select(i => ($"Mix_In{i}", $"Mix_In{i}Does")).ToArray()),
                new GuideText("Mix_Garden")),
            new GuideMethods("Mix_Starts",
            [
                Method("foliage", "Mix_Foliage", Items("Mix_Foliage1", "Mix_Foliage2")),
                new GuideMethod("aroids", "Mix_Aroids", [Items("Mix_Aroids1", "Mix_Aroids2")], "Mix_AroidsNote"),
                Method("cacti", "Mix_Cacti", Items("Mix_Cacti1", "Mix_Cacti2")),
                Method("orchids", "Mix_Orchids", Items("Mix_Orchids1")),
            ], NoteKey: "Mix_StartsNote"),
            Section("InStikling", new GuideText("Mix_InApp")),
        ],
        [RhsMedia, UfMix, RhsRepot, RhsCuttings, RhsCacti]),
    ];

    public static Guide? Find(string? id) => All.FirstOrDefault(g => g.Id == id);

    /// <summary>
    /// The method a guide opens on when it is opened from a plant or propagation in this medium, or null for the first one.
    /// </summary>
    public static string? StartMethod(string guideId, GrowingMedium? medium) => (guideId, medium) switch
    {
        (Cutting, GrowingMedium.Water) => "water",
        (Cutting, GrowingMedium.Soil or GrowingMedium.Perlite) => "mix",
        (Cutting, GrowingMedium.Sphagnum) => "sphagnum",
        (Wick, GrowingMedium.Leca or GrowingMedium.Pon) => "semi-hydro",
        (Wick, GrowingMedium.Soil) => "soil",
        _ => null
    };
}
