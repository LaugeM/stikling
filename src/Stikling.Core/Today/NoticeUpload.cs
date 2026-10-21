using Stikling.Core.Models;
using Stikling.Core.Notifications;

namespace Stikling.Core.Today;

/// <summary>Which of the worked-out days go to the server, by what the person picked in Settings.</summary>
public static class NoticeUpload
{
    /// <summary>The longest text the server takes for a day.</summary>
    public const int MaxTextLength = 400;

    /// <summary>
    /// The days to upload: all of them for every day, the round days or the picked weekdays
    /// otherwise. With no round days or no weekdays picked, none. Something overdue is still on
    /// Today the next day, so it's in the text of a later day that is sent.
    /// </summary>
    public static IReadOnlyList<NotificationDay> Days(IReadOnlyList<NoticeDay> days, UserSettings settings)
    {
        var wanted = settings.NotifyOn switch
        {
            NotifyDays.RoundDays => settings.RoundDays,
            NotifyDays.Picked => settings.NotifyWeekdays,
            _ => null
        };

        return
        [
            .. days
                .Where(d => settings.NotifyOn == NotifyDays.EveryDay || wanted?.Contains(d.Day.DayOfWeek.ToWeekday()) == true)
                .Select(d => new NotificationDay(d.Day, Fit(d.Text)))
        ];
    }

    // One text that was too long would make the server turn the whole upload down
    private static string Fit(string text) =>
        text.Length <= MaxTextLength ? text : text[..CutAt(text)] + "…";

    // Room for the ellipsis, and never between the two halves of a character like an emoji
    private static int CutAt(string text)
    {
        var cut = MaxTextLength - 1;
        return char.IsLowSurrogate(text[cut]) ? cut - 1 : cut;
    }
}
