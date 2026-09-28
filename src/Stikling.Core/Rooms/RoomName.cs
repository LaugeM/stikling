namespace Stikling.Core.Rooms;

/// <summary>
/// A place written as one line of text: a room on its own ("Living room"), or a spot inside a
/// room ("Living room / On top of the PC"). Places are stored as <see cref="Models.Place"/>
/// records; this is how they're typed in and shown.
/// </summary>
public static class RoomName
{
    /// <summary>What separates the room from the spot when a place is written out.</summary>
    public const string Separator = " / ";

    /// <summary>Tidies a typed place, or null when nothing worth keeping was typed.</summary>
    public static string? Clean(string? place)
    {
        if (string.IsNullOrWhiteSpace(place))
            return null;

        var parts = place.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : string.Join(Separator, parts);
    }

    /// <summary>Writes a room and a spot as one place. Either may be empty.</summary>
    public static string? Combine(string? room, string? spot) =>
        string.IsNullOrWhiteSpace(spot) ? Clean(room) : Clean($"{room}/{spot}");

    /// <summary>The room and the spot inside it, both null when there is no place.</summary>
    public static (string? Room, string? Spot) Split(string? place)
    {
        if (Clean(place) is not { } cleaned)
            return (null, null);

        // Anything past the first separator is the spot, so a deeper name survives untouched
        var cut = cleaned.IndexOf('/');
        return cut < 0 ? (cleaned, null) : (cleaned[..cut].TrimEnd(), cleaned[(cut + 1)..].TrimStart());
    }

    /// <summary>"Living room" for "Living room / On top of the PC".</summary>
    public static string? RoomOf(string? place) => Split(place).Room;

    /// <summary>"On top of the PC" for "Living room / On top of the PC", else null.</summary>
    public static string? SpotOf(string? place) => Split(place).Spot;

    /// <summary>Two places are the same when they only differ in case or spacing.</summary>
    public static bool Same(string? a, string? b) =>
        string.Equals(Clean(a), Clean(b), StringComparison.OrdinalIgnoreCase);
}
