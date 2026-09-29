using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

public sealed class Plant : Entity
{
    /// <summary>Your own name for the plant, e.g. "Big Monstera" or "Kitchen basil".</summary>
    public string? Nickname { get; set; }

    public string? Genus { get; set; }
    public string? Species { get; set; }

    /// <summary>Named variety, e.g. "Thai Constellation".</summary>
    public string? Cultivar { get; set; }

    /// <summary>The room or spot it stands in. See <see cref="Place"/>.</summary>
    public Guid? PlaceId { get; set; }

    /// <summary>How much light it gets where it stands. Null when not set.</summary>
    public LightLevel? Light { get; set; }

    public PlantOrigin Origin { get; set; } = PlantOrigin.Purchased;

    /// <summary>When it was got, to whatever precision is remembered: a day, a month or a year.</summary>
    public LooseDate? AcquiredOn { get; set; }

    /// <summary>Where it came from: a shop, a friend, a swap event...</summary>
    public string? Source { get; set; }

    public PlantStatus Status { get; set; } = PlantStatus.Active;

    /// <summary>An optional note on what went wrong, kept while the status is Died and cleared if it comes back.</summary>
    public string? CauseOfDeath { get; set; }

    public GrowingMedium Medium { get; set; } = GrowingMedium.Soil;

    /// <summary>The pot the roots are in.</summary>
    public Guid? InnerPotId { get; set; }

    /// <summary>What it stands in, when there is one.</summary>
    public Guid? OuterPotId { get; set; }

    /// <summary>
    /// Water kept in the outer pot, wicking up through the medium. An ordinary ceramic run as a
    /// self-watering pot, which is a different thing from a pot built to water itself.
    /// </summary>
    public bool WaterInOuterPot { get; set; }

    /// <summary>
    /// Which pots went with it when it was given away or sold. The pot ids stay on the plant, so
    /// its history still says what it lived in; this says whether they left the house with it.
    /// </summary>
    public PotsTaken PotsTaken { get; set; } = PotsTaken.None;

    /// <summary>The soil mix it was potted in, when it is one you have saved.</summary>
    public Guid? SoilMixId { get; set; }

    public string? Notes { get; set; }

    /// <summary>Your own labels, e.g. "variegated", "rare" or "for swap". See <see cref="Plants.PlantTags"/>.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// The day it went into quarantine, kept apart from the other plants. Null when it isn't in
    /// quarantine, so there is no flag that can disagree with the date.
    /// </summary>
    public DateOnly? QuarantinedSince { get; set; }

    [JsonIgnore]
    public bool InQuarantine => QuarantinedSince is not null;

    /// <summary>How many days the quarantine lasts. Null when it isn't in quarantine.</summary>
    public int? QuarantineDays { get; set; }

    /// <summary>The length given to a quarantine when it starts.</summary>
    public const int DefaultQuarantineDays = 14;

    /// <summary>The day the quarantine is up, or null when it isn't in quarantine.</summary>
    [JsonIgnore]
    public DateOnly? QuarantineEnds =>
        QuarantinedSince?.AddDays(QuarantineDays ?? DefaultQuarantineDays);

    /// <summary>
    /// The day it went dormant, e.g. an alocasia that dropped its leaves for winter. Null while
    /// it's growing, the same way as quarantine.
    /// </summary>
    public DateOnly? DormantSince { get; set; }

    [JsonIgnore]
    public bool IsDormant => DormantSince is not null;

    /// <summary>Pinned to the top of the plant list.</summary>
    public bool Favourite { get; set; }

    /// <summary>Something it needs doing, which keeps it on Today until cleared. Null when nothing is.</summary>
    public Attention? Attention { get; set; }

    /// <summary>Given away or sold: gone to someone else, and possibly with its pots.</summary>
    [JsonIgnore]
    public bool HasLeft => Status is PlantStatus.GivenAway or PlantStatus.Sold;

    public Guid? CoverPhotoId { get; set; }

    /// <summary>The plant this one was propagated from, if known.</summary>
    public Guid? ParentPlantId { get; set; }

    /// <summary>The propagation this plant was promoted from, if any.</summary>
    public Guid? FromPropagationId { get; set; }

    /// <summary>"Monstera deliciosa 'Thai Constellation'", or null when no botanical name is set.</summary>
    [JsonIgnore]
    public string? BotanicalName => PlantNames.Botanical(Genus, Species, Cultivar);

    /// <summary>The name to show in lists: the nickname, else the botanical name.</summary>
    [JsonIgnore]
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Nickname) ? Nickname.Trim() : BotanicalName ?? "Unnamed plant";

    /// <summary>A copy to compare with after editing. The tags get their own list.</summary>
    public Plant Copy()
    {
        var copy = (Plant)MemberwiseClone();
        copy.Tags = [.. Tags];
        return copy;
    }

    /// <summary>
    /// A new plant to add alongside this one, e.g. a second basil pot. It gets the names, room,
    /// light, origin, source, medium, soil mix and tags. The nickname, the date it was got, pots, notes,
    /// photos and anything about how this plant is doing right now stay behind.
    /// </summary>
    public Plant Duplicate() => new()
    {
        Genus = Genus,
        Species = Species,
        Cultivar = Cultivar,
        PlaceId = PlaceId,
        Light = Light,
        Origin = Origin,
        Source = Source,
        Medium = Medium,
        SoilMixId = SoilMixId,
        Tags = [.. Tags],
        ParentPlantId = ParentPlantId
    };

    /// <summary>
    /// True while the plant takes up this pot. Only a plant in the collection does: a pot whose
    /// plant died or left is either free again or went with it.
    /// </summary>
    public bool Uses(Guid potId) =>
        Status == PlantStatus.Active && (InnerPotId == potId || OuterPotId == potId);

    /// <summary>True when this pot left the house with the plant.</summary>
    public bool TookAway(Guid potId) => HasLeft && PotsTaken switch
    {
        PotsTaken.Inner => InnerPotId == potId,
        PotsTaken.All => InnerPotId == potId || OuterPotId == potId,
        _ => false
    };

    /// <summary>
    /// The choice as far as this plant's pots allow, so several plants given away together can
    /// share one answer: a plant without an outer pot can't take one, and one without pots takes none.
    /// </summary>
    public PotsTaken Fit(PotsTaken choice) => choice switch
    {
        PotsTaken.All when OuterPotId is not null => PotsTaken.All,
        PotsTaken.All or PotsTaken.Inner when InnerPotId is not null => PotsTaken.Inner,
        _ => PotsTaken.None
    };

    /// <summary>Days in quarantine, counting the day it went in as day 0.</summary>
    public int? DaysInQuarantine(DateOnly today) =>
        QuarantinedSince is { } since ? Math.Max(0, today.DayNumber - since.DayNumber) : null;

    /// <summary>Days dormant, counting the day it went dormant as day 0.</summary>
    public int? DaysDormant(DateOnly today) =>
        DormantSince is { } since ? Math.Max(0, today.DayNumber - since.DayNumber) : null;

    /// <summary>Validation rules shared by every place a plant can be saved from.</summary>
    public IReadOnlyList<string> Validate(DateOnly today)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Nickname) && string.IsNullOrWhiteSpace(Genus))
            errors.Add("Give the plant a nickname or a genus.");
        if (ParentPlantId is not null && ParentPlantId == Id)
            errors.Add("A plant can't be its own parent.");
        if (AcquiredOn is { } acquired && acquired.Start > today)
            errors.Add("The date you got it can't be in the future.");
        if (InnerPotId is not null && InnerPotId == OuterPotId)
            errors.Add("A plant can't have the same pot inside and outside.");
        if (QuarantinedSince > today)
            errors.Add("The quarantine can't start in the future.");
        if (QuarantineDays < 1)
            errors.Add("A quarantine has to last at least a day.");
        if (DormantSince > today)
            errors.Add("It can't go dormant in the future.");
        if (PotsTaken != PotsTaken.None && !HasLeft)
            errors.Add("Only a plant that was given away or sold can take its pots with it.");
        return errors;
    }
}
