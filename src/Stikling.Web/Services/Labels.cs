using System.Globalization;
using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Pots;
using Stikling.Core.SoilMixes;

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

    public static string For(LightLevel light) => light switch
    {
        LightLevel.Low => "Low light",
        LightLevel.Medium => "Medium light",
        LightLevel.BrightIndirect => "Bright indirect",
        LightLevel.DirectSun => "Some direct sun",
        _ => light.ToString()
    };

    public static string For(GrowingMedium medium) => medium switch
    {
        GrowingMedium.Leca => "LECA",
        GrowingMedium.Pon => "PON",
        GrowingMedium.Sphagnum => "Sphagnum moss",
        GrowingMedium.CormRiser => "Corm riser",
        _ => medium.ToString()
    };

    public static string For(RootingAid aid) => aid switch
    {
        RootingAid.RootingPowder => "Rooting powder",
        RootingAid.RootingGel => "Rooting gel",
        RootingAid.WillowWater => "Willow water",
        RootingAid.HumidityDome => "Humidity dome or bag",
        RootingAid.HeatMat => "Heat mat",
        _ => aid.ToString()
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

    /// <summary>"All plants", "Living room" or "4 plants", for a case's one-line summary.</summary>
    public static string Scope(PestCaseView view) => view.Case.Scope switch
    {
        PestScope.Everywhere => "All plants",
        PestScope.Room => view.Room ?? "A room",
        _ => Plants(view.Plants.Count)
    };

    /// <summary>"Living room · 4 plants", or just "2 plants" when the case covers picked plants.</summary>
    public static string ScopeAndCount(PestCaseView view) =>
        view.Case.Scope == PestScope.PickedPlants
            ? Plants(view.Plants.Count)
            : $"{Scope(view)} · {Plants(view.Plants.Count)}";

    /// <summary>"due today", "2 days overdue", "next in 3 days", or "check due today" and so on for a check.</summary>
    public static string Due(int daysUntil, bool check = false)
    {
        var what = check ? "check " : "";
        return daysUntil switch
        {
            0 => $"{what}due today",
            -1 => $"{what}1 day overdue",
            < 0 => $"{what}{-daysUntil} days overdue",
            1 => $"next {what}tomorrow",
            _ => $"next {what}in {daysUntil} days"
        };
    }

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
        LightLevel l => For(l),
        RootingAid a => For(a),
        CareKind c => For(c),
        PropagationType t => For(t),
        PropagationStage s => Stage(s),
        Pest p => For(p),
        PestCaseStatus s => For(s),
        PotGroup g => For(g),
        PotMaterial m => For(m),
        PotWatering w => For(w),
        MixUnit u => For(u),
        DoseUnit u => For(u),
        ProductKind k => For(k),
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
    public static string Use(PotUse use)
    {
        var here = use switch
        {
            { Here: 0 } => null,
            { InUse: 0 } => use.Here == 1 ? "free" : $"all {use.Here} free",
            { Free: 0 } => use.Here == 1 ? "in use" : $"all {use.Here} in use",
            _ => $"{use.InUse} of {use.Here} in use, {use.Free} free"
        };
        var gone = use.Gone switch
        {
            0 => null,
            1 => "1 went with a plant",
            var n => $"{n} went with plants"
        };
        return string.Join(", ", new[] { here, gone }.OfType<string>());
    }

    /// <summary>
    /// The answers to "what went with it" that fit the plants' pots: always just the plant, then
    /// its pot, then both when there is an outer pot too. Empty when none of them has a pot.
    /// </summary>
    public static IReadOnlyList<(PotsTaken Value, string Label)> PotsTakenChoices(IReadOnlyCollection<Plant> plants)
    {
        var inner = plants.Any(p => p.InnerPotId is not null);
        var outer = plants.Any(p => p.OuterPotId is not null);
        var one = plants.Count == 1;

        if (!inner && !outer)
            return [];

        var choices = new List<(PotsTaken, string)> { (PotsTaken.None, one ? "Just the plant" : "Just the plants") };
        if (inner)
            choices.Add((PotsTaken.Inner, one ? "With its pot" : "With their pots"));
        if (outer)
            choices.Add((PotsTaken.All, (one, inner) switch
            {
                (true, true) => "With both pots",
                (true, false) => "With its outer pot",
                _ => "With outer pots too"
            }));
        return choices;
    }

    public static string For(MixUnit unit) => unit switch
    {
        MixUnit.Parts => "Parts",
        MixUnit.Percent => "Percent",
        _ => "No amounts"
    };

    public static string For(DoseUnit unit) => Doses.Symbol(unit);

    public static string For(ProductKind kind) => kind switch
    {
        ProductKind.Stimulant => "Rooting or growth stimulant",
        ProductKind.CalMag => "Cal-mag",
        ProductKind.Ph => "pH up or down",
        ProductKind.Other => "Something else",
        _ => kind.ToString()
    };

    /// <summary>Said under the ingredients when the percentages do not reach 100. Never blocks a save.</summary>
    public static string PercentTotal(decimal total) =>
        $"That adds up to {SoilMix.Number(total)}%, not 100%. You can still save it.";
}
