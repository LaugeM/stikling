using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Stikling.Core.Calendar;
using CoreFeed = Stikling.Core.Calendar.CalendarFeed;

namespace Stikling.Api.Calendar;

/// <summary>The rules for a person's calendar link, apart from the database.</summary>
public static class CalendarRules
{
    public const int MaxDays = 31;
    public const int TokenLength = 43;

    /// <summary>Days older than this are not written to the feed.</summary>
    public const int KeepBackDays = 7;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>32 random bytes as base64url without padding.</summary>
    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static bool IsWellFormed(string? token) =>
        token is { Length: TokenLength } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public static string WriteDays(IEnumerable<CalendarDay> days) =>
        JsonSerializer.Serialize(days.OrderBy(d => d.Day), Json);

    public static List<CalendarDay> ReadDays(string json) =>
        JsonSerializer.Deserialize<List<CalendarDay>>(json, Json) ?? [];

    /// <summary>A problem with the uploaded days, or null. Days run from 2 days ago to 32 days ahead, in UTC.</summary>
    public static string? DaysProblem(IReadOnlyList<CalendarDay>? days, DateTimeOffset now)
    {
        if (days is null)
            return "days is required.";
        if (days.Count > MaxDays)
            return $"At most {MaxDays} days can be sent.";

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        foreach (var day in days)
        {
            if (day is null)
                return "A day is missing.";
            if (day.Day < today.AddDays(-2) || day.Day > today.AddDays(MaxDays + 1))
                return "A day is outside the range from 2 days ago to 32 days ahead.";
            if (string.IsNullOrEmpty(day.Title) || day.Title.Length > CoreFeed.MaxTitleLength)
                return $"A title must be 1 to {CoreFeed.MaxTitleLength} characters.";
            if (day.Description is { } description && description.Length > CoreFeed.MaxDescriptionLength)
                return $"A description can be at most {CoreFeed.MaxDescriptionLength} characters.";
        }

        if (days.Select(d => d.Day).Distinct().Count() != days.Count)
            return "Two entries are for the same day.";

        return null;
    }
}
