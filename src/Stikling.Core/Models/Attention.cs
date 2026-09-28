namespace Stikling.Core.Models;

/// <summary>
/// Something a plant or propagation needs, e.g. "Repot soon" or "Change the water". It stays on
/// Today until it's cleared. The record holds null when nothing is flagged, the same way as
/// dormancy, so there is no flag that can disagree with the reason.
/// </summary>
public sealed record Attention(string Reason, DateOnly Since)
{
    /// <summary>Days since it was flagged, counting that day as day 0.</summary>
    public int DaysSince(DateOnly today) => Math.Max(0, today.DayNumber - Since.DayNumber);

    /// <summary>
    /// The flag after giving it a reason, or null when the reason is empty. A flag that is only
    /// reworded keeps the day it was first raised.
    /// </summary>
    public static Attention? Change(Attention? current, string? reason, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return null;

        var trimmed = reason.Trim();
        return current is null ? new(trimmed, today) : current with { Reason = trimmed };
    }
}
