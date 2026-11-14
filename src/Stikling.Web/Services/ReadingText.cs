using System.Globalization;
using Stikling.Core.Models;
using Stikling.Core.Readings;
using Stikling.Core.Text;
using Strings = Stikling.Web.Resources;
using static Stikling.Core.Text.Format;

namespace Stikling.Web.Services;

/// <summary>Text for light, temperature and humidity readings.</summary>
public static class ReadingText
{
    /// <summary>"About bright indirect": a rough guess at the light level from the lux.</summary>
    public static string Guess(int lux) =>
        F(Strings.Shared.ReadingGuess, Labels.For(LightGuess.For(lux)).ToLower(CultureInfo.CurrentCulture));

    public static string MeasureName(ReadingMeasure measure) => measure switch
    {
        ReadingMeasure.Light => Strings.Shared.ReadingLight,
        ReadingMeasure.Temperature => Strings.Shared.ReadingTemperature,
        _ => Strings.Shared.ReadingHumidity
    };

    /// <summary>A point's value as it is shown, from the value in stored units.</summary>
    public static string Value(ReadingMeasure measure, decimal stored) => measure switch
    {
        ReadingMeasure.Light => Climate.Light((int)Math.Round(stored, MidpointRounding.AwayFromZero)),
        ReadingMeasure.Temperature => Climate.Temperature(stored),
        _ => Climate.Humidity((int)Math.Round(stored, MidpointRounding.AwayFromZero))
    };

    /// <summary>"Where it stands: 1,200 lux, 19.5°, 60% · 3 Nov", from the latest readings of the place.</summary>
    public static string Stands(PlaceConditions conditions)
    {
        var values = new List<string>();
        if (conditions.Light is { } light)
            values.Add(Climate.Light(light.Reading.Lux ?? 0));
        if (conditions.Temperature is { } temperature)
            values.Add(Climate.TemperatureShort(temperature.Reading.TemperatureC ?? 0));
        if (conditions.Humidity is { } humidity)
            values.Add(Climate.Humidity(humidity.Reading.HumidityPercent ?? 0));
        return F(Strings.Shared.ReadingStands, string.Join(", ", values), Stikling.Core.Problems.ProblemText.ShortDay(conditions.NewestDay ?? default));
    }

    /// <summary>"1,200 lux · 19.5 °C · 60%": the values that are set.</summary>
    public static string Values(PlaceReading reading)
    {
        var parts = new List<string>();
        if (reading.Lux is { } lux)
            parts.Add(Climate.Light(lux));
        if (reading.TemperatureC is { } c)
            parts.Add(Climate.Temperature(c));
        if (reading.HumidityPercent is { } humidity)
            parts.Add(Climate.Humidity(humidity));
        return string.Join(" · ", parts);
    }
}
