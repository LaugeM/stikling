using System.Globalization;
using Stikling.Core.Models;

namespace Stikling.Core.Text;

/// <summary>
/// Shows temperatures in °C or °F, light in lux or foot-candles and humidity, and converts what is typed.
/// Readings are always stored in °C, lux and percent, so only what is shown and typed changes. The
/// temperature unit is set from the person's settings in the web project only, so the server keeps °C.
/// Light follows <see cref="Lengths"/>: foot-candles when lengths are inches. "lux", "fc", "°C" and "°F"
/// are symbols and are the same in every language.
/// </summary>
public static class Climate
{
    private const decimal LuxPerFootCandle = 10.764m;

    public static TemperatureUnit TemperatureUnit { get; set; } = TemperatureUnit.Celsius;

    public static bool IsFahrenheit => TemperatureUnit == TemperatureUnit.Fahrenheit;

    /// <summary>"°C" or "°F".</summary>
    public static string TemperatureUnitName => IsFahrenheit ? "°F" : "°C";

    /// <summary>"lux", or "fc" when lengths are inches.</summary>
    public static string LightUnitName => Lengths.IsInches ? "fc" : "lux";

    /// <summary>A temperature stored in °C with its unit, like "19.5 °C" or "67.1 °F".</summary>
    public static string Temperature(decimal c) => Number(TemperatureToInput(c)) + " " + TemperatureUnitName;

    /// <summary>A temperature with only a degree sign, like "19.5°", for a tight spot.</summary>
    public static string TemperatureShort(decimal c) => Number(TemperatureToInput(c)) + "°";

    /// <summary>The number for an input box showing a temperature stored in °C.</summary>
    public static decimal TemperatureToInput(decimal c) =>
        IsFahrenheit ? Math.Round(c * 9 / 5 + 32, 1, MidpointRounding.AwayFromZero) : c;

    /// <summary>The °C to store for a number typed in an input box, rounded to one decimal.</summary>
    public static decimal TemperatureFromInput(decimal typed) =>
        Math.Round(IsFahrenheit ? (typed - 32) * 5 / 9 : typed, 1, MidpointRounding.AwayFromZero);

    /// <summary>Light stored in lux with its unit, like "1,200 lux" or "111 fc".</summary>
    public static string Light(int lux)
    {
        if (!Lengths.IsInches)
            return lux.ToString("N0", CultureInfo.CurrentCulture) + " lux";

        var footCandles = lux / LuxPerFootCandle;
        var text = footCandles < 10
            ? footCandles.ToString("0.#", CultureInfo.CurrentCulture)
            : Math.Round(footCandles, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.CurrentCulture);
        return text + " fc";
    }

    /// <summary>The number for an input box showing light stored in lux. Foot-candles get two decimals, so the lux typed in before comes back exact.</summary>
    public static decimal LightToInput(int lux) =>
        Lengths.IsInches ? Math.Round(lux / LuxPerFootCandle, 2, MidpointRounding.AwayFromZero) : lux;

    /// <summary>The whole lux to store for a number typed in an input box.</summary>
    public static int LightFromInput(decimal typed) =>
        (int)Math.Round(Lengths.IsInches ? typed * LuxPerFootCandle : typed, 0, MidpointRounding.AwayFromZero);

    /// <summary>Humidity like "60%".</summary>
    public static string Humidity(int percent) => percent.ToString(CultureInfo.CurrentCulture) + "%";

    private static string Number(decimal value) => value.ToString("0.#", CultureInfo.CurrentCulture);
}
