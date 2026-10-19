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
    // The columns before the name columns were added, which an older export still has
    internal static readonly string[] PlantHeaderBeforeNameSure =
        ["Name", "Number", "Nickname", "Genus", "Species", "Cultivar", "Status", "Room", "Acquired", "Origin", "Source", "Price paid", "Tags", "Notes", "Added"];

    // The columns before Number was added, which an older export still has
    internal static readonly string[] PlantHeaderBeforeNumber =
        ["Name", "Nickname", "Genus", "Species", "Cultivar", "Status", "Room", "Acquired", "Origin", "Source", "Price paid", "Tags", "Notes", "Added"];

    // The columns before Raised was added, which an older export still has
    internal static readonly string[] PlantHeaderBeforeRaised =
        ["Name", "Number", "Nickname", "Genus", "Species", "Cultivar", "Name sure", "Locality or code", "Status", "Room", "Acquired", "Origin", "Source", "Price paid", "Tags", "Notes", "Added"];

    // The columns before How many was added, which an older export still has. Own fields can follow it.
    internal static readonly string[] PlantHeaderBeforeHowMany =
        ["Name", "Number", "Nickname", "Genus", "Species", "Cultivar", "Name sure", "Locality or code", "Status", "Room", "Acquired", "Origin", "Raised", "Source", "Price paid", "Tags", "Notes", "Added"];

    // The columns before Potted with was added, which an older export still has. Own fields can follow it.
    internal static readonly string[] PlantHeaderBeforePottedWith =
        ["Name", "Number", "Nickname", "Genus", "Species", "Cultivar", "Name sure", "Locality or code", "Status", "Room", "Acquired", "Origin", "Raised", "Source", "Price paid", "How many", "Tags", "Notes", "Added"];

    /// <summary>The fixed columns. The person's own fields follow, one column each, headed with the field's name.</summary>
    internal static readonly string[] PlantHeader =
        ["Name", "Number", "Nickname", "Genus", "Species", "Cultivar", "Name sure", "Locality or code", "Status", "Room", "Acquired", "Origin", "Raised", "Source", "Price paid", "How many", "Potted with", "Tags", "Notes", "Added"];

    private static readonly string[] PropagationHeader =
        ["Name", "Number", "Parent plant", "Stage", "Medium", "Started", "Room", "Notes"];

    /// <param name="fields">The own fields that aren't deleted, each a column after Added holding the stored value.</param>
    public static string Plants(IEnumerable<Plant> plants, Places places, IReadOnlyList<OwnField> fields)
    {
        var all = plants.ToList();
        // Only the name, one step: the import keeps it as text
        string? PottedWith(Plant p) => p.PottedWithPlantId is { } id && id != p.Id
            ? all.FirstOrDefault(o => o.Id == id && !o.IsDeleted)?.DisplayName
            : null;
        var rows = all
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => (new[]
            {
                p.DisplayName,
                p.CollectionNumber,
                p.Nickname,
                p.Genus,
                p.KnownSpecies,
                p.Cultivar,
                PlantNames.QualifierText(p.NameQualifier),
                p.NameNote,
                p.Status.ToString(),
                places.NameOf(p.PlaceId),
                p.AcquiredOn?.ToString(),
                p.Origin.ToString(),
                p.Raised?.ToString(),
                p.Source,
                p.PricePaid?.ToString("0.##", CultureInfo.InvariantCulture),
                p.HowMany.ToString(CultureInfo.InvariantCulture),
                PottedWith(p),
                string.Join("; ", p.Tags),
                p.Notes,
                p.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            }).Concat(fields.Select(f => p.OwnFields.TryGetValue(f.Id, out var value) ? value : null)).ToArray());
        return Write([.. PlantHeader, .. fields.Select(f => f.Name)], rows);
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
                p.CollectionNumber,
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
