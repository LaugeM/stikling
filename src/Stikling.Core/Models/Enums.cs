using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

// Enums are stored as strings (not numbers) so stored data and backups stay readable
// and don't break if the order of the values changes.

[JsonConverter(typeof(JsonStringEnumConverter<PlantStatus>))]
public enum PlantStatus
{
    Active,
    Died,
    GivenAway,
    Sold
}

/// <summary>How the plant came into the collection.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlantOrigin>))]
public enum PlantOrigin
{
    Purchased,
    Propagated,
    GrownFromSeed,
    Gift,
    Swap,
    Unknown
}

/// <summary>What the plant or propagation grows in.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GrowingMedium>))]
public enum GrowingMedium
{
    Soil,
    Leca,
    Pon,
    Perlite,
    Sphagnum,
    Water,

    /// <summary>Held above water rather than in it, e.g. a corm on a riser in a sealed box.</summary>
    CormRiser,

    Other
}

/// <summary>What was taken from the parent (or sown) to start a propagation.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PropagationType>))]
public enum PropagationType
{
    Cutting,
    Corm,
    Offset,
    Seed,
    Division,
    AirLayer,
    Leaf,
    Other
}

/// <summary>
/// How far a propagation has come. Started, Rooting and Rooted are set by hand; Done and
/// Failed are set automatically once every unit has been potted up or has failed.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PropagationStage>))]
public enum PropagationStage
{
    Started,
    Rooting,
    Rooted,
    Done,
    Failed
}
