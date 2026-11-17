using System.Globalization;
using System.Text;

namespace Stikling.Core.Calendar;

/// <summary>Writes the days as an iCalendar feed (RFC 5545) that a calendar app can subscribe to.</summary>
public static class IcsWriter
{
    private const string Crlf = "\r\n";
    private const int FoldAt = 75;

    /// <param name="feedId">Made again with each new link. The events' UIDs hold it, never the token or the person.</param>
    /// <param name="stamp">When the days were last uploaded, as DTSTAMP.</param>
    public static string Write(Guid feedId, IEnumerable<CalendarDay> days, DateTimeOffset stamp)
    {
        var text = new StringBuilder();
        void Line(string line) => Fold(text, line);

        Line("BEGIN:VCALENDAR");
        Line("VERSION:2.0");
        Line("PRODID:-//Stikling//Today//EN");
        Line("CALSCALE:GREGORIAN");
        Line("METHOD:PUBLISH");
        Line("X-WR-CALNAME:Stikling");
        Line("REFRESH-INTERVAL;VALUE=DURATION:PT1H");
        Line("X-PUBLISHED-TTL:PT1H");

        var dtstamp = stamp.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        foreach (var day in days.OrderBy(d => d.Day))
        {
            var date = day.Day.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            Line("BEGIN:VEVENT");
            Line($"UID:{date}-{feedId:N}@stikling.app");
            Line($"DTSTAMP:{dtstamp}");
            Line($"DTSTART;VALUE=DATE:{date}");
            Line($"DTEND;VALUE=DATE:{day.Day.AddDays(1).ToString("yyyyMMdd", CultureInfo.InvariantCulture)}");
            Line("SUMMARY:" + Escape(day.Title));
            if (!string.IsNullOrEmpty(day.Description))
                Line("DESCRIPTION:" + Escape(day.Description));
            Line("TRANSP:TRANSPARENT");
            Line("END:VEVENT");
        }

        Line("END:VCALENDAR");
        return text.ToString();
    }

    private static string Escape(string value) => value
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n")
        .Replace("\r", "\\n");

    // Lines of at most 75 octets, continued with one space, and never cut inside a character
    private static void Fold(StringBuilder text, string line)
    {
        var octets = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (octets + size > FoldAt)
            {
                text.Append(Crlf).Append(' ');
                octets = 1;
            }

            text.Append(rune.ToString());
            octets += size;
        }

        text.Append(Crlf);
    }
}
