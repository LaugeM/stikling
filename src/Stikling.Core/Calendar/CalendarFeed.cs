using Stikling.Core.Models;
using Stikling.Core.Text;
using Stikling.Core.Today;
using Strings = Stikling.Core.Resources;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Calendar;

/// <summary>
/// The all-day events for the calendar link: one for each day with something due on Today. The
/// text is the notification's, so the two never drift apart.
/// </summary>
public static class CalendarFeed
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 2000;

    /// <summary>The most lines listed in a description before "and N more".</summary>
    public const int MostLines = 20;

    public static IReadOnlyList<CalendarDay> Days(
        TodayData data, TimeProvider time, string todayUrl, IReadOnlyCollection<string>? leaveOut = null, int days = TodayNotice.DaysAhead)
    {
        var today = time.Today();
        var result = new List<CalendarDay>();
        for (var i = 0; i < days; i++)
        {
            var day = today.AddDays(i);
            var sections = TodaySections.For(data, day, time);
            var parts = TodayNotice.Parts(sections, leaveOut);
            if (TodayNotice.Join(parts) is not { } text)
                continue;

            var description = Describe(parts, todayUrl);

            result.Add(new CalendarDay(
                day,
                Fit(F(Strings.Calendar.TitleFormat, text), MaxTitleLength),
                Fit(description, MaxDescriptionLength)));
        }

        return result;
    }

    /// <summary>The lines under an event: one for each part, at most 20, a blank line and where to open Today.</summary>
    public static string Describe(IReadOnlyList<string> parts, string todayUrl)
    {
        var lines = parts.Count > MostLines
            ? [.. parts.Take(MostLines), F(Strings.Calendar.AndMore, parts.Count - MostLines)]
            : parts.ToList();
        return string.Join("\n", lines) + "\n\n" + F(Strings.Calendar.OpenToday, todayUrl);
    }

    // Room for the ellipsis, and never between the two halves of a character like an emoji
    private static string Fit(string text, int max)
    {
        if (text.Length <= max)
            return text;

        var cut = max - 1;
        if (char.IsLowSurrogate(text[cut]))
            cut--;
        return text[..cut] + "…";
    }
}
