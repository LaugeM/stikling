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

    /// <summary>Free text for the pot/container, e.g. "Lechuza self-watering pot".</summary>
    public string? Container { get; set; }

    public string? Notes { get; set; }

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

    /// <summary>A shallow copy, e.g. to compare with after editing.</summary>
    public Plant Copy() => (Plant)MemberwiseClone();

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
        return errors;
    }
}
