using System.Globalization;
using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Text;
using Stikling.Core.Today;
using Strings = Stikling.Web.Resources;

namespace Stikling.Web.Services;

/// <summary>
/// What the watering reminder says, on a plant's page and on Today. It only states what the
/// plant's own log shows and asks the person to check, and never says to water.
/// </summary>
public static class WateringText
{
    /// <summary>The line on the plant's page.</summary>
    public static string Summary(WateringGuess guess)
    {
        if (guess.Mode == WateringMode.Learning)
        {
            return F(Strings.Care.WateringLearning,
                Plural.Of(guess.WateringsNeeded, Strings.Care.WateringsMore_One, Strings.Care.WateringsMore_Other));
        }

        var text = (guess.Form, guess.Mode) switch
        {
            (WateringForm.TopWatered, WateringMode.Meter) =>
                F(Strings.Care.WateringSummaryMeter, Days(guess.UsualDays ?? 1), Waterings(guess.LearnedFrom)),
            (WateringForm.TopWatered, _) =>
                F(Strings.Care.WateringSummaryTop, Every(guess.UsualDays ?? 1), Waterings(guess.LearnedFrom)),
            (WateringForm.Reservoir, _) =>
                F(Strings.Care.WateringSummaryReservoir, Days(guess.UsualDays ?? 1),
                    Plural.Of(guess.LearnedFrom, Strings.Care.WateringRefills_One, Strings.Care.WateringRefills_Other)),
            _ =>
                F(Strings.Care.WateringSummaryInWater, Every(guess.UsualDays ?? 1), guess.LearnedFrom)
        };
        return Season(guess) is { } season ? $"{text} {season}" : text;
    }

    /// <summary>The line under a plant's name on Today.</summary>
    public static string Row(WateringDue due, DateOnly today)
    {
        var guess = due.Guess;
        var text = (guess.Form, guess.Mode) switch
        {
            (WateringForm.TopWatered, WateringMode.Meter) => MeterRow(due, today),
            (WateringForm.TopWatered, _) =>
                F(Strings.Care.WateringRowTop, Every(guess.UsualDays ?? 1), Days(guess.DaysSince ?? 0)),
            (WateringForm.Reservoir, _) =>
                F(Strings.Care.WateringRowReservoir, Days(guess.UsualDays ?? 1), Days(guess.DaysSince ?? 0)),
            _ =>
                F(Strings.Care.WateringRowInWater, Every(guess.UsualDays ?? 1), Days(guess.DaysSince ?? 0))
        };
        return Season(guess) is { } season ? $"{text} · {season}" : text;
    }

    private static string MeterRow(WateringDue due, DateOnly today)
    {
        var guess = due.Guess;
        var days = (guess.CheckOn ?? today).DayNumber - today.DayNumber;
        if (guess.LatestReading is { } reading)
        {
            var ago = Labels.DaysAgo(reading.DaysAgo);
            return days <= 0 ? F(Strings.Care.WateringDryNowReading, reading.Value, ago)
                : days == 1 ? F(Strings.Care.WateringDryTomorrowReading, reading.Value, ago)
                : F(Strings.Care.WateringDryInReading, days, reading.Value, ago);
        }

        return days <= 0 ? Strings.Care.WateringDryNow
            : days == 1 ? Strings.Care.WateringDryTomorrow
            : F(Strings.Care.WateringDryIn, days);
    }

    /// <summary>When the plant last had water, e.g. "watered 3 days ago" or "refilled 3 days ago".</summary>
    public static string LastWater(LastCare last, WateringForm form)
    {
        var format = last.Kind == CareKind.ToppedUp
            ? form == WateringForm.Reservoir ? Strings.Care.WateringRefilledAgo : Strings.Care.WateringToppedUpAgo
            : Strings.Care.WateringWateredAgo;
        return F(format, Labels.DaysAgo(last.DaysAgo));
    }

    private static string F(string format, params object[] args) => string.Format(CultureInfo.CurrentCulture, format, args);

    private static string Days(int days) => Plural.Of(days, Strings.Care.WateringDays_One, Strings.Care.WateringDays_Other);

    private static string Waterings(int count) =>
        Plural.Of(count, Strings.Care.WateringWaterings_One, Strings.Care.WateringWaterings_Other);

    private static string Every(int days) =>
        days == 1 ? Strings.Care.WateringEveryDay : F(Strings.Care.WateringEveryDays, days);

    private static string? Season(WateringGuess guess) =>
        guess is { SeasonAdjusted: true, DaysShortening: true } ? Strings.Care.WateringLonger : null;

    /// <summary>Said on the tick, and what is logged: Watered, or Refilled / Topped up for the others.</summary>
    public static string DoneLabel(WateringForm form) => form switch
    {
        WateringForm.TopWatered => Strings.Care.WateringDoneWatered,
        WateringForm.Reservoir => Strings.Care.WateringDoneRefilled,
        _ => Strings.Care.WateringDoneToppedUp
    };

    /// <summary>The second button: the plant is still wet, or the water isn't due yet.</summary>
    public static string NotYetLabel(WateringForm form) =>
        form == WateringForm.TopWatered ? Strings.Care.WateringStillWet : Strings.Care.WateringNotYet;
}
