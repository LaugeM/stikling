namespace Stikling.Core.Rooms;

/// <summary>
/// Where a plant or a propagation sits, written as one line of text: a room on its own
/// ("Living room"), or a spot inside a room ("Living room / On top of the PC"). Keeping both
/// in one field means a place is a single value to compare, rename and match against a pest
/// case, and a place that only names a room needs nothing special.
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

    /// <summary>True when the place is the given room, or a spot inside it.</summary>
    public static bool IsIn(string? place, string? room)
    {
        if (Clean(room) is not { } scope || Clean(place) is not { } actual)
            return false;

        return actual.Equals(scope, StringComparison.OrdinalIgnoreCase)
            || actual.StartsWith(scope + Separator, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The place with <paramref name="from"/> renamed to <paramref name="to"/>, keeping whatever
    /// sits inside it: renaming "Stue" to "Living room" turns "Stue / Windowsill" into
    /// "Living room / Windowsill". Places outside the rename are left alone.
    /// </summary>
    public static string? Rename(string? place, string? from, string? to)
    {
        if (!IsIn(place, from) || Clean(to) is not { } target)
            return Clean(place);

        var inside = Clean(place)![Clean(from)!.Length..];
        return Clean(target + inside);
    }
}
