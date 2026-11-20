using System.Text.Json.Serialization;
using Stikling.Core.Crosses;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>How the pollen got onto the flower.</summary>
public enum CrossMethod
{
    Brush,
    ByHand,
    Other
}

/// <summary>What came of a cross. A cross with no outcome yet is not known.</summary>
public enum CrossOutcome
{
    Took,
    DidNotTake
}

/// <summary>What an amount of seed is counted in.</summary>
public enum SeedUnit
{
    Seeds,
    Grams
}

/// <summary>
/// A pollination: a pod parent, a pollen parent, a day and how it was done, and later what came of it.
/// Each parent is a plant in the app or a name typed in, and a plant's name is kept beside its id so
/// the cross still reads when that plant is deleted on another device. The same plant on both sides
/// is a self. What the seedlings are is not stored here: they are the propagations sown from the
/// cross's seed lots, so nothing is written twice.
/// </summary>
public sealed class Cross : Entity
{
    public const int MaxCount = 100000;

    /// <summary>The working name, used on the seedlings until they have a name of their own.</summary>
    public string? Name { get; set; }

    /// <summary>The plant the pod grew on. Null when the parent isn't in the app.</summary>
    public Guid? PodParentPlantId { get; set; }

    /// <summary>The pod parent as text: what was typed, or the plant's name kept beside its id.</summary>
    public string? PodParentName { get; set; }

    public Guid? PollenParentPlantId { get; set; }

    public string? PollenParentName { get; set; }

    /// <summary>The day the flower was pollinated.</summary>
    public DateOnly PollinatedOn { get; set; }

    [JsonConverter(typeof(TolerantNullableEnumConverter<CrossMethod>))]
    public CrossMethod? Method { get; set; }

    /// <summary>The pollen had been stored. False is fresh pollen.</summary>
    public bool PollenIsStored { get; set; }

    /// <summary>The stored pollen it came from, when it is one that is kept in the app.</summary>
    public Guid? PollenId { get; set; }

    /// <summary>The flower was bagged. Null when it isn't said.</summary>
    public bool? Bagged { get; set; }

    public int? FlowersPollinated { get; set; }

    public string? Note { get; set; }

    /// <summary>Null while it isn't known.</summary>
    [JsonConverter(typeof(TolerantNullableEnumConverter<CrossOutcome>))]
    public CrossOutcome? Outcome { get; set; }

    /// <summary>How many pods set.</summary>
    public int? PodsSet { get; set; }

    public DateOnly? SeedRipeOn { get; set; }

    /// <summary>How much seed there was. See <see cref="SeedAmounts"/>.</summary>
    public decimal? SeedAmount { get; set; }

    [JsonConverter(typeof(TolerantNullableEnumConverter<SeedUnit>))]
    public SeedUnit? SeedAmountUnit { get; set; }

    public bool SeedAmountIsAbout { get; set; }

    /// <summary>The same plant on both sides, or the same name typed on both when no plant is picked.</summary>
    [JsonIgnore]
    public bool IsSelf =>
        PodParentPlantId is { } pod && pod == PollenParentPlantId
        || PodParentPlantId is null && PollenParentPlantId is null
            && !string.IsNullOrWhiteSpace(PodParentName)
            && string.Equals(PodParentName.Trim(), PollenParentName?.Trim(), StringComparison.CurrentCultureIgnoreCase);

    /// <summary>True when the seed is ripe or an amount of it is written down, so a lot can be made.</summary>
    [JsonIgnore]
    public bool HasSeed => SeedRipeOn is not null || SeedAmount is > 0;

    public Cross Copy() => (Cross)MemberwiseClone();

    public IReadOnlyList<string> Validate(DateOnly today)
    {
        var errors = new List<string>();
        if (PodParentPlantId is null && string.IsNullOrWhiteSpace(PodParentName))
            errors.Add(Strings.Crosses.ErrNeedPodParent);
        if (PollenParentPlantId is null && string.IsNullOrWhiteSpace(PollenParentName))
            errors.Add(Strings.Crosses.ErrNeedPollenParent);
        if (PollinatedOn > today)
            errors.Add(Strings.Crosses.ErrFuture);
        if (SeedRipeOn is { } ripe && ripe < PollinatedOn)
            errors.Add(Strings.Crosses.ErrRipeBeforePollinated);
        if (FlowersPollinated is < 1 or > MaxCount || PodsSet is < 0 or > MaxCount)
            errors.Add(Strings.Crosses.ErrCount);
        errors.AddRange(SeedAmounts.Errors(SeedAmount, SeedAmountUnit));
        return errors;
    }
}
