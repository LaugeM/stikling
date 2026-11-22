using System.Globalization;
using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Backup;

/// <summary>What one ZIP file of a backup holds besides data.json, which every part has whole.</summary>
/// <param name="Number">From 1.</param>
public sealed record BackupPart(
    int Number,
    IReadOnlySet<Guid> PlantIds,
    IReadOnlySet<Guid> PropagationIds,
    IReadOnlySet<Guid> PhotoIds,
    IReadOnlySet<Guid> FileIds);

/// <summary>How a backup is split into ZIP files. A collection that fits has one part.</summary>
public sealed record BackupPlan(IReadOnlyList<BackupPart> Parts)
{
    public int Count => Parts.Count;

    /// <summary>The number of the part that holds the plant or propagation, or null when none does.</summary>
    public int? PartOf(Guid recordId) =>
        Parts.FirstOrDefault(p => p.PlantIds.Contains(recordId) || p.PropagationIds.Contains(recordId))?.Number;
}

/// <summary>
/// Splits a backup into parts when its images would be too much to build in browser memory in one go.
/// Each part is a whole backup of its own: it has all of data.json, and the images of the plants and cuttings
/// it holds, so a photo is always in the same part as the page of the record it belongs to.
/// </summary>
public static class BackupParts
{
    /// <summary>
    /// The most image data in one part, 1 GB. The ZIP is built in the browser's memory, where a tab runs out
    /// well before the device does, so a larger collection is saved as several files instead of failing.
    /// </summary>
    public const long MaxPartBytes = 1_000_000_000;

    /// <summary>
    /// A guess at how big a photo's two images are, from its pixels, since the real size is only known by reading it.
    /// Photos are saved as WebP or JPEG at a bit more than a byte for every four pixels, plus the thumbnail. It leans
    /// high, so a part ends up under the limit.
    /// </summary>
    public static long EstimatedBytes(Photo photo) =>
        Math.Max(photo.Width, 0) * (long)Math.Max(photo.Height, 0) / 3 + 40_000;

    private sealed class Item
    {
        public List<Guid> Plants { get; } = [];
        public List<Guid> Propagations { get; } = [];
        public List<Guid> Photos { get; } = [];
        public List<Guid> Files { get; } = [];
        public long Bytes { get; set; }
    }

    /// <summary>
    /// Walks the plants in index order, then the cuttings, then whatever else has images (pots, wishes, places,
    /// attached files of orders), and starts a new part when the next record's images would pass the limit.
    /// A record's images are never divided, so one that is larger than the limit has a part to itself.
    /// </summary>
    public static BackupPlan Plan(BackupData data, Places places, long maxBytes = MaxPartBytes, Func<Photo, long>? sizeOf = null, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        sizeOf ??= EstimatedBytes;
        var photos = data.Photos.Where(p => !p.IsDeleted).ToList();
        var files = data.Files.Where(f => !f.IsDeleted).ToList();
        var usedPhotos = new HashSet<Guid>();
        var usedFiles = new HashSet<Guid>();

        Item Take(SubjectType type, Guid id, FileSubject fileType)
        {
            var item = new Item();
            foreach (var photo in photos.Where(p => p.SubjectType == type && p.SubjectId == id))
            {
                item.Photos.Add(photo.Id);
                item.Bytes += sizeOf(photo);
                usedPhotos.Add(photo.Id);
            }
            foreach (var file in files.Where(f => f.SubjectType == fileType && f.SubjectId == id))
            {
                item.Files.Add(file.Id);
                item.Bytes += Math.Max(file.Size, 0);
                usedFiles.Add(file.Id);
            }
            return item;
        }

        var items = new List<Item>();
        foreach (var plant in ReadableIndex.Plants(data, places, culture).SelectMany(g => g.Plants))
        {
            var item = Take(SubjectType.Plant, plant.Id, FileSubject.Plant);
            item.Plants.Add(plant.Id);
            items.Add(item);
        }
        foreach (var cutting in ReadableIndex.Cuttings(data, culture).SelectMany(g => g.Cuttings))
        {
            var item = Take(SubjectType.Propagation, cutting.Id, FileSubject.Propagation);
            item.Propagations.Add(cutting.Id);
            items.Add(item);
        }

        // Photos of anything else, kept together by what they belong to
        foreach (var group in photos.Where(p => !usedPhotos.Contains(p.Id)).GroupBy(p => (p.SubjectType, p.SubjectId)).ToList())
        {
            var item = new Item();
            foreach (var photo in group)
            {
                item.Photos.Add(photo.Id);
                item.Bytes += sizeOf(photo);
                usedPhotos.Add(photo.Id);
            }
            items.Add(item);
        }
        foreach (var group in files.Where(f => !usedFiles.Contains(f.Id)).GroupBy(f => (f.SubjectType, f.SubjectId)).ToList())
        {
            var item = new Item();
            foreach (var file in group)
            {
                item.Files.Add(file.Id);
                item.Bytes += Math.Max(file.Size, 0);
            }
            items.Add(item);
        }

        var parts = new List<BackupPart>();
        var current = new Item();
        void Close()
        {
            parts.Add(new BackupPart(parts.Count + 1, current.Plants.ToHashSet(), current.Propagations.ToHashSet(),
                current.Photos.ToHashSet(), current.Files.ToHashSet()));
            current = new Item();
        }

        foreach (var item in items)
        {
            var isEmpty = current.Plants.Count + current.Propagations.Count + current.Photos.Count + current.Files.Count == 0;
            if (!isEmpty && current.Bytes + item.Bytes > maxBytes)
                Close();
            current.Plants.AddRange(item.Plants);
            current.Propagations.AddRange(item.Propagations);
            current.Photos.AddRange(item.Photos);
            current.Files.AddRange(item.Files);
            current.Bytes += item.Bytes;
        }
        Close();
        return new BackupPlan(parts);
    }

    /// <summary>"stikling-backup-2026-11-22-part-1-of-3.zip", or without the part for a backup of one file.</summary>
    public static string FileName(string prefix, DateOnly day, int part, int of, bool small = false)
    {
        var name = $"{prefix}{(small ? "-small" : "")}-{day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        return of > 1 ? $"{name}-part-{part}-of-{of}.zip" : $"{name}.zip";
    }
}
