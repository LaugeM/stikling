using Stikling.Core.Care;
using Stikling.Core.Lights;
using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Today;

/// <summary>
/// The note on Today before the short days: plants that like bright light and have no grow light. It shows from 15 October
/// to the end of February in the northern hemisphere and from 15 April to 31 August in the southern. It only lists plants,
/// and nothing about it changes one.
/// </summary>
public static class WinterLight
{
    /// <summary>The first and last day of the season the day is in, or null outside it.</summary>
    public static (DateOnly Start, DateOnly End)? SeasonOf(DateOnly day, Hemisphere hemisphere)
    {
        if (hemisphere == Hemisphere.Southern)
        {
            var start = new DateOnly(day.Year, 4, 15);
            var end = new DateOnly(day.Year, 8, 31);
            return day >= start && day <= end ? (start, end) : null;
        }

        // The season runs over New Year, and starts the year before for a day in January or February
        var from = day.Month <= 2 ? day.Year - 1 : day.Year;
        var first = new DateOnly(from, 10, 15);
        var last = new DateOnly(from + 1, 3, 1).AddDays(-1);
        return day >= first && day <= last ? (first, last) : null;
    }

    /// <summary>The key of the put-off for the season, with the year it started in it.</summary>
    public static string Key(DateOnly seasonStart) => $"winter-light:{seasonStart.Year}";

    /// <summary>The day the note comes back after being put off: the day after the season ends.</summary>
    public static DateOnly PutOffUntil((DateOnly Start, DateOnly End) season) => season.End.AddDays(1);

    /// <summary>
    /// The plants to list: in the collection, liking bright indirect light or direct sun, not under a grow light and not
    /// outdoors. Empty outside the season or once put off.
    /// </summary>
    public static IReadOnlyList<Plant> Plants(
        IEnumerable<Plant> plants, IEnumerable<GrowLight> lights, Places places, DateOnly day, Hemisphere hemisphere, PutOffs putOffs)
    {
        if (SeasonOf(day, hemisphere) is not { } season || putOffs.IsPutOff(Key(season.Start), day))
            return [];

        var lamps = lights as IReadOnlyList<GrowLight> ?? [.. lights];
        return [.. plants
            .Where(p => !p.IsDeleted && p.InCollection
                && p.LikesLight is LightLevel.BrightIndirect or LightLevel.DirectSun
                && !GrowLights.IsLit(p, lamps, places)
                && !places.IsOutdoors(p.PlaceId))
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
    }
}
