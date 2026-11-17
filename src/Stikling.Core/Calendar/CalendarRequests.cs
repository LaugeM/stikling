using System.Text.Json.Serialization;

namespace Stikling.Core.Calendar;

/// <summary>One all-day event in a person's calendar link: the day, the title and the lines under it.</summary>
public record CalendarDay(
    [property: JsonPropertyName("day")] DateOnly Day,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description);

/// <summary>The days a device uploads for the calendar link.</summary>
public record CalendarDaysRequest(List<CalendarDay>? Days);

/// <summary>The token of a new calendar link. The server only shows it this once.</summary>
public record CalendarLinkResponse(string Token);

/// <summary>Whether the person has a calendar link on.</summary>
public record CalendarStatusResponse(bool On);
