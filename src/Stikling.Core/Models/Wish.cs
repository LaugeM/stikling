namespace Stikling.Core.Models;

/// <summary>
/// A plant you want to get. It is kept after it is got, with the day and the plant or propagation
/// it became, so the wishlist also says what came of it.
/// </summary>
public sealed class Wish : Entity
{
    public string? Genus { get; set; }
    public string? Species { get; set; }
    public string? Cultivar { get; set; }

    /// <summary>What to call it when there is no botanical name yet, e.g. "that pink syngonium".</summary>
    public string? Name { get; set; }

    /// <summary>Where to find it, in the person's own words.</summary>
    public string? FoundAt { get; set; }

    /// <summary>What you liked about it, or what to look for.</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// The most you would pay, as a number with no currency, the same way as <see cref="Plant.PricePaid"/>.
    /// Null when not entered.
    /// </summary>
    public decimal? MostToPay { get; set; }

    /// <summary>The day it was got. Null while it is still a wish.</summary>
    public DateOnly? GotOn { get; set; }

    /// <summary>The plant it became, once got as a plant.</summary>
    public Guid? PlantId { get; set; }

    /// <summary>The propagation it became, once got as a cutting.</summary>
    public Guid? PropagationId { get; set; }

    public bool IsGot => GotOn is not null;

    /// <summary>"Monstera deliciosa 'Thai Constellation'", or null when no botanical name is set.</summary>
    public string? BotanicalName => PlantNames.Botanical(Genus, Species, Cultivar);

    /// <summary>The botanical name, else the name typed in.</summary>
    public string DisplayName =>
        BotanicalName ?? (string.IsNullOrWhiteSpace(Name) ? "Unnamed wish" : Name.Trim());

    public IReadOnlyList<string> Validate(DateOnly today)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Genus) && string.IsNullOrWhiteSpace(Name))
            errors.Add("Give the wish a genus, or a name of your own.");
        if (MostToPay < 0)
            errors.Add("The most you would pay can't be negative.");
        return errors;
    }
}
