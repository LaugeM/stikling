using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Today;

namespace Stikling.Web.Services;

/// <summary>
/// What the watering reminder says, on a plant's page and on Today. It only states what the
/// plant's own log shows and asks the person to check, and never says to water.
/// </summary>
public static class WateringText
{
    private const string Longer = "Longer now the days are shorter.";

    /// <summary>The line on the plant's page.</summary>
    public static string Summary(WateringGuess guess)
    {
        if (guess.Mode == WateringMode.Learning)
        {
            return $"Learning how often it needs water: {Labels.Count(guess.WateringsNeeded, "more watering")} before it can say.";
        }

        var text = (guess.Form, guess.Mode) switch
        {
            (WateringForm.TopWatered, WateringMode.Meter) =>
                $"Usually dry about {Labels.Count(guess.UsualDays ?? 1, "day")} after watering, learned from {Labels.Count(guess.LearnedFrom, "watering")}.",
            (WateringForm.TopWatered, _) =>
                $"Usually watered about {Every(guess.UsualDays ?? 1)}, learned from {Labels.Count(guess.LearnedFrom, "watering")}.",
            (WateringForm.Reservoir, _) =>
                $"The reservoir usually lasts about {Labels.Count(guess.UsualDays ?? 1, "day")}, learned from {Labels.Count(guess.LearnedFrom, "refill")}.",
            _ =>
                $"The water is usually topped up about {Every(guess.UsualDays ?? 1)}, learned from {guess.LearnedFrom}."
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
                $"Usually watered about {Every(guess.UsualDays ?? 1)}, it's been {Labels.Count(guess.DaysSince ?? 0, "day")}",
            (WateringForm.Reservoir, _) =>
                $"The reservoir usually lasts about {Labels.Count(guess.UsualDays ?? 1, "day")}, it's been {Labels.Count(guess.DaysSince ?? 0, "day")}",
            _ =>
                $"The water is usually topped up about {Every(guess.UsualDays ?? 1)}, it's been {Labels.Count(guess.DaysSince ?? 0, "day")}"
        };
        return Season(guess) is { } season ? $"{text} · {season}" : text;
    }

    private static string MeterRow(WateringDue due, DateOnly today)
    {
        var guess = due.Guess;
        var days = (guess.CheckOn ?? today).DayNumber - today.DayNumber;
        var line = due.DueNow
            ? "Probably dry by now, check with the meter"
            : days == 1 ? "Probably dry tomorrow" : $"Probably dry in {days} days";
        if (guess.LatestReading is { } reading)
            line += $", last reading {reading.Value}, {Labels.DaysAgo(reading.DaysAgo)}";
        return line;
    }

    private static string Every(int days) => days == 1 ? "every day" : $"every {days} days";

    private static string? Season(WateringGuess guess) =>
        guess is { SeasonAdjusted: true, DaysShortening: true } ? Longer : null;

    /// <summary>Said on the tick, and what is logged: Watered, or Refilled / Topped up for the others.</summary>
    public static string DoneLabel(WateringForm form) => form switch
    {
        WateringForm.TopWatered => "Watered",
        WateringForm.Reservoir => "Refilled",
        _ => "Topped up"
    };

    /// <summary>The care entry the tick logs.</summary>
    public static CareKind DoneKind(WateringForm form) =>
        form == WateringForm.TopWatered ? CareKind.Watered : CareKind.ToppedUp;

    /// <summary>The second button: the plant is still wet, or the water isn't due yet.</summary>
    public static string NotYetLabel(WateringForm form) =>
        form == WateringForm.TopWatered ? "Still wet" : "Not yet";
}
