using Stikling.Core.Models;
using Stikling.Core.Propagations;

namespace Stikling.Core.Sharing;

/// <summary>The one big number on a share card, with the words under it: 18 and "days to root".</summary>
public sealed record ShareFigure(int Value, string Caption);

/// <summary>
/// What a share card says about a plant or a propagation, written from what the app keeps and
/// nothing else. A part the app has nothing true to say about is left out, and the person can
/// change every part before sharing. Nothing here is saved.
/// </summary>
/// <param name="Name">The nickname, or the botanical name when there is no nickname.</param>
/// <param name="Latin">Genus and species, set in italic under the name. Null when the name already is the botanical name.</param>
/// <param name="Cultivar">The cultivar without its quotes, set upright after <paramref name="Latin"/>.</param>
/// <param name="Line">The plain sentence about it, or null when there is nothing true to say.</param>
/// <param name="Figure">The number the card is built around when it has no photo, or null when there is none.</param>
public sealed record ShareCardText(string Name, string? Latin, string? Cultivar, string? Line, ShareFigure? Figure)
{
    /// <summary>
    /// "Cutting from my Monstera, rooted in 18 days in LECA." A batch adds how many were potted up.
    /// </summary>
    /// <param name="parent">The plant it was taken from, when it has one that isn't deleted.</param>
    public static ShareCardText ForPropagation(Propagation propagation, Plant? parent, DateOnly today)
    {
        var (latin, cultivar) = Botanical(propagation.Nickname, propagation.Genus, propagation.Species, propagation.Cultivar);
        return new ShareCardText(propagation.DisplayName, latin, cultivar, PropagationLine(propagation, parent, today), PropagationFigure(propagation, today));
    }

    /// <summary>
    /// What a plant came from and how it started: "Propagated from my Monstera, rooted in 18 days in
    /// LECA. With me since June 2024."
    /// </summary>
    /// <param name="parent">The plant it was propagated from.</param>
    /// <param name="from">The propagation it was potted up from.</param>
    /// <param name="taken">The propagations taken from this plant.</param>
    public static ShareCardText ForPlant(Plant plant, Plant? parent, Propagation? from, IReadOnlyCollection<Propagation> taken, DateOnly today)
    {
        var (latin, cultivar) = Botanical(plant.Nickname, plant.Genus, plant.Species, plant.Cultivar);
        return new ShareCardText(plant.DisplayName, latin, cultivar, PlantLine(plant, parent, from, today), PlantFigure(plant, from, taken, today));
    }

    // The botanical name goes under the name, unless it is the name already
    private static (string? Latin, string? Cultivar) Botanical(string? nickname, string? genus, string? species, string? cultivar)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return (null, null);
        var latin = string.Join(' ', new[] { PlantNames.Genus(genus), PlantNames.Species(species) }.OfType<string>());
        return (latin.Length > 0 ? latin : null, PlantNames.Cultivar(cultivar));
    }

    private static string? PropagationLine(Propagation p, Plant? parent, DateOnly today)
    {
        var seed = p.Type == PropagationType.Seed;
        var origin = parent is null ? TypeWord(p.Type) : $"{TypeWord(p.Type)} from my {parent.DisplayName}";
        var medium = MediumPhrase(p.Medium);
        var sign = PropagationOutcome.DaysToFirstSign(p);
        var ran = p.FinishedOn is { } end ? Math.Max(0, end.DayNumber - p.StartedOn.DayNumber) : (int?)null;
        var ranText = ran is { } n ? $" after {Days(n)}" : "";

        string what;
        if (p.IsActive)
        {
            // A first root that was noted says "first roots on day 18", while a propagation that only
            // has the day it reached Rooted can say no more than that it rooted in that time
            var first = seed ? p.FirstGerminatedOn : p.FirstRootOn;
            var started = p.DaysSinceStart(today);
            if (first is { } day)
                what = $"{(seed ? "first seedling" : "first roots")} on day {Math.Max(0, day.DayNumber - p.StartedOn.DayNumber)}{medium}";
            else if (sign is { } rooted)
                what = $"{(seed ? "came up" : "rooted")} in {Days(rooted)}{medium}";
            else
                what = (started == 0 ? "started today" : $"day {started}") + medium;
        }
        else if (sign is { } d)
        {
            what = $"{(seed ? "came up" : "rooted")} in {Days(d)}{medium}";
            if (p.PottedUpCount > 0 && p.InitialCount > 1)
                what += $". {p.PottedUpCount} of {p.InitialCount} potted up";
            else if (p.Stage == PropagationStage.Failed)
                what += ". It didn't make it";
        }
        else if (p.Stage == PropagationStage.Failed)
            what = "didn't make it" + ranText + medium;
        else
            what = "potted up" + ranText + medium;

        return $"{origin}, {what}.";
    }

    private static ShareFigure? PropagationFigure(Propagation p, DateOnly today)
    {
        var seed = p.Type == PropagationType.Seed;
        if (PropagationOutcome.DaysToFirstSign(p) is { } sign)
            return Counted(sign, seed ? "to come up" : "to root");

        if (!p.IsActive)
        {
            if (p.FinishedOn is not { } end)
                return null;
            return Counted(Math.Max(0, end.DayNumber - p.StartedOn.DayNumber), p.Stage == PropagationStage.Done ? "to pot up" : "it ran");
        }

        var started = p.DaysSinceStart(today);
        return started == 0 ? null : Counted(started, "so far");
    }

    private static string? PlantLine(Plant plant, Plant? parent, Propagation? from, DateOnly today)
    {
        var origin = parent is not null ? $"Propagated from my {parent.DisplayName}"
            : plant.Origin switch
            {
                PlantOrigin.GrownFromSeed => "Grown from seed",
                PlantOrigin.Gift => "A gift",
                PlantOrigin.Swap => "From a swap",
                _ => null
            };

        string? rooted = null;
        if (from is not null && PropagationOutcome.DaysToFirstSign(from) is { } days)
            rooted = $"{(from.Type == PropagationType.Seed ? "came up" : "rooted")} in {Days(days)}{MediumPhrase(from.Medium)}";

        var first = origin is null ? Capital(rooted) : rooted is null ? origin : $"{origin}, {rooted}";

        // Only a plant that is still here has been "with me" until now, since the day it left isn't kept
        var since = plant.Status == PlantStatus.Active && plant.AcquiredOn is { } acquired ? $"With me since {acquired.Text()}" : null;

        var sentences = new[] { first, since }.OfType<string>().Select(s => s + ".").ToList();
        return sentences.Count == 0 ? null : string.Join(' ', sentences);
    }

    private static ShareFigure? PlantFigure(Plant plant, Propagation? from, IReadOnlyCollection<Propagation> taken, DateOnly today)
    {
        if (taken.Count > 0)
            return new ShareFigure(taken.Count, taken.Count == 1 ? "propagation" : "propagations");

        if (from is not null && PropagationOutcome.DaysToFirstSign(from) is { } sign)
            return Counted(sign, from.Type == PropagationType.Seed ? "to come up" : "to root");

        // Only a day that was entered whole can be counted from; a month or a year would be a guess
        if (plant.Status == PlantStatus.Active && plant.AcquiredOn is { Precision: DatePrecision.Day } acquired)
        {
            var date = acquired.Start;
            var days = today.DayNumber - date.DayNumber;
            if (days < 0)
                return null;
            var months = (today.Year - date.Year) * 12 + today.Month - date.Month - (today.Day < date.Day ? 1 : 0);
            return days < 60 ? Counted(days, "with me", unit: "day")
                : months < 24 ? Counted(months, "with me", unit: "month")
                : Counted(months / 12, "with me", unit: "year");
        }

        return null;
    }

    // "18" and "days to root"
    private static ShareFigure Counted(int value, string rest, string unit = "day") =>
        new(value, $"{(value == 1 ? unit : unit + "s")} {rest}");

    private static string Days(int days) => days == 1 ? "1 day" : $"{days} days";

    private static string? Capital(string? text) =>
        string.IsNullOrEmpty(text) ? null : char.ToUpperInvariant(text[0]) + text[1..];

    private static string TypeWord(PropagationType type) => type switch
    {
        PropagationType.Cutting => "Cutting",
        PropagationType.Corm => "Corm",
        PropagationType.Offset => "Offset",
        PropagationType.Seed => "Seeds",
        PropagationType.Division => "Division",
        PropagationType.AirLayer => "Air layer",
        PropagationType.Leaf => "Leaf cutting",
        _ => "Propagation"
    };

    // " in water", with a leading space so a medium the app can't name leaves nothing behind
    private static string MediumPhrase(GrowingMedium medium) => medium switch
    {
        GrowingMedium.Soil => " in soil",
        GrowingMedium.OrchidBark => " in orchid bark",
        GrowingMedium.Leca => " in LECA",
        GrowingMedium.Pon => " in PON",
        GrowingMedium.Perlite => " in perlite",
        GrowingMedium.Sphagnum => " in sphagnum moss",
        GrowingMedium.Water => " in water",
        GrowingMedium.CormRiser => " on a corm riser",
        _ => ""
    };
}
