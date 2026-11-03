namespace Stikling.Core.Names;

/// <summary>
/// Each genus's family and where its species grow wild, as stored in <c>wwwroot/data/plant-regions.json</c>.
/// The file is built by the script in <c>tools/plant-names</c> and never edited by hand.
/// </summary>
public sealed record PlantRegionData
{
    public static readonly PlantRegionData Empty = new();

    public IReadOnlyList<NameSource> Sources { get; init; } = [];

    public IReadOnlyList<GenusRegions> Genera { get; init; } = [];
}

/// <summary>
/// A genus with its family and the areas of its species. An old genus name has <see cref="Now"/>
/// instead. Species keys are as in plant-names.json, and a value is Kew level 3 area codes
/// separated by spaces.
/// </summary>
public sealed record GenusRegions
{
    public required string Name { get; init; }

    public string? Family { get; init; }

    public string? Now { get; init; }

    public IReadOnlyDictionary<string, string> Species { get; init; } = new Dictionary<string, string>();
}
