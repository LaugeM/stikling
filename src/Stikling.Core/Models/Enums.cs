using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

// Enums are stored as strings (not numbers) so stored data and backups stay readable
// and don't break if the order of the values changes.

[JsonConverter(typeof(JsonStringEnumConverter<LightLevel>))]
public enum LightLevel
{
    Low,
    Medium,
    BrightIndirect,
    DirectSun
}

[JsonConverter(typeof(JsonStringEnumConverter<PlantStatus>))]
public enum PlantStatus
{
    Active,
    Died,
    GivenAway,
    Sold
}

/// <summary>
/// Which of its pots went with a plant that was given away or sold. A rooted cutting usually
/// goes without one, a plant for someone without soil at home in its pot, and now and then one
/// goes in its outer pot too.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PotsTaken>))]
public enum PotsTaken
{
    /// <summary>Just the plant. Its pots stay and are free again.</summary>
    None,

    /// <summary>The pot its roots are in.</summary>
    Inner,

    /// <summary>Every pot it had: the inner pot and the outer one.</summary>
    All
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
/// Something used to help a propagation root, so the results page can compare batches with
/// and without it. More than one can be used at once.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RootingAid>))]
public enum RootingAid
{
    RootingPowder,
    RootingGel,
    Cinnamon,
    WillowWater,
    HumidityDome,
    HeatMat
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

[JsonConverter(typeof(JsonStringEnumConverter<ThemeMode>))]
public enum ThemeMode
{
    System,
    Light,
    Dark
}
