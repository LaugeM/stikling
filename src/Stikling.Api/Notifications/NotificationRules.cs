using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stikling.Api.Notifications;

public record NotificationDay(
    [property: JsonPropertyName("day")] DateOnly Day,
    [property: JsonPropertyName("text")] string Text);

/// <summary>The rules for a person's notification schedule, apart from the database.</summary>
public static class NotificationRules
{
    public const int MaxDays = 31;
    public const int MaxTextLength = 400;

    /// <summary>A day more than this late after its time is skipped, e.g. because the job was stopped.</summary>
    public static readonly TimeSpan MissedAfter = TimeSpan.FromHours(2);

    public static readonly TimeSpan Ttl = TimeSpan.FromHours(8);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string HashEndpoint(string endpoint) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint)));

    public static string WriteDays(IEnumerable<NotificationDay> days) =>
        JsonSerializer.Serialize(days.OrderBy(d => d.Day), Json);

    public static List<NotificationDay> ReadDays(string json) =>
        JsonSerializer.Deserialize<List<NotificationDay>>(json, Json) ?? [];

    public static bool TryParseTimeZone(string? id, out TimeZoneInfo zone)
    {
        zone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    /// <summary>Reads "HH:mm" when it is on a quarter hour.</summary>
    public static bool TryParseNotifyAt(string? text, out TimeOnly time)
    {
        time = default;
        return text is { Length: 5 }
            && TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time)
            && time.Minute % 15 == 0;
    }

    public static DateOnly LocalDay(DateTimeOffset moment, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, zone).DateTime);

    /// <summary>A problem with the uploaded days, or null. Days run from yesterday to 31 days ahead, in the person's zone.</summary>
    public static string? DaysProblem(IReadOnlyList<NotificationDay>? days, TimeZoneInfo zone, DateTimeOffset now)
    {
        if (days is null)
            return "days is required.";
        if (days.Count > MaxDays)
            return $"At most {MaxDays} days can be sent.";

        var today = LocalDay(now, zone);
        foreach (var day in days)
        {
            if (day.Day < today.AddDays(-1) || day.Day > today.AddDays(MaxDays))
                return "A day is outside the range from yesterday to 31 days ahead.";
            if (day.Text is null || day.Text.Length > MaxTextLength)
                return $"A text can be at most {MaxTextLength} characters.";
        }

        if (days.Select(d => d.Day).Distinct().Count() != days.Count)
            return "Two entries are for the same day.";

        return null;
    }

    /// <summary>
    /// When the next day with text is due: the first one after the day last sent and not before the
    /// current local day, at the time of day in the person's zone. Null when none is left.
    /// </summary>
    public static DateTimeOffset? NextSendAt(
        string timeZone, TimeOnly notifyAt, IEnumerable<NotificationDay> days, DateOnly? lastSentOn, DateTimeOffset now)
    {
        if (!TryParseTimeZone(timeZone, out var zone))
            return null;

        var today = LocalDay(now, zone);
        var next = days
            .Where(d => !string.IsNullOrWhiteSpace(d.Text) && d.Day >= today && (lastSentOn is null || d.Day > lastSentOn))
            .OrderBy(d => d.Day)
            .Select(d => (DateOnly?)d.Day)
            .FirstOrDefault();

        return next is { } day ? At(day, notifyAt, zone) : null;
    }

    /// <summary>A local time as a moment. A time that doesn't exist because the clocks jump forward moves on an hour.</summary>
    public static DateTimeOffset At(DateOnly day, TimeOnly time, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
            local = local.AddHours(1);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }
}
