using Stikling.Core.Models;

namespace Stikling.Core.Readings;

/// <summary>
/// A rough guess at the light level of a place from three answers: hours of sun, distance from the window and
/// what is in the way. Nothing writes the guess to a plant, since the answers are a judgment too.
/// </summary>
public static class LightCheck
{
    /// <summary>Null until all three are answered.</summary>
    public static LightLevel? Guess(SunHours? sun, WindowDistance? distance, Shade? shade)
    {
        if (sun is not { } hours || distance is not { } far || shade is not { } blocked)
            return null;

        var level = hours switch
        {
            SunHours.None => LightLevel.Medium,
            SunHours.UnderTwo or SunHours.TwoToFour => LightLevel.BrightIndirect,
            _ => LightLevel.DirectSun
        };

        var down = far switch
        {
            WindowDistance.OneToTwoMetres => 1,
            WindowDistance.OverTwoMetres => 2,
            _ => 0
        } + blocked switch
        {
            Shade.Some => 1,
            Shade.Lots => 2,
            _ => 0
        };

        return (LightLevel)Math.Max((int)LightLevel.Low, (int)level - down);
    }

    public static LightLevel? Guess(Place place) => Guess(place.SunHours, place.FromWindow, place.InTheWay);
}
