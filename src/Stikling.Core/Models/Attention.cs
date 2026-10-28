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

/// <summary>
/// The day a flag starts to show on Today, for a flag set "for later". It only counts while
/// <paramref name="FlagSince"/> equals the current flag's <see cref="Attention.Since"/>, so a
/// stale one left by an older version (which drops it when it clears a flag) never hides a new
/// flag. <paramref name="From"/> is a month or a day, and a month starts on its 1st.
/// </summary>
public sealed record AttentionLater(DateOnly FlagSince, LooseDate From)
{
    /// <summary>The day the flag starts to show, or null when it has no start.</summary>
    public static DateOnly? StartsOn(Attention? flag, AttentionLater? later) =>
        flag is not null && later is not null && later.FlagSince == flag.Since ? later.From.Start : null;

    /// <summary>The month or day the flag starts to show, or null when it has no start.</summary>
    public static LooseDate? FromOf(Attention? flag, AttentionLater? later) =>
        flag is not null && later is not null && later.FlagSince == flag.Since ? later.From : null;

    /// <summary>True while the flag has a start that hasn't come yet.</summary>
    public static bool IsWaiting(Attention? flag, AttentionLater? later, DateOnly today) =>
        StartsOn(flag, later) is { } start && start > today;

    /// <summary>The later to save for a flag: null when there is no start or no flag.</summary>
    public static AttentionLater? For(Attention? flag, LooseDate? from) =>
        flag is not null && from is not null ? new(flag.Since, from) : null;
}
