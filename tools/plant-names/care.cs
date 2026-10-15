// Builds the care the app offers for common plants: src/Stikling.Web/wwwroot/data/plant-care.json, from care.txt in this folder.
// Run from the repository root: dotnet run tools/plant-names/care.cs
// It works offline and doesn't touch plant-names.json. See README.md next to this file, and the header of care.txt for the format.

#:project ../../src/Stikling.Core/Stikling.Core.csproj
#:property PublishAot=false

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Plants;

var root = FindRepositoryRoot();
var dataFolder = Path.Combine(root, "src", "Stikling.Web", "wwwroot", "data");
var input = Path.Combine(root, "tools", "plant-names", "care.txt");
var output = Path.Combine(dataFolder, "plant-care.json");

var lights = new Dictionary<string, LightLevel>
{
    ["low"] = LightLevel.Low, ["medium"] = LightLevel.Medium,
    ["bright-indirect"] = LightLevel.BrightIndirect, ["direct-sun"] = LightLevel.DirectSun
};
var waters = new Dictionary<string, WaterNeed>
{
    ["dry-out"] = WaterNeed.DryOut, ["dry-partly"] = WaterNeed.DryPartly, ["moist"] = WaterNeed.EvenlyMoist
};
var humidities = new Dictionary<string, HumidityNeed>
{
    ["low"] = HumidityNeed.Low, ["medium"] = HumidityNeed.Medium, ["high"] = HumidityNeed.High
};
var seasons = new Dictionary<string, GrowingSeason>
{
    ["summer"] = GrowingSeason.Summer, ["winter"] = GrowingSeason.Winter, ["all-year"] = GrowingSeason.NoRest
};
var rests = new Dictionary<string, RestKind>
{
    ["cooler"] = RestKind.Cooler, ["drier"] = RestKind.Drier,
    ["cooler-drier"] = RestKind.CoolerAndDrier, ["dies-back"] = RestKind.DiesBack
};
var toxicities = new Dictionary<string, Toxicity>
{
    ["toxic"] = Toxicity.Toxic, ["non-toxic"] = Toxicity.NonToxic
};

var errors = new List<string>();
var sources = new List<CareSource>();
var plants = new List<PlantCare>();
var seen = new Dictionary<string, int>();

foreach (var (text, number) in File.ReadLines(input).Select((l, i) => (l.Trim(), i + 1)))
{
    if (text.Length == 0 || text.StartsWith('#'))
        continue;

    if (text.StartsWith("source ", StringComparison.Ordinal))
    {
        var colon = text.IndexOf(':');
        var host = colon < 0 ? "" : text[7..colon].Trim();
        var name = colon < 0 ? "" : text[(colon + 1)..].Trim();
        if (host.Length == 0 || name.Length == 0)
            errors.Add($"care.txt line {number}: a source line is 'source <host>: <Name>'");
        else
            sources.Add(new CareSource(host, name));
        continue;
    }

    var split = text.IndexOf(':');
    if (split <= 0)
    {
        errors.Add($"care.txt line {number}: expected 'Name: 11 values separated by |'");
        continue;
    }

    var plantName = Tidy(text[..split].Trim());
    var values = text[(split + 1)..].Split('|', StringSplitOptions.TrimEntries);
    if (values.Length != 11)
    {
        errors.Add($"care.txt line {number}: {plantName} has {values.Length} values, expected 11");
        continue;
    }

    var before = errors.Count;
    var light = Word(values[0], lights, "light");
    var water = Word(values[1], waters, "water");
    var humidity = Word(values[2], humidities, "humidity");
    var season = Word(values[3], seasons, "season");
    var rest = Word(values[4], rests, "rest");
    var cats = Word(values[5], toxicities, "cats");
    var dogs = Word(values[6], toxicities, "dogs");
    var people = Word(values[7], toxicities, "people");
    var careFrom = Url(values[8], "care source");
    var petsFrom = Url(values[9], "pets source");
    var peopleFrom = Url(values[10], "people source");

    if (rest is not null && season is not (GrowingSeason.Summer or GrowingSeason.Winter))
        errors.Add($"care.txt line {number}: {plantName} has a way of resting without a season of summer or winter");
    if ((light is not null || water is not null || humidity is not null || season is not null || rest is not null) && careFrom is null)
        errors.Add($"care.txt line {number}: {plantName} has care values but no care source");
    if ((cats ?? dogs) is not null && petsFrom is null)
        errors.Add($"care.txt line {number}: {plantName} has cats or dogs set but no pets source");
    if (people is not null && peopleFrom is null)
        errors.Add($"care.txt line {number}: {plantName} has people set but no people source");

    var key = Key(plantName);
    if (seen.TryGetValue(key, out var first))
        errors.Add($"care.txt line {number}: {plantName} is already on line {first}");
    else
        seen[key] = number;

    if (errors.Count == before)
    {
        plants.Add(new PlantCare
        {
            Name = plantName, Light = light, Water = water, Humidity = humidity, Season = season, Rest = rest,
            Cats = cats, Dogs = dogs, People = people, CareFrom = careFrom, PetsFrom = petsFrom, PeopleFrom = peopleFrom
        });
    }

    T? Word<T>(string value, Dictionary<string, T> allowed, string column) where T : struct
    {
        if (value == "?")
            return null;
        if (allowed.TryGetValue(value, out var found))
            return found;
        errors.Add($"care.txt line {number}: '{value}' isn't a {column} value ({string.Join(", ", allowed.Keys)})");
        return null;
    }

    string? Url(string value, string column)
    {
        if (value == "?")
            return null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            errors.Add($"care.txt line {number}: '{value}' isn't a web address ({column})");
            return null;
        }
        if (!sources.Any(s => s.Host == uri.Host))
            errors.Add($"care.txt line {number}: {uri.Host} has no source line ({column})");
        return value;
    }
}

if (errors.Count > 0)
{
    foreach (var error in errors)
        Console.Error.WriteLine($"error: {error}");
    Console.Error.WriteLine($"Nothing written: {errors.Count} {(errors.Count == 1 ? "problem" : "problems")} in care.txt");
    return 1;
}

// The names plant-names.json knows, to warn about a typo and to find the old names
var names = JsonSerializer.Deserialize<PlantNameData>(
    await File.ReadAllTextAsync(Path.Combine(dataFolder, "plant-names.json")), PlantNameData.JsonOptions) ?? PlantNameData.Empty;
var genera = names.Genera.ToDictionary(g => g.Name, StringComparer.OrdinalIgnoreCase);

var formerly = new SortedDictionary<string, string>(StringComparer.Ordinal);
foreach (var care in plants)
{
    var parts = care.Name.Split(' ', 2);
    var genusName = parts[0];
    var species = parts.Length > 1 ? parts[1] : null;

    if (!genera.TryGetValue(genusName, out var genus))
    {
        Console.WriteLine($"warning: {care.Name}: {genusName} isn't a genus in plant-names.json");
    }
    else
    {
        if (genus.Now is { } now)
            Console.WriteLine($"warning: {care.Name}: {genusName} is an old genus name, it goes by {now} now");
        if (species is not null && !genus.Species.Contains(species, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(genus.Synonyms.FirstOrDefault(s => s.Key.Equals(species, StringComparison.OrdinalIgnoreCase)) is { Value: not null } old
                ? $"warning: {care.Name} is an old name, it goes by {old.Value} now"
                : $"warning: {care.Name}: {species} isn't an accepted species of {genus.Name} in plant-names.json");
        }
    }

    // Old genus names that go by this genus now
    foreach (var old in names.Genera.Where(g => string.Equals(g.Now, genusName, StringComparison.OrdinalIgnoreCase)))
        formerly[old.Name] = genusName;

    // Old species names that go by this one now
    if (species is not null)
    {
        foreach (var g in names.Genera)
        {
            foreach (var (oldSpecies, now) in g.Synonyms.Where(s => string.Equals(s.Value, care.Name, StringComparison.OrdinalIgnoreCase)))
                formerly[$"{g.Name} {oldSpecies}"] = care.Name;
        }
    }
}

var options = new JsonSerializerOptions(PlantNameData.JsonOptions)
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // keeps × readable in the file
};

// One plant to a line, like plant-names.json, so a change shows as the plants it touched
var json = new StringBuilder("{\n\"sources\":[");
json.Append(string.Join(",", sources.Select(s => "\n" + JsonSerializer.Serialize(s, options))));
json.Append("\n],\n\"plants\":[");
json.Append(string.Join(",", plants.Select(p => "\n" + JsonSerializer.Serialize(p, options))));
json.Append("\n],\n\"formerly\":{");
json.Append(string.Join(",", formerly.Select(f => $"\n{JsonSerializer.Serialize(f.Key, options)}:{JsonSerializer.Serialize(f.Value, options)}")));
json.Append("\n}\n}\n");

Directory.CreateDirectory(dataFolder);
await File.WriteAllTextAsync(output, json.ToString());
Console.WriteLine(
    $"Wrote {Path.GetRelativePath(root, output)}: {plants.Count(p => p.Name.Contains(' '))} species lines, " +
    $"{plants.Count(p => !p.Name.Contains(' '))} genus lines, {formerly.Count} old names, {new FileInfo(output).Length / 1024} KB");
return 0;

// Names are compared without case, extra spaces or the hybrid sign, so 'Alocasia × x' and 'alocasia x' are the same line
static string Key(string name) => string.Join(' ', name.ToLowerInvariant().Replace("×", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries));

static string Tidy(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));

static string FindRepositoryRoot()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "Stikling.slnx")))
            return dir.FullName;
    }
    throw new InvalidOperationException("Run this from inside the Stikling repository");
}
