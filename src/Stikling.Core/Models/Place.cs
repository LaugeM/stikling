using Strings = Stikling.Core.Resources;
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

    /// <summary>
    /// Outside, like a balcony or a garden. A spot in an outdoor room counts as outdoors too.
    /// Moving a plant from outdoors to indoors offers a quarantine.
    /// </summary>
    public bool Outdoors { get; set; }

    /// <summary>
    /// One text about the place, like which window it is by or a draught in winter. It is edited as
    /// a whole, so the newest version wins when two devices change it. Null when there is none, and
    /// null on records from before it existed.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// The day of a move house this room, or spot, was left behind on. Set on a room and on its spots
    /// by the moving flow. The room stays as it was, with its history and photos, but isn't offered
    /// for new things any more. Null for a place in use, and on records from before it existed.
    /// </summary>
    public DateOnly? LeftOn { get; set; }

    /// <summary>
    /// The order the plants and propagations in a room are visited in when going round it, as their
    /// ids. Only on a room, never on a spot. Ids that are gone are skipped, and what isn't in the list
    /// follows its neighbours. Null for the usual order, and on records from before it existed.
    /// </summary>
    public List<Guid>? RoundOrder { get; set; }

    [JsonIgnore]
    public bool IsSpot => RoomId is not null;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add(IsSpot ? Strings.Shared.PlaceNeedsNameSpot : Strings.Shared.PlaceNeedsNameRoom);
        // A spot can have one, since the first / is the one that splits a place in two
        else if (!IsSpot && Name.Contains('/'))
            errors.Add(Strings.Shared.PlaceNameSlash);

        return errors;
    }
}
