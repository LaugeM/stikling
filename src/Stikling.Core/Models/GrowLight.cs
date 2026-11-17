using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>
/// A grow light, standing for the plants in the room or spot it lights. For now it only says
/// that the place is lit, which the watering reminder uses to leave the seasons out of its guess.
/// The grow lights feature (a name, the wattage, the hours on per day) adds those as new fields
/// here later, so nothing stored now has to change.
/// </summary>
public sealed class GrowLight : Entity
{
    public string Name { get; set; } = "";

    /// <summary>
    /// The room or spot it lights. A light on a room lights the spots inside it too. Null while
    /// it isn't pointed at anything.
    /// </summary>
    public Guid? PlaceId { get; set; }

    public string? Notes { get; set; }

    /// <summary>The power of the lamp in watts, 1 to 5000. Null when it isn't written down, and on lamps from before it existed.</summary>
    public int? Watts { get; set; }

    /// <summary>Hours the lamp is on each day, 0 to 24 in halves. Null when it isn't written down.</summary>
    public decimal? HoursPerDay { get; set; }

    public const int MaxWatts = 5000;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name))
            errors.Add(Strings.Care.GrowLightNeedsName);
        if (Watts is < 1 or > MaxWatts)
            errors.Add(Strings.Light.WattsRange);
        if (HoursPerDay is < 0 or > 24 || HoursPerDay is { } h && h * 2 != decimal.Truncate(h * 2))
            errors.Add(Strings.Light.HoursRange);
        return errors;
    }
}
