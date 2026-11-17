using Stikling.Core.Models;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;

namespace Stikling.Core.Import;

/// <summary>What an import of propagations added, so the result can say so and Undo can take it back.</summary>
public sealed class PropagationImportResult
{
    private readonly List<Guid> propagationIds = [];
    private readonly List<Guid> placesMade = [];
    private readonly List<ImportSkipped> skipped = [];

    public IReadOnlyList<Guid> PropagationIds => propagationIds;

    /// <summary>The rooms and spots that didn't exist before, in the order they were made.</summary>
    public IReadOnlyList<Guid> PlacesMade => placesMade;

    public IReadOnlyList<ImportSkipped> Skipped => skipped;

    public int Added => propagationIds.Count;

    public int RoomsMade => placesMade.Count;

    internal void AddPropagation(Guid id) => propagationIds.Add(id);
    internal void AddPlace(Guid id) => placesMade.Add(id);
    internal void Skip(ImportSkipped row) => skipped.Add(row);
}

/// <summary>
/// Adds the propagations from a spreadsheet through the services the rest of the app uses, so each one gets
/// its history line, its parent's history line and an ordinary room. Nothing new is stored.
/// </summary>
public sealed class PropagationImportService(
    PropagationService propagations,
    RoomService rooms,
    TimeProvider time)
{
    /// <summary>
    /// What the latest <see cref="ImportAsync"/> has added so far. It is filled in as the import goes, so when a
    /// repository throws half way through, what was added before that is still here for <see cref="UndoAsync"/>.
    /// </summary>
    public PropagationImportResult? Last { get; private set; }

    /// <summary>
    /// Adds the rows it is given, in order, and tells <paramref name="progress"/> how many rows are done after
    /// each. A row that can't be added is skipped with the reason and the rest go on. An exception from a
    /// repository stops the import and is thrown. A room or spot that doesn't exist is made; if its name can't
    /// be a place, the propagation is added without one and the text goes into About it.
    /// </summary>
    public async Task<PropagationImportResult> ImportAsync(
        IReadOnlyList<PropagationImportRow> rows, Func<Enum, string> label, IProgress<int>? progress = null)
    {
        var result = Last = new PropagationImportResult();
        var today = time.Today();

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
            var propagation = row.Draft.Copy();
            if (row.Place is not null)
                await PlaceAsync(propagation, row.Place, result);

            await propagations.CreateAsync(propagation, label);
            result.AddPropagation(propagation.Id);
            progress?.Report(i + 1);
        }
        return result;
    }

    private async Task PlaceAsync(Propagation propagation, string text, PropagationImportResult result)
    {
        var (roomName, spotName) = RoomName.Split(text);
        if (roomName is null)
            return;

        try
        {
            var (room, roomExisted) = await rooms.AddPlaceAsync(roomName);
            if (!roomExisted)
                result.AddPlace(room.Id);
            propagation.PlaceId = room.Id;

            if (spotName is not null)
            {
                var (spot, spotExisted) = await rooms.AddPlaceAsync(spotName, room.Id);
                if (!spotExisted)
                    result.AddPlace(spot.Id);
                propagation.PlaceId = spot.Id;
            }
        }
        catch (ArgumentException)
        {
            // A name that can't be a place: it goes in without one, and the text is kept
            propagation.PlaceId = null;
            var line = $"Room: {text}";
            propagation.Notes = string.IsNullOrEmpty(propagation.Notes) ? line : $"{propagation.Notes}\n{line}";
        }
    }

    /// <summary>
    /// Takes an import back: the propagations are deleted (soft, like deleting them on their own pages), then the
    /// rooms and spots it made, spots first. A place something else is in now, or that is already gone, stays as
    /// it is. The history lines stay, the same as when a propagation is deleted from its page.
    /// </summary>
    public async Task UndoAsync(PropagationImportResult result)
    {
        foreach (var id in result.PropagationIds)
            await propagations.DeleteAsync(id);

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
