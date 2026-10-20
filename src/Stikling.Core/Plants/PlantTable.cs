using Stikling.Core.Care;
using System.Globalization;
using Stikling.Core.Fields;
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
    Raised,
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

/// <summary>A column of the table: one the app has, or a field of the person's own.</summary>
public readonly record struct TableColumn(PlantColumn? Builtin, Guid? FieldId)
{
    public static implicit operator TableColumn(PlantColumn column) => new(column, null);

    public static TableColumn Field(Guid id) => new(null, id);

    public bool IsField => FieldId is not null;

    /// <summary>How it is stored on the device: the name of a built-in column, or "field:" and the id.</summary>
    public override string ToString() => FieldId is { } id ? $"field:{id:D}" : Builtin.ToString()!;
}

/// <summary>Which column the table is sorted by, and which way. Name A to Z when nothing is said.</summary>
public sealed record PlantTableSort(TableColumn Column, bool Descending = false)
{
    public PlantTableSort() : this(new TableColumn(PlantColumn.Name, null))
    {
    }
}

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
    public static readonly IReadOnlyList<TableColumn> Defaults =
        [PlantColumn.Name, PlantColumn.Cultivar, PlantColumn.Watered, PlantColumn.Fed];

    private const string FieldPrefix = "field:";

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

    /// <summary>The heading of a column: the field's name for a field of the person's own.</summary>
    public static string Header(TableColumn column, IEnumerable<OwnField> fields) =>
        column.FieldId is { } id
            ? fields.FirstOrDefault(f => f.Id == id)?.Name ?? ""
            : Header(column.Builtin ?? PlantColumn.Name);

    /// <summary>
    /// The columns stored on the device, in table order: the built-in ones, then the fields of the
    /// person's own by name. Name is always there, and nothing known gives the defaults. A field
    /// that isn't in <paramref name="fields"/> (removed, or not yet synced) is dropped.
    /// </summary>
    public static IReadOnlyList<TableColumn> Parse(string? stored, IEnumerable<OwnField>? fields = null)
    {
        var live = (fields ?? []).Where(f => !f.IsDeleted).ToList();
        var found = new List<TableColumn>();
        foreach (var part in (stored ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            TableColumn column;
            if (ByName.TryGetValue(part, out var builtin))
                column = builtin;
            else if (part.StartsWith(FieldPrefix, StringComparison.Ordinal)
                && Guid.TryParse(part[FieldPrefix.Length..], out var id) && live.Any(f => f.Id == id))
                column = TableColumn.Field(id);
            else
                continue;
            if (!found.Contains(column))
                found.Add(column);
        }

        return found.Count == 0 ? Defaults : NameFirst(found);
    }

    /// <summary>Written in the order given, with Name first.</summary>
    public static string Write(IEnumerable<TableColumn> columns) =>
        string.Join(',', NameFirst(columns).Select(c => c.ToString()));

    private static List<TableColumn> NameFirst(IEnumerable<TableColumn> columns)
    {
        TableColumn name = PlantColumn.Name;
        return [name, .. columns.Where(c => c != name).Distinct()];
    }

    /// <summary>Moves a column from one place to another and returns the new list. Name, at the start, stays there and nothing goes above it.</summary>
    public static IReadOnlyList<TableColumn> Move(IReadOnlyList<TableColumn> columns, int from, int to)
    {
        if (from < 1 || from >= columns.Count)
            return columns;
        to = Math.Clamp(to, 1, columns.Count - 1);
        var list = columns.ToList();
        var column = list[from];
        list.RemoveAt(from);
        list.Insert(to, column);
        return list;
    }

    /// <summary>Turns a column on, at the end, or off. Name can't be turned off.</summary>
    public static IReadOnlyList<TableColumn> Toggle(IReadOnlyList<TableColumn> columns, TableColumn column)
    {
        if (column == (TableColumn)PlantColumn.Name)
            return columns;
        return columns.Contains(column) ? columns.Where(c => c != column).ToList() : [.. columns, column];
    }

    /// <summary>
    /// Stored as the column, then " desc" when reversed: "Watered desc" or "field:{id} desc". Anything
    /// unknown, or a field that isn't in <paramref name="fields"/>, reads as Name A to Z.
    /// </summary>
    public static PlantTableSort ParseSort(string? stored, IEnumerable<OwnField>? fields = null)
    {
        var parts = (stored ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is 1 or 2 && (parts.Length == 1 || parts[1] == "desc"))
        {
            if (ByName.TryGetValue(parts[0], out var column))
                return new PlantTableSort(column, parts.Length == 2);
            if (parts[0].StartsWith(FieldPrefix, StringComparison.Ordinal)
                && Guid.TryParse(parts[0][FieldPrefix.Length..], out var id)
                && (fields ?? []).Any(f => f.Id == id && !f.IsDeleted))
                return new PlantTableSort(TableColumn.Field(id), parts.Length == 2);
        }
        return new PlantTableSort();
    }

    public static string WriteSort(PlantTableSort sort) =>
        sort.Descending ? $"{sort.Column} desc" : sort.Column.ToString();

    /// <summary>Dates, where newest first is what people look for, start with the newest. Own fields start ascending.</summary>
    public static bool StartsDescending(TableColumn column) =>
        column.Builtin is PlantColumn.Watered or PlantColumn.Fed or PlantColumn.Repotted or PlantColumn.Got or PlantColumn.Added;

    /// <summary>What tapping a heading does: turn the order round when it is the sorted one, otherwise sort by it.</summary>
    public static PlantTableSort Tap(PlantTableSort current, TableColumn column) =>
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
                CurrentReading(byPlant[plant.Id], today),
                placeName(plant.PlaceId),
                everydayName(plant),
                floweringOf?.Invoke(plant));
        }).ToList();
    }

    // A reading is taken before watering, so one with a watering or top-up after it says nothing about the soil now
    private static MoistureReading? CurrentReading(IEnumerable<CareLog> logs, DateOnly today)
    {
        var all = logs.ToList();
        var reading = all.Where(l => l.Kind == CareKind.MoistureReading && l.Moisture is not null)
            .OrderByDescending(l => l.OccurredOn).ThenByDescending(l => l.CreatedAt).FirstOrDefault();
        if (reading is not { Moisture: { } value })
            return null;
        var watered = all.Any(l => (l.Kind == CareKind.Watered || l.Kind == CareKind.ToppedUp)
            && (l.OccurredOn > reading.OccurredOn || (l.OccurredOn == reading.OccurredOn && l.CreatedAt > reading.CreatedAt)));
        return watered ? null : new MoistureReading(value, reading.OccurredOn, DaysBetween(reading.OccurredOn, today));
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
        Func<Enum, string>? labelOf = null,
        IReadOnlyList<OwnField>? fields = null)
    {
        var text = StringComparer.CurrentCultureIgnoreCase;
        var direction = sort.Descending ? -1 : 1;
        var label = labelOf ?? (e => e.ToString());
        var keyed = rows
            .Select(row => (Row: row, Key: KeyOf(row, sort.Column, cultivarShown, label, fields)))
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

    // Where a choice sits: its place in the list, or after all of them for a value that isn't listed
    private readonly record struct ChoiceKey(int Rank, string Text) : IComparable
    {
        public int CompareTo(object? other) =>
            other is ChoiceKey o
                ? Rank != o.Rank ? Rank.CompareTo(o.Rank) : StringComparer.CurrentCultureIgnoreCase.Compare(Text, o.Text)
                : 1;
    }

    // A value that doesn't fit the kind of its field sorts as blank
    private static IComparable? FieldKey(Plant plant, OwnField? field)
    {
        if (field is null || !plant.OwnFields.TryGetValue(field.Id, out var raw) || string.IsNullOrWhiteSpace(raw))
            return null;
        var value = raw.Trim();
        switch (field.Kind)
        {
            case OwnFieldKind.Number:
                var number = OwnFieldValues.Normalize(OwnFieldKind.Number, value, out var numberError);
                return numberError is null && number is not null ? decimal.Parse(number, CultureInfo.InvariantCulture) : null;
            case OwnFieldKind.Date:
                var date = OwnFieldValues.Normalize(OwnFieldKind.Date, value, out var dateError);
                return dateError is null && date is not null
                    ? DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : null;
            case OwnFieldKind.Choice:
                var at = field.Choices.FindIndex(c => string.Equals(c.Trim(), value, StringComparison.CurrentCultureIgnoreCase));
                return new ChoiceKey(at >= 0 ? at : int.MaxValue, value);
            default:
                return value;
        }
    }

    private static IComparable? KeyOf(PlantRow row, TableColumn tableColumn, bool cultivarShown, Func<Enum, string> label, IReadOnlyList<OwnField>? fields)
    {
        var plant = row.Plant;
        if (tableColumn.FieldId is { } fieldId)
            return FieldKey(plant, fields?.FirstOrDefault(f => f.Id == fieldId));
        var column = tableColumn.Builtin ?? PlantColumn.Name;
        return column switch
        {
            PlantColumn.Name => NameFor(plant, cultivarShown),
            PlantColumn.Cultivar => Text(PlantNames.Cultivar(plant.Cultivar)),
            PlantColumn.Watered => row.Watered?.On,
            PlantColumn.Fed => row.Fed?.On,
            PlantColumn.Genus => Text(plant.Genus),
            PlantColumn.Species => Text(plant.KnownSpecies),
            PlantColumn.EverydayName => Text(row.EverydayName),
            PlantColumn.Room => Text(row.Room),
            PlantColumn.Got => plant.AcquiredOn?.Start,
            PlantColumn.Origin => label(plant.Origin),
            PlantColumn.Raised => plant.Raised is { } raised ? label(raised) : null,
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
