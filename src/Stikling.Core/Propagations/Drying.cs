namespace Stikling.Core.Propagations;

/// <summary>How long a cut end dried before it went in, stored as hours and entered as hours or days.</summary>
public static class Drying
{
    public const int HoursPerDay = 24;

    /// <summary>The most that can be entered, in hours or days.</summary>
    public const int MaxAmount = 999;

    /// <summary>The hours to store, or null when nothing was entered or it isn't more than nothing.</summary>
    public static int? ToHours(int? amount, bool inDays) =>
        amount is > 0 and <= MaxAmount ? amount * (inDays ? HoursPerDay : 1) : null;

    /// <summary>What to show when editing: days when it divides evenly by 24 and is at least 24, else hours.</summary>
    public static (int Amount, bool InDays) Split(int hours) =>
        hours >= HoursPerDay && hours % HoursPerDay == 0 ? (hours / HoursPerDay, true) : (hours, false);

    /// <summary>"2 days" or "6 hours".</summary>
    public static string Text(int hours)
    {
        var (amount, inDays) = Split(hours);
        var unit = inDays ? "day" : "hour";
        return $"{amount} {unit}{(amount == 1 ? "" : "s")}";
    }
}
