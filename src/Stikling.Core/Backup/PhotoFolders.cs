using System.Globalization;
using System.Text;
using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Backup;

/// <param name="IncludeGone">Whether plants that are no longer in the collection and finished propagations are included.</param>
/// <param name="IncludeReferences">Whether reference photos are included, in a Reference folder inside their plant's or propagation's.</param>
public sealed record PhotoFolderOptions(bool IncludeGone, bool IncludeReferences = true);

/// <summary>
/// Where each photo goes in a download of the photos on their own: one folder per plant,
/// propagation and pot, each file named by when the photo was taken. Meant for looking through
/// outside the app, unlike the backup, which is made for the app to read back.
/// </summary>
public static class PhotoFolders
{
    private const int MaxNameLength = 80;
    private const string Unnamed = "Unnamed";
    private const string RemovedPlace = "Place removed";

    private static readonly HashSet<string> ReservedOnWindows = new(
        ["CON", "PRN", "AUX", "NUL", .. Enumerable.Range(1, 9).SelectMany(n => new[] { $"COM{n}", $"LPT{n}" })],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>The photos to include with the path each gets, without an extension, in a stable order.</summary>
    public static IReadOnlyList<(Guid PhotoId, string Path)> Plan(
        IEnumerable<Plant> plants, IEnumerable<Propagation> propagations, IEnumerable<Pot> pots,
        IEnumerable<Photo> photos, PhotoFolderOptions options, TimeZoneInfo zone, Places? places = null)
    {
        places ??= Places.None;
        var photoList = photos.ToList();

        // Shelf photos go in a folder for each place, by the place's whole name, following a merged place to
        // where it went. Photos of a place that was removed share one folder.
        Guid PlaceKey(Photo photo) => places.Find(photo.SubjectId)?.Id ?? Guid.Empty;
        var placeSubjects = photoList
            .Where(p => !p.IsDeleted && p.SubjectType == SubjectType.Place)
            .Select(PlaceKey).Distinct()
            .Select(id => id == Guid.Empty
                ? (Id: id, CreatedAt: DateTimeOffset.MaxValue, Name: RemovedPlace)
                : (Id: id, places.Find(id)!.CreatedAt, Name: places.NameOf(id) ?? RemovedPlace))
            .ToList();

        var folders = new Dictionary<(SubjectType, Guid), string>();
        AddFolders(folders, SubjectType.Plant, "Plants", plants
            .Where(p => !p.IsDeleted && (options.IncludeGone || p.InCollection))
            .Select(p => (p.Id, p.CreatedAt, Name: PlantName(p))));
        AddFolders(folders, SubjectType.Propagation, "Propagations", propagations
            .Where(p => !p.IsDeleted && (options.IncludeGone || p.IsActive))
            .Select(p => (p.Id, p.CreatedAt, Name: $"{p.DisplayName} {p.StartedOn.ToString("yyyy-MM", CultureInfo.InvariantCulture)}")));
        AddFolders(folders, SubjectType.Pot, "Pots", pots
            .Where(p => !p.IsDeleted)
            .Select(p => (p.Id, p.CreatedAt, Name: p.FamilyName)));

        AddFolders(folders, SubjectType.Place, "Shelf photos", placeSubjects);

        var result = new List<(Guid, string)>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        (SubjectType, Guid) KeyOf(Photo p) => p.SubjectType == SubjectType.Place ? (p.SubjectType, PlaceKey(p)) : (p.SubjectType, p.SubjectId);
        var ordered = photoList
            .Where(p => !p.IsDeleted && folders.ContainsKey(KeyOf(p)))
            .Where(p => options.IncludeReferences || !p.IsReference)
            .OrderBy(p => p.TakenAt).ThenBy(p => p.CreatedAt).ThenBy(p => p.Id);
        foreach (var photo in ordered)
        {
            var folder = folders[KeyOf(photo)] + (photo.IsReference ? "/Reference" : "");
            var local = TimeZoneInfo.ConvertTime(photo.TakenAt, zone);
            var stem = $"{folder}/{local.ToString("yyyy-MM-dd HH-mm", CultureInfo.InvariantCulture)}";
            var path = stem;
            for (var n = 2; !used.Add(path); n++)
                path = $"{stem} ({n})";
            result.Add((photo.Id, path));
        }
        return result;
    }

    private static string PlantName(Plant plant) =>
        string.IsNullOrWhiteSpace(plant.CollectionNumber)
            ? plant.DisplayName
            : $"{plant.CollectionNumber.Trim()} {plant.DisplayName}";

    private static void AddFolders(
        Dictionary<(SubjectType, Guid), string> folders, SubjectType type, string root,
        IEnumerable<(Guid Id, DateTimeOffset CreatedAt, string Name)> subjects)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var subject in subjects.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id))
        {
            var name = SafeName(subject.Name);
            var candidate = name;
            for (var n = 2; !taken.Add(candidate); n++)
                candidate = $"{name} ({n})";
            folders[(type, subject.Id)] = $"{root}/{candidate}";
        }
    }

    /// <summary>A name that is allowed as a file or folder name on Windows, macOS and Android.</summary>
    public static string SafeName(string? name)
    {
        var builder = new StringBuilder();
        foreach (var c in name ?? "")
        {
            if (char.IsControl(c))
                continue;
            builder.Append(c is '\\' or '/' or ':' or '*' or '?' or '"' or '<' or '>' or '|' ? ' ' : c);
        }

        var text = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (text.Length > MaxNameLength)
            text = text[..MaxNameLength];
        text = text.TrimEnd('.', ' ');
        if (text.Length == 0)
            return Unnamed;
        // Windows can't make a file or folder with a device name, whatever comes after a dot
        return ReservedOnWindows.Contains(text.Split('.')[0]) ? $"_{text}" : text;
    }
}
