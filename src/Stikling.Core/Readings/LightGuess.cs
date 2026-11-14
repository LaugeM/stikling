using Stikling.Core.Models;

namespace Stikling.Core.Readings;

/// <summary>
/// Turns a lux reading into the nearest light level, as a guess to show as text. Nothing writes it to a
/// plant, since a reading is one moment and a plant's light level is a judgment.
/// </summary>
/// <remarks>
/// The cut-offs come from Clemson Cooperative Extension, Home &amp; Garden Information Center, "Indoor
/// Plants - Cleaning, Fertilizing, Containers &amp; Light Requirements", which gives preferred ranges in
/// foot-candles for low, medium, high and very high light.
/// </remarks>
public static class LightGuess
{
    /// <summary>Lux in one foot-candle.</summary>
    public const decimal LuxPerFootCandle = 10.764m;

    /// <summary>Where medium light starts, in foot-candles.</summary>
    public const int MediumFootCandles = 200;

    /// <summary>Where bright indirect light starts, in foot-candles.</summary>
    public const int BrightFootCandles = 500;

    /// <summary>Where direct sun starts, in foot-candles.</summary>
    public const int DirectSunFootCandles = 1000;

    public static LightLevel For(int lux)
    {
        var footCandles = lux / LuxPerFootCandle;
        return footCandles < MediumFootCandles ? LightLevel.Low
            : footCandles < BrightFootCandles ? LightLevel.Medium
            : footCandles < DirectSunFootCandles ? LightLevel.BrightIndirect
            : LightLevel.DirectSun;
    }
}
