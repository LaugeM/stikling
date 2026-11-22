using Stikling.Core.Examples;
using Stikling.Core.Models;

namespace Stikling.Core.Tidy;

public enum RemovedKind
{
    Plant,
    Propagation,
    Photo,
    Note
}

/// <summary>What a removed photo or note belonged to. <paramref name="IsRemoved"/> says it is gone too, so putting the photo back puts this back as well.</summary>
public sealed record RemovedOwner(Guid Id, SubjectType Type, string Name, bool IsRemoved);

/// <summary>
/// One thing removed in the last 30 days that still has its content. <paramref name="Name"/> is a plant's or a
/// propagation's name and <paramref name="Text"/> the first line of a note. A photo or a note says what it
/// belonged to in <paramref name="Owner"/>. <paramref name="PhotoId"/> is the picture to show beside it.
/// </summary>
public sealed record RemovedItem(
    RemovedKind Kind, Guid Id, DateTimeOffset RemovedAt, string? Name, string? Text, Guid? PhotoId, RemovedOwner? Owner);

/// <summary>
/// <paramref name="Stripped"/> counts what was removed in the same time but can't be put back here: the device only
/// has the copy the server keeps of a removal, which has no content, or a photo's images are no longer on the device.
/// A stripped note can't be told from any other stripped entry, so notes aren't in the count.
/// </summary>
public sealed record RemovedReport(IReadOnlyList<RemovedItem> Items, int Stripped)
{
    public IEnumerable<RemovedItem> Of(RemovedKind kind) => Items.Where(i => i.Kind == kind);
}

/// <summary>Works out what Recently removed lists, from the stored records with the deleted ones.</summary>
public static class RemovedRules
{
    /// <summary>How long a removed thing is listed and its photo images are kept on the device.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromDays(30);

    /// <summary>Whether a removal at this moment is still within <see cref="Window"/>, inclusive.</summary>
    public static bool IsRecent(DateTimeOffset removedAt, DateTimeOffset now) => removedAt >= now - Window;

    // A record counts as full when it still has what it can't do without. The server keeps a removed record as
    // only its id and dates, which reads back as an empty record, so a copy like that has none of these: a plant
    // or a propagation has a name (what Validate asks for), and a photo or a note belongs to something.
    public static bool IsFull(Plant plant) =>
        !string.IsNullOrWhiteSpace(plant.Nickname) || !string.IsNullOrWhiteSpace(plant.Genus) || plant.NameQualifier == NameQualifier.Unknown;

    public static bool IsFull(Propagation propagation) =>
        !string.IsNullOrWhiteSpace(propagation.Nickname) || !string.IsNullOrWhiteSpace(propagation.Genus) || propagation.NameQualifier == NameQualifier.Unknown;

    public static bool IsFull(Photo photo) => photo.SubjectId != Guid.Empty;

    public static bool IsFull(TimelineEntry entry) => entry.SubjectId != Guid.Empty;

    public static RemovedReport Build(
        IEnumerable<Plant> plants,
        IEnumerable<Propagation> propagations,
        IEnumerable<Photo> photos,
        IEnumerable<TimelineEntry> entries,
        IReadOnlySet<Guid> photosWithImages,
        DateTimeOffset now)
    {
        var plantList = plants.ToList();
        var propagationList = propagations.ToList();

        // What a photo or a note can belong to, by id. An owner that is itself a stripped copy can't be named,
        // so what belongs to it isn't offered.
        var owners = new Dictionary<Guid, RemovedOwner>();
        foreach (var plant in plantList.Where(IsFull))
            owners[plant.Id] = new RemovedOwner(plant.Id, SubjectType.Plant, plant.DisplayName, plant.IsDeleted);
        foreach (var propagation in propagationList.Where(IsFull))
            owners[propagation.Id] = new RemovedOwner(propagation.Id, SubjectType.Propagation, propagation.DisplayName, propagation.IsDeleted);

        var items = new List<RemovedItem>();
        var stripped = 0;

        foreach (var plant in plantList.Where(p => Recent(p, now) && !ExampleFamily.PlantIds.Contains(p.Id)))
        {
            if (IsFull(plant))
                items.Add(new RemovedItem(RemovedKind.Plant, plant.Id, plant.DeletedAt!.Value, plant.DisplayName, null, plant.CoverPhotoId, null));
            else
                stripped++;
        }

        foreach (var propagation in propagationList.Where(p => Recent(p, now) && !ExampleFamily.PropagationIds.Contains(p.Id)))
        {
            if (IsFull(propagation))
                items.Add(new RemovedItem(RemovedKind.Propagation, propagation.Id, propagation.DeletedAt!.Value, propagation.DisplayName, null, propagation.CoverPhotoId, null));
            else
                stripped++;
        }

        foreach (var photo in photos.Where(p => Recent(p, now) && !ExampleFamily.PhotoIds.Contains(p.Id)))
        {
            if (!IsFull(photo))
                stripped++;
            else if (owners.TryGetValue(photo.SubjectId, out var owner))
            {
                // Photos of pots, wishes and places aren't listed, since none of those is in the owners
                if (photosWithImages.Contains(photo.Id))
                    items.Add(new RemovedItem(RemovedKind.Photo, photo.Id, photo.DeletedAt!.Value, null, null, photo.Id, owner));
                else
                    stripped++;
            }
        }

        foreach (var entry in entries.Where(e => Recent(e, now) && e.Kind == TimelineKind.Note && IsFull(e) && !ExampleFamily.EntryIds.Contains(e.Id)))
        {
            if (owners.TryGetValue(entry.SubjectId, out var owner))
                items.Add(new RemovedItem(RemovedKind.Note, entry.Id, entry.DeletedAt!.Value, null, FirstLine(entry.Text), entry.PhotoIds.Count > 0 ? entry.PhotoIds[0] : null, owner));
        }

        return new RemovedReport([.. items.OrderByDescending(i => i.RemovedAt)], stripped);
    }

    private static bool Recent(Entity entity, DateTimeOffset now) =>
        entity.DeletedAt is { } at && IsRecent(at, now);

    /// <summary>The first line of a note, or null when it has no text.</summary>
    public static string? FirstLine(string? text) =>
        text?.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
}
