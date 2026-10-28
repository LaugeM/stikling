namespace Stikling.Api.Data;

/// <summary>
/// What to send a person each day and when. The text for each day is made by the app, which uploads
/// the next 30 days, and the server only sends it. Only kept on the server, and not a synced record.
/// </summary>
public class NotificationSchedule
{
    public Guid PersonId { get; set; }

    /// <summary>The device's IANA time zone.</summary>
    public required string TimeZone { get; set; }

    /// <summary>The local time of day to send at, on a quarter hour.</summary>
    public TimeOnly NotifyAt { get; set; }

    /// <summary>A JSON list of <c>{ "day": "yyyy-MM-dd", "text": "..." }</c>.</summary>
    public required string DaysJson { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>The local day that was last sent or skipped.</summary>
    public DateOnly? LastSentOn { get; set; }

    /// <summary>When the next day with text is due, or null when there is none.</summary>
    public DateTimeOffset? NextSendAt { get; set; }
}
