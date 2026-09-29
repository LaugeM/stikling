using System.Globalization;
using System.Text;
using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Backup;

/// <summary>
/// Plants and propagations as CSV text for a spreadsheet. This is a one-way copy and not a backup,
/// so it only holds what is worth reading in a table.
/// </summary>
public static class CsvExport
{
    private static readonly string[] PlantHeader =
        ["Name", "Nickname", "Genus", "Species", "Cultivar", "Status", "Room", "Acquired", "Origin", "Source", "Tags", "Notes", "Added"];

    private static readonly string[] PropagationHeader =
        ["Name", "Parent plant", "Stage", "Medium", "Started", "Room", "Notes"];

    public static string Plants(IEnumerable<Plant> plants, Places places)
    {
        var rows = plants
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new[]
            {
                p.DisplayName,
                p.Nickname,
                p.Genus,
                p.Species,
                p.Cultivar,
                p.Status.ToString(),
                places.NameOf(p.PlaceId),
                p.AcquiredOn?.ToString(),
                p.Origin.ToString(),
                p.Source,
                string.Join("; ", p.Tags),
                p.Notes,
                p.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            });
        return Write(PlantHeader, rows);
    }

    public static string Propagations(IEnumerable<Propagation> propagations, IEnumerable<Plant> plants, Places places)
    {
        var parents = plants.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First().DisplayName);
        var rows = propagations
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new[]
            {
                p.DisplayName,
                p.ParentPlantId is { } parent && parents.TryGetValue(parent, out var name) ? name : null,
                p.Stage.ToString(),
                p.Medium.ToString(),
                p.StartedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                places.NameOf(p.PlaceId),
                p.Notes
            });
        return Write(PropagationHeader, rows);
    }

    /// <summary>The bytes to save: UTF-8 with a byte order mark, so Excel reads æøå properly.</summary>
    public static byte[] Encode(string csv) =>
        [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv)];

    private static string Write(string[] header, IEnumerable<string?[]> rows)
    {
        var text = new StringBuilder();
        AppendRow(text, header);
        foreach (var row in rows)
            AppendRow(text, row);
        return text.ToString();
    }

    private static void AppendRow(StringBuilder text, IEnumerable<string?> fields) =>
        text.AppendJoin(',', fields.Select(Quote)).Append("\r\n");

    private static string Quote(string? field)
    {
        if (string.IsNullOrEmpty(field))
            return "";
        return field.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? $"\"{field.Replace("\"", "\"\"")}\"" : field;
    }
}
