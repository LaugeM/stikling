using System.Text;
using Stikling.Core.Calendar;

namespace Stikling.Core.Tests;

public class IcsWriterTests
{
    private static readonly Guid Feed = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly DateTimeOffset Stamp = new(2026, 4, 20, 14, 30, 5, TimeSpan.FromHours(2));

    private static string Write(params CalendarDay[] days) => IcsWriter.Write(Feed, days, Stamp);

    // The lines with the folding undone
    private static List<string> Unfolded(string ics) =>
        [.. ics.Replace("\r\n ", "").Split("\r\n", StringSplitOptions.RemoveEmptyEntries)];

    [Fact]
    public void An_empty_list_is_still_a_valid_calendar()
    {
        var lines = Unfolded(Write());

        Assert.Equal("BEGIN:VCALENDAR", lines[0]);
        Assert.Equal("END:VCALENDAR", lines[^1]);
        Assert.Contains("VERSION:2.0", lines);
        Assert.Contains("PRODID:-//Stikling//Today//EN", lines);
        Assert.Contains("METHOD:PUBLISH", lines);
        Assert.Contains("REFRESH-INTERVAL;VALUE=DURATION:PT1H", lines);
        Assert.DoesNotContain("BEGIN:VEVENT", lines);
    }

    [Fact]
    public void An_event_has_a_stable_uid_the_stamp_in_utc_and_ends_the_next_day()
    {
        var lines = Unfolded(Write(new CalendarDay(new DateOnly(2026, 4, 30), "Stikling: Fern", "Fern")));

        Assert.Contains("UID:20260430-11111111222233334444555555555555@stikling.app", lines);
        Assert.Contains("DTSTAMP:20260420T123005Z", lines);
        Assert.Contains("DTSTART;VALUE=DATE:20260430", lines);
        Assert.Contains("DTEND;VALUE=DATE:20260501", lines);
        Assert.Contains("SUMMARY:Stikling: Fern", lines);
        Assert.Contains("DESCRIPTION:Fern", lines);
        Assert.Contains("TRANSP:TRANSPARENT", lines);
    }

    [Fact]
    public void Text_is_escaped_and_an_empty_description_is_left_out()
    {
        var ics = Write(
            new CalendarDay(new DateOnly(2026, 4, 21), "A; B, C\\D", "one\ntwo\r\nthree"),
            new CalendarDay(new DateOnly(2026, 4, 22), "Plain", null));
        var lines = Unfolded(ics);

        Assert.Contains("SUMMARY:A\\; B\\, C\\\\D", lines);
        Assert.Contains("DESCRIPTION:one\\ntwo\\nthree", lines);
        Assert.Single(lines, l => l.StartsWith("DESCRIPTION"));
    }

    [Fact]
    public void Lines_end_in_crlf_and_events_come_in_date_order()
    {
        var ics = Write(
            new CalendarDay(new DateOnly(2026, 5, 2), "Later", null),
            new CalendarDay(new DateOnly(2026, 5, 1), "Earlier", null));

        Assert.EndsWith("\r\n", ics);
        Assert.DoesNotContain("\n", ics.Replace("\r\n", ""));
        Assert.True(ics.IndexOf("Earlier", StringComparison.Ordinal) < ics.IndexOf("Later", StringComparison.Ordinal));
    }

    [Fact]
    public void Long_lines_fold_at_75_octets_without_cutting_a_character()
    {
        var title = string.Concat(Enumerable.Repeat("æøå🌿", 40));
        var ics = Write(new CalendarDay(new DateOnly(2026, 4, 21), title, null));

        foreach (var line in ics.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.True(Encoding.UTF8.GetByteCount(line) <= 75, line);
            Assert.DoesNotContain('�', line);
        }

        Assert.Contains("\r\n ", ics);
        Assert.Contains("SUMMARY:" + title, Unfolded(ics));
    }
}
