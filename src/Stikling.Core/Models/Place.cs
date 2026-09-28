using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>
/// A room, or a spot inside one, like "On top of the PC" in the living room. Plants,
/// propagations and pest cases point at a place by id, so renaming one changes a single record.
/// </summary>
public sealed class Place : Entity
{
    public string Name { get; set; } = "";

    /// <summary>For a spot, the room it's in. Null for a room.</summary>
    public Guid? RoomId { get; set; }

    /// <summary>
    /// Set when the place was merged into another one. It's deleted then, and whatever still points
    /// at it counts as being in the other place. That includes things placed here on another
    /// device before the merge reached it.
    /// </summary>
    public Guid? MergedIntoId { get; set; }

    [JsonIgnore]
    public bool IsSpot => RoomId is not null;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add(IsSpot ? "Give the spot a name." : "Give the room a name.");
        // A spot can have one, since the first / is the one that splits a place in two
        else if (!IsSpot && Name.Contains('/'))
            errors.Add("A room's name can't have a / in it, since that's what goes between a room and a spot.");

        return errors;
    }
}
