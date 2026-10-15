using Stikling.Core.Care;
using Stikling.Core.Flowering;
using Stikling.Core.Models;

namespace Stikling.Core.Plants;

/// <summary>The columns of the plant table, in the order they show and are offered.</summary>
public enum PlantColumn
{
    Name,
    Cultivar,
    Watered,
    Fed,
    Genus,
    Species,
    EverydayName,
    Room,
    Got,
    Origin,
    Source,
    Price,
    Medium,
    LikesLight,
    LikesWater,
    LikesHumidity,
    Season,
    Repotted,
    Moisture,
    Status,
    Tags,
    Added,
    Number
}

/// <summary>Which column the table is sorted by, and which way.</summary>
public sealed record PlantTableSort(PlantColumn Column = PlantColumn.Name, bool Descending = false);

/// <summary>One plant's values in the table, worked out once so a cell never has to look through the care log.</summary>
public sealed record PlantRow(
    Plant Plant,
    LastCare? Watered,
    LastCare? Fed,
    LastCare? Repotted,
    MoistureReading? Moisture,
    string? Room,
    string? EverydayName,
    FloweringNow? Flowering = null);

/// <summary>The table view of the plant list: which columns, how they are stored and how they sort.</summary>
public static class PlantTable
{
    public static readonly IReadOnlyList<PlantColumn> Defaults =
        [PlantColumn.Name, PlantColumn.Cultivar, PlantColumn.Watered, PlantColumn.Fed];

    private static readonly Dictionary<string, PlantColumn> ByName =
        Enum.GetValues<PlantColumn>().ToDictionary(c => c.ToString());

    public static string Header(PlantColumn column) => column switch
    {
        PlantColumn.EverydayName => "Everyday name",
        PlantColumn.Source => "From",
        PlantColumn.Number => "No.",
        PlantColumn.LikesLight => "Likes light",
        PlantColumn.LikesWater => "Water",
        PlantColumn.LikesHumidity => "Humidity",
        PlantColumn.Season => "Season",
        _ => column.ToString()
    };

    /// <summary>The columns stored on the device, in table order. Name is always there, and nothing known gives the defaults.</summary>
    public static IReadOnlyList<PlantColumn> Parse(string? stored)
    {
        var picked = (stored ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(ByName.ContainsKey)
            .Select(name => ByName[name])
            .ToHashSet();

        return picked.Count == 0 ? Defaults : Ordered(picked);
    }

    public static string Write(IEnumerable<PlantColumn> columns) =>
        string.Join(',', Ordered(columns.ToHashSet()));

    private static List<PlantColumn> Ordered(HashSet<PlantColumn> picked)
    {
        picked.Add(PlantColumn.Name);
        return Enum.GetValues<PlantColumn>().Where(picked.Contains).ToList();
    }

    /// <summary>Stored as the column, then " desc" when reversed: "Watered desc". Anything unknown reads as Name A to Z.</summary>
    public static PlantTableSort ParseSort(string? stored)
    {
        var parts = (stored ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is 1 or 2 && ByName.TryGetValue(parts[0], out var column)
            && (parts.Length == 1 || parts[1] == "desc"))
            return new PlantTableSort(column, parts.Length == 2);
        return new PlantTableSort();
    }

    public static string WriteSort(PlantTableSort sort) =>
        sort.Descending ? $"{sort.Column} desc" : sort.Column.ToString();

    /// <summary>Dates, where newest first is what people look for, start with the newest.</summary>
    public static bool StartsDescending(PlantColumn column) =>
        column is PlantColumn.Watered or PlantColumn.Fed or PlantColumn.Repotted or PlantColumn.Got or PlantColumn.Added;

    /// <summary>What tapping a heading does: turn the order round when it is the sorted one, otherwise sort by it.</summary>
    public static PlantTableSort Tap(PlantTableSort current, PlantColumn column) =>
        current.Column == column
            ? current with { Descending = !current.Descending }
            : new PlantTableSort(column, StartsDescending(column));

    /// <summary>
    /// The name in the Name column. With the Cultivar column showing, a plant without a nickname leaves the
    /// cultivar out of its name, so it isn't said twice.
    /// </summary>
    public static string NameFor(Plant plant, bool cultivarShown)
    {
        if (!cultivarShown || !string.IsNullOrWhiteSpace(plant.Nickname))
            return plant.DisplayName;
        return PlantNames.Botanical(plant.Genus, plant.Species, null, plant.NameQualifier, plant.NameNote)
            ?? PlantNames.Cultivar(plant.Cultivar)
            ?? plant.DisplayName;
    }

    /// <summary>A row for each plant, with the care log read once.</summary>
    public static List<PlantRow> Rows(
        IEnumerable<Plant> plants,
        IEnumerable<CareLog> careLogs,
        DateOnly today,
        Func<Guid?, string?> placeName,
        Func<Plant, string?> everydayName,
        Func<Plant, FloweringNow?>? floweringOf = null)
    {
        var byPlant = careLogs.Where(l => !l.IsDeleted).ToLookup(l => l.PlantId);

        return plants.Select(plant =>
        {
            var logs = CareSummary.For(byPlant[plant.Id], plant.Id); // newest first
            return new PlantRow(
                plant,
                Last(logs, today, CareKind.Watered, CareKind.ToppedUp),
                Last(logs, today, CareKind.Fertilised),
                Last(logs, today, CareKind.Repotted),
                logs.FirstOrDefault(l => l.Kind == CareKind.MoistureReading && l.Moisture is not null)
                    is { Moisture: { } value } reading
                    ? new MoistureReading(value, reading.OccurredOn, DaysBetween(reading.OccurredOn, today))
                    : null,
                placeName(plant.PlaceId),
                everydayName(plant),
                floweringOf?.Invoke(plant));
        }).ToList();
    }

    private static LastCare? Last(IReadOnlyList<CareLog> logs, DateOnly today, params CareKind[] kinds) =>
        logs.FirstOrDefault(l => kinds.Contains(l.Kind)) is { } entry
            ? new LastCare(entry.Kind, entry.OccurredOn, DaysBetween(entry.OccurredOn, today), entry.Notes)
            : null;

    private static int DaysBetween(DateOnly then, DateOnly today) => Math.Max(0, today.DayNumber - then.DayNumber);

    /// <summary>
    /// Orders the rows by a column. Text goes A to Z and dates oldest first when ascending, and an empty value
    /// goes last either way. Ties fall back on the name A to Z.
    /// </summary>
    /// <param name="cultivarShown">Whether the Cultivar column is on, which changes what the Name column holds.</param>
    /// <param name="labelOf">The text shown for Origin, Status and Medium, so they sort the way they read.</param>
    public static List<PlantRow> Sort(
        IEnumerable<PlantRow> rows,
        PlantTableSort sort,
        bool cultivarShown = false,
        Func<Enum, string>? labelOf = null)
    {
        var text = StringComparer.CurrentCultureIgnoreCase;
        var direction = sort.Descending ? -1 : 1;
        var label = labelOf ?? (e => e.ToString());
        var keyed = rows
            .Select(row => (Row: row, Key: KeyOf(row, sort.Column, cultivarShown, label)))
            .ToList();

        var comparer = Comparer<(PlantRow Row, IComparable? Key)>.Create((a, b) =>
        {
            if ((a.Key is null) != (b.Key is null))
                return a.Key is null ? 1 : -1; // blanks last in both directions
            var result = a.Key is null ? 0
                : a.Key is string x ? text.Compare(x, (string)b.Key!) * direction
                : a.Key.CompareTo(b.Key) * direction;
            return result != 0 ? result : text.Compare(a.Row.Plant.DisplayName, b.Row.Plant.DisplayName);
        });
        return keyed.Order(comparer).Select(k => k.Row).ToList();
    }

    private static IComparable? KeyOf(PlantRow row, PlantColumn column, bool cultivarShown, Func<Enum, string> label)
    {
        var plant = row.Plant;
        return column switch
        {
            PlantColumn.Name => NameFor(plant, cultivarShown),
            PlantColumn.Cultivar => Text(PlantNames.Cultivar(plant.Cultivar)),
            PlantColumn.Watered => row.Watered?.On,
            PlantColumn.Fed => row.Fed?.On,
            PlantColumn.Genus => Text(plant.Genus),
            PlantColumn.Species => Text(plant.Species),
            PlantColumn.EverydayName => Text(row.EverydayName),
            PlantColumn.Room => Text(row.Room),
            PlantColumn.Got => plant.AcquiredOn?.Start,
            PlantColumn.Origin => label(plant.Origin),
            PlantColumn.Source => Text(plant.Source),
            PlantColumn.Price => plant.PricePaid,
            PlantColumn.Medium => label(plant.Medium),
            PlantColumn.LikesLight => plant.LikesLight,
            PlantColumn.LikesWater => plant.LikesWater,
            PlantColumn.LikesHumidity => plant.LikesHumidity,
            PlantColumn.Season => plant.GrowingSeason,
            PlantColumn.Repotted => row.Repotted?.On,
            PlantColumn.Moisture => row.Moisture?.Value,
            PlantColumn.Status => label(plant.Status),
            PlantColumn.Tags => Text(string.Join(", ", plant.Tags)),
            PlantColumn.Added => plant.CreatedAt,
            PlantColumn.Number => CollectionNumbers.SortKey(plant.CollectionNumber),
            _ => null
        };
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
