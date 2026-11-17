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
    /// The directions of the windows the place gets light from. Null or empty when none is written
    /// down, and a spot then uses its room's. Names this version doesn't know are dropped on reading.
    /// </summary>
    [JsonConverter(typeof(TolerantEnumListConverter<WindowDirection>))]
    public List<WindowDirection>? Windows { get; set; }

    /// <summary>How much sun the place gets, from the light check. Null until answered.</summary>
    [JsonConverter(typeof(TolerantNullableEnumConverter<SunHours>))]
    public SunHours? SunHours { get; set; }

    /// <summary>How far from the window it is, from the light check. Null until answered.</summary>
    [JsonConverter(typeof(TolerantNullableEnumConverter<WindowDistance>))]
    public WindowDistance? FromWindow { get; set; }

    /// <summary>How much is in the way of the light, from the light check. Null until answered.</summary>
    [JsonConverter(typeof(TolerantNullableEnumConverter<Shade>))]
    public Shade? InTheWay { get; set; }

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

/// <summary>The side a window faces. Roof is a skylight or a roof window.</summary>
public enum WindowDirection
{
    North,
    East,
    South,
    West,
    Roof
}

/// <summary>Hours of direct sun a place gets, as one of the light check's answers.</summary>
public enum SunHours
{
    None,
    UnderTwo,
    TwoToFour,
    OverFour
}

/// <summary>How far a place is from its window, as one of the light check's answers.</summary>
public enum WindowDistance
{
    AtWindow,
    UnderOneMetre,
    OneToTwoMetres,
    OverTwoMetres
}

/// <summary>How much stands between the window and the place, as one of the light check's answers.</summary>
public enum Shade
{
    Nothing,
    Some,
    Lots
}
