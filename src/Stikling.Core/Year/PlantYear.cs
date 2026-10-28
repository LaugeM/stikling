using Stikling.Core.Models;
using Stikling.Core.Propagations;

namespace Stikling.Core.Year;

/// <summary>How many plants came in one way.</summary>
public sealed record OriginCount(PlantOrigin Origin, int Count);

/// <summary>The plants added in the year, and where they came from, the biggest group first.</summary>
public sealed record PlantsAdded(int Count, IReadOnlyList<OriginCount> ByOrigin);

/// <summary>What was propagated in the year.</summary>
/// <param name="Started">Propagations started in the year.</param>
/// <param name="Succeeded">Units that made it, among those started in the year.</param>
/// <param name="Decided">Units that have gone one way or the other, among those started in the year.</param>
/// <param name="Rooted">Propagations that rooted in the year, whenever they were started.</param>
/// <param name="AllCuttings">Every one started in the year is a cutting, so the page can say "cuttings".</param>
/// <param name="RootedAllCuttings">Every one that rooted in the year is a cutting, for "cuttings rooted". A different set from <paramref name="AllCuttings"/>, since one can root in a later year than it was started.</param>
public sealed record PropagationsYear(int Started, int Succeeded, int Decided, int Rooted, bool AllCuttings, bool RootedAllCuttings);

/// <summary>The propagation that rooted fastest, with a photo to show it by.</summary>
public sealed record QuickestRoot(Propagation Propagation, int DaysToRoot, Guid? PhotoId);

/// <summary>
/// The medium that did best for what was started in the year. <paramref name="Compared"/> is false
/// when it is the only one with enough results, so it is just the one used.
/// </summary>
public sealed record BestMedium(
    GrowingMedium Medium,
    int Succeeded,
    int Decided,
    double? AverageDaysToRoot,
    bool Compared,
    int MediumsCompared);

/// <summary>The plant with the most photos taken in the year.</summary>
public sealed record MostPhotographed(Plant Plant, int PhotoCount, Guid PhotoId);

/// <summary>A plant's first and last photo of the year, far enough apart to show it growing.</summary>
public sealed record GrownThisYear(Plant Plant, Photo First, Photo Last, int DaysApart);

/// <summary>One plant or propagation in the collage, with its newest photo of the year.</summary>
public sealed record CollageItem(SubjectType SubjectType, Guid SubjectId, Photo Photo);

/// <summary>A plant that died in the year, and when. This list is never shared.</summary>
public sealed record LostPlant(Plant Plant, DateOnly Died, string? Cause);

/// <summary>Everything "Your year with your plants" shows for one calendar year.</summary>
public sealed record PlantYearSummary(
    int Year,
    bool SoFar,
    PlantsAdded Added,
    PropagationsYear Propagations,
    QuickestRoot? Quickest,
    BestMedium? Medium,
    MostPhotographed? MostPhotographed,
    GrownThisYear? Grown,
    IReadOnlyList<CollageItem> Collage,
    int PhotosTaken,
    IReadOnlyList<LostPlant> Lost)
{
    /// <summary>Nothing was added, started or photographed, so there is nothing to tell.</summary>
    public bool IsEmpty => Added.Count == 0 && Propagations.Started == 0 && PhotosTaken == 0;

    /// <summary>How many cards the page shows. Today's section only shows from two.</summary>
    public int CardCount =>
        (IsEmpty ? 0 : 1)
        + (Added.Count > 0 ? 1 : 0)
        + (Propagations.Started > 0 ? 1 : 0)
        + (Quickest is null ? 0 : 1)
        + (Medium is null ? 0 : 1)
        + (MostPhotographed is null ? 0 : 1)
        + (Grown is null ? 0 : 1)
        + (Collage.Count >= PlantYear.MinimumForCollage ? 1 : 0);
}

/// <summary>
/// A calendar year of someone's plants worked out from their records: what was added, what rooted,
/// which photos tell the story. Deleted records, photos that came with a received history and
/// photos of pots never count, and a plant that died never appears on a photo card.
/// </summary>
public static class PlantYear
{
    /// <summary>A plant's first and last photo of the year have to be at least this many days apart.</summary>
    public const int MinimumDaysApart = 90;

    /// <summary>A medium needs this many decided units before it is compared.</summary>
    public const int MinimumDecided = 3;

    /// <summary>A plant needs this many photos in the year to be the most photographed.</summary>
    public const int MinimumPhotos = 3;

    /// <summary>The collage card needs this many pictures.</summary>
    public const int MinimumForCollage = 4;

    // A counted photo with the plant it is of, or null plant for a propagation
    private sealed record CountedPhoto(Photo Photo, Plant? Plant);

    // The photos of one plant or propagation in the year, newest first
    private sealed record SubjectPhotos(SubjectType Type, Guid Id, Plant? Plant, List<Photo> Photos);

    public static PlantYearSummary For(
        int year,
        IEnumerable<Plant> plants,
        IEnumerable<Propagation> propagations,
        IEnumerable<Photo> photos,
        IEnumerable<TimelineEntry> plantChanges,
        DateOnly today,
        Func<DateTimeOffset, DateOnly> localDay,
        string diedLabel)
    {
        var allPlants = plants.Where(p => !p.IsDeleted && !p.LivesElsewhere).ToList();
        var allProps = propagations.Where(p => !p.IsDeleted).ToList();
        var allPhotos = photos.ToList();
        var counted = Counted(allPlants, allProps, allPhotos);
        var inYear = counted.Where(c => localDay(c.Photo.TakenAt).Year == year).ToList();

        var added = allPlants.Where(p => AddedYear(p, localDay) == year).ToList();
        var byOrigin = added
            .GroupBy(p => p.Origin)
            .Select(g => new OriginCount(g.Key, g.Count()))
            .OrderByDescending(o => o.Count)
            .ThenBy(o => o.Origin)
            .ToList();

        var started = allProps.Where(p => p.StartedOn.Year == year).ToList();
        var overall = PropagationResults.Overall(started);
        var rooted = allProps.Where(p => p.RootedOn?.Year == year).ToList();
        var propagationsYear = new PropagationsYear(
            started.Count,
            overall?.Succeeded ?? 0,
            overall?.Decided ?? 0,
            rooted.Count,
            started.All(p => p.Type == PropagationType.Cutting),
            rooted.Count > 0 && rooted.All(p => p.Type == PropagationType.Cutting));

        // A plant that died stays off every photo card
        var subjects = inYear
            .Where(c => c.Plant is not { Status: PlantStatus.Died })
            .GroupBy(c => (c.Photo.SubjectType, c.Photo.SubjectId))
            .Select(g => new SubjectPhotos(
                g.Key.SubjectType,
                g.Key.SubjectId,
                g.First().Plant,
                g.Select(c => c.Photo).OrderByDescending(p => p.TakenAt).ThenBy(p => p.Id).ToList()))
            .ToList();
        var plantSubjects = subjects.Where(s => s.Plant is not null).ToList();

        return new PlantYearSummary(
            year,
            year == today.Year,
            new PlantsAdded(added.Count, byOrigin),
            propagationsYear,
            Quickest(rooted, allPhotos, counted),
            Medium(started),
            Most(plantSubjects),
            Grown(plantSubjects, localDay),
            subjects
                .OrderByDescending(s => s.Photos.Count)
                .ThenByDescending(s => s.Photos[0].TakenAt)
                .ThenBy(s => s.Id)
                .Select(s => new CollageItem(s.Type, s.Id, s.Photos[0]))
                .ToList(),
            inYear.Count,
            Lost(year, allPlants, plantChanges, localDay, diedLabel));
    }

    /// <summary>
    /// The years that have something counted in them (a plant added, a propagation started, a photo
    /// taken), newest first. This year is always in the list, even when it is empty.
    /// </summary>
    public static IReadOnlyList<int> Years(
        IEnumerable<Plant> plants,
        IEnumerable<Propagation> propagations,
        IEnumerable<Photo> photos,
        DateOnly today,
        Func<DateTimeOffset, DateOnly> localDay)
    {
        var allPlants = plants.Where(p => !p.IsDeleted && !p.LivesElsewhere).ToList();
        var allProps = propagations.Where(p => !p.IsDeleted).ToList();
        var years = new HashSet<int> { today.Year };
        years.UnionWith(allPlants.Select(p => AddedYear(p, localDay)));
        years.UnionWith(allProps.Select(p => p.StartedOn.Year));
        years.UnionWith(Counted(allPlants, allProps, photos).Select(c => localDay(c.Photo.TakenAt).Year));
        return years.OrderByDescending(y => y).ToList();
    }

    // A plant dated only to a year or a month still counts in that year
    private static int AddedYear(Plant plant, Func<DateTimeOffset, DateOnly> localDay) =>
        plant.AcquiredOn?.Year ?? localDay(plant.CreatedAt).Year;

    // The photos that count: not deleted, not from a received history, of a live plant or propagation
    private static List<CountedPhoto> Counted(List<Plant> plants, List<Propagation> propagations, IEnumerable<Photo> photos)
    {
        var plantById = plants.ToDictionary(p => p.Id);
        var propIds = propagations.Select(p => p.Id).ToHashSet();
        var found = new List<CountedPhoto>();
        foreach (var photo in photos)
        {
            if (photo.IsDeleted || photo.IsReference || photo.ReceivedHistoryId is not null)
                continue;
            if (photo.SubjectType == SubjectType.Plant && plantById.TryGetValue(photo.SubjectId, out var plant))
                found.Add(new CountedPhoto(photo, plant));
            else if (photo.SubjectType == SubjectType.Propagation && propIds.Contains(photo.SubjectId))
                found.Add(new CountedPhoto(photo, null));
        }

        return found;
    }

    private static QuickestRoot? Quickest(List<Propagation> rooted, List<Photo> allPhotos, List<CountedPhoto> counted)
    {
        var best = rooted
            .Where(p => p.DaysToRoot is not null)
            .OrderBy(p => p.DaysToRoot)
            .ThenBy(p => p.RootedOn)
            .ThenBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Id)
            .FirstOrDefault();
        if (best is null)
            return null;

        // The cover when it still exists, otherwise its newest photo
        Guid? photoId = null;
        if (best.CoverPhotoId is { } cover && allPhotos.Any(p => p.Id == cover && !p.IsDeleted && !p.IsReference))
            photoId = cover;
        else
            photoId = counted
                .Where(c => c.Photo.SubjectType == SubjectType.Propagation && c.Photo.SubjectId == best.Id)
                .OrderByDescending(c => c.Photo.TakenAt)
                .ThenBy(c => c.Photo.Id)
                .Select(c => (Guid?)c.Photo.Id)
                .FirstOrDefault();

        return new QuickestRoot(best, best.DaysToRoot!.Value, photoId);
    }

    private static BestMedium? Medium(List<Propagation> started)
    {
        var qualifying = PropagationResults.ByMedium(started).Where(r => r.Decided >= MinimumDecided).ToList();
        if (qualifying.Count == 0)
            return null;

        var best = qualifying
            .OrderByDescending(r => r.SuccessRate)
            .ThenByDescending(r => r.Decided)
            .ThenBy(r => r.AverageDaysToRoot is null)
            .ThenBy(r => r.AverageDaysToRoot)
            .ThenBy(r => r.Key)
            .First();
        return new BestMedium(
            best.Key, best.Succeeded, best.Decided, best.AverageDaysToRoot, qualifying.Count > 1, qualifying.Count);
    }

    private static MostPhotographed? Most(List<SubjectPhotos> plantSubjects)
    {
        var best = plantSubjects
            .Where(s => s.Photos.Count >= MinimumPhotos)
            .OrderByDescending(s => s.Photos.Count)
            .ThenByDescending(s => s.Photos[0].TakenAt)
            .ThenBy(s => s.Plant!.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(s => s.Id)
            .FirstOrDefault();
        if (best is null)
            return null;

        // The cover when it was taken this year, otherwise the newest photo
        var cover = best.Plant!.CoverPhotoId is { } id ? best.Photos.FirstOrDefault(p => p.Id == id) : null;
        return new MostPhotographed(best.Plant, best.Photos.Count, (cover ?? best.Photos[0]).Id);
    }

    private static GrownThisYear? Grown(List<SubjectPhotos> plantSubjects, Func<DateTimeOffset, DateOnly> localDay)
    {
        var best = plantSubjects
            .Select(s =>
            {
                var first = s.Photos.OrderBy(p => p.TakenAt).ThenBy(p => p.Id).First();
                var last = s.Photos[0];
                return new
                {
                    Plant = s.Plant!,
                    Count = s.Photos.Count,
                    First = first,
                    Last = last,
                    Days = localDay(last.TakenAt).DayNumber - localDay(first.TakenAt).DayNumber
                };
            })
            .Where(g => g.Days >= MinimumDaysApart)
            .OrderByDescending(g => g.Days)
            .ThenByDescending(g => g.Count)
            .ThenBy(g => g.Plant.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(g => g.Plant.Id)
            .FirstOrDefault();
        return best is null ? null : new GrownThisYear(best.Plant, best.First, best.Last, best.Days);
    }

    private static List<LostPlant> Lost(
        int year,
        List<Plant> plants,
        IEnumerable<TimelineEntry> plantChanges,
        Func<DateTimeOffset, DateOnly> localDay,
        string diedLabel)
    {
        // The text PlantChanges.Describe writes when a status changes
        var diedLine = $"Status: {diedLabel}";
        var newestDeath = plantChanges
            .Where(e => !e.IsDeleted && e.Text is not null
                && e.Text.Split('\n').Any(line => line.StartsWith(diedLine, StringComparison.Ordinal)))
            .GroupBy(e => e.SubjectId)
            .ToDictionary(g => g.Key, g => g.Max(e => e.OccurredAt));

        return plants
            .Where(p => p.Status == PlantStatus.Died)
            .Select(p => new LostPlant(
                p,
                p.StatusSince
                    ?? (newestDeath.TryGetValue(p.Id, out var at) ? localDay(at) : localDay(p.UpdatedAt)),
                p.CauseOfDeath))
            .Where(l => l.Died.Year == year)
            .OrderBy(l => l.Died)
            .ThenBy(l => l.Plant.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(l => l.Plant.Id)
            .ToList();
    }
}
