using Stikling.Core.Care;
using Stikling.Core.Fields;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Rooms;

namespace Stikling.Core.Import;

/// <summary>A row that wasn't added, and why.</summary>
public sealed record ImportSkipped(int Line, string Name, string Reason);

/// <summary>What an import added, so the result can say so and Undo can take it back.</summary>
public sealed class ImportResult
{
    private readonly List<Guid> plantIds = [];
    private readonly List<Guid> careLogIds = [];
    private readonly List<Guid> placesMade = [];
    private readonly List<Guid> fieldsMade = [];
    private readonly List<ImportSkipped> skipped = [];

    public IReadOnlyList<Guid> PlantIds => plantIds;
    public IReadOnlyList<Guid> CareLogIds => careLogIds;

    /// <summary>The rooms and spots that didn't exist before, in the order they were made.</summary>
    public IReadOnlyList<Guid> PlacesMade => placesMade;

    /// <summary>The fields of the person's own that weren't live before the import, including ones brought back from deleted.</summary>
    public IReadOnlyList<Guid> FieldsMade => fieldsMade;

    public IReadOnlyList<ImportSkipped> Skipped => skipped;

    public int Added => plantIds.Count;
    public int CareDates => careLogIds.Count;

    /// <summary>Rooms and spots made.</summary>
    public int RoomsMade => placesMade.Count;

    internal void AddPlant(Guid id) => plantIds.Add(id);
    internal void AddCareLog(Guid id) => careLogIds.Add(id);
    internal void AddPlace(Guid id) => placesMade.Add(id);
    internal void AddField(Guid id) => fieldsMade.Add(id);
    internal void Skip(ImportSkipped row) => skipped.Add(row);
}

/// <summary>
/// Adds the plants from a spreadsheet through the services the rest of the app uses, so each plant
/// gets its history line and the care entries and rooms are ordinary ones. Nothing new is stored.
/// </summary>
public sealed class SpreadsheetImportService(
    PlantService plants,
    CareService care,
    RoomService rooms,
    OwnFieldService ownFields,
    IPlantRepository plantRepository,
    ICareLogRepository careLogs,
    TimeProvider time)
{
    /// <summary>
    /// What the latest <see cref="ImportAsync"/> has added so far. It is filled in as the import goes,
    /// so when a repository throws half way through, the plants added before that are still here
    /// for <see cref="UndoAsync"/> to take back.
    /// </summary>
    public ImportResult? Last { get; private set; }

    /// <summary>
    /// Adds the rows it is given, in order, and tells <paramref name="progress"/> how many rows are
    /// done after each. A row that can't be added is skipped with the reason and the rest go on. An
    /// exception from a repository stops the import and is thrown, and <see cref="Last"/> holds what
    /// was added before it. A room or spot that doesn't exist is made; if its name can't be a place,
    /// the plant is added without one and the text goes into About it.
    /// </summary>
    public async Task<ImportResult> ImportAsync(
        IReadOnlyList<ImportRow> rows,
        Func<Enum, string> label,
        IProgress<int>? progress = null,
        IReadOnlyList<ImportOwnField?>? targets = null)
    {
        var result = Last = new ImportResult();
        var today = time.Today();
        var fieldIds = await PrepareFieldsAsync(rows, targets, result);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var reason = row.CantAdd ?? row.Draft.Validate(today).FirstOrDefault();
            if (reason is not null)
            {
                result.Skip(new ImportSkipped(row.Line, row.Draft.DisplayName, reason));
                progress?.Report(i + 1);
                continue;
            }

            // A copy, so the row the page is showing isn't changed by adding it
            var plant = row.Draft.Copy();
            foreach (var (from, to) in fieldIds)
            {
                if (plant.OwnFields.Remove(from, out var value))
                    plant.OwnFields[to] = value;
            }
            if (row.Place is not null)
                await PlaceAsync(plant, row.Place, result);

            await plants.CreateAsync(plant);
            result.AddPlant(plant.Id);

            foreach (var item in row.Care)
            {
                var entry = new CareLog { PlantId = plant.Id, Kind = item.Kind, OccurredOn = item.On };
                if (entry.Validate(today).Count > 0)
                    continue;
                await care.LogAsync(entry, label);
                result.AddCareLog(entry.Id);
            }

            progress?.Report(i + 1);
        }
        return result;
    }

    // Makes the new fields before any plant is added and adds the choices a Choice field is missing.
    // Returns the fields whose id isn't the one the preview used, which happens when a renamed field holds it.
    private async Task<Dictionary<Guid, Guid>> PrepareFieldsAsync(
        IReadOnlyList<ImportRow> rows, IReadOnlyList<ImportOwnField?>? targets, ImportResult result)
    {
        var moved = new Dictionary<Guid, Guid>();
        if (targets is null)
            return moved;

        var rowsToAdd = rows.Where(r => r.CantAdd is null).ToList();
        var liveBefore = (await ownFields.GetAllAsync()).ToDictionary(f => f.Id);
        var done = new HashSet<Guid>();
        foreach (var target in targets.OfType<ImportOwnField>())
        {
            if (!done.Add(target.PreviewId))
                continue;
            var values = rowsToAdd
                .Select(r => r.Draft.OwnFields.TryGetValue(target.PreviewId, out var v) ? v.Trim() : "")
                .Where(v => v.Length > 0);

            if (target.IsNew)
            {
                var choices = target.Kind == OwnFieldKind.Choice ? FitChoices(values, []) : null;
                var field = await ownFields.AddAsync(target.Name, target.Kind, choices);
                if (!liveBefore.ContainsKey(field.Id))
                    result.AddField(field.Id);
                if (field.Id != target.PreviewId)
                    moved[target.PreviewId] = field.Id;
            }
            else if (liveBefore.TryGetValue(target.PreviewId, out var existing) && existing.Kind == OwnFieldKind.Choice)
            {
                var known = new List<string>(existing.Choices);
                foreach (var choice in FitChoices(values, known))
                {
                    await ownFields.AddChoiceAsync(existing.Id, choice);
                    known.Add(choice);
                }
            }
        }
        return moved;
    }

    // The distinct values (the first spelling kept) that aren't in the list yet and fit in it, while it has room
    private static List<string> FitChoices(IEnumerable<string> values, List<string> known)
    {
        var all = new List<string>(known);
        var fresh = new List<string>();
        foreach (var value in values)
        {
            if (all.Count >= OwnField.MaxChoices)
                break;
            if (value.Length > OwnField.MaxNameLength || all.Any(c => string.Equals(c.Trim(), value, StringComparison.CurrentCultureIgnoreCase)))
                continue;
            all.Add(value);
            fresh.Add(value);
        }
        return fresh;
    }

    private async Task PlaceAsync(Plant plant, string text, ImportResult result)
    {
        var (roomName, spotName) = RoomName.Split(text);
        if (roomName is null)
            return;

        try
        {
            var (room, roomExisted) = await rooms.AddPlaceAsync(roomName);
            if (!roomExisted)
                result.AddPlace(room.Id);
            plant.PlaceId = room.Id;

            if (spotName is not null)
            {
                var (spot, spotExisted) = await rooms.AddPlaceAsync(spotName, room.Id);
                if (!spotExisted)
                    result.AddPlace(spot.Id);
                plant.PlaceId = spot.Id;
            }
        }
        catch (ArgumentException)
        {
            // A name that can't be a place: the plant goes in without one, and the text is kept
            plant.PlaceId = null;
            var line = $"Room: {text}";
            plant.Notes = string.IsNullOrEmpty(plant.Notes) ? line : $"{plant.Notes}\n{line}";
        }
    }

    /// <summary>
    /// Takes an import back: the plants and care entries are deleted (soft, like deleting them on
    /// their own pages), then the rooms and spots it made, spots first. A place something else is
    /// in now, or that is already gone, stays as it is. The history lines stay, the same as when a
    /// plant is deleted from its page. The fields it made are removed after the plants (their values stay
    /// on the plants that are deleted, like any removed field), while choices it added to a field that
    /// was already there stay.
    /// </summary>
    public async Task UndoAsync(ImportResult result)
    {
        foreach (var id in result.PlantIds)
            await plantRepository.DeleteAsync(id);
        foreach (var id in result.CareLogIds)
            await careLogs.DeleteAsync(id);

        foreach (var id in result.FieldsMade)
            await ownFields.RemoveAsync(id);

        var places = await rooms.GetPlacesAsync();
        foreach (var id in result.PlacesMade.OrderBy(id => places.Find(id)?.IsSpot == true ? 0 : 1))
        {
            try
            {
                await rooms.RemoveAsync(id);
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException)
            {
                // Something else stands there now, or it's gone already
            }
        }
    }
}
