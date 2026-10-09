namespace Stikling.Core.Care;

public enum Hemisphere
{
    Northern,
    Southern
}

/// <summary>
/// The time of year, as far as watering goes: whether the days are getting shorter, and which
/// part of the year two dates share. Only a guess at the hemisphere is needed, from the time zone.
/// </summary>
public static class Seasons
{
    // Zones in the south that don't use summer time, so they can't be told apart by it
    private static readonly string[] SouthernZones =
    [
        "Australia/", "Antarctica/", "Pacific/Auckland", "Pacific/Chatham", "Pacific/Fiji",
        "America/Argentina", "America/Buenos_Aires", "America/Sao_Paulo", "America/Santiago",
        "America/Montevideo", "America/Asuncion", "America/Lima", "America/La_Paz",
        "Africa/Johannesburg", "Africa/Maputo", "Africa/Harare", "Africa/Windhoek",
        "Indian/Mauritius", "Indian/Reunion", "Indian/Antananarivo"
    ];

    /// <summary>
    /// Southern when the zone is on summer time in mid January, or is one of the southern zones
    /// without summer time. Northern otherwise, which includes the zones near the equator.
    /// </summary>
    /// <param name="year">The year to look at in January. This year unless given, for tests.</param>
    public static Hemisphere HemisphereOf(TimeZoneInfo zone, int? year = null)
    {
        if (SouthernZones.Any(prefix => zone.Id.StartsWith(prefix, StringComparison.Ordinal)))
            return Hemisphere.Southern;

        var january = new DateTime(year ?? DateTime.UtcNow.Year, 1, 15, 12, 0, 0, DateTimeKind.Unspecified);
        return zone.SupportsDaylightSavingTime && zone.IsDaylightSavingTime(january)
            ? Hemisphere.Southern
            : Hemisphere.Northern;
    }

    /// <summary>
    /// True while the days are getting shorter: from 21 June up to 21 December in the north, and
    /// the other half of the year in the south.
    /// </summary>
    public static bool DaysShortening(DateOnly day, Hemisphere hemisphere)
    {
        var key = day.Month * 100 + day.Day;
        var northShortening = key >= 621 && key < 1221;
        return hemisphere == Hemisphere.Northern ? northShortening : !northShortening;
    }

    /// <summary>True when two dates fall within some days of each other in the year, wrapping round new year.</summary>
    public static bool SameTimeOfYear(DateOnly a, DateOnly b, int withinDays = 30)
    {
        var apart = Math.Abs(a.DayOfYear - b.DayOfYear);
        return Math.Min(apart, 365 - apart) <= withinDays;
    }
}
