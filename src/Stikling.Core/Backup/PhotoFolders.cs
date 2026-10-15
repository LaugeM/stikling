using System.Globalization;
using System.Text;
using Stikling.Core.Models;

namespace Stikling.Core.Backup;

/// <param name="IncludeGone">Whether plants that are no longer in the collection and finished propagations are included.</param>
public sealed record PhotoFolderOptions(bool IncludeGone);

/// <summary>
/// Where each photo goes in a download of the photos on their own: one folder per plant,
/// propagation and pot, each file named by when the photo was taken. Meant for looking through
/// outside the app, unlike the backup, which is made for the app to read back.
/// </summary>
public static class PhotoFolders
{
    private const int MaxNameLength = 80;
    private const string Unnamed = "Unnamed";

    private static readonly HashSet<string> ReservedOnWindows = new(
        ["CON", "PRN", "AUX", "NUL", .. Enumerable.Range(1, 9).SelectMany(n => new[] { $"COM{n}", $"LPT{n}" })],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>The photos to include with the path each gets, without an extension, in a stable order.</summary>
    public static IReadOnlyList<(Guid PhotoId, string Path)> Plan(
        IEnumerable<Plant> plants, IEnumerable<Propagation> propagations, IEnumerable<Pot> pots,
        IEnumerable<Photo> photos, PhotoFolderOptions options, TimeZoneInfo zone)
    {
        var folders = new Dictionary<(SubjectType, Guid), string>();
        AddFolders(folders, SubjectType.Plant, "Plants", plants
            .Where(p => !p.IsDeleted && (options.IncludeGone || p.Status == PlantStatus.Active))
            .Select(p => (p.Id, p.CreatedAt, Name: PlantName(p))));
        AddFolders(folders, SubjectType.Propagation, "Propagations", propagations
            .Where(p => !p.IsDeleted && (options.IncludeGone || p.IsActive))
            .Select(p => (p.Id, p.CreatedAt, Name: $"{p.DisplayName} {p.StartedOn.ToString("yyyy-MM", CultureInfo.InvariantCulture)}")));
        AddFolders(folders, SubjectType.Pot, "Pots", pots
            .Where(p => !p.IsDeleted)
            .Select(p => (p.Id, p.CreatedAt, Name: string.IsNullOrWhiteSpace(p.Name) ? "Pot" : p.Name)));

        var result = new List<(Guid, string)>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = photos
            .Where(p => !p.IsDeleted && folders.ContainsKey((p.SubjectType, p.SubjectId)))
            .OrderBy(p => p.TakenAt).ThenBy(p => p.CreatedAt).ThenBy(p => p.Id);
        foreach (var photo in ordered)
        {
            var folder = folders[(photo.SubjectType, photo.SubjectId)];
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
