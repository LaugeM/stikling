using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Readings;

/// <summary>What a reading measures. Never stored.</summary>
public enum ReadingMeasure
{
    Light,
    Temperature,
    Humidity
}

/// <summary>A measure's newest reading, and whether it was read in the room around a spot.</summary>
public sealed record ConditionValue(PlaceReading Reading, bool FromRoom);

/// <summary>The newest light, temperature and humidity at a place. Each is null when nothing was read.</summary>
public sealed record PlaceConditions(ConditionValue? Light, ConditionValue? Temperature, ConditionValue? Humidity)
{
    public bool Any => Light is not null || Temperature is not null || Humidity is not null;

    /// <summary>The newest day among the three, or null with none.</summary>
    public DateOnly? NewestDay
    {
        get
        {
            DateOnly? newest = null;
            foreach (var value in new[] { Light, Temperature, Humidity })
                if (value is not null && (newest is null || value.Reading.Day > newest))
                    newest = value.Reading.Day;
            return newest;
        }
    }
}

/// <summary>One point on a curve: the day, the value in stored units (lux, °C or percent) and the reading it came from.</summary>
public sealed record ReadingPoint(DateOnly Day, decimal Value, Guid ReadingId);

public sealed record ReadingCurve(ReadingMeasure Measure, IReadOnlyList<ReadingPoint> Points);

/// <summary>
/// Readings of light, temperature and humidity for rooms and spots. A reading belongs to the place
/// <see cref="Places.Find"/> gives for its place id, so a reading of a merged place counts as the other
/// place's, and a reading whose place was removed is kept in storage but left out everywhere.
/// </summary>
public sealed class PlaceReadingService(IPlaceReadingRepository readings, IPlaceRepository places)
{
    /// <summary>The fewest points a curve is drawn from.</summary>
    public const int CurveMinimum = 3;

    public async Task<PlaceReading?> GetAsync(Guid id) => await readings.GetAsync(id);

    /// <summary>The place's own readings, newest first.</summary>
    public async Task<IReadOnlyList<PlaceReading>> GetForPlaceAsync(Guid placeId)
    {
        var known = await places.GetPlacesAsync();
        return Newest(OwnReadings(known, await readings.GetAllAsync(), placeId));
    }

    /// <summary>The newest of each measure at the place, falling back to its room for a spot.</summary>
    public async Task<PlaceConditions> ConditionsForAsync(Guid placeId)
    {
        var known = await places.GetPlacesAsync();
        return Conditions(known, await readings.GetAllAsync(), placeId);
    }

    /// <summary>The curves that have enough points, in the order light, temperature, humidity.</summary>
    public async Task<IReadOnlyList<ReadingCurve>> CurvesAsync(Guid placeId)
    {
        var known = await places.GetPlacesAsync();
        return Curves(OwnReadings(known, await readings.GetAllAsync(), placeId));
    }

    /// <summary>The windows written on the place's readings, newest reading first.</summary>
    public async Task<IReadOnlyList<string>> WindowsUsedAsync(Guid placeId)
    {
        var known = await places.GetPlacesAsync();
        return WindowsUsed(OwnReadings(known, await readings.GetAllAsync(), placeId));
    }

    /// <summary>Tidies the text and the temperature, then saves. The repository checks the rest.</summary>
    public async Task SaveAsync(PlaceReading reading)
    {
        reading.Window = Tidy(reading.Window);
        reading.Notes = Tidy(reading.Notes);
        reading.TemperatureC = reading.TemperatureC is { } c ? Math.Round(c, 1, MidpointRounding.AwayFromZero) : null;
        await readings.SaveAsync(reading);
    }

    public Task DeleteAsync(Guid id) => readings.DeleteAsync(id);

    // ---- pure helpers ----

    /// <summary>The readings that belong to the place, in no order. None for a place that was removed.</summary>
    public static IEnumerable<PlaceReading> OwnReadings(Places places, IEnumerable<PlaceReading> all, Guid placeId)
    {
        var place = places.Find(placeId);
        if (place is null)
            return [];
        return all.Where(r => !r.IsDeleted && r.HasValue && places.Find(r.PlaceId)?.Id == place.Id);
    }

    /// <summary>Newest first: day, then time with none last, then when it was written down.</summary>
    public static IReadOnlyList<PlaceReading> Newest(IEnumerable<PlaceReading> readings) =>
        readings
            .OrderByDescending(r => r.Day)
            .ThenByDescending(r => r.Time.HasValue)
            .ThenByDescending(r => r.Time)
            .ThenByDescending(r => r.CreatedAt)
            .ToList();

    public static PlaceConditions Conditions(Places places, IEnumerable<PlaceReading> all, Guid placeId)
    {
        var list = all.ToList();
        var own = Newest(OwnReadings(places, list, placeId));
        var place = places.Find(placeId);

        IReadOnlyList<PlaceReading>? room = null;
        if (place is { IsSpot: true } && places.RoomOf(placeId) is { } roomPlace && roomPlace.Id != place.Id)
            room = Newest(OwnReadings(places, list, roomPlace.Id));

        ConditionValue? Pick(Func<PlaceReading, bool> has) =>
            own.FirstOrDefault(has) is { } mine ? new ConditionValue(mine, false)
            : room?.FirstOrDefault(has) is { } fromRoom ? new ConditionValue(fromRoom, true)
            : null;

        return new PlaceConditions(
            Pick(r => r.Lux is not null),
            Pick(r => r.TemperatureC is not null),
            Pick(r => r.HumidityPercent is not null));
    }

    public static IReadOnlyList<ReadingCurve> Curves(IEnumerable<PlaceReading> own)
    {
        var newest = Newest(own);
        var curves = new List<ReadingCurve>();
        AddCurve(curves, newest, ReadingMeasure.Light, r => r.Lux);
        AddCurve(curves, newest, ReadingMeasure.Temperature, r => r.TemperatureC);
        AddCurve(curves, newest, ReadingMeasure.Humidity, r => r.HumidityPercent);
        return curves;
    }

    public static IReadOnlyList<string> WindowsUsed(IEnumerable<PlaceReading> own)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var windows = new List<string>();
        foreach (var reading in Newest(own))
            if (reading.Window?.Trim() is { Length: > 0 } window && seen.Add(window))
                windows.Add(window);
        return windows;
    }

    // Readings come in newest first, so the first one seen for a day is that day's newest
    private static void AddCurve(List<ReadingCurve> curves, IReadOnlyList<PlaceReading> newest, ReadingMeasure measure, Func<PlaceReading, decimal?> value)
    {
        var points = newest
            .Where(r => value(r) is not null)
            .GroupBy(r => r.Day)
            .Select(g => g.First())
            .Select(r => new ReadingPoint(r.Day, value(r)!.Value, r.Id))
            .OrderBy(p => p.Day)
            .ToList();
        if (points.Count >= CurveMinimum)
            curves.Add(new ReadingCurve(measure, points));
    }

    private static string? Tidy(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
