using System.Globalization;
using System.Text.Json.Serialization;
using Stikling.Core.Models;
using Stikling.Core.Propagations;
using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Year;

/// <summary>How a card is laid out when it is drawn.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<YearCardKind>))]
public enum YearCardKind
{
    Title,
    Number,
    Photo,
    Pair,
    Collage,
}

/// <summary>A number on a card with the words under it: "24" and "plants added".</summary>
public sealed record YearFigure(string Value, string Caption);

/// <summary>A photo to draw on a card, with the way it is framed.</summary>
public sealed record YearCardPhoto(Guid PhotoId, PhotoFrame? Frame);

/// <summary>
/// One card of "Your year with your plants", with the words on it. The drawing is in
/// wwwroot/js/yearcards.js. Nothing here is saved.
/// </summary>
/// <param name="Id">A short name for the card, also used in its file name: "plants-added".</param>
/// <param name="Tone">"plant" or "propagation", which sets the colour of a card without a photo.</param>
/// <param name="Collage">Every photo the collage could show, in order. The page cuts it to what fits.</param>
/// <param name="AltText">What the card says, in a sentence or two, for someone who can't see it.</param>
public sealed record YearCard(
    string Id,
    YearCardKind Kind,
    string Tone,
    string? Kicker,
    string Title,
    string? Latin,
    string? Cultivar,
    string? Line,
    IReadOnlyList<YearFigure> Figures,
    YearCardPhoto? Photo,
    YearCardPhoto? Before,
    YearCardPhoto? After,
    string? BeforeLabel,
    string? AfterLabel,
    IReadOnlyList<YearCardPhoto> Collage,
    string AltText);

/// <summary>
/// The cards of a year with the words on them, written from a <see cref="PlantYearSummary"/> and
/// nothing else. A card with nothing to say is left out.
/// </summary>
public static class YearCards
{
    private static readonly IReadOnlyList<YearFigure> NoFigures = [];
    private static readonly IReadOnlyList<YearCardPhoto> NoPhotos = [];

    /// <summary>The cards in order: title, plants added, propagations, quickest to root, medium, most photographed, grown, collage.</summary>
    /// <param name="label">The app's words for the enums, such as "LECA" and "Soil".</param>
    /// <param name="photos">The year's photos, only used to find how a photo on a card is framed.</param>
    /// <param name="localDay">The day a moment falls on where the person is. The device's own zone when not given.</param>
    public static IReadOnlyList<YearCard> For(
        PlantYearSummary summary,
        Func<Enum, string> label,
        IEnumerable<Photo>? photos = null,
        Func<DateTimeOffset, DateOnly>? localDay = null)
    {
        if (summary.IsEmpty)
            return [];

        var year = summary.Year;
        localDay ??= moment => DateOnly.FromDateTime(moment.ToLocalTime().DateTime);
        var frames = new Dictionary<Guid, PhotoFrame?>();
        foreach (var photo in photos ?? [])
            frames[photo.Id] = photo.Frame;
        YearCardPhoto? Shown(Guid id) => new(id, frames.GetValueOrDefault(id));
        YearCardPhoto OfPhoto(Photo photo) => new(photo.Id, photo.Frame);

        var cards = new List<YearCard> { Title(summary, Shown, OfPhoto) };

        if (summary.Added.Count > 0)
            cards.Add(PlantsAdded(summary));

        if (summary.Propagations.Started > 0)
            cards.Add(Propagations(summary));

        if (summary.Quickest is { } quickest)
            cards.Add(Quickest(quickest, label, Shown));

        if (summary.Medium is { } medium)
            cards.Add(Medium(year, medium, label));

        if (summary.MostPhotographed is { } most)
            cards.Add(Most(year, most, Shown));

        if (summary.Grown is { } grown)
            cards.Add(Grown(summary, grown, OfPhoto, localDay));

        if (summary.Collage.Count >= PlantYear.MinimumForCollage)
            cards.Add(Collage(summary, OfPhoto));

        return cards;
    }

    private static YearCard Title(PlantYearSummary summary, Func<Guid, YearCardPhoto?> shown, Func<Photo, YearCardPhoto> ofPhoto)
    {
        var figures = new List<YearFigure>();
        if (summary.Added.Count > 0)
            figures.Add(Figure(summary.Added.Count, Strings.Shared.YearFigureAdded_One, Strings.Shared.YearFigureAdded_Other));
        if (summary.Propagations.Rooted > 0)
            figures.Add(summary.Propagations.RootedAllCuttings
                ? Figure(summary.Propagations.Rooted, Strings.Shared.YearFigureCutting_One, Strings.Shared.YearFigureCutting_Other)
                : Figure(summary.Propagations.Rooted, Strings.Shared.YearFigurePropagation_One, Strings.Shared.YearFigurePropagation_Other));
        if (summary.PhotosTaken > 0)
            figures.Add(Figure(summary.PhotosTaken, Strings.Shared.YearFigurePhoto_One, Strings.Shared.YearFigurePhoto_Other));
        figures = figures.Take(3).ToList();

        var photo = summary.MostPhotographed is { } most ? shown(most.PhotoId)
            : summary.Collage.Count > 0 ? ofPhoto(summary.Collage[0].Photo)
            : null;

        var title = string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearTitle, summary.Year);
        var alt = figures.Count == 0
            ? $"{title}."
            : $"{title}. {string.Join(", ", figures.Select(f => $"{f.Value} {f.Caption}"))}.";
        return new YearCard("title", YearCardKind.Title, "propagation", null, title, null, null, null,
            figures, photo, null, null, null, null, NoPhotos, alt);
    }

    private static YearCard PlantsAdded(PlantYearSummary summary)
    {
        var kicker = string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearKickerAdded, summary.Year);
        var line = string.Join(" · ", summary.Added.ByOrigin.Select(o => Origin(o.Origin, o.Count)));
        var count = summary.Added.Count;
        return new YearCard("plants-added", YearCardKind.Number, "plant", kicker, kicker, null, null, line,
            [Figure(count, Strings.Shared.YearFigureAdded_One, Strings.Shared.YearFigureAdded_Other)], null, null, null, null, null, NoPhotos,
            $"{kicker}: {count}. {string.Join(", ", summary.Added.ByOrigin.Select(o => Origin(o.Origin, o.Count)))}.");
    }

    private static string Origin(PlantOrigin origin, int count) => origin switch
    {
        PlantOrigin.Purchased => string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearOriginBought, count),
        PlantOrigin.Propagated => string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearOriginPropagated, count),
        PlantOrigin.GrownFromSeed => string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearOriginSeed, count),
        PlantOrigin.Gift => Plural.Of(count, Strings.Shared.YearOriginGift_One, Strings.Shared.YearOriginGift_Other),
        PlantOrigin.Swap => Plural.Of(count, Strings.Shared.YearOriginSwap_One, Strings.Shared.YearOriginSwap_Other),
        _ => string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearOriginOther, count),
    };

    private static YearCard Propagations(PlantYearSummary summary)
    {
        var p = summary.Propagations;
        var kicker = string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearKickerCuttings, summary.Year);
        if (!p.AllCuttings)
            kicker = string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearKickerPropagations, summary.Year);
        var figure = p.Decided > 0
            ? new YearFigure(p.Succeeded.ToString(CultureInfo.InvariantCulture), string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearMadeIt, p.Decided))
            : new YearFigure(p.Started.ToString(CultureInfo.InvariantCulture), Strings.Shared.YearStarted);
        var line = string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearStartedRooted, p.Started, p.Rooted);
        return new YearCard("propagations", YearCardKind.Number, "propagation", kicker, kicker, null, null, line,
            [figure], null, null, null, null, null, NoPhotos,
            string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearPropagationsAlt, kicker, figure.Value, figure.Caption, p.Started, p.Rooted));
    }

    private static YearCard Quickest(QuickestRoot quickest, Func<Enum, string> label, Func<Guid, YearCardPhoto?> shown)
    {
        var kicker = Strings.Shared.YearQuickest;
        var propagation = quickest.Propagation;
        var (latin, cultivar) = Botanical(propagation.Nickname, propagation.Genus, propagation.Species, propagation.Cultivar, propagation.NameQualifier, propagation.NameNote);
        var name = propagation.DisplayName;
        var medium = InText(label(propagation.Medium));
        var days = Days(quickest.DaysToRoot);

        if (quickest.PhotoId is { } photoId)
            return new YearCard("quickest-to-root", YearCardKind.Photo, "propagation", kicker, name, latin, cultivar,
                string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearQuickestLine, days, medium), NoFigures, shown(photoId), null, null, null, null, NoPhotos,
                string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearQuickestAltPhoto, kicker, name, days, medium));

        return new YearCard("quickest-to-root", YearCardKind.Number, "propagation", kicker, name, latin, cultivar,
            string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearQuickestLineNumber, name, medium), [Figure(quickest.DaysToRoot, Strings.Shared.YearFigureDay_One, Strings.Shared.YearFigureDay_Other)], null, null, null, null, null, NoPhotos,
            string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearQuickestAltNumber, kicker, name, days, medium));
    }

    private static YearCard Medium(int year, BestMedium best, Func<Enum, string> label)
    {
        var kicker = best.Compared ? string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearKickerBestMedium, year) : string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearKickerMyMedium, year);
        var medium = InText(label(best.Medium));
        var figure = new YearFigure(best.Succeeded.ToString(CultureInfo.InvariantCulture), string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearMediumMadeIt, best.Decided, medium));

        var sentences = new List<string>();
        if (best.Compared)
            sentences.Add(string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearMediumBest, best.MediumsCompared));
        if (best.AverageDaysToRoot is { } average)
            sentences.Add(string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearMediumAverage, Days((int)Math.Round(average, MidpointRounding.AwayFromZero))));
        var line = sentences.Count == 0 ? null : string.Join(' ', sentences);

        var alt = $"{kicker}: {figure.Value} {figure.Caption}." + (line is null ? "" : $" {line}");
        return new YearCard("medium", YearCardKind.Number, "propagation", kicker, kicker, null, null, line,
            [figure], null, null, null, null, null, NoPhotos, alt);
    }

    private static YearCard Most(int year, MostPhotographed most, Func<Guid, YearCardPhoto?> shown)
    {
        var kicker = Strings.Shared.YearMostPhotographed;
        var plant = most.Plant;
        var (latin, cultivar) = Botanical(plant.Nickname, plant.Genus, plant.Species, plant.Cultivar, plant.NameQualifier, plant.NameNote);
        var line = string.Format(CultureInfo.CurrentCulture, most.PhotoCount == 1 ? Strings.Shared.YearMostLine_One : Strings.Shared.YearMostLine_Other, most.PhotoCount, year);
        return new YearCard("most-photographed", YearCardKind.Photo, "plant", kicker, plant.DisplayName, latin, cultivar,
            line, NoFigures, shown(most.PhotoId), null, null, null, null, NoPhotos,
            string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearMostAlt, kicker, plant.DisplayName, line));
    }

    private static YearCard Grown(PlantYearSummary summary, GrownThisYear grown, Func<Photo, YearCardPhoto> ofPhoto, Func<DateTimeOffset, DateOnly> localDay)
    {
        var kicker = summary.SoFar ? Strings.Shared.YearGrownSoFar : string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearGrownIn, summary.Year);
        var plant = grown.Plant;
        var (latin, cultivar) = Botanical(plant.Nickname, plant.Genus, plant.Species, plant.Cultivar, plant.NameQualifier, plant.NameNote);
        var line = string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearGrownLine, grown.DaysApart);
        var before = localDay(grown.First.TakenAt).ToString(Strings.Shared.YearGrownDayFormat, TextCulture.ForDates);
        var after = localDay(grown.Last.TakenAt).ToString(Strings.Shared.YearGrownDayFormat, TextCulture.ForDates);
        return new YearCard("grown", YearCardKind.Pair, "plant", kicker, plant.DisplayName, latin, cultivar, line,
            NoFigures, null, ofPhoto(grown.First), ofPhoto(grown.Last), before, after, NoPhotos,
            string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearGrownAlt, kicker, plant.DisplayName, before, after, line));
    }

    private static YearCard Collage(PlantYearSummary summary, Func<Photo, YearCardPhoto> ofPhoto)
    {
        var kicker = string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearCollageKicker, summary.Year);
        var line = string.Format(CultureInfo.CurrentCulture, summary.PhotosTaken == 1 ? Strings.Shared.YearCollageLine_One : Strings.Shared.YearCollageLine_Other, summary.PhotosTaken);
        return new YearCard("collage", YearCardKind.Collage, "plant", kicker, string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearCollageTitle, summary.Year), null, null, line,
            NoFigures, null, null, null, null, null, summary.Collage.Select(c => ofPhoto(c.Photo)).ToList(),
            string.Format(CultureInfo.CurrentCulture, Strings.Shared.YearCollageAlt, summary.Year, line));
    }

    private static YearFigure Figure(int value, string singular, string plural) =>
        new(value.ToString(CultureInfo.InvariantCulture), value == 1 ? singular : plural);

    private static string Days(int days) => Plural.Of(days, Strings.Shared.YearDays_One, Strings.Shared.YearDays_Other);

    // "in water", "in LECA": lower case unless the label is all capitals
    private static string InText(string label) =>
        label.Any(char.IsLower) ? label.ToLowerInvariant() : label;

    // The botanical name goes under the name, unless it is the name already
    private static (string? Latin, string? Cultivar) Botanical(string? nickname, string? genus, string? species, string? cultivar,
        NameQualifier? qualifier, string? note)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return (null, null);
        var latin = PlantNames.Botanical(genus, species, null, qualifier, note) ?? "";
        return (latin.Length > 0 ? latin : null, PlantNames.Cultivar(cultivar));
    }
}
