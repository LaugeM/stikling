using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;

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
            parts.Add(CareText.F(Strings.Care.NoticeBackToLookOver, sections.BackRows.Count));

        if (sections.RoundSuggestion is { } round)
            parts.Add(CareText.F(Strings.Care.NoticeGoRound, round.Room.Name));

        Count(parts, sections.DueTreatments.Count, Strings.Care.NoticeTreatmentDue_One, Strings.Care.NoticeTreatmentDue_Other);
        Count(parts, sections.DueChecks.Count, Strings.Care.NoticeCheck_One, Strings.Care.NoticeCheck_Other);
        Count(parts, sections.ReadyNow.Count, Strings.Care.NoticeReady_One, Strings.Care.NoticeReady_Other);
        Count(parts, sections.PlanDoneNow.Count, Strings.Care.NoticePlanDone_One, Strings.Care.NoticePlanDone_Other);

        One(parts, sections.FlaggedNow.Count, Strings.Care.NoticeFlaggedOne, () => sections.FlaggedNow[0].Name, Strings.Care.NoticeFlaggedMany);
        One(parts, sections.LookAgainNow.Count, Strings.Care.NoticeLookAgainOne, () => sections.LookAgainNow[0].Plant.DisplayName, Strings.Care.NoticeLookAgainMany);
        One(parts, sections.QuarantineNow.Count, Strings.Care.NoticeQuarantineOne, () => sections.QuarantineNow[0].DisplayName, Strings.Care.NoticeQuarantineMany);
        One(parts, sections.TransitionNow.Count, Strings.Care.NoticeTransitionOne, () => sections.TransitionNow[0].DisplayName, Strings.Care.NoticeTransitionMany);
        One(parts, sections.RestNow.Count, Strings.Care.NoticeRestOne, () => sections.RestNow[0].Plant.DisplayName, Strings.Care.NoticeRestMany);

        if (!holding)
        {
            // Only the soil checks, never a request to water
            var soil = sections.WaterNow.Where(d => d.Guess.Form == WateringForm.TopWatered).ToList();
            One(parts, soil.Count, Strings.Care.NoticeSoilOne, () => soil[0].Plant.DisplayName, Strings.Care.NoticeSoilMany);

            foreach (var group in sections.CareNow)
            {
                var plants = group.Items.Select(i => i.Plant).DistinctBy(p => p.Id).ToList();
                One(parts, plants.Count, Strings.Care.NoticeCareOne, () => plants[0].DisplayName, Strings.Care.NoticeCareMany, group.Label);
            }

            One(parts, sections.ChecksNow.Count, Strings.Care.NoticeCheckOne, () => sections.ChecksNow[0].Propagation.ListName, Strings.Care.NoticeCheckMany);
        }

        if (sections.PhotoRoundShows)
            Count(parts, sections.PhotosDue.Count, Strings.Care.NoticePhoto_One, Strings.Care.NoticePhoto_Other);

        if (parts.Count == 0)
            return null;

        // "and 1 more" would be no shorter than the part it stands for
        if (parts.Count > MostParts + 1)
            parts = [.. parts.Take(MostParts), CareText.F(Strings.Care.NoticeAndMore, parts.Count - MostParts)];
        return string.Join(" · ", parts);
    }

    private static void Count(List<string> parts, int count, string one, string other)
    {
        if (count > 0)
            parts.Add(Plural.Of(count, one, other));
    }

    // One row names it, several are counted. The care kind, when there is one, comes first in both texts
    private static void One(List<string> parts, int count, string one, Func<string> name, string many, string? kind = null)
    {
        if (count == 0)
            return;
        if (kind is null)
            parts.Add(count == 1 ? CareText.F(one, name()) : CareText.F(many, count));
        else
            parts.Add(count == 1 ? CareText.F(one, kind, name()) : CareText.F(many, kind, count));
    }
}
