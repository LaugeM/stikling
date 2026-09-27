using System.Text.Json;

namespace Stikling.Core.Names;

/// <summary>
/// The plant names that come with the app, as stored in <c>wwwroot/data/plant-names.json</c>.
/// The file is built by the script in <c>tools/plant-names</c> and never edited by hand.
/// </summary>
public sealed record PlantNameData
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static readonly PlantNameData Empty = new();

    /// <summary>Where the names come from, to credit them in Settings.</summary>
    public IReadOnlyList<NameSource> Sources { get; init; } = [];

    public IReadOnlyList<GenusNames> Genera { get; init; } = [];
}

/// <param name="Covers">What this source gave, e.g. "Genera and species".</param>
public sealed record NameSource(string Name, string Covers, string Licence, string Url, string? Version = null);

/// <summary>A genus with its species, the old names for them, and the cultivars.</summary>
public sealed record GenusNames
{
    public required string Name { get; init; }

    public string? Family { get; init; }

    /// <summary>The genus this is now part of, when the name itself is an old one, e.g. Thaumatophyllum is now Philodendron.</summary>
    public string? Now { get; init; }

    /// <summary>The accepted species, as the part after the genus: "deliciosa", "× mortfontanensis", "hederaceum var. oxycardium".</summary>
    public IReadOnlyList<string> Species { get; init; } = [];

    /// <summary>Old species names in this genus and the full name each goes by now, e.g. "aureus" is now "Epipremnum aureum".</summary>
    public IReadOnlyDictionary<string, string> Synonyms { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<CultivarName> Cultivars { get; init; } = [];
}

/// <param name="Species">The species it's a cultivar of, or null for hybrids and ones nobody agrees on.</param>
public sealed record CultivarName(string Name, string? Species = null);
