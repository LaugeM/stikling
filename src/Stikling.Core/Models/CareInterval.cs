using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>The unit of "every 2 weeks". Stored as text like the other enums.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<IntervalUnit>))]
public enum IntervalUnit
{
    Days,
    Weeks,
    Months,
    Years
}

/// <summary>Which plants an interval covers. Everywhere, Room and Tag are rules worked out from the plants as they are now.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CareScope>))]
public enum CareScope
{
    /// <summary>Every plant in the collection.</summary>
    Everywhere,

    /// <summary>One room, including the spots inside it.</summary>
    Room,

    /// <summary>The plants with one tag.</summary>
    Tag,

    /// <summary>A hand-picked set of plants.</summary>
    PickedPlants
}

/// <summary>
/// Care that comes back by itself, like "fertilise every 2 weeks from April to October" for the
/// plants with a tag. Today lists what is due, and logging it writes ordinary care entries. It
/// never covers watering, which the watering reminder learns from each plant.
/// </summary>
public sealed class CareInterval : Entity
{
    /// <summary>What it is for. For an own kind this is what the kind counts as: Fertilised, or Rotated for neither.</summary>
    public CareKind Kind { get; set; } = CareKind.Fertilised;

    /// <summary>The own kind of care it is for, when it is one.</summary>
    public Guid? OwnKindId { get; set; }

    /// <summary>The saved feed to fertilise with. Only when the interval is for a feed.</summary>
    public Guid? FeedId { get; set; }

    public int Every { get; set; } = 2;

    public IntervalUnit Unit { get; set; } = IntervalUnit.Weeks;

    /// <summary>The first month of the year it applies, 1 to 12. With <see cref="ToMonth"/>, or neither.</summary>
    public int? FromMonth { get; set; }

    /// <summary>The last month it applies. Earlier than <see cref="FromMonth"/> means it wraps over the new year.</summary>
    public int? ToMonth { get; set; }

    public CareScope Scope { get; set; } = CareScope.Everywhere;

    /// <summary>The room or spot. Only on <see cref="CareScope.Room"/>.</summary>
    public Guid? PlaceId { get; set; }

    /// <summary>The tag. Only on <see cref="CareScope.Tag"/>.</summary>
    public string? Tag { get; set; }

    /// <summary>The plants. Only on <see cref="CareScope.PickedPlants"/>.</summary>
    public List<Guid> PlantIds { get; set; } = [];

    /// <summary>The day it was set. A plant with nothing logged counts from here.</summary>
    public DateOnly StartsOn { get; set; }

    public string? Notes { get; set; }

    /// <summary>Whether this is for a feed, so it can name one and stops while a plant is dormant.</summary>
    [JsonIgnore]
    public bool IsFeed => Kind == CareKind.Fertilised;

    /// <summary>Whether it only applies to part of the year.</summary>
    [JsonIgnore]
    public bool HasMonths => FromMonth is not null && ToMonth is not null;

    /// <summary>The kinds an interval can be set for without an own kind. Watering is never one of them.</summary>
    public static readonly IReadOnlyList<CareKind> AllowedKinds =
    [
        CareKind.Fertilised, CareKind.Flushed, CareKind.Repotted, CareKind.Pruned,
        CareKind.Rotated, CareKind.LeavesCleaned, CareKind.Harvested
    ];

    public CareInterval Copy()
    {
        var copy = (CareInterval)MemberwiseClone();
        copy.PlantIds = [.. PlantIds];
        return copy;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (OwnKindId is null)
        {
            if (!AllowedKinds.Contains(Kind))
                errors.Add("Watering stays with the watering reminder, so pick another kind of care.");
        }
        else if (Kind is not (CareKind.Fertilised or CareKind.Rotated))
        {
            errors.Add("Watering stays with the watering reminder, so pick another kind of care.");
        }

        if (FeedId is not null && Kind != CareKind.Fertilised)
            errors.Add("Only a feeding can have a feed.");

        if (Every < 1)
            errors.Add("Repeat at least every 1, so the number has to be 1 or more.");

        if ((FromMonth is null) != (ToMonth is null))
            errors.Add("Pick both the first and the last month, or neither.");
        else if (FromMonth is < 1 or > 12 || ToMonth is < 1 or > 12)
            errors.Add("The months go from 1 to 12.");

        if (Scope == CareScope.Room && PlaceId is null)
            errors.Add("Pick the room it covers.");

        if (Scope == CareScope.Tag && string.IsNullOrWhiteSpace(Tag))
            errors.Add("Pick the tag it covers.");

        if (Scope == CareScope.PickedPlants && PlantIds.Count == 0)
            errors.Add("Pick at least one plant.");

        return errors;
    }
}
