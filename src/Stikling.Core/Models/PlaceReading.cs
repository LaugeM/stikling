using System.Text.Json.Serialization;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>
/// A light, temperature or humidity reading typed in for a room or a spot, from a light meter or a
/// hygrometer. Any of the three values can be left out, but not all. Values are stored in lux, °C and
/// percent, whatever units are shown.
/// </summary>
public sealed class PlaceReading : Entity
{
    /// <summary>The room or spot it was read in. A place merged into another counts as the other.</summary>
    public Guid PlaceId { get; set; }

    /// <summary>The day it was read.</summary>
    public DateOnly Day { get; set; }

    /// <summary>
    /// The time of day at the place, if written down. A day and a time rather than a moment in time, so
    /// 14:00 at the window stays 14:00 in any time zone. Null for readings typed in later without one.
    /// </summary>
    public TimeOnly? Time { get; set; }

    /// <summary>Light in whole lux.</summary>
    public int? Lux { get; set; }

    /// <summary>Temperature in °C with one decimal.</summary>
    public decimal? TemperatureC { get; set; }

    /// <summary>Relative humidity, 0 to 100.</summary>
    public int? HumidityPercent { get; set; }

    /// <summary>Which window the reading was taken at, in the person's own words.</summary>
    public string? Window { get; set; }

    public string? Notes { get; set; }

    public const int MaxLux = 200000;
    public const decimal MinTemperatureC = -50;
    public const decimal MaxTemperatureC = 60;

    /// <summary>True when at least one of the three values is set.</summary>
    [JsonIgnore]
    public bool HasValue => Lux is not null || TemperatureC is not null || HumidityPercent is not null;

    public PlaceReading Copy() => (PlaceReading)MemberwiseClone();

    public IReadOnlyList<string> Validate(DateOnly today)
    {
        var errors = new List<string>();
        if (PlaceId == Guid.Empty)
            errors.Add(Strings.Shared.ReadingNeedsPlace);
        if (!HasValue)
            errors.Add(Strings.Shared.ReadingNeedsValue);
        if (Day > today)
            errors.Add(Strings.Shared.ReadingFuture);
        if (Lux is < 0 or > MaxLux)
            errors.Add(Strings.Shared.ReadingLuxRange);
        if (TemperatureC is < MinTemperatureC or > MaxTemperatureC)
            errors.Add(Strings.Shared.ReadingTemperatureRange);
        if (HumidityPercent is < 0 or > 100)
            errors.Add(Strings.Shared.ReadingHumidityRange);
        return errors;
    }
}
