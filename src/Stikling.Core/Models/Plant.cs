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

    public string? Location { get; set; }

    public PlantOrigin Origin { get; set; } = PlantOrigin.Purchased;

    /// <summary>When it was got, to whatever precision is remembered: a day, a month or a year.</summary>
    public LooseDate? AcquiredOn { get; set; }

    /// <summary>Where it came from: a shop, a friend, a swap event...</summary>
    public string? Source { get; set; }

    public PlantStatus Status { get; set; } = PlantStatus.Active;

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

    /// <summary>Days in quarantine, counting the day it went in as day 0.</summary>
    public int? DaysInQuarantine(DateOnly today) =>
        QuarantinedSince is { } since ? Math.Max(0, today.DayNumber - since.DayNumber) : null;

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
        return errors;
    }
}
