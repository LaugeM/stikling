namespace Stikling.Api.Data;

/// <summary>
/// A person's calendar link: the all-day events for the coming days, as an iCalendar feed anyone with
/// the link can read. The text for each day is made by the app, which uploads it, and the server only
/// writes it out. Only the hash of the token is kept, so only the device that made the link can show it
/// again. Only kept on the server, and not a synced record.
/// </summary>
public class CalendarFeed
{
    public Guid PersonId { get; set; }

    /// <summary>SHA-256 of the token, as 64 lowercase hex characters.</summary>
    public required string TokenHash { get; set; }

    /// <summary>Made again each time a token is made. The events' UIDs hold it, so an old link's events don't update in place.</summary>
    public Guid FeedId { get; set; }

    /// <summary>The collection the days were uploaded for, or null before the first upload.</summary>
    public Guid? CollectionId { get; set; }

    /// <summary>A JSON list of <c>{ "day": "yyyy-MM-dd", "title": "...", "description": "..." }</c>.</summary>
    public required string DaysJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the days were last uploaded or the link was made.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
