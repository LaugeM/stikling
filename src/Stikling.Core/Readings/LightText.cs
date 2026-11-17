using Stikling.Core.Models;
using static Stikling.Core.Text.Format;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Readings;

/// <summary>Words for windows and for the light check.</summary>
public static class LightText
{
    public static string Direction(WindowDirection direction) => direction switch
    {
        WindowDirection.North => Strings.Light.WindowNorth,
        WindowDirection.East => Strings.Light.WindowEast,
        WindowDirection.South => Strings.Light.WindowSouth,
        WindowDirection.West => Strings.Light.WindowWest,
        _ => Strings.Light.WindowRoof
    };

    /// <summary>"South and west windows", in the order north, east, south, west, roof. Empty with none.</summary>
    public static string Windows(IEnumerable<WindowDirection> windows)
    {
        var names = windows.Distinct().Order().Select(Direction).ToList();
        if (names.Count == 0)
            return "";

        var lower = names.Select(n => n.ToLower(System.Globalization.CultureInfo.CurrentUICulture)).ToList();
        string Join(List<string> list) => list.Count == 1 ? list[0]
            : F(Strings.Light.ListAnd, string.Join(", ", list.Take(list.Count - 1)), list[^1]);

        var list = Join(lower);
        // English starts the line with a capital letter ("South and west windows"), Danish with the word for windows
        if (Strings.Light.WindowsCapital == "1")
            list = char.ToUpper(list[0], System.Globalization.CultureInfo.CurrentUICulture) + list[1..];
        return F(names.Count, Strings.Light.WindowsList_One, Strings.Light.WindowsList_Other, list);
    }

    /// <summary>The light level as a phrase inside a sentence, like "bright indirect light".</summary>
    public static string Level(LightLevel level) => level switch
    {
        LightLevel.Low => Strings.Light.LevelLow,
        LightLevel.Medium => Strings.Light.LevelMedium,
        LightLevel.BrightIndirect => Strings.Light.LevelBrightIndirect,
        _ => Strings.Light.LevelDirectSun
    };

    /// <summary>
    /// A place's own windows, or its room's when it has none. FromRoom says which. Empty when neither has any.
    /// </summary>
    public static (IReadOnlyList<WindowDirection> Windows, bool FromRoom) WindowsOf(Place place, Place? room) =>
        place.Windows is { Count: > 0 } own ? (own, false)
        : place.IsSpot && room?.Windows is { Count: > 0 } theirs ? (theirs, true)
        : ([], false);

    /// <summary>"South and west windows" for the place a plant stands in, from its own windows or its room's. Empty when none.</summary>
    public static string WindowsLine(Rooms.Places places, Guid? placeId) =>
        places.Find(placeId) is { } place
            ? Windows(WindowsOf(place, place.IsSpot ? places.Find(place.RoomId) : null).Windows)
            : "";

    /// <summary>A place's light check guess, or its room's when it has no answers of its own.</summary>
    public static (LightLevel? Level, bool FromRoom) CheckOf(Place place, Place? room)
    {
        if (LightCheck.Guess(place) is { } own)
            return (own, false);
        if (place.IsSpot && room is not null && LightCheck.Guess(room) is { } theirs)
            return (theirs, true);
        return (null, false);
    }
}
