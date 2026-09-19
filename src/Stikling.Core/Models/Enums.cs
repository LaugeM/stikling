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
    Other
}
