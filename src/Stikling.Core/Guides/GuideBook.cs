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
    public const string Aroids = "aroids";
    public const string Begonias = "begonias";
    public const string Bromeliads = "bromeliads";
    public const string Succulents = "cacti-and-succulents";
    public const string Carnivorous = "carnivorous-plants";
    public const string Ferns = "ferns";
    public const string Hoyas = "hoyas";
    public const string Orchids = "orchids";
    public const string PrayerPlants = "prayer-plants";

    public const string Propagating = "Propagating";
    public const string GrowingMedia = "GrowingMedia";
    public const string Pests = "Pests";
    public const string PlantGroups = "PlantGroups";

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

    private static readonly GuideSource RhsMonstera = new("rhs-monstera", "How to grow Swiss cheese plants", "Royal Horticultural Society", "https://www.rhs.org.uk/plants/swiss-cheese-plants/how-to-grow-swiss-cheese-plants");
    private static readonly GuideSource RhsPhilodendron = new("rhs-philodendron", "How to grow philodendrons", "Royal Horticultural Society", "https://www.rhs.org.uk/plants/philodendron/growing-guide");
    private static readonly GuideSource RhsAlocasia = new("rhs-alocasia", "How to grow alocasias", "Royal Horticultural Society", "https://www.rhs.org.uk/plants/alocasia/growing-guide");
    private static readonly GuideSource IastateAroids = new("iastate-aroids", "Haynes, A is for Aroids!, 2021", "Iowa State University Extension and Outreach", "https://yardandgarden.extension.iastate.edu/article/2021/01/aroids");
    private static readonly GuideSource IastatePhilodendron = new("iastate-philodendron", "Steil, Growing Philodendrons at Home, reviewed 2025", "Iowa State University Extension and Outreach", "https://yardandgarden.extension.iastate.edu/how-to/growing-philodendrons-home");
    private static readonly GuideSource RhsHoya = new("rhs-hoya", "How to grow hoya", "Royal Horticultural Society", "https://www.rhs.org.uk/plants/hoya/how-to-grow");
    private static readonly GuideSource IastateHoya = new("iastate-hoya", "Steil, All About Hoyas, reviewed 2026", "Iowa State University Extension and Outreach", "https://yardandgarden.extension.iastate.edu/how-to/all-about-hoyas");
    private static readonly GuideSource PsuHoya = new("psu-hoya", "Smith, Hoyas as Houseplants, 2026", "Penn State Extension", "https://extension.psu.edu/hoyas-as-houseplants");
    private static readonly GuideSource RhsPhal = new("rhs-phal", "How to grow Phalaenopsis (moth orchids)", "Royal Horticultural Society", "https://www.rhs.org.uk/plants/phalaenopsis/growing-guide");
    private static readonly GuideSource RhsOrchids = new("rhs-orchids", "Richards, Top tips for growing orchids at home", "Royal Horticultural Society", "https://www.rhs.org.uk/plants/types/houseplants/top-tips-for-growing-orchids-at-home");
    private static readonly GuideSource AosPhal = new("aos-phal", "Sullivan, Novice Culture Sheet: Phalaenopsis, 2012", "American Orchid Society", "https://secure.aos.org/media/Content-Images/PDFs/Phalaenopsis_novice.pdf");
    private static readonly GuideSource IastateSucculents = new("iastate-succulents", "Steil, Growing Succulents Indoors, reviewed 2024", "Iowa State University Extension and Outreach", "https://yardandgarden.extension.iastate.edu/how-to/growing-succulents-indoors");
    private static readonly GuideSource IllinoisSucculents = new("illinois-succulents", "Anderson, Propagating Succulents and Cacti, 2015", "University of Illinois Extension", "https://extension.illinois.edu/blogs/hort-home-landscape/2015-06-04-propagating-succulents-and-cacti");
    private static readonly GuideSource RhsCarnivorous = new("rhs-carnivorous", "How to grow carnivorous plants", "Royal Horticultural Society", "https://www.rhs.org.uk/plants/types/carnivorous/growing-guide");
    private static readonly GuideSource RhsFlytrap = new("rhs-flytrap", "Venus fly trap", "Royal Horticultural Society", "https://www.rhs.org.uk/plants/venus-fly-trap");
    private static readonly GuideSource IcpsFlytrap = new("icps-flytrap", "Brittnacher, Growing Dionaea muscipula, updated 2019", "International Carnivorous Plant Society", "https://www.carnivorousplants.org/grow/guides/Dionaea");
    private static readonly GuideSource IcpsNepenthes = new("icps-nepenthes", "Brittnacher, Growing Nepenthes", "International Carnivorous Plant Society", "https://www.carnivorousplants.org/grow/guides/Nepenthes");
    private static readonly GuideSource RhsBromeliads = new("rhs-bromeliads", "How to grow bromeliads", "Royal Horticultural Society", "https://www.rhs.org.uk/plants/bromeliads/growing-guide");
    private static readonly GuideSource RhsAirplants = new("rhs-airplants", "Air plants", "Royal Horticultural Society", "https://www.rhs.org.uk/plants/types/houseplants/air-plants");
    private static readonly GuideSource UfBromeliads = new("uf-bromeliads", "Bromeliads", "UF/IFAS Gardening Solutions", "https://gardeningsolutions.ifas.ufl.edu/plants/ornamentals/bromeliads/");
    private static readonly GuideSource RhsCalathea = new("rhs-calathea", "How to grow calatheas", "Royal Horticultural Society", "https://www.rhs.org.uk/plants/calathea/growing-guide");
    private static readonly GuideSource IllinoisPrayer = new("illinois-prayer", "Prayer Plant", "University of Illinois Extension", "https://extension.illinois.edu/houseplants/prayer-plant");
    private static readonly GuideSource UmdPrayer = new("umd-prayer", "Prayer plant leaves turning yellow/brown, 2023", "Home and Garden Information Center, University of Maryland Extension, on Ask Extension", "https://ask.extension.org/kb/faq.php?id=852330");
    private static readonly GuideSource RhsFerns = new("rhs-ferns", "How to grow epiphytic ferns", "Royal Horticultural Society", "https://www.rhs.org.uk/plants/epiphytic-ferns/how-to-grow-epiphytic-ferns");
    private static readonly GuideSource RhsTenderFerns = new("rhs-tender-ferns", "Tender ferns", "Royal Horticultural Society", "https://www.rhs.org.uk/plants/types/ferns/tender-ferns-houseplants");
    private static readonly GuideSource ClemsonFerns = new("clemson-ferns", "Russ, Pertuit and Smith, Indoor Ferns, HGIC 1505, revised 2015", "Clemson Cooperative Extension", "https://hgic.clemson.edu/factsheet/indoor-ferns/");
    private static readonly GuideSource RhsBegonias = new("rhs-begonias", "Begonias: houseplants", "Royal Horticultural Society", "https://www.rhs.org.uk/plants/begonias/houseplants");
    private static readonly GuideSource IastateBegonias = new("iastate-begonias", "Haynes, Growing Begonias in Iowa, reviewed 2024", "Iowa State University Extension and Outreach", "https://yardandgarden.extension.iastate.edu/how-to/growing-begonias-iowa");
    private static readonly GuideSource IastateLeafvein = new("iastate-leafvein", "Steil, How to Propagate Houseplants by Leaf Vein Cuttings, reviewed 2025", "Iowa State University Extension and Outreach", "https://yardandgarden.extension.iastate.edu/how-to/how-propagate-houseplants-leaf-vein-cuttings");

    private static readonly string[] Ingredients =
        ["Multipurpose", "Peat", "Coir", "Loam", "Bark", "Perlite", "Pumice", "Grit", "Vermiculite", "Castings", "Charcoal", "SphagnumMoss"];

    private static GuidePhoto Photo(string file, string captionKey, int width, int height, string author, string licence, string source, bool cropped = false) =>
        new(file, captionKey, width, height, author, licence, "https://commons.wikimedia.org/wiki/File:" + source, cropped);

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

        new(Aroids, PlantGroups, "TitleAroids", "RowAroids", "Aroids_Lead", null, "LinkAroids",
        [
            Section("LookAt", Items("Aroids_Look1", "Aroids_Look2", "Aroids_Look3", "Aroids_Look4", "Aroids_Look5")),
            Section("MixPot", Items("Aroids_Mix1", "Aroids_Mix2", "Aroids_Mix3")),
            new GuideMethods("Aroids_Climb",
            [
                Method("moss-pole", "Aroids_MossPole",
                    new GuideField("FieldWhy", "Aroids_MossWhy"),
                    new GuideField("FieldHow", "Aroids_MossHow"),
                    new GuideField("FieldWatch", "Aroids_MossWatch")),
                Method("stake", "Aroids_Stake",
                    new GuideField("FieldWhy", "Aroids_StakeWhy"),
                    new GuideField("FieldHow", "Aroids_StakeHow")),
                Method("trail", "Aroids_Trail",
                    new GuideField("FieldWhy", "Aroids_TrailWhy"),
                    new GuideField("FieldWatch", "Aroids_TrailWatch")),
            ], NoteKey: "Aroids_ClimbNote"),
            Section("Aroids_AerialTitle", new GuideText("Aroids_Aerial")),
            Section("Aroids_WinterTitle", new GuideText("Aroids_Winter")),
            Section("NewPlants", Items("Aroids_New1", "Aroids_New2", "Aroids_New3", "Aroids_New4")),
            Section("GlovesTitle", new GuideText("Aroids_Gloves")),
            Section("Problems", new GuidePestLinks("Aroids_Problems", [Mealybugs, SpiderMites, Scale])),
            Section("InStikling", new GuideText("Aroids_InApp")),
        ],
        [RhsMonstera, RhsPhilodendron, RhsAlocasia, IastatePhilodendron, IastateAroids]),

        new(Begonias, PlantGroups, "TitleBegonias", "RowBegonias", "Begonias_Lead", null, "LinkBegonias",
        [
            Section("LookAt", Items("Begonias_Look1", "Begonias_Look2", "Begonias_Look3", "Begonias_Look4")),
            Section("Begonias_DryTitle", new GuideText("Begonias_Dry")),
            Section("Begonias_HumidTitle", new GuideText("Begonias_Humid")),
            Section("MixPot", new GuideText("Begonias_Mix")),
            new GuideMethods("NewPlants",
            [
                Method("leaf", "Begonias_Leaf",
                    new GuideField("FieldFor", "Begonias_LeafFor"),
                    new GuideField("FieldHow", "Begonias_LeafHow"),
                    new GuideField("FieldHowLong", "Begonias_LeafLong")),
                Method("stem", "Begonias_Stem",
                    new GuideField("FieldFor", "Begonias_StemFor"),
                    new GuideField("FieldHow", "Begonias_StemHow")),
                Method("division", "Begonias_Division",
                    new GuideField("FieldFor", "Begonias_DivisionFor"),
                    new GuideField("FieldHow", "Begonias_DivisionHow")),
            ]),
            Section("RestTitle", new GuideText("Begonias_Rest")),
            Section("Problems", new GuidePestLinks("Begonias_Problems", [Aphids, SpiderMites])),
            Section("InStikling", new GuideText("Begonias_InApp")),
        ],
        [RhsBegonias, IastateBegonias, IastateLeafvein]),

        new(Bromeliads, PlantGroups, "TitleBrom", "RowBrom", "Brom_Lead", null, "LinkBrom",
        [
            Section("LookAt", Items("Brom_Look1", "Brom_Look2", "Brom_Look3", "Brom_Look4")),
            Section("Brom_CupTitle", new GuideText("Brom_Cup")),
            Section("MixPot", new GuideText("Brom_Mix")),
            Section("Brom_AirTitle", Items("Brom_Air1", "Brom_Air2", "Brom_Air3", "Brom_Air4")),
            new GuideMethods("Brom_Watering",
            [
                Method("soaking", "Brom_Soaking",
                    new GuideField("FieldWhy", "Brom_SoakWhy"),
                    new GuideField("FieldHow", "Brom_SoakHow"),
                    new GuideField("FieldWatch", "Brom_SoakWatch")),
                Method("misting", "Brom_Misting",
                    new GuideField("FieldWhy", "Brom_MistWhy"),
                    new GuideField("FieldHow", "Brom_MistHow")),
            ]),
            Section("Brom_FlowersTitle", new GuideText("Brom_Flowers")),
            Section("NewPlants", new GuideText("Brom_New")),
            Section("Problems", new GuidePestLinks("Brom_Problems", [Mealybugs, Scale])),
            Section("InStikling", new GuideText("Brom_InApp")),
        ],
        [RhsAirplants, RhsBromeliads, UfBromeliads]),

        new(Succulents, PlantGroups, "TitleSucc", "RowSucc", "Succ_Lead", null, "LinkSucc",
        [
            Section("LookAt", Items("Succ_Look1", "Succ_Look2", "Succ_Look3", "Succ_Look4", "Succ_Look5")),
            Section("Succ_ForestTitle", new GuideText("Succ_Forest")),
            Section("MixPot", Items("Succ_Mix1", "Succ_Mix2", "Succ_Mix3")),
            Section("Succ_RestTitle", new GuideText("Succ_Rest")),
            Section("Succ_StretchTitle", new GuideText("Succ_Stretch")),
            new GuideMethods("NewPlants",
            [
                Method("offsets", "Succ_Offsets",
                    new GuideField("FieldFor", "Succ_OffsetsFor"),
                    new GuideField("FieldHow", "Succ_OffsetsHow")),
                Method("stem", "Succ_Stem",
                    new GuideField("FieldFor", "Succ_StemFor"),
                    new GuideField("FieldHow", "Succ_StemHow"),
                    new GuideField("FieldHowLong", "Succ_StemLong")),
                Method("leaves", "Succ_Leaves",
                    new GuideField("FieldFor", "Succ_LeavesFor"),
                    new GuideField("FieldHow", "Succ_LeavesHow"),
                    new GuideField("FieldHowLong", "Succ_LeavesLong")),
                Method("seed", "Succ_Seed",
                    new GuideField("FieldHow", "Succ_SeedHow")),
            ]),
            Section("Problems", new GuidePestLinks("Succ_Problems", [Mealybugs, SpiderMites, Scale])),
            Section("InStikling", new GuideText("Succ_InApp")),
        ],
        [RhsCacti, IastateSucculents, IllinoisSucculents]),

        new(Carnivorous, PlantGroups, "TitleCarniv", "RowCarniv", "Carniv_Lead", null, "LinkCarniv",
        [
            Section("WaterTitle", new GuideText("Carniv_Water")),
            Section("Carniv_StandingTitle", new GuideText("Carniv_Standing")),
            Section("MixPot", Items("Carniv_Mix1", "Carniv_Mix2", "Carniv_Mix3")),
            Section("Carniv_FoodTitle", Items("Carniv_Food1", "Carniv_Food2", "Carniv_Food3")),
            Section("Carniv_LightTitle", new GuideText("Carniv_Light")),
            new GuideMethods("Carniv_Winter",
            [
                Method("rest", "Carniv_Rest",
                    new GuideField("FieldWhy", "Carniv_RestWhy"),
                    new GuideField("FieldHow", "Carniv_RestHow")),
                Method("grow-on", "Carniv_GrowOn",
                    new GuideField("FieldWhy", "Carniv_GrowWhy"),
                    new GuideField("FieldHow", "Carniv_GrowHow"),
                    new GuideField("FieldWatch", "Carniv_GrowWatch")),
            ]),
            new GuideText("Carniv_Tropical"),
            Section("FlowersTitle", new GuideText("Carniv_Flowers")),
            Section("NewPlants", Items("Carniv_New1", "Carniv_New2", "Carniv_New3", "Carniv_New4")),
            Section("Problems", new GuidePestLinks("Carniv_Problems", [Aphids, Mealybugs, FungusGnats])),
            Section("InStikling", new GuideText("Carniv_InApp")),
        ],
        [RhsCarnivorous, IcpsFlytrap, RhsFlytrap, IcpsNepenthes]),

        new(Ferns, PlantGroups, "TitleFerns", "RowFerns", "Ferns_Lead", null, "LinkFerns",
        [
            Section("LookAt",
                Items("Ferns_Look1", "Ferns_Look2", "Ferns_Look3", "Ferns_Look4"),
                new GuidePhotos([Photo("fern-sori.webp", "Ferns_PhotoSori", 800, 600, "Ixitixel", "CC BY-SA 3.0", "Nephrolepis_cordifolia_(Sori).jpg")])),
            Section("HumidAirTitle", new GuideText("Ferns_Humid")),
            Section("WaterTitle", new GuideText("Ferns_Water")),
            Section("MixPot", Items("Ferns_Mix1", "Ferns_Mix2", "Ferns_Mix3")),
            Section("NewPlants", Items("Ferns_New1", "Ferns_New2", "Ferns_New3")),
            Section("Problems", new GuidePestLinks("Ferns_Problems", [Scale, SpiderMites, Mealybugs])),
            Section("InStikling", new GuideText("Ferns_InApp")),
        ],
        [RhsFerns, ClemsonFerns, RhsTenderFerns, Uc]),

        new(Hoyas, PlantGroups, "TitleHoyas", "RowHoyas", "Hoyas_Lead", null, "LinkHoyas",
        [
            Section("LookAt", Items("Hoyas_Look1", "Hoyas_Look2", "Hoyas_Look3", "Hoyas_Look4")),
            Section("MixPot", Items("Hoyas_Mix1", "Hoyas_Mix2", "Hoyas_Mix3")),
            Section("FlowersTitle",
                Items("Hoyas_Flowers1", "Hoyas_Flowers2", "Hoyas_Flowers3"),
                new GuidePhotos([Photo("hoya-peduncle.webp", "Hoyas_PhotoPeduncle", 800, 800, "MPF (Wikimedia Commons user)", "CC BY 2.5", "Hoya_carnosa1.jpg")])),
            Section("RestTitle", new GuideText("Hoyas_Rest")),
            Section("Hoyas_PruningTitle", new GuideText("Hoyas_Pruning")),
            new GuideMethods("Hoyas_Root",
            [
                Method("water", "Hoyas_Water",
                    new GuideField("FieldHow", "Hoyas_WaterHow"),
                    new GuideField("FieldHowLong", "Hoyas_WaterLong")),
                Method("sphagnum", "Hoyas_Sphagnum",
                    new GuideField("FieldHow", "Hoyas_SphagnumHow")),
                Method("mix", "Hoyas_LightMix",
                    new GuideField("FieldHow", "Hoyas_MixHow")),
            ], NoteKey: "Hoyas_RootNote"),
            Section("Hoyas_LayeringTitle", new GuideText("Hoyas_Layering")),
            Section("GlovesTitle", new GuideText("Hoyas_Gloves")),
            Section("Problems", new GuidePestLinks("Hoyas_Problems", [Mealybugs, Scale, SpiderMites])),
            Section("InStikling", new GuideText("Hoyas_InApp")),
        ],
        [IastateHoya, RhsHoya, PsuHoya]),

        new(Orchids, PlantGroups, "TitleOrchids", "RowOrchids", "Orchids_Lead", null, "LinkOrchids",
        [
            Section("LookAt", Items("Orchids_Look1", "Orchids_Look2", "Orchids_Look3", "Orchids_Look4")),
            Section("WaterTitle", Items("Orchids_Water1", "Orchids_Water2", "Orchids_Water3", "Orchids_Water4")),
            new GuideMethods("Orchids_How",
            [
                Method("run", "Orchids_Run",
                    new GuideField("FieldHow", "Orchids_RunHow")),
                Method("soak", "Orchids_Soak",
                    new GuideField("FieldHow", "Orchids_SoakHow")),
            ]),
            new GuideText("Orchids_Ice"),
            Section("MixPot", Items("Orchids_Mix1", "Orchids_Mix2")),
            Section("Orchids_AerialTitle", new GuideText("Orchids_Aerial")),
            Section("Orchids_AfterTitle",
                new GuideText("Orchids_AfterLead"),
                Items("Orchids_After1", "Orchids_After2")),
            Section("Orchids_AgainTitle", new GuideText("Orchids_Again")),
            Section("Orchids_KeikisTitle",
                new GuideText("Orchids_Keikis"),
                new GuidePhotos([Photo("orchid-keiki.webp", "Orchids_PhotoKeiki", 600, 800, "Dominique Lyon", "CC0", "Phalaenopsis_keiki.jpg")])),
            Section("Problems", new GuidePestLinks("Orchids_Problems", [Mealybugs])),
            Section("InStikling", new GuideText("Orchids_InApp")),
        ],
        [RhsPhal, AosPhal, RhsOrchids]),

        new(PrayerPlants, PlantGroups, "TitlePrayer", "RowPrayer", "Prayer_Lead", "Prayer_Where", "LinkPrayer",
        [
            Section("LookAt", Items("Prayer_Look1", "Prayer_Look2", "Prayer_Look3", "Prayer_Look4", "Prayer_Look5")),
            Section("WaterTitle", new GuideText("Prayer_Water")),
            Section("HumidAirTitle", new GuideText("Prayer_Humid")),
            Section("MixPot", new GuideText("Prayer_Mix")),
            Section("NewPlants", Items("Prayer_New1", "Prayer_New2")),
            Section("Problems", new GuidePestLinks("Prayer_Problems", [SpiderMites, Mealybugs, Scale, FungusGnats])),
            Section("InStikling", new GuideText("Prayer_InApp")),
        ],
        [RhsCalathea, UmdPrayer, IllinoisPrayer]),
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

    /// <summary>
    /// The families and genera each plant group guide is for, see docs/dev/guides.md. A family is matched first, then a genus,
    /// so a genus the dictionary has no family for still gets its guide. Hoyas are the genus only, since their family
    /// (Apocynaceae) has plants no guide here is about.
    /// </summary>
    private static readonly (string Guide, string[] Families, string[] Genera)[] PlantGroupMembers =
    [
        (Aroids, ["Araceae"], []),
        (Begonias, ["Begoniaceae"], ["Begonia"]),
        (Bromeliads, ["Bromeliaceae"], []),
        (Succulents, ["Cactaceae", "Crassulaceae", "Aizoaceae"], ["Aloe", "Haworthia", "Haworthiopsis", "Gasteria", "Agave", "Curio"]),
        (Carnivorous, ["Droseraceae", "Sarraceniaceae", "Nepenthaceae", "Lentibulariaceae", "Cephalotaceae"],
            ["Dionaea", "Drosera", "Sarracenia", "Nepenthes", "Pinguicula", "Utricularia", "Darlingtonia", "Heliamphora", "Cephalotus"]),
        (Ferns, ["Aspleniaceae", "Davalliaceae", "Nephrolepidaceae", "Polypodiaceae", "Pteridaceae"],
            ["Adiantum", "Asplenium", "Blechnum", "Cyrtomium", "Davallia", "Microsorum", "Nephrolepis", "Pellaea", "Phlebodium", "Platycerium", "Polypodium", "Pteris"]),
        (Hoyas, [], ["Hoya"]),
        (Orchids, ["Orchidaceae"], []),
        (PrayerPlants, ["Marantaceae"], ["Maranta", "Calathea", "Goeppertia", "Ctenanthe", "Stromanthe"]),
    ];

    /// <summary>
    /// The plant group guide for a plant, from its genus and its family where the dictionary has one, or null.
    /// Ignores case. The family is checked first, then the genus.
    /// </summary>
    public static string? ForPlant(string? genus, string? family)
    {
        if (family?.Trim() is { Length: > 0 } botanicalFamily)
        {
            foreach (var (guide, families, _) in PlantGroupMembers)
            {
                if (families.Contains(botanicalFamily, StringComparer.OrdinalIgnoreCase))
                    return guide;
            }
        }

        if (genus?.Trim() is { Length: > 0 } name)
        {
            foreach (var (guide, _, genera) in PlantGroupMembers)
            {
                if (genera.Contains(name, StringComparer.OrdinalIgnoreCase))
                    return guide;
            }
        }

        return null;
    }

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
