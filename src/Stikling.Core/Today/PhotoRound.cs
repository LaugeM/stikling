using Stikling.Core.Models;

namespace Stikling.Core.Today;

/// <summary>A plant or propagation still waiting for its photo this month.</summary>
/// <param name="Subject">The <see cref="Plant"/> or <see cref="Propagation"/>.</param>
/// <param name="LastPhoto">The day its newest photo was taken, or null when it has none.</param>
public sealed record PhotoDue(Entity Subject, string Name, string? Location, DateOnly? LastPhoto);

/// <summary>
/// The monthly photo reminder: once a month, everything in the collection gets a new photo, so
/// every history keeps growing. It's off until turned on in Settings.
/// </summary>
public static class PhotoRound
{
    /// <summary>
    /// Plants in the collection and propagations still going that have no photo taken this
    /// month, grouped by room so the round can be walked one room at a time. Dormant ones are
    /// left out, since a resting corm looks the same from one month to the next.
    /// </summary>
    /// <param name="newestPhotos">The newest photo of each plant and propagation.</param>
    public static IReadOnlyList<PhotoDue> Due(
        IEnumerable<Plant> plants,
        IEnumerable<Propagation> propagations,
        IReadOnlyDictionary<Guid, Photo> newestPhotos,
        TimeProvider time)
    {
        var monthStart = StartOfMonth(time.Today());

        DateOnly? LastPhoto(Guid id) =>
            newestPhotos.TryGetValue(id, out var photo) ? time.LocalDay(photo.TakenAt) : null;

        var subjects =
            plants.Where(p => !p.IsDeleted && p.Status == PlantStatus.Active && !p.IsDormant)
                .Select(p => new PhotoDue(p, p.DisplayName, p.Location, LastPhoto(p.Id)))
            .Concat(propagations.Where(p => !p.IsDeleted && p.IsActive && !p.IsDormant)
                .Select(p => new PhotoDue(p, p.DisplayName, p.Location, LastPhoto(p.Id))));

        return subjects
            .Where(d => d.LastPhoto is null || d.LastPhoto < monthStart)
            .OrderBy(d => string.IsNullOrWhiteSpace(d.Location) ? 1 : 0)
            .ThenBy(d => d.Location, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>The day the next round starts, which is what skipping this one puts it off until.</summary>
    public static DateOnly NextRound(DateOnly today) => StartOfMonth(today).AddMonths(1);

    private static DateOnly StartOfMonth(DateOnly day) => new(day.Year, day.Month, 1);
}
