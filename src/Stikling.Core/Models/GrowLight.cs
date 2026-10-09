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

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Give the grow light a name.");
        return errors;
    }
}
