using System.Text.Json;
using Stikling.Core.Models;

namespace Stikling.Core.Today;

/// <summary>
/// Things on Today put off for a while without logging anything, each under a key with the day
/// it comes back. They belong to the device rather than the collection, so they are kept with
/// its own settings and not in backups.
/// </summary>
public sealed class PutOffs
{
    /// <summary>The choices offered: tomorrow, in 3 days and in a week.</summary>
    public static readonly IReadOnlyList<int> Days = [1, 3, 7];

    /// <summary>The key for the monthly photo reminder as a whole.</summary>
    public const string PhotoRound = "photos";

    private readonly Dictionary<string, DateOnly> until;

    private PutOffs(Dictionary<string, DateOnly> until) => this.until = until;

    public static PutOffs Empty() => new([]);

    /// <summary>The key for a propagation waiting to be looked at.</summary>
    public static string Check(Guid propagationId) => $"check:{propagationId}";

    /// <summary>
    /// The key for a flag. The day it was raised is part of it, so a flag cleared and raised
    /// again later isn't still hidden.
    /// </summary>
    public static string Flag(Guid subjectId, Attention attention) =>
        $"attention:{subjectId}:{attention.Since:yyyy-MM-dd}";

    public bool IsPutOff(string key, DateOnly today) =>
        until.TryGetValue(key, out var back) && today < back;

    /// <summary>Hides it until the given day, when it shows again.</summary>
    public void PutOff(string key, DateOnly back) => until[key] = back;

    /// <summary>
    /// Reads what was saved, keeping only what hasn't come back yet so the list doesn't grow.
    /// Anything unreadable starts over empty, since losing a put-off only brings it back early.
    /// </summary>
    public static PutOffs Parse(string? json, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Empty();

        try
        {
            var saved = JsonSerializer.Deserialize<Dictionary<string, DateOnly>>(json) ?? [];
            return new(saved.Where(p => p.Value > today).ToDictionary(p => p.Key, p => p.Value));
        }
        catch (JsonException)
        {
            return Empty();
        }
    }

    public string ToJson() => JsonSerializer.Serialize(until);
}
