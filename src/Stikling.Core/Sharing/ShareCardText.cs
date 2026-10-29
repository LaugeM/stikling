using System.Globalization;
using Stikling.Core.Models;
using Stikling.Core.Propagations;
using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;

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
        var (latin, cultivar) = Botanical(propagation.Nickname, propagation.Genus, propagation.Species, propagation.Cultivar, propagation.NameQualifier, propagation.NameNote);
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
        var (latin, cultivar) = Botanical(plant.Nickname, plant.Genus, plant.Species, plant.Cultivar, plant.NameQualifier, plant.NameNote);
        return new ShareCardText(plant.DisplayName, latin, cultivar, PlantLine(plant, parent, from, today), PlantFigure(plant, from, taken, today));
    }

    // The botanical name goes under the name, unless it is the name already
    private static (string? Latin, string? Cultivar) Botanical(string? nickname, string? genus, string? species, string? cultivar,
        NameQualifier? qualifier, string? note)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return (null, null);
        // The note ends the botanical name, so it is set with the Latin part
        var latin = PlantNames.Botanical(genus, species, null, qualifier, note) ?? "";
        return (latin.Length > 0 ? latin : null, PlantNames.Cultivar(cultivar));
    }

    private static string? PropagationLine(Propagation p, Plant? parent, DateOnly today)
    {
        var seed = p.Type == PropagationType.Seed;
        var origin = parent is null ? TypeWord(p.Type) : Format(Strings.Propagations.ShareTypeFromMy, TypeWord(p.Type), parent.DisplayName);
        var medium = MediumPhrase(p.Medium);
        var sign = PropagationOutcome.DaysToFirstSign(p);
        var ran = p.FinishedOn is { } end ? Math.Max(0, end.DayNumber - p.StartedOn.DayNumber) : (int?)null;

        string what;
        if (p.IsActive)
        {
            // A first root that was noted says "first roots on day 18", while a propagation that only
            // has the day it reached Rooted can say no more than that it rooted in that time
            var first = seed ? p.FirstGerminatedOn : p.FirstRootOn;
            var started = p.DaysSinceStart(today);
            if (first is { } day)
                what = Format(seed ? Strings.Propagations.ShareFirstSeedling : Strings.Propagations.ShareFirstRoots, Math.Max(0, day.DayNumber - p.StartedOn.DayNumber), medium);
            else if (sign is { } rooted)
                what = Format(seed ? Strings.Propagations.ShareCameUpIn : Strings.Propagations.ShareRootedIn, Days(rooted), medium);
            else
                what = started == 0 ? Format(Strings.Propagations.ShareStartedToday, medium) : Format(Strings.Propagations.ShareDayN, started, medium);
        }
        else if (sign is { } d)
        {
            what = Format(seed ? Strings.Propagations.ShareCameUpIn : Strings.Propagations.ShareRootedIn, Days(d), medium);
            if (p.PottedUpCount > 0 && p.InitialCount > 1)
                what += Format(Strings.Propagations.ShareSomePottedUp, p.PottedUpCount, p.InitialCount);
            else if (p.Stage == PropagationStage.Failed)
                what += Strings.Propagations.ShareItDidntMakeIt;
        }
        else if (p.Stage == PropagationStage.Failed)
            what = ran is { } n ? Format(Strings.Propagations.ShareDidntMakeItAfter, Days(n), medium) : Format(Strings.Propagations.ShareDidntMakeIt, medium);
        else
            what = ran is { } n2 ? Format(Strings.Propagations.SharePottedUpAfter, Days(n2), medium) : Format(Strings.Propagations.SharePottedUp, medium);

        return Format(Strings.Propagations.ShareLine, origin, what);
    }

    private static ShareFigure? PropagationFigure(Propagation p, DateOnly today)
    {
        var seed = p.Type == PropagationType.Seed;
        if (PropagationOutcome.DaysToFirstSign(p) is { } sign)
            return seed ? Counted(sign, Strings.Propagations.FigureToComeUp_One, Strings.Propagations.FigureToComeUp_Other)
                : Counted(sign, Strings.Propagations.FigureToRoot_One, Strings.Propagations.FigureToRoot_Other);

        if (!p.IsActive)
        {
            if (p.FinishedOn is not { } end)
                return null;
            var ranDays = Math.Max(0, end.DayNumber - p.StartedOn.DayNumber);
            return p.Stage == PropagationStage.Done
                ? Counted(ranDays, Strings.Propagations.FigureToPotUp_One, Strings.Propagations.FigureToPotUp_Other)
                : Counted(ranDays, Strings.Propagations.FigureItRan_One, Strings.Propagations.FigureItRan_Other);
        }

        var started = p.DaysSinceStart(today);
        return started == 0 ? null : Counted(started, Strings.Propagations.FigureSoFar_One, Strings.Propagations.FigureSoFar_Other);
    }

    private static string? PlantLine(Plant plant, Plant? parent, Propagation? from, DateOnly today)
    {
        var origin = parent is not null ? Format(Strings.Propagations.SharePropagatedFromMy, parent.DisplayName)
            : plant.Origin switch
            {
                PlantOrigin.GrownFromSeed => Strings.Propagations.ShareGrownFromSeed,
                PlantOrigin.Gift => Strings.Propagations.ShareAGift,
                PlantOrigin.Swap => Strings.Propagations.ShareFromASwap,
                _ => null
            };

        string? rooted = null;
        if (from is not null && PropagationOutcome.DaysToFirstSign(from) is { } days)
            rooted = Format(from.Type == PropagationType.Seed ? Strings.Propagations.ShareCameUpIn : Strings.Propagations.ShareRootedIn, Days(days), MediumPhrase(from.Medium));

        var first = origin is null ? Capital(rooted) : rooted is null ? origin : Format(Strings.Propagations.ShareJoin, origin, rooted);

        // Only a plant that is still here has been "with me" until now, since the day it left isn't kept
        var since = plant.InCollection && plant.AcquiredOn is { } acquired ? Format(Strings.Propagations.ShareWithMeSince, acquired.Text()) : null;

        var sentences = new[] { first, since }.OfType<string>().Select(s => s + ".").ToList();
        return sentences.Count == 0 ? null : string.Join(' ', sentences);
    }

    private static ShareFigure? PlantFigure(Plant plant, Propagation? from, IReadOnlyCollection<Propagation> taken, DateOnly today)
    {
        if (taken.Count > 0)
            return new ShareFigure(taken.Count, taken.Count == 1 ? Strings.Propagations.FigurePropagation_One : Strings.Propagations.FigurePropagation_Other);

        if (from is not null && PropagationOutcome.DaysToFirstSign(from) is { } sign)
            return from.Type == PropagationType.Seed
                ? Counted(sign, Strings.Propagations.FigureToComeUp_One, Strings.Propagations.FigureToComeUp_Other)
                : Counted(sign, Strings.Propagations.FigureToRoot_One, Strings.Propagations.FigureToRoot_Other);

        // Only a day that was entered whole can be counted from; a month or a year would be a guess
        if (plant.InCollection && plant.AcquiredOn is { Precision: DatePrecision.Day } acquired)
        {
            var date = acquired.Start;
            var days = today.DayNumber - date.DayNumber;
            if (days < 0)
                return null;
            var months = WholeMonths(date, today);
            return days < 60 ? Counted(days, Strings.Propagations.FigureDaysWithMe_One, Strings.Propagations.FigureDaysWithMe_Other)
                : months < 24 ? Counted(months, Strings.Propagations.FigureMonthsWithMe_One, Strings.Propagations.FigureMonthsWithMe_Other)
                : Counted(months / 12, Strings.Propagations.FigureYearsWithMe_One, Strings.Propagations.FigureYearsWithMe_Other);
        }

        return null;
    }

    private static int WholeMonths(DateOnly from, DateOnly to) =>
        (to.Year - from.Year) * 12 + to.Month - from.Month - (to.Day < from.Day ? 1 : 0);

    /// <summary>
    /// "46 days apart." under 60 days, "8 months apart." under 24 months, "2 years apart." after that.
    /// Null for the same day. The caller passes the older day first; a later <paramref name="before"/>
    /// is treated as the same day and gives null.
    /// </summary>
    public static string? ApartLine(DateOnly before, DateOnly after)
    {
        var days = after.DayNumber - before.DayNumber;
        if (days <= 0)
            return null;
        var months = WholeMonths(before, after);
        return days < 60 ? Plural.Of(days, Strings.Propagations.ApartDays_One, Strings.Propagations.ApartDays_Other)
            : months < 24 ? Plural.Of(months, Strings.Propagations.ApartMonths_One, Strings.Propagations.ApartMonths_Other)
            : Plural.Of(months / 12, Strings.Propagations.ApartYears_One, Strings.Propagations.ApartYears_Other);
    }

    // "18" and "days to root"
    private static ShareFigure Counted(int value, string one, string other) =>
        new(value, value == 1 ? one : other);

    private static string Days(int days) => Plural.Of(days, Strings.Propagations.ShareDays_One, Strings.Propagations.ShareDays_Other);

    private static string Format(string format, params object[] args) => string.Format(CultureInfo.CurrentCulture, format, args);

    private static string? Capital(string? text) =>
        string.IsNullOrEmpty(text) ? null : char.ToUpperInvariant(text[0]) + text[1..];

    private static string TypeWord(PropagationType type) => type switch
    {
        PropagationType.Cutting => Strings.Propagations.ShareTypeCutting,
        PropagationType.Corm => Strings.Propagations.ShareTypeCorm,
        PropagationType.Offset => Strings.Propagations.ShareTypeOffset,
        PropagationType.Seed => Strings.Propagations.ShareTypeSeed,
        PropagationType.Division => Strings.Propagations.ShareTypeDivision,
        PropagationType.AirLayer => Strings.Propagations.ShareTypeAirLayer,
        PropagationType.Leaf => Strings.Propagations.ShareTypeLeaf,
        _ => Strings.Propagations.ShareTypeOther
    };

    // " in water", with a leading space so a medium the app can't name leaves nothing behind
    internal static string MediumPhrase(GrowingMedium medium) => medium switch
    {
        GrowingMedium.Soil => Strings.Propagations.MediumInSoil,
        GrowingMedium.OrchidBark => Strings.Propagations.MediumInOrchidBark,
        GrowingMedium.Leca => Strings.Propagations.MediumInLeca,
        GrowingMedium.Pon => Strings.Propagations.MediumInPon,
        GrowingMedium.Perlite => Strings.Propagations.MediumInPerlite,
        GrowingMedium.Sphagnum => Strings.Propagations.MediumInSphagnum,
        GrowingMedium.Water => Strings.Propagations.MediumInWater,
        GrowingMedium.CormRiser => Strings.Propagations.MediumOnCormRiser,
        _ => ""
    };
}
