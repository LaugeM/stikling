using Stikling.Core.Models;
using Stikling.Core.Settings;
using Stikling.Core.Text;

namespace Stikling.Web.Services;

/// <summary>
/// Keeps how lengths, dates and the clock are shown in step with the person's settings. Core reads the
/// choice from <see cref="Lengths"/> and <see cref="TextCulture"/>, which only this app sets, so the
/// server keeps the defaults. Settings with nothing chosen mean centimetres, the language's own date
/// pattern and the 24-hour clock.
/// </summary>
public sealed class DisplayPreferences(SettingsService settings)
{
    /// <summary>Sets the three choices from the settings, or the defaults when there are none.</summary>
    public static void Apply(UserSettings? saved)
    {
        Lengths.Unit = saved?.Lengths ?? LengthUnit.Centimetres;
        TextCulture.Style = saved?.Dates ?? DateStyle.DayFirst;
        TextCulture.Clock = saved?.Clock ?? ClockStyle.TwentyFourHour;
    }

    /// <summary>Reads the saved settings and applies them. Call it again after settings change, e.g. after a sync or a restore.</summary>
    public async Task ApplySavedAsync() => Apply(await settings.GetSavedAsync());
}
