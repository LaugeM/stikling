using System.Globalization;
using Stikling.Core.Models;

namespace Stikling.Web.Services;

/// <summary>Human-readable text for enum values and dates shown in the UI.</summary>
public static class Labels
{
    public static string For(PlantStatus status) => status switch
    {
        PlantStatus.Active => "In collection",
        PlantStatus.Died => "Died",
        PlantStatus.GivenAway => "Given away",
        PlantStatus.Sold => "Sold",
        _ => status.ToString()
    };

    public static string For(PlantOrigin origin) => origin switch
    {
        PlantOrigin.Purchased => "Purchased",
        PlantOrigin.Propagated => "Propagated",
        PlantOrigin.GrownFromSeed => "Grown from seed",
        PlantOrigin.Gift => "Gift",
        PlantOrigin.Swap => "Swap",
        PlantOrigin.Unknown => "Unknown",
        _ => origin.ToString()
    };

    public static string For(GrowingMedium medium) => medium switch
    {
        GrowingMedium.Leca => "LECA",
        GrowingMedium.Pon => "PON",
        GrowingMedium.Sphagnum => "Sphagnum moss",
        _ => medium.ToString()
    };

    public static string For(PropagationType type) => type switch
    {
        PropagationType.Offset => "Offset / pup",
        PropagationType.AirLayer => "Air layer",
        _ => type.ToString()
    };

    /// <summary>Stage names. Seeds get their own words: sown, germinating, sprouted.</summary>
    public static string Stage(PropagationStage stage, PropagationType type = PropagationType.Cutting) =>
        (stage, type == PropagationType.Seed) switch
        {
            (PropagationStage.Started, true) => "Sown",
            (PropagationStage.Rooting, true) => "Germinating",
            (PropagationStage.Rooted, true) => "Sprouted",
            _ => stage.ToString()
        };

    /// <summary>Any of the app's enums, for code that works with several (e.g. history text).</summary>
    public static string For(Enum value) => value switch
    {
        PlantStatus s => For(s),
        PlantOrigin o => For(o),
        GrowingMedium m => For(m),
        PropagationType t => For(t),
        PropagationStage s => Stage(s),
        _ => value.ToString()
    };

    /// <summary>Like <see cref="For(Enum)"/>, with stage names that fit the propagation's type.</summary>
    public static Func<Enum, string> For(Propagation propagation) =>
        value => value is PropagationStage stage ? Stage(stage, propagation.Type) : For(value);

    /// <summary>"3× corm · LECA".</summary>
    public static string Summary(Propagation propagation) =>
        $"{propagation.InitialCount}× {For(propagation.Type).ToLowerInvariant()} · {For(propagation.Medium)}";

    /// <summary>"started today" / "day 24".</summary>
    public static string Age(Propagation propagation, DateOnly today) =>
        propagation.DaysSinceStart(today) switch
        {
            0 => "started today",
            var days => $"day {days}"
        };

    /// <summary>"12.4 MB", for the storage line in Settings.</summary>
    public static string Bytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:0.#} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024):0.#} MB",
        _ => $"{bytes / (1024d * 1024 * 1024):0.##} GB"
    };

    /// <summary>"today", "yesterday", "12 days ago".</summary>
    public static string DaysAgo(int days) => days switch
    {
        <= 0 => "today",
        1 => "yesterday",
        _ => $"{days} days ago"
    };

    /// <summary>"1 plant" / "3 plants".</summary>
    public static string Plants(int count) => Count(count, "plant");

    /// <summary>"1 propagation" / "3 propagations". Plural is the singular plus "s" unless given.</summary>
    public static string Count(int count, string singular, string? plural = null) =>
        count == 1 ? $"1 {singular}" : $"{count} {plural ?? singular + "s"}";

    public static string Time(DateTimeOffset moment) =>
        moment.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    // Invariant culture gives "19 Sep 2026" (en-GB would write "Sept")
    public static string Date(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
}
