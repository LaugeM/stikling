using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Care;

/// <summary>
/// One sentence for a card or a row, read out as its name by a screen reader, like "Monstera, living
/// room, watered 6 days ago". It says what the card shows, joined with commas, and leaves out what the card doesn't.
/// </summary>
public static class CardSentences
{
    /// <summary>What a card on Go round shows, with what is logged today.</summary>
    /// <param name="place">The spot or room the card sits under, or null.</param>
    /// <param name="form">How the plant is watered. Null for a propagation, which has no watering line.</param>
    /// <param name="lastWateredDaysAgo">Days since the newest watering, or null when there is none.</param>
    /// <param name="readingToday">The meter reading logged today, or null.</param>
    /// <param name="lastReading">The newest earlier reading, shown when there is none today.</param>
    /// <param name="dueLine">The reminder line the card shows when the plant is due, already written out.</param>
    /// <param name="handLine">The line about plants that need a hand, already written out.</param>
    public static string GoRoundCard(
        string name, string? place, WateringForm? form, int? lastWateredDaysAgo, int? readingToday,
        (int Value, int DaysAgo)? lastReading, string? dueLine, string? handLine, bool dormant,
        bool lookedOverToday, bool photoToday)
    {
        var parts = new List<string?> { name, place, handLine };
        if (form is { } f)
        {
            parts.Add(lastWateredDaysAgo is { } days ? F(Watered(f), DaysAgo(days)) : F(NotWatered(f)));
            if (readingToday is { } reading)
                parts.Add(F(Strings.Care.SentenceReads, reading));
            else if (lastReading is { } last)
                parts.Add(F(Strings.Care.SentenceLastReading, last.Value, DaysAgo(last.DaysAgo)));
        }
        parts.Add(dueLine);
        if (dormant)
            parts.Add(Strings.Care.SentenceDormant);
        if (lookedOverToday)
            parts.Add(Strings.Care.SentenceLookedOverToday);
        if (photoToday)
            parts.Add(Strings.Care.SentencePhotoToday);
        return Join(parts);
    }

    /// <summary>
    /// A row on Water plants. <paramref name="doneText"/> is what was logged today, like "Watered with Feed",
    /// and the lines are the rest of what the row shows, like "Dry" or "watered 3 days ago".
    /// </summary>
    public static string WaterRow(string name, string? doneText, params string?[] lines) =>
        doneText is { Length: > 0 } done
            ? Join([name, F(Strings.Care.SentenceDoneToday, char.ToLowerInvariant(done[0]) + done[1..])])
            : Join([name, .. lines]);

    /// <summary>A watering row on Today, with the line the row shows under the name.</summary>
    public static string TodayWaterRow(string name, string line) => Join([name, line]);

    /// <summary>
    /// A plant in a care interval card on Today. <paramref name="lastDaysAgo"/> is null for a first time,
    /// and <paramref name="dueInDays"/> is only given on a card that is coming up.
    /// </summary>
    public static string CareRow(string name, int? lastDaysAgo, int? dueInDays = null)
    {
        var last = lastDaysAgo is { } days ? F(Strings.Care.SentenceLast, DaysAgo(days)) : Strings.Care.SentenceFirstTime;
        var due = dueInDays switch
        {
            null => null,
            <= 0 => Strings.Care.SentenceDueToday,
            1 => Strings.Care.SentenceDueTomorrow,
            var n => F(Strings.Care.SentenceDueIn, n)
        };
        return Join([name, due, last]);
    }

    private static string Watered(WateringForm form) => form switch
    {
        WateringForm.TopWatered => Strings.Care.SentenceWatered,
        WateringForm.Reservoir => Strings.Care.SentenceRefilled,
        _ => Strings.Care.SentenceToppedUp
    };

    private static string NotWatered(WateringForm form) => form switch
    {
        WateringForm.TopWatered => Strings.Care.SentenceNotWatered,
        WateringForm.Reservoir => Strings.Care.SentenceNotRefilled,
        _ => Strings.Care.SentenceNotToppedUp
    };

    /// <summary>"today", "yesterday", "12 days ago".</summary>
    private static string DaysAgo(int days) => days switch
    {
        <= 0 => Strings.Care.SentenceToday,
        1 => Strings.Care.SentenceYesterday,
        _ => F(Strings.Care.SentenceDaysAgo, days)
    };

    private static string Join(IEnumerable<string?> parts) =>
        string.Join(", ", parts.Select(Clean).OfType<string>());
}
