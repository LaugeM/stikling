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
    public const string CheckingForPests = "checking-for-pests";
    public const string Thrips = "thrips";
    public const string SpiderMites = "spider-mites";
    public const string FungusGnats = "fungus-gnats";
    public const string Mealybugs = "mealybugs";
    public const string Scale = "scale";
    public const string Aphids = "aphids";
    public const string Whitefly = "whitefly";

    public const string Propagating = "Propagating";
    public const string GrowingMedia = "GrowingMedia";
    public const string Pests = "Pests";

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

    private static readonly GuideSource Uc = new("ucipm", "Pest Notes: Houseplant Problems, UC ANR Publication 74172, updated 2020", "University of California", "https://ipm.ucanr.edu/home-and-landscape/houseplant-problems/");
    private static readonly GuideSource UcThrips = new("ucipm-thrips", "Bethke, Dreistadt and Varela, Pest Notes: Thrips, UC ANR Publication 7429, 2014", "University of California", "https://ipm.ucanr.edu/PMG/PESTNOTES/pn7429.html");
    private static readonly GuideSource UcMites = new("ucipm-mites", "Godfrey, Pest Notes: Spider Mites, UC ANR Publication 7405, 2011", "University of California", "https://ipm.ucanr.edu/PMG/PESTNOTES/pn7405.html");
    private static readonly GuideSource UcGnats = new("ucipm-gnats", "Bethke and Dreistadt, Pest Notes: Fungus Gnats, UC ANR Publication 7448, 2013", "University of California", "https://ipm.ucanr.edu/PMG/PESTNOTES/pn7448.html");
    private static readonly GuideSource UcMealybugs = new("ucipm-mealybugs", "Flint, Pest Notes: Mealybugs, UC ANR Publication 74174, 2016", "University of California", "https://ipm.ucanr.edu/PMG/PESTNOTES/pn74174.html");
    private static readonly GuideSource UcScales = new("ucipm-scales", "Kabashima and Dreistadt, Pest Notes: Scales, UC ANR Publication 7408, 2014", "University of California", "https://ipm.ucanr.edu/PMG/PESTNOTES/pn7408.html");
    private static readonly GuideSource UcAphids = new("ucipm-aphids", "Flint, Pest Notes: Aphids, UC ANR Publication 7404, 2013", "University of California", "https://ipm.ucanr.edu/PMG/PESTNOTES/pn7404.html");
    private static readonly GuideSource UcWhiteflies = new("ucipm-whiteflies", "Flint, Pest Notes: Whiteflies, UC ANR Publication 7401, 2015", "University of California", "https://ipm.ucanr.edu/PMG/PESTNOTES/pn7401.html");
    private static readonly GuideSource RhsSap = new("rhs-sap", "Indoor plants: sap feeders", "Royal Horticultural Society", "https://www.rhs.org.uk/biodiversity/indoor-plants-sap-feeding-insects");
    private static readonly GuideSource RhsHouseplants = new("rhs-houseplants", "How to grow houseplants", "Royal Horticultural Society", "https://www.rhs.org.uk/plants/types/houseplants/growing-guide");
    private static readonly GuideSource RhsBio = new("rhs-bio", "Biological control in the home garden", "Royal Horticultural Society", "https://www.rhs.org.uk/prevention-protection/biological-control-garden");
    private static readonly GuideSource RhsThrips = new("rhs-thrips", "Thrips", "Royal Horticultural Society", "https://www.rhs.org.uk/biodiversity/thrips");
    private static readonly GuideSource RhsMite = new("rhs-mite", "Glasshouse red spider mite", "Royal Horticultural Society", "https://www.rhs.org.uk/biodiversity/glasshouse-red-spider-mite");
    private static readonly GuideSource RhsGnats = new("rhs-gnats", "Fungus gnats (sciarid flies)", "Royal Horticultural Society", "https://www.rhs.org.uk/biodiversity/fungus-gnats");
    private static readonly GuideSource RhsMealybug = new("rhs-mealybug", "Mealybug (glasshouse)", "Royal Horticultural Society", "https://www.rhs.org.uk/biodiversity/mealybug");
    private static readonly GuideSource RhsScale = new("rhs-scale", "Scale insects", "Royal Horticultural Society", "https://www.rhs.org.uk/biodiversity/scale-insects");
    private static readonly GuideSource RhsAphids = new("rhs-aphids", "Aphids", "Royal Horticultural Society", "https://www.rhs.org.uk/biodiversity/aphids");
    private static readonly GuideSource RhsWhitefly = new("rhs-whitefly", "Glasshouse whitefly", "Royal Horticultural Society", "https://www.rhs.org.uk/biodiversity/glasshouse-whitefly");

    private static readonly string[] Ingredients =
        ["Multipurpose", "Peat", "Coir", "Loam", "Bark", "Perlite", "Pumice", "Grit", "Vermiculite", "Castings", "Charcoal", "SphagnumMoss"];

    private static GuidePhoto Photo(string file, string captionKey, int width, int height, string author, string licence, string source) =>
        new(file, captionKey, width, height, author, licence, "https://commons.wikimedia.org/wiki/File:" + source);

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

        new(CheckingForPests, Pests, "TitleCheck", "RowCheck", "Check_Lead", null, "LinkCheck",
        [
            Section("Check_NewTitle", Steps("Check_New", 4)),
            Section("Check_HowTitle", Items("Check_How1", "Check_How2", "Check_How3", "Check_How4")),
            Section("Check_TrapsTitle", new GuideText("Check_Traps")),
            Section("Check_WhichTitle", new GuideLinkTable(
            [
                (Thrips, "Check_WhichThrips"),
                (SpiderMites, "Check_WhichMites"),
                (FungusGnats, "Check_WhichGnats"),
                (Mealybugs, "Check_WhichMealy"),
                (Scale, "Check_WhichScale"),
                (Aphids, "Check_WhichAphids"),
                (Whitefly, "Check_WhichWhitefly"),
            ])),
            Section("Check_FoundTitle", Steps("Check_Found", 4)),
            Section("Check_SprayTitle", Items("Check_Spray1", "Check_Spray2", "Check_Spray3", "Check_Spray4", "Check_Spray5")),
            Section("Check_EnemiesTitle", new GuideText("Check_Enemies")),
            Section("InStikling", new GuideText("Check_InApp")),
        ],
        [RhsMealybug, UcMealybugs, Uc, RhsThrips, RhsWhitefly, RhsSap, RhsMite, UcMites, RhsAphids, RhsGnats, UcWhiteflies, RhsHouseplants, UcAphids, RhsBio]),

        new(Thrips, Pests, "TitleThrips", "RowThrips", "Thrips_Lead", null, "LinkThrips",
        [
            Section("PestSee",
                Items("Thrips_See1", "Thrips_See2", "Thrips_See3", "Thrips_See4"),
                new GuidePhotos([Photo("thrips-adult.webp", "Thrips_PhotoAdult", 800, 703, "Jesse Rorabaugh", "CC0", "Frankliniella_occidentalis_14827630.jpg")])),
            Section("MistakenFor", new GuideText("Thrips_Mistaken")),
            Section("WhyBack", new GuideText("Thrips_Back")),
            Section("First", Steps("Thrips_First", 3)),
            new GuideMethods("TreatItWith",
            [
                Method("rinse", "Thrips_Rinse",
                    new GuideField("FieldHow", "Thrips_RinseHow"),
                    new GuideField("FieldWatch", "Thrips_RinseWatch")),
                Method("soap", "MethodSoapOil",
                    new GuideField("FieldHow", "Thrips_SoapHow"),
                    new GuideText("Thrips_SoapRound"),
                    new GuideField("FieldWatch", "Thrips_SoapWatch")),
                Method("enemies", "MethodEnemies",
                    new GuideField("FieldWhat", "Thrips_EnemiesWhat"),
                    new GuideText("Thrips_EnemiesUc"),
                    new GuideField("FieldWatch", "Thrips_EnemiesWatch"),
                    new GuideSeeAlso("SeeAlso", CheckingForPests)),
                Method("product", "MethodProduct",
                    new GuideField("FieldHow", "Thrips_ProductHow")),
            ]),
            Section("WhenOver", new GuideText("Thrips_Over")),
            Section("InStikling", new GuideText("Thrips_InApp")),
        ],
        [RhsThrips, Uc, RhsSap, UcThrips, UcMites, RhsBio]),

        new(SpiderMites, Pests, "TitleMites", "RowMites", "Mites_Lead", null, "LinkMites",
        [
            Section("PestSee",
                Items("Mites_See1", "Mites_See2", "Mites_See3", "Mites_See4"),
                new GuidePhotos(
                [
                    Photo("spider-mite.webp", "Mites_PhotoMite", 800, 536, "Gilles San Martin", "CC BY-SA 2.0", "Tetranychus_urticae_with_silk_threads.jpg"),
                    Photo("spider-mite-webbing.webp", "Mites_PhotoWebbing", 800, 800, "Clemson University, USDA Cooperative Extension Slide Series, Bugwood.org", "CC BY 3.0 US", "Solanum_lycopersicum_-_spider_mite_-Clemson_University_-_USDA.jpg"),
                ])),
            Section("MistakenFor", new GuideText("Mites_Mistaken")),
            Section("WhyBack", new GuideText("Mites_Back")),
            Section("First", Steps("Mites_First", 2)),
            new GuideMethods("TreatItWith",
            [
                Method("rinse", "Mites_Rinse",
                    new GuideField("FieldHow", "Mites_RinseHow"),
                    new GuideText("Mites_RinseMist")),
                Method("soap", "MethodSoapOil",
                    new GuideField("FieldHow", "Mites_SoapHow"),
                    new GuideText("Mites_SoapRound"),
                    new GuideField("FieldWatch", "Mites_SoapWatch")),
                Method("enemies", "MethodEnemies",
                    new GuideField("FieldWhat", "Mites_EnemiesWhat"),
                    new GuideField("FieldWatch", "Mites_EnemiesWatch")),
                Method("product", "MethodProduct",
                    new GuideField("FieldHow", "Mites_ProductHow")),
            ]),
            Section("IfNotStop", new GuideText("Mites_Stop")),
            Section("WhenOver", new GuideText("Mites_Over")),
            Section("InStikling", new GuideText("Mites_InApp")),
        ],
        [Uc, RhsMite, UcMites, RhsThrips, RhsSap, RhsBio]),

        new(FungusGnats, Pests, "TitleGnats", "RowGnats", "Gnats_Lead", null, "LinkGnats",
        [
            Section("PestSee",
                Items("Gnats_See1", "Gnats_See2", "Gnats_See3"),
                new GuidePhotos(
                [
                    Photo("fungus-gnat-trap.webp", "Gnats_PhotoTrap", 662, 800, "Ich (Wikimedia Commons user)", "CC BY-SA 4.0", "Gelbsticker_mit_Trauerm%C3%BCcken.jpg"),
                    Photo("fungus-gnat.webp", "Gnats_PhotoGnat", 800, 600, "AfroBrazilian", "CC BY-SA 3.0", "Bradysia_sp._01.JPG"),
                ])),
            Section("Gnats_HarmTitle", new GuideText("Gnats_Harm")),
            Section("MistakenFor", new GuideText("Gnats_Mistaken")),
            Section("WhyBack", new GuideText("Gnats_Back")),
            Section("First",
                Steps("Gnats_First", 3),
                new GuideSeeAlso("Gnats_FirstMix", Mixing)),
            new GuideMethods("TreatItWith",
            [
                Method("traps", "Gnats_Traps",
                    new GuideField("FieldHow", "Gnats_TrapsHow"),
                    new GuideField("FieldWatch", "Gnats_TrapsWatch")),
                Method("bti", "Gnats_Bti",
                    new GuideField("FieldHow", "Gnats_BtiHow"),
                    new GuideText("Gnats_BtiRepeat")),
                Method("nematodes", "Gnats_Nematodes",
                    new GuideField("FieldHow", "Gnats_NemaHow"),
                    new GuideText("Gnats_NemaNeed")),
                Method("mites", "Gnats_Mites",
                    new GuideField("FieldHow", "Gnats_MitesHow"),
                    new GuideText("Gnats_MitesUc")),
            ]),
            new GuideText("Gnats_NoSpray"),
            Section("WhenOver", new GuideText("Gnats_Over")),
            Section("InStikling", new GuideText("Gnats_InApp")),
        ],
        [RhsGnats, UcGnats, Uc, RhsBio]),

        new(Mealybugs, Pests, "TitleMealy", "RowMealy", "Mealy_Lead", null, "LinkMealy",
        [
            Section("PestSee",
                Items("Mealy_See1", "Mealy_See2", "Mealy_See3", "Mealy_See4", "Mealy_See5"),
                new GuidePhotos(
                [
                    Photo("mealybug-colony.webp", "Mealy_PhotoColony", 800, 552, "Sinikka Halme", "CC BY-SA 4.0", "Citrus_mealybug_Planococcus_citrus_on_Clivia_miniata_02.jpg"),
                    Photo("mealybug-long-tailed.webp", "Mealy_PhotoLong", 800, 533, "Gianni Del Bufalo", "CC BY 2.0", "Cocciniglia_-_Pseudococcus_longispinus_-_Mealybug_-Gianni_Del_Bufalo_bygdb.jpg"),
                ])),
            Section("MistakenFor", new GuideText("Mealy_Mistaken")),
            Section("WhyBack", new GuideText("Mealy_Back")),
            Section("First", Steps("Mealy_First", 2)),
            new GuideMethods("TreatItWith",
            [
                Method("wipe", "Mealy_Wipe",
                    new GuideField("FieldHow", "Mealy_WipeHow"),
                    new GuideField("FieldWatch", "Mealy_WipeWatch")),
                Method("spray", "Mealy_Spray",
                    new GuideField("FieldHow", "Mealy_SprayHow"),
                    new GuideText("Mealy_SprayAlcohol"),
                    new GuideField("FieldWatch", "Mealy_SprayWatch")),
                Method("enemies", "MethodEnemies",
                    new GuideField("FieldWhat", "Mealy_EnemiesWhat"),
                    new GuideField("FieldWatch", "Mealy_EnemiesWatch")),
                Method("product", "MethodProduct",
                    new GuideField("FieldHow", "Mealy_ProductHow")),
            ]),
            Section("IfNotStop", new GuideText("Mealy_Stop")),
            Section("WhenOver", new GuideText("Mealy_Over")),
            Section("InStikling", new GuideText("Mealy_InApp")),
        ],
        [RhsMealybug, UcMealybugs, RhsSap, Uc, RhsScale, RhsAphids, RhsBio]),

        new(Scale, Pests, "TitleScale", "RowScale", "Scale_Lead", null, "LinkScale",
        [
            Section("PestSee",
                Items("Scale_See1", "Scale_See2", "Scale_See3", "Scale_See4", "Scale_See5"),
                new GuidePhotos(
                [
                    Photo("scale-ficus.webp", "Scale_PhotoFicus", 800, 533, "Whitney Cranshaw, Colorado State University, Bugwood.org", "CC BY 3.0 US", "Coccus_hesperidum01.jpg"),
                    Photo("scale-single.webp", "Scale_PhotoSingle", 800, 600, "AfroBrazilian", "CC BY-SA 3.0", "Coccus_hesperidum_01.JPG"),
                ])),
            Section("Scale_SoftTitle", new GuideText("Scale_Soft")),
            Section("Scale_AliveTitle", new GuideText("Scale_Alive")),
            Section("WhyBack", new GuideText("Scale_Back")),
            Section("First", Steps("Scale_First", 2)),
            new GuideMethods("TreatItWith",
            [
                Method("scrape", "Scale_Scrape",
                    new GuideField("FieldHow", "Scale_ScrapeHow"),
                    new GuideField("FieldWatch", "Scale_ScrapeWatch")),
                Method("soap", "MethodSoapOil",
                    new GuideField("FieldHow", "Scale_SoapHow"),
                    new GuideText("Scale_SoapRound")),
                Method("enemies", "MethodEnemies",
                    new GuideField("FieldWhat", "Scale_EnemiesWhat")),
                Method("product", "MethodProduct",
                    new GuideField("FieldHow", "Scale_ProductHow")),
            ]),
            Section("Scale_FewTitle", new GuideText("Scale_Few")),
            Section("WhenOver", new GuideText("Scale_Over")),
            Section("InStikling", new GuideText("Scale_InApp")),
        ],
        [UcScales, RhsScale, RhsHouseplants, RhsSap, Uc, RhsBio]),

        new(Aphids, Pests, "TitleAphids", "RowAphids", "Aphids_Lead", null, "LinkAphids",
        [
            Section("PestSee",
                Items("Aphids_See1", "Aphids_See2", "Aphids_See3", "Aphids_See4", "Aphids_See5"),
                new GuidePhotos([Photo("aphids.webp", "Aphids_Photo", 646, 800, "Jules Verne Times Two", "CC BY-SA 4.0", "Green_peach_aphid_(Myzus_persicae)_on_a_stem,_Lisbon,_Portugal_(approx._GPS_location)_julesvernex2.jpg")])),
            Section("MistakenFor", new GuideText("Aphids_Mistaken")),
            Section("WhyBack", new GuideText("Aphids_Back")),
            Section("First", Steps("Aphids_First", 2)),
            new GuideMethods("TreatItWith",
            [
                Method("squash", "Aphids_Squash",
                    new GuideField("FieldHow", "Aphids_SquashHow"),
                    new GuideText("Aphids_SquashMould")),
                Method("soap", "MethodSoapOil",
                    new GuideField("FieldHow", "Aphids_SoapHow"),
                    new GuideText("Aphids_SoapRound"),
                    new GuideField("FieldWatch", "Aphids_SoapWatch")),
                Method("enemies", "MethodEnemies",
                    new GuideField("FieldWhat", "Aphids_EnemiesWhat")),
                Method("product", "MethodProduct",
                    new GuideField("FieldHow", "Aphids_ProductHow")),
            ]),
            Section("WhenOver", new GuideText("Aphids_Over")),
            Section("InStikling", new GuideText("Aphids_InApp")),
        ],
        [RhsAphids, UcAphids, RhsSap, Uc, RhsWhitefly, RhsBio]),

        new(Whitefly, Pests, "TitleWhitefly", "RowWhitefly", "Whitefly_Lead", null, "LinkWhitefly",
        [
            Section("PestSee",
                Items("Whitefly_See1", "Whitefly_See2", "Whitefly_See3", "Whitefly_See4", "Whitefly_See5"),
                new GuidePhotos(
                [
                    Photo("whitefly-adults.webp", "Whitefly_PhotoAdults", 800, 531, "Pablo Oliveri (Pro Huerta)", "CC BY-SA 2.5", "Pro_Huerta_-_Cucurbita_maxima_var._zapallito_-_Trialeurodes_vaporariorum_01_adults.jpg"),
                    Photo("whitefly-young.webp", "Whitefly_PhotoYoung", 800, 533, "Tashkoskim", "CC BY-SA 4.0", "%D0%9B%D0%B0%D1%80%D0%B2%D0%B8_%D0%B8_%D1%98%D0%B0%D1%98%D1%86%D0%B0_%D0%BE%D0%B4_%D0%B1%D0%B5%D0%BB%D0%BE%D0%BA%D1%80%D0%B8%D0%BB%D0%BA%D0%B8.jpg"),
                ])),
            Section("MistakenFor", new GuideText("Whitefly_Mistaken")),
            Section("WhyBack", new GuideText("Whitefly_Back")),
            Section("First", Steps("Whitefly_First", 2)),
            new GuideMethods("TreatItWith",
            [
                Method("traps", "Whitefly_Traps",
                    new GuideField("FieldHow", "Whitefly_TrapsHow"),
                    new GuideField("FieldWatch", "Whitefly_TrapsWatch")),
                Method("rinse", "Whitefly_Rinse",
                    new GuideField("FieldHow", "Whitefly_RinseHow")),
                Method("soap", "MethodSoapOil",
                    new GuideField("FieldHow", "Whitefly_SoapHow"),
                    new GuideField("FieldWatch", "Whitefly_SoapWatch")),
                Method("enemies", "MethodEnemies",
                    new GuideField("FieldWhat", "Whitefly_EnemiesWhat")),
            ]),
            Section("IfNotStop", new GuideText("Whitefly_Stop")),
            Section("WhenOver", new GuideText("Whitefly_Over")),
            Section("InStikling", new GuideText("Whitefly_InApp")),
        ],
        [RhsWhitefly, UcWhiteflies, Uc, RhsBio]),
    ];

    /// <summary>The guide for a pest a case can be for. A case for Other opens Checking for pests.</summary>
    public static string For(Pest pest) => pest switch
    {
        Pest.Thrips => Thrips,
        Pest.SpiderMites => SpiderMites,
        Pest.FungusGnats => FungusGnats,
        Pest.Mealybugs => Mealybugs,
        Pest.Scale => Scale,
        Pest.Aphids => Aphids,
        Pest.Whitefly => Whitefly,
        _ => CheckingForPests
    };

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
