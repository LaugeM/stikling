using Stikling.Core.Models;

namespace Stikling.Core.Plants;

/// <summary>
/// What common houseplants usually like and whether they are toxic, as stored in
/// <c>wwwroot/data/plant-care.json</c>. The file is built from <c>tools/plant-names/care.txt</c> by
/// <c>care.cs</c> and never edited by hand.
/// </summary>
public sealed record PlantCareData
{
    public static readonly PlantCareData Empty = new();

    /// <summary>The sources and the name the app shows for each.</summary>
    public IReadOnlyList<CareSource> Sources { get; init; } = [];

    public IReadOnlyList<PlantCare> Plants { get; init; } = [];

    /// <summary>Old names and the name each goes by now: "Sansevieria" to "Dracaena", "Scindapsus aureus" to "Epipremnum aureum".</summary>
    public IReadOnlyDictionary<string, string> Formerly { get; init; } = new Dictionary<string, string>();
}

/// <param name="Host">The host of the source's web address, like plants.ces.ncsu.edu.</param>
public sealed record CareSource(string Host, string Name);

/// <summary>
/// What one genus or species usually likes. A value is null when no source says. Every value comes
/// from a source: the care from <see cref="CareFrom"/>, cats and dogs from <see cref="PetsFrom"/>
/// and people from <see cref="PeopleFrom"/>.
/// </summary>
public sealed record PlantCare
{
    /// <summary>"Genus" or "Genus species", as in care.txt.</summary>
    public required string Name { get; init; }

    public LightLevel? Light { get; init; }
    public WaterNeed? Water { get; init; }
    public HumidityNeed? Humidity { get; init; }
    public GrowingSeason? Season { get; init; }
    public RestKind? Rest { get; init; }
    public Toxicity? Cats { get; init; }
    public Toxicity? Dogs { get; init; }
    public Toxicity? People { get; init; }
    public string? CareFrom { get; init; }
    public string? PetsFrom { get; init; }
    public string? PeopleFrom { get; init; }
}
