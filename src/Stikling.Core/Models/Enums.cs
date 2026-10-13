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

    /// <summary>Chunky bark with little or no soil in it, the way orchids are usually potted.</summary>
    OrchidBark,
    Leca,
    Pon,
    Perlite,
    Sphagnum,
    Water,

    /// <summary>Held above water rather than in it, e.g. a corm on a riser in a sealed box.</summary>
    CormRiser,

    Other
}

/// <summary>
/// Something done to a propagation. Its own enum rather than <see cref="CareKind"/>, which isn't
/// tolerant of values it doesn't know, and a propagation in water needs ChangedWater.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PropagationCareKind>))]
public enum PropagationCareKind
{
    Watered,
    ChangedWater,

    /// <summary>A reading off the moisture meter, 0 to 10.</summary>
    MoistureReading
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

/// <summary>The language of the everyday plant names the app shows, like "Snake plant" or "Svigermors tunge".</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EverydayNameLanguage>))]
public enum EverydayNameLanguage
{
    English,
    Danish
}

/// <summary>How someone checks whether a plant needs water on the Water plants screen.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WaterCheck>))]
public enum WaterCheck
{
    /// <summary>With a moisture meter, reading 0 to 10.</summary>
    Meter,

    /// <summary>By pushing a finger into the soil: dry, or still damp.</summary>
    Soil
}

/// <summary>Why a stretch of days is set aside: away from home, or a break from the app.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AwayKind>))]
public enum AwayKind
{
    /// <summary>Gone from home, with someone looking after the plants or not.</summary>
    Away,

    /// <summary>Home, but Today shouldn't ask for anything.</summary>
    Quiet
}

/// <summary>How a plant is watered, for whoever looks after it while its owner is away.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WaterMethod>))]
public enum WaterMethod
{
    /// <summary>Water in the saucer or outer pot, taken up from below.</summary>
    Bottom,

    /// <summary>Poured onto the soil.</summary>
    Top,

    /// <summary>The pot stands in water for a while.</summary>
    Soak
}

/// <summary>How the plant-sitter sheet is laid out.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SheetLayout>))]
public enum SheetLayout
{
    Labels,
    Visits,
    Chart
}

/// <summary>How often the plant-sitter comes by.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisitPattern>))]
public enum VisitPattern
{
    None,
    EveryDay,
    EveryTwoDays,
    EveryThreeDays,
    TwiceAWeek,
    Weekly
}
