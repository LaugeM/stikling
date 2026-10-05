// Builds the plant names the app suggests: src/Stikling.Web/wwwroot/data/plant-names.json with the
// botanical names, and everyday-names.en.json and everyday-names.da.json next to it with what people call them.
// Run from the repository root: dotnet run tools/plant-names/build.cs
// See README.md next to this file for where the names come from and how to add more.

#:project ../../src/Stikling.Core/Stikling.Core.csproj
#:property PublishAot=false

using System.Collections;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Stikling.Core.Names;

// Families or genera whose genera, species and old names are all included
string[] include =
[
    // Families
    "Araceae", "Begoniaceae", "Droseraceae", "Marantaceae", "Musaceae", "Nepenthaceae", "Sarraceniaceae", "Strelitziaceae",
    // Genera
    "Adiantum", "Aeschynanthus", "Aloe", "Aphelandra", "Asparagus", "Aspidistra", "Beaucarnea", "Callisia", "Ceropegia",
    "Chamaedorea", "Chlorophytum", "Chrysalidocarpus", "Codiaeum", "Coleus", "Crassula", "Curio", "Cycas", "Cymbidium", "Dischidia", "Dracaena",
    "Dypsis", "Echeveria", "Epiphyllum", "Episcia", "Fatsia", "Fittonia", "Gasteria", "Guzmania", "Haworthia", "Haworthiopsis",
    "Hedera", "Howea", "Hoya", "Hypoestes", "Kalanchoe", "Lithops", "Ludisia", "Miltoniopsis", "Nematanthus", "Nephrolepis",
    "Pachira", "Paphiopedilum", "Peperomia", "Phalaenopsis", "Pilea", "Platycerium", "Plectranthus", "Rhapis", "Rhipsalis",
    "Schefflera", "Schlumbergera", "Sinningia", "Streptocarpus", "Tillandsia", "Tradescantia", "Vanda", "Vriesea", "Yucca", "Zamia"
];

// Genera too big to include in full, where most of the species are never grown indoors. Only the species
// with an everyday name are included, and the ones with a cultivar.
string[] namedOnly =
[
    "Aechmea", "Asplenium", "Cattleya", "Cissus", "Davallia", "Dendrobium", "Euphorbia", "Ficus", "Heptapleurum", "Livistona",
    "Mammillaria", "Microsorum", "Neoregelia", "Oncidium", "Opuntia", "Oxalis", "Phoenix", "Polyscias", "Pteris", "Sedum"
];

// Genera offered by name only, without their species
string[] genusOnly = ["Laurus", "Ocimum", "Petroselinum"];

// The languages with a file of everyday names. English is always loaded, the others only on a device that uses them.
string[] languages = ["en", "da"];

var root = FindRepositoryRoot();
var dataFolder = Path.Combine(root, "src", "Stikling.Web", "wwwroot", "data");
var output = Path.Combine(dataFolder, "plant-names.json");
var tools = Path.Combine(root, "tools", "plant-names");
var cultivarList = Path.Combine(tools, "cultivars.txt");

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("stikling-plant-names/1.0 (https://github.com/LaugeM/stikling)");

const string Col = "https://api.checklistbank.org/dataset/3LR";
var colInfo = await http.GetFromJsonAsync<JsonObject>(Col) ?? throw new InvalidOperationException("No Catalogue of Life info");
var colVersion = (string?)colInfo["version"];
Console.WriteLine($"Catalogue of Life {colVersion}");

var genera = new SortedDictionary<string, Genus>(StringComparer.OrdinalIgnoreCase);

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

var everything = include.Concat(namedOnly).ToList();
var ids = new Dictionary<string, string>();
foreach (var name in everything)
{
    var taxon = await FindAsync(name, null) ?? throw new InvalidOperationException($"{name} isn't in the Catalogue of Life");
    ids[name] = (string)taxon["id"]!;
}

// Every genus first, old ones included, so an old species name is kept even when its old genus
// comes from another part of the list, like Plectranthus scutellarioides, which is Coleus scutellarioides now
foreach (var name in everything)
{
    Console.WriteLine($"{name}: fetching genera");
    foreach (var usage in await AllAsync(ids[name], "genus", "accepted"))
    {
        var genus = GenusFor(Name(usage)["scientificName"]!.ToString(), FamilyOf(usage));
        if (namedOnly.Contains(name))
            genus.NamedOnly = true;
    }
}
foreach (var name in everything)
{
    // An old name that is also an accepted one is a homonym, e.g. an Alocasia by another author
    // that is now Arisaema. The accepted genus is the one people mean.
    foreach (var usage in await AllAsync(ids[name], "genus", "synonym"))
    {
        var old = Name(usage)["scientificName"]!.ToString();
        if (!genera.ContainsKey(old))
            GenusFor(old, FamilyOf(usage)).Now = AcceptedName(usage);
    }
}

foreach (var name in everything)
{
    Console.WriteLine($"{name}: fetching species");
    foreach (var rank in new[] { "species", "subspecies", "variety" })
    {
        foreach (var usage in await AllAsync(ids[name], rank, "accepted"))
        {
            if (SpeciesPart(Name(usage)) is { } species && genera.TryGetValue(Name(usage)["genus"]!.ToString(), out var genus))
                genus.Species.Add(species);
        }
    }

    // Old species names only. Old genera are covered above, and old varieties are too many to be worth it.
    foreach (var usage in await AllAsync(ids[name], "species", "synonym"))
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
foreach (var name in everything)
{
    foreach (var (genus, species, cultivar) in await WikidataCultivarsAsync(name))
    {
        if (genera.TryGetValue(genus, out var known) && known.Cultivars.Any(c => c.Name.Equals(cultivar, StringComparison.OrdinalIgnoreCase)))
            continue;
        if (AddCultivar(genus, species, cultivar, null))
            fromWikidata++;
    }
}

// Everyday names: the lists kept by hand come first, so the name shown is one people really use,
// then Wikidata's. Wikidata has one for many species, but often not the one on the label in a shop.
var everyday = languages.ToDictionary(l => l, _ => new Dictionary<Target, List<string>>());
var everydayByHand = languages.ToDictionary(l => l, _ => 0);
// Names from Wikidata a list kept by hand says to leave out, as language, plant and the name's key
var leaveOut = new HashSet<(string, Target, string)>();
// The plants on the lists kept by hand, which the search box puts first
var keptByHand = new HashSet<Target>();
foreach (var language in languages)
{
    var file = Path.Combine(tools, $"everyday-names.{language}.txt");
    foreach (var (text, number) in File.ReadLines(file).Select((l, i) => (l.Trim(), i + 1)))
    {
        if (text.Length == 0 || text.StartsWith('#'))
            continue;
        var where = $"everyday-names.{language}.txt line {number}";
        var leave = text.StartsWith('-');
        var line = leave ? text[1..].Trim() : text;
        var colon = line.IndexOf(':');
        if (colon <= 0)
        {
            Console.WriteLine($"warning: {where} isn't Genus [species] ['Cultivar']: Name, Name: {line}");
            continue;
        }
        var taxon = line[..colon].Trim();
        var cultivar = ParseCultivar(taxon);
        if (Resolve(cultivar is var (g, s, _) ? (s is null ? g : $"{g} {s}") : taxon) is not { } found)
        {
            Console.WriteLine($"warning: {where}: {taxon} isn't an included genus or species");
            continue;
        }
        var target = found with { Cultivar = cultivar?.Cultivar };
        foreach (var name in line[(colon + 1)..].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (leave)
            {
                leaveOut.Add((language, target, EverydayKey(name)));
                continue;
            }
            AddEveryday(language, target, name);
            keptByHand.Add(target);
            everydayByHand[language]++;
        }
    }
}

Console.WriteLine("Wikidata: fetching everyday names");
var everydayFromWikidata = languages.ToDictionary(l => l, _ => 0);
var wikidataNames = new List<(string Language, Target Target, string Name)>();
foreach (var name in everything.Concat(genusOnly))
{
    // The variant with the fewest capitals first, so "spider plant" wins over "Spider Plant"
    foreach (var (taxon, language, common) in (await WikidataEverydayAsync(name)).OrderBy(n => n.Name.Count(char.IsUpper)).ThenBy(n => n.Name, StringComparer.Ordinal))
    {
        if (Resolve(taxon) is not { } target
            || CleanEveryday(common, taxon, language, genera.Keys) is not { } clean
            || leaveOut.Contains((language, target, EverydayKey(clean))))
            continue;
        if (AddEveryday(language, target, clean))
        {
            everydayFromWikidata[language]++;
            wikidataNames.Add((language, target, clean));
        }
    }
}

// A name Wikidata gives three or more species, like "Fig" or "Orchid", names a group rather than a plant
foreach (var group in wikidataNames.Where(n => n.Target.Species is not null).GroupBy(n => (n.Language, EverydayKey(n.Name))).Where(g => g.Count() >= 3))
{
    foreach (var (language, target, name) in group)
    {
        everyday[language][target].Remove(name);
        everydayFromWikidata[language]--;
    }
}

// The big genera keep only the species someone could look for by name
var named = everyday.Values.SelectMany(d => d.Where(e => e.Value.Count > 0).Select(e => e.Key)).Where(t => t.Species is not null).Select(t => (t.Genus, t.Species!)).ToHashSet();
foreach (var genus in genera.Values.Where(g => g.NamedOnly))
{
    var withCultivar = genus.Cultivars.Select(c => c.Species).OfType<string>().ToHashSet();
    genus.Species.RemoveWhere(s => !named.Contains((genus.Name, s)) && !withCultivar.Contains(s));
}
var leftOut = genera.Values.Where(g => g.NamedOnly).Select(g => g.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
foreach (var genus in genera.Values)
{
    foreach (var (old, now) in genus.Synonyms.ToList())
    {
        if (now.Split(' ', 2) is [var nowGenus, var nowSpecies] && leftOut.Contains(nowGenus) && !genera[nowGenus].Species.Contains(nowSpecies))
            genus.Synonyms.Remove(old);
    }
}

var colSource = new NameSource("Catalogue of Life", "Genera, species and old names, from the World Checklist of Vascular Plants by Royal Botanic Gardens, Kew",
    "CC BY 4.0", "https://www.catalogueoflife.org", colVersion);
var data = new PlantNameData
{
    Sources = [colSource, new NameSource("Wikidata", "Cultivars", "CC0", "https://www.wikidata.org")],
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

Directory.CreateDirectory(dataFolder);
var options = new JsonSerializerOptions(PlantNameData.JsonOptions)
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keeps × and ' readable in the file
    TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { SkipEmptyCollections } }
};

// One genus to a line: a third smaller than indenting every name, and a change still shows as the genera it touched
await File.WriteAllTextAsync(output, Lines(("sources", data.Sources), ("genera", data.Genera)));
Console.WriteLine(
    $"Wrote {Path.GetRelativePath(root, output)}: {data.Genera.Count} genera, {data.Genera.Sum(g => g.Species.Count)} species, " +
    $"{data.Genera.Sum(g => g.Synonyms.Count)} old names, {fromWikidata} cultivars from Wikidata and {byHand} from cultivars.txt, " +
    $"{new FileInfo(output).Length / 1024} KB");

foreach (var language in languages)
{
    var plants = everyday[language]
        .Where(e => e.Value.Count > 0 && genera.TryGetValue(e.Key.Genus, out var g) && (e.Key.Species is null || g.Species.Contains(e.Key.Species)))
        .OrderBy(e => e.Key.Genus, StringComparer.Ordinal)
        .ThenBy(e => e.Key.Species, StringComparer.Ordinal)
        .ThenBy(e => e.Key.Cultivar, StringComparer.OrdinalIgnoreCase)
        .Select(e => new EverydayNames(e.Key.Genus, e.Key.Species, e.Key.Cultivar, e.Value, keptByHand.Contains(e.Key)))
        .ToList();
    var file = Path.Combine(dataFolder, $"everyday-names.{language}.json");
    var head = $"{{\"language\":\"{language}\",";
    await File.WriteAllTextAsync(file, head + Lines(("sources", (IEnumerable<NameSource>)[new("Wikidata", "Everyday names", "CC0", "https://www.wikidata.org")]), ("plants", plants))[1..]);
    Console.WriteLine(
        $"Wrote {Path.GetRelativePath(root, file)}: {plants.Count} plants, {everydayByHand[language]} names by hand and " +
        $"{everydayFromWikidata[language]} from Wikidata, {new FileInfo(file).Length / 1024} KB");
}

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

// The genus or species a name stands for now. An old species name gives the one it goes by now, but an
// old genus on its own gives nothing, since its everyday name would land on a genus it's only part of.
Target? Resolve(string taxon)
{
    var parts = taxon.Split(' ', 2, StringSplitOptions.TrimEntries);
    if (!genera.TryGetValue(parts[0], out var genus))
        return null;
    if (parts.Length == 1)
        return genus.Now is null ? new Target(genus.Name, null, null) : null;
    if (genus.Species.Contains(parts[1]))
        return new Target(genus.Name, parts[1], null);
    if (genus.Synonyms.TryGetValue(parts[1], out var now) && now.Split(' ', 2) is [var nowGenus, var nowSpecies] && genera.ContainsKey(nowGenus))
        return new Target(genera[nowGenus].Name, nowSpecies, null);
    return null;
}

// Each name once for a plant, whatever its capitals or hyphens. False when it was there already.
bool AddEveryday(string language, Target target, string name)
{
    if (!everyday[language].TryGetValue(target, out var names))
        everyday[language][target] = names = [];
    if (names.Any(n => EverydayKey(n) == EverydayKey(name)))
        return false;
    names.Add(name);
    return true;
}

static string EverydayKey(string name) => new([.. name.ToLowerInvariant().Where(char.IsLetter)]);

// Wikidata's names as the app shows them, or null for ones that are only a botanical name again,
// like "aloe vera", or that are more of a description than a name
static string? CleanEveryday(string name, string taxon, string language, IEnumerable<string> genera)
{
    var clean = Regex.Replace(Regex.Replace(name, @"\(.*?\)", ""), @"\s+", " ").Trim();
    if (clean.Length is < 2 or > 40 || clean.Any(c => char.IsDigit(c) || c is '(' or ')' or '/' or ';'))
        return null;

    // Danish labels name a genus as "Vedbend-slægten", which is "the ivy genus"
    if (clean.EndsWith("slægten", StringComparison.OrdinalIgnoreCase))
        return null;

    // A botanical name, like "Thaumatophyllum", isn't an everyday one. In Danish, nor is one with the
    // genus in it, like "Aloe moledarana", where in English "Watermelon peperomia" is a real name.
    var words = clean.Split(' ');
    var genus = taxon.Split(' ')[0];
    if ((words.Length == 1 && genera.Contains(clean, StringComparer.OrdinalIgnoreCase))
        || (language == "da" && words.Any(w => w.Equals(genus, StringComparison.OrdinalIgnoreCase))))
        return null;

    // Sentence case: Danish writes nearly every word after the first in lower case, and in English
    // a name with every word capitalised, like "Blushing Bride", is a title rather than proper names
    if (language == "da" || (words.Length > 1 && words.All(w => char.IsUpper(w[0]))))
        clean = clean[0] + clean[1..].ToLowerInvariant();
    return char.ToUpperInvariant(clean[0]) + clean[1..];
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

async Task<JsonArray> WikidataAsync(string query)
{
    var url = "https://query.wikidata.org/sparql?format=json&query=" + Uri.EscapeDataString(query);
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            var answer = await http.GetFromJsonAsync<JsonObject>(url) ?? throw new InvalidOperationException("No answer from Wikidata");
            return answer["results"]!["bindings"]!.AsArray();
        }
        catch (HttpRequestException e) when (attempt < 3)
        {
            // The query service asks callers to slow down now and then
            Console.WriteLine($"  Wikidata: {e.Message}, trying again");
            await Task.Delay(TimeSpan.FromSeconds(10 * attempt));
        }
    }
}

async Task<List<(string Genus, string? Species, string Cultivar)>> WikidataCultivarsAsync(string taxon)
{
    var rows = await WikidataAsync($$"""
        SELECT ?label ?parentName WHERE {
          ?root wdt:P225 "{{taxon}}" .
          ?item wdt:P31/wdt:P279* wd:Q4886 ; wdt:P171 ?parent ; rdfs:label ?label .
          FILTER(LANG(?label) = "en")
          ?parent wdt:P171* ?root ; wdt:P225 ?parentName .
        }
        """);

    var cultivars = new List<(string, string?, string)>();
    foreach (var row in rows)
    {
        var label = row!["label"]!["value"]!.ToString();
        var parent = row["parentName"]!["value"]!.ToString().Split(' ', 2);
        if (ParseCultivar(label) is var (_, _, cultivar))
            cultivars.Add((parent[0], parent.Length > 1 ? parent[1] : null, cultivar));
    }
    Console.WriteLine($"  {taxon}: {cultivars.Count} cultivars");
    return cultivars;
}

// Wikidata's common names (P1843), and the Danish label, which is the Danish name when the plant has one
async Task<List<(string Taxon, string Language, string Name)>> WikidataEverydayAsync(string taxon)
{
    var rows = await WikidataAsync($$"""
        SELECT ?taxon ?name WHERE {
          ?root wdt:P225 "{{taxon}}" .
          ?item wdt:P171* ?root ; wdt:P225 ?taxon .
          { ?item wdt:P1843 ?name . } UNION { ?item rdfs:label ?name . FILTER(LANG(?name) = "da") }
          FILTER(LANG(?name) = "da" || LANG(?name) = "en" || STRSTARTS(LANG(?name), "en-"))
        }
        """);

    var names = new List<(string, string, string)>();
    foreach (var row in rows)
    {
        var language = row!["name"]!["xml:lang"]!.ToString();
        names.Add((row["taxon"]!["value"]!.ToString(), language.StartsWith("en") ? "en" : language, row["name"]!["value"]!.ToString()));
    }
    Console.WriteLine($"  {taxon}: {names.Count} everyday names");
    return names;
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

// A JSON object whose lists have one item to a line
string Lines(params (string Name, IEnumerable Items)[] lists)
{
    var text = new StringBuilder("{");
    foreach (var (name, items) in lists)
    {
        text.Append(text.Length == 1 ? "" : ",").Append($"\n\"{name}\":[");
        var first = true;
        foreach (var item in items)
        {
            text.Append(first ? "\n" : ",\n").Append(JsonSerializer.Serialize(item, item.GetType(), options));
            first = false;
        }
        text.Append("\n]");
    }
    return text.Append("\n}\n").ToString();
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

    /// <summary>One of the big genera, which only keeps its species with an everyday name.</summary>
    public bool NamedOnly { get; set; }

    public HashSet<string> Species { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Synonyms { get; } = new(StringComparer.Ordinal);
    public HashSet<CultivarName> Cultivars { get; } = [];
}

/// <summary>What an everyday name is for: a genus, a species, or a cultivar.</summary>
sealed record Target(string Genus, string? Species, string? Cultivar);
