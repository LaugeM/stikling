// Builds src/Stikling.Web/wwwroot/data/plant-names.json, the plant names the app suggests.
// Run from the repository root: dotnet run tools/plant-names/build.cs
// See README.md next to this file for where the names come from and how to add more.

#:project ../../src/Stikling.Core/Stikling.Core.csproj
#:property PublishAot=false

using System.Collections;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Stikling.Core.Names;

// Families or genera whose genera, species and old names are all included
string[] include = ["Araceae", "Coleus"];

// Genera offered by name only, without their species, until their family is added above
string[] genusOnly =
[
    "Asparagus", "Begonia", "Calathea", "Crassula", "Ctenanthe", "Dracaena", "Ficus", "Goeppertia", "Hoya",
    "Laurus", "Maranta", "Musa", "Ocimum", "Pachira", "Peperomia", "Petroselinum", "Phalaenopsis", "Pilea",
    "Plectranthus", "Sansevieria", "Strelitzia", "Tradescantia"
];

var root = FindRepositoryRoot();
var output = Path.Combine(root, "src", "Stikling.Web", "wwwroot", "data", "plant-names.json");
var cultivarList = Path.Combine(root, "tools", "plant-names", "cultivars.txt");

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("stikling-plant-names/1.0 (https://github.com/LaugeM/stikling)");

const string Col = "https://api.checklistbank.org/dataset/3LR";
var colInfo = await http.GetFromJsonAsync<JsonObject>(Col) ?? throw new InvalidOperationException("No Catalogue of Life info");
var colVersion = (string?)colInfo["version"];
Console.WriteLine($"Catalogue of Life {colVersion}");

var genera = new SortedDictionary<string, Genus>(StringComparer.OrdinalIgnoreCase);

// First, so the old species names below are kept for these genera too, like Plectranthus scutellarioides,
// which is Coleus scutellarioides now
foreach (var name in genusOnly)
{
    var taxon = await FindAsync(name, "genus");
    if (taxon is null)
    {
        Console.WriteLine($"warning: genus {name} isn't in the Catalogue of Life");
        continue;
    }
    var genus = GenusFor(name, FamilyOf(taxon["usage"]!));
    if ((string?)taxon["usage"]!["status"] == "synonym")
        genus.Now = AcceptedName(taxon["usage"]!);
}

foreach (var name in include)
{
    var taxon = await FindAsync(name, null) ?? throw new InvalidOperationException($"{name} isn't in the Catalogue of Life");
    var id = (string)taxon["id"]!;
    Console.WriteLine($"{name}: fetching names");

    foreach (var usage in await AllAsync(id, "genus", "accepted"))
        GenusFor(Name(usage)["scientificName"]!.ToString(), FamilyOf(usage));

    // An old name that is also an accepted one is a homonym, e.g. an Alocasia by another author
    // that is now Arisaema. The accepted genus is the one people mean.
    foreach (var usage in await AllAsync(id, "genus", "synonym"))
    {
        var old = Name(usage)["scientificName"]!.ToString();
        if (!genera.ContainsKey(old))
            GenusFor(old, FamilyOf(usage)).Now = AcceptedName(usage);
    }

    foreach (var rank in new[] { "species", "subspecies", "variety" })
    {
        foreach (var usage in await AllAsync(id, rank, "accepted"))
        {
            if (SpeciesPart(Name(usage)) is { } species && genera.TryGetValue(Name(usage)["genus"]!.ToString(), out var genus))
                genus.Species.Add(species);
        }
    }

    // Old species names only. Old genera are covered above, and old varieties are too many to be worth it.
    foreach (var usage in await AllAsync(id, "species", "synonym"))
    {
        var name1 = Name(usage);
        if (SpeciesPart(name1) is { } species
            && AcceptedName(usage) is { } now
            && genera.TryGetValue(name1["genus"]!.ToString(), out var genus)
            && !genus.Species.Contains(species))
        {
            genus.Synonyms[species] = now;
        }
    }
}

var byHand = 0;
foreach (var (line, number) in File.ReadLines(cultivarList).Select((l, i) => (l.Trim(), i + 1)))
{
    if (line.Length == 0 || line.StartsWith('#'))
        continue;
    if (ParseCultivar(line) is not var (genus, species, cultivar))
    {
        Console.WriteLine($"warning: cultivars.txt line {number} isn't Genus [species] 'Cultivar': {line}");
        continue;
    }
    if (AddCultivar(genus, species, cultivar, $"cultivars.txt line {number}"))
        byHand++;
}

// Only the names cultivars.txt doesn't have, so a species set by hand isn't doubled by Wikidata's version
Console.WriteLine("Wikidata: fetching cultivars");
var fromWikidata = 0;
foreach (var name in include)
{
    foreach (var (genus, species, cultivar) in await WikidataCultivarsAsync(name))
    {
        if (genera.TryGetValue(genus, out var known) && known.Cultivars.Any(c => c.Name.Equals(cultivar, StringComparison.OrdinalIgnoreCase)))
            continue;
        if (AddCultivar(genus, species, cultivar, null))
            fromWikidata++;
    }
}

var data = new PlantNameData
{
    Sources =
    [
        new NameSource("Catalogue of Life", "Genera, species and old names, from the World Checklist of Vascular Plants by Royal Botanic Gardens, Kew",
            "CC BY 4.0", "https://www.catalogueoflife.org", colVersion),
        new NameSource("Wikidata", "Cultivars", "CC0", "https://www.wikidata.org")
    ],
    Genera =
    [
        .. genera.Values.Select(g => new GenusNames
        {
            Name = g.Name,
            Family = g.Family,
            Now = g.Now,
            Species = [.. g.Species.Order(StringComparer.Ordinal)],
            Synonyms = new SortedDictionary<string, string>(g.Synonyms, StringComparer.Ordinal),
            Cultivars = [.. g.Cultivars.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Species, StringComparer.Ordinal)]
        })
    ]
};

Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var options = new JsonSerializerOptions(PlantNameData.JsonOptions)
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keeps × and ' readable in the file
    TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { SkipEmptyCollections } }
};
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(data, options) + "\n");

Console.WriteLine(
    $"Wrote {Path.GetRelativePath(root, output)}: {data.Genera.Count} genera, {data.Genera.Sum(g => g.Species.Count)} species, " +
    $"{data.Genera.Sum(g => g.Synonyms.Count)} old names, {fromWikidata} cultivars from Wikidata and {byHand} from cultivars.txt, " +
    $"{new FileInfo(output).Length / 1024} KB");

Genus GenusFor(string name, string? family)
{
    if (!genera.TryGetValue(name, out var genus))
        genera[name] = genus = new Genus(name, family);
    return genus;
}

// Warnings only for the list kept by hand, where they point at a typo. "where" says where it came from.
bool AddCultivar(string genusName, string? species, string cultivar, string? where)
{
    if (!genera.TryGetValue(genusName, out var genus))
    {
        if (where is not null)
            Console.WriteLine($"warning: {where}: {genusName} isn't one of the genera included");
        return false;
    }
    if (where is not null && species is not null && !genus.Species.Contains(species))
        Console.WriteLine($"warning: {where}: {genus.Name} {species} isn't an accepted species");

    return genus.Cultivars.Add(new CultivarName(cultivar, species));
}

// A name in Catalogue of Life search results, or the taxon itself
async Task<JsonNode?> FindAsync(string name, string? rank)
{
    var url = $"{Col}/nameusage/search?q={Uri.EscapeDataString(name)}&limit=50" + (rank is null ? "" : $"&rank={rank}");
    var page = await http.GetFromJsonAsync<JsonObject>(url);
    // The accepted name first, in case an old homonym by another author is listed too
    return page?["result"]?.AsArray()
        .Where(r =>
            Name(r!["usage"]!)["scientificName"]!.ToString() == name
            && r["classification"]!.AsArray().Any(c => (string?)c!["name"] == "Plantae")
            && (string?)r["usage"]!["status"] is "accepted" or "synonym")
        .OrderBy(r => (string?)r!["usage"]!["status"] != "accepted")
        .FirstOrDefault();
}

async Task<List<JsonNode>> AllAsync(string taxonId, string rank, string status)
{
    const int PageSize = 1000;
    var usages = new List<JsonNode>();
    for (var offset = 0; ; offset += PageSize)
    {
        var url = $"{Col}/nameusage/search?TAXON_ID={taxonId}&rank={rank}&status={status}&limit={PageSize}&offset={offset}";
        var page = await http.GetFromJsonAsync<JsonObject>(url) ?? throw new InvalidOperationException($"No answer from {url}");
        var results = page["result"]?.AsArray() ?? [];
        usages.AddRange(results.Select(r => r!["usage"]!));
        if (results.Count < PageSize)
            break;
    }
    Console.WriteLine($"  {rank} {status}: {usages.Count}");
    return usages;
}

static JsonNode Name(JsonNode usage) => usage["name"]!;

static string? FamilyOf(JsonNode usageOrResult) =>
    // Search results carry the classification next to the usage, a usage on its own doesn't
    usageOrResult.Parent?["classification"]?.AsArray().FirstOrDefault(c => (string?)c!["rank"] == "family")?["name"]?.ToString();

static string? AcceptedName(JsonNode usage) => usage["accepted"]?["name"]?["scientificName"]?.ToString();

// "deliciosa", "× mortfontanensis" or "hederaceum var. oxycardium". Null for autonyms like
// "hederaceum var. hederaceum", which only repeat the species.
static string? SpeciesPart(JsonNode name)
{
    var epithet = name["specificEpithet"]?.ToString();
    if (string.IsNullOrEmpty(epithet))
        return null;

    var notho = name["notho"]?.AsArray().Select(n => n!.ToString()).ToList() ?? [];
    var species = (notho.Contains("specific") ? "× " : "") + epithet;

    var infra = name["infraspecificEpithet"]?.ToString();
    if (string.IsNullOrEmpty(infra))
        return species;
    if (infra == epithet)
        return null;

    var marker = (string?)name["rank"] switch { "subspecies" => "subsp.", "variety" => "var.", _ => null };
    return marker is null ? null : $"{species} {marker} {infra}";
}

async Task<List<(string Genus, string? Species, string Cultivar)>> WikidataCultivarsAsync(string taxon)
{
    var query = $$"""
        SELECT ?label ?parentName WHERE {
          ?root wdt:P225 "{{taxon}}" .
          ?item wdt:P31/wdt:P279* wd:Q4886 ; wdt:P171 ?parent ; rdfs:label ?label .
          FILTER(LANG(?label) = "en")
          ?parent wdt:P171* ?root ; wdt:P225 ?parentName .
        }
        """;
    var url = "https://query.wikidata.org/sparql?format=json&query=" + Uri.EscapeDataString(query);
    var answer = await http.GetFromJsonAsync<JsonObject>(url) ?? throw new InvalidOperationException("No answer from Wikidata");

    var cultivars = new List<(string, string?, string)>();
    foreach (var row in answer["results"]!["bindings"]!.AsArray())
    {
        var label = row!["label"]!["value"]!.ToString();
        var parent = row["parentName"]!["value"]!.ToString().Split(' ', 2);
        if (ParseCultivar(label) is var (_, _, cultivar))
            cultivars.Add((parent[0], parent.Length > 1 ? parent[1] : null, cultivar));
    }
    Console.WriteLine($"  {cultivars.Count} found");
    return cultivars;
}

// "Monstera deliciosa 'Thai Constellation'" or "Philodendron 'Birkin'", with any kind of quote
static (string Genus, string? Species, string Cultivar)? ParseCultivar(string text)
{
    char[] quotes = ['\'', '‘', '’', 'ʽ', 'ʼ', '"', '“', '”'];
    var open = text.IndexOfAny(quotes);
    var close = text.LastIndexOfAny(quotes);
    if (open <= 0 || close <= open + 1)
        return null;

    var cultivar = text[(open + 1)..close].Trim();
    var taxon = text[..open].Trim().Split(' ', 2, StringSplitOptions.TrimEntries);
    return cultivar.Length == 0 ? null : (taxon[0], taxon.Length > 1 ? taxon[1] : null, cultivar);
}

static void SkipEmptyCollections(JsonTypeInfo type)
{
    foreach (var property in type.Properties)
    {
        if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(string))
            property.ShouldSerialize = (_, value) => value is not ICollection { Count: 0 };
    }
}

static string FindRepositoryRoot()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "Stikling.slnx")))
            return dir.FullName;
    }
    throw new InvalidOperationException("Run this from inside the Stikling repository");
}

sealed class Genus(string name, string? family)
{
    public string Name { get; } = name;
    public string? Family { get; } = family;
    public string? Now { get; set; }
    public HashSet<string> Species { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Synonyms { get; } = new(StringComparer.Ordinal);
    public HashSet<CultivarName> Cultivars { get; } = [];
}
