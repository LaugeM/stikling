using Stikling.Core.Care;
using Stikling.Core.Models;

namespace Stikling.Core.Today;

/// <summary>The text of the notification for one day.</summary>
public sealed record NoticeDay(DateOnly Day, string Text);

/// <summary>
/// The one notification a day: what Today asks of someone, minus watering. It reads the same
/// sections Today shows, so the two can't drift apart.
/// </summary>
public static class TodayNotice
{
    /// <summary>How many days from today are worked out.</summary>
    public const int DaysAhead = 30;

    private const int MostParts = 4;

    /// <summary>The days from today that have something to say, each with its text.</summary>
    public static IReadOnlyList<NoticeDay> Days(TodayData data, TimeProvider time, int days = DaysAhead)
    {
        var today = time.Today();
        var result = new List<NoticeDay>();
        for (var i = 0; i < days; i++)
        {
            var day = today.AddDays(i);
            if (Text(TodaySections.For(data, day, time)) is { } text)
                result.Add(new NoticeDay(day, text));
        }
        return result;
    }

    /// <summary>The body for a day, or null when nothing is due and nothing is sent.</summary>
    public static string? Text(TodaySections sections)
    {
        if (sections.Away)
            return null;

        var parts = new List<string>();
        var holding = sections.Holding;

        if (holding)
            parts.Add($"{sections.BackRows.Count} to look over after your time away");

        if (sections.RoundSuggestion is { } round)
            parts.Add($"Go round {round.Room.Name}");

        Count(parts, sections.DueTreatments.Count, "1 pest treatment due", n => $"{n} pest treatments due");
        Count(parts, sections.DueChecks.Count, "1 pest case to check", n => $"{n} pest cases to check");
        Count(parts, sections.ReadyNow.Count, "1 pest case ready to resolve", n => $"{n} pest cases ready to resolve");
        Count(parts, sections.PlanDoneNow.Count, "1 treatment plan done", n => $"{n} treatment plans done");

        One(parts, sections.FlaggedNow.Count, () => $"{sections.FlaggedNow[0].Name} needs attention", n => $"{n} need attention");
        One(parts, sections.LookAgainNow.Count, () => $"Look again at {sections.LookAgainNow[0].Plant.DisplayName}", n => $"Look again at {n} plants");
        One(parts, sections.QuarantineNow.Count, () => $"Quarantine is up for {sections.QuarantineNow[0].DisplayName}", n => $"Quarantine is up for {n} plants");
        One(parts, sections.TransitionNow.Count, () => $"Check {sections.TransitionNow[0].DisplayName} in semi-hydro", n => $"Check {n} plants moving to semi-hydro");
        One(parts, sections.RestNow.Count, () => $"Has {sections.RestNow[0].Plant.DisplayName} gone dormant?", n => $"Have {n} plants gone dormant?");

        if (!holding)
        {
            // Only the soil checks, never a request to water
            var soil = sections.WaterNow.Where(d => d.Guess.Form == WateringForm.TopWatered).ToList();
            One(parts, soil.Count, () => $"Check the soil of {soil[0].Plant.DisplayName}", n => $"Check the soil of {n} plants");

            foreach (var group in sections.CareNow)
            {
                var plants = group.Items.Select(i => i.Plant).DistinctBy(p => p.Id).ToList();
                One(parts, plants.Count, () => $"{group.Label}: {plants[0].DisplayName}", n => $"{group.Label}: {n} plants");
            }

            One(parts, sections.ChecksNow.Count, () => $"Check {sections.ChecksNow[0].Propagation.ListName}", n => $"Check {n} propagations");
        }

        if (sections.PhotoRoundShows)
            Count(parts, sections.PhotosDue.Count, "Take 1 photo", n => $"Take {n} photos");

        if (parts.Count == 0)
            return null;

        if (parts.Count > MostParts)
            parts = [.. parts.Take(MostParts), $"and {parts.Count - MostParts} more"];
        return string.Join(" · ", parts);
    }

    private static void Count(List<string> parts, int count, string one, Func<int, string> many)
    {
        if (count > 0)
            parts.Add(count == 1 ? one : many(count));
    }

    // One row names it, several are counted
    private static void One(List<string> parts, int count, Func<string> one, Func<int, string> many)
    {
        if (count > 0)
            parts.Add(count == 1 ? one() : many(count));
    }
}
