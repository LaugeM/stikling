using System.Globalization;
using Stikling.Core.Models;
using Stikling.Core.Pots;

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
        GrowingMedium.CormRiser => "Corm riser",
        _ => medium.ToString()
    };

    public static string For(CareKind kind) => kind switch
    {
        CareKind.ToppedUp => "Topped up",
        CareKind.LeavesCleaned => "Leaves cleaned",
        CareKind.MoistureReading => "Moisture reading",
        _ => kind.ToString()
    };

    public static string For(PropagationType type) => type switch
    {
        PropagationType.Offset => "Offset / pup",
        PropagationType.AirLayer => "Air layer",
        _ => type.ToString()
    };

    public static string For(Pest pest) => pest switch
    {
        Pest.SpiderMites => "Spider mites",
        Pest.FungusGnats => "Fungus gnats",
        _ => pest.ToString()
    };

    public static string For(PestCaseStatus status) => status switch
    {
        PestCaseStatus.Active => "Treating",
        PestCaseStatus.Monitoring => "Watching",
        PestCaseStatus.Resolved => "Resolved",
        _ => status.ToString()
    };

    /// <summary>"Everywhere", "Living room" or "4 plants", for a case's one-line summary.</summary>
    public static string Scope(PestCase item, int plantCount) => item.Scope switch
    {
        PestScope.Everywhere => "Everywhere",
        PestScope.Room => item.Room ?? "A room",
        _ => Plants(plantCount)
    };

    /// <summary>"due today", "2 days overdue", "next in 3 days".</summary>
    public static string Due(int daysUntil) => daysUntil switch
    {
        0 => "due today",
        -1 => "1 day overdue",
        < 0 => $"{-daysUntil} days overdue",
        1 => "next tomorrow",
        _ => $"next in {daysUntil} days"
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
        CareKind c => For(c),
        PropagationType t => For(t),
        PropagationStage s => Stage(s),
        Pest p => For(p),
        PestCaseStatus s => For(s),
        PotGroup g => For(g),
        PotMaterial m => For(m),
        PotWatering w => For(w),
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

    /// <summary>"2024", "June 2024" or "12 Jun 2024", depending on how much is known.</summary>
    public static string Date(LooseDate date) => date.Text();

    public static string For(PotGroup group) => group switch
    {
        PotGroup.Inner => "Nursery pot",
        PotGroup.Outer => "Outer pot",
        _ => "Stands on its own"
    };

    /// <summary>The heading over a group of pots in the library.</summary>
    public static string Heading(PotGroup group) => group switch
    {
        PotGroup.Inner => "Nursery pots",
        PotGroup.Outer => "Outer pots",
        _ => "Pots that stand on their own"
    };

    public static string For(PotMaterial material) => material switch
    {
        PotMaterial.Terracotta => "Terracotta",
        PotMaterial.Ceramic => "Ceramic",
        PotMaterial.Glass => "Glass",
        PotMaterial.Metal => "Metal",
        PotMaterial.Other => "Other",
        _ => "Plastic"
    };

    public static string For(PotWatering watering) =>
        watering == PotWatering.Wick ? "Wick" : "Submerged";

    /// <summary>"2 of 3 in use, 1 free", so the library says what is still available.</summary>
    public static string Use(PotUse use) => use switch
    {
        { InUse: 0 } => use.Owned == 1 ? "free" : $"all {use.Owned} free",
        { Free: 0 } => use.Owned == 1 ? "in use" : $"all {use.Owned} in use",
        _ => $"{use.InUse} of {use.Owned} in use, {use.Free} free"
    };
}
