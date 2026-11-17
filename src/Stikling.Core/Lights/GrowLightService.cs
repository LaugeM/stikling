using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.Timeline;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Lights;

/// <summary>Grow lights, and the quick way of saying a room or spot has one.</summary>
/// <remarks>
/// With the repositories for plants, propagations, places and the timeline, adding, removing or moving a light also
/// writes a line on the history of each plant and propagation it starts or stops lighting. Without them it only saves.
/// </remarks>
public sealed class GrowLightService(
    IGrowLightRepository lights,
    IPlantRepository? plants = null,
    IPropagationRepository? propagations = null,
    IPlaceRepository? places = null,
    ITimelineRepository? timeline = null,
    TimeProvider? time = null)
{
    /// <summary>The name given to a light added by saying a place is lit.</summary>
    public const string DefaultName = "Grow light";

    public Task<IReadOnlyList<GrowLight>> GetAllAsync() => lights.GetAllAsync();

    /// <summary>Saves a light with its name trimmed. Throws if it has no name.</summary>
    public async Task SaveAsync(GrowLight light)
    {
        light.Name = light.Name?.Trim() ?? "";
        light.Notes = Clean(light.Notes);
        var errors = light.Validate();
        if (errors.Count > 0)
            throw new InvalidOperationException(errors[0]);

        var stored = await lights.GetAsync(light.Id);
        var was = stored is { IsDeleted: false } ? stored.PlaceId : null;
        var wasName = stored?.Name ?? light.Name;
        var now = light.DeletedAt is null ? light.PlaceId : null;
        await lights.SaveAsync(light);

        // The history is written after the save, so a failure in it doesn't leave the light unsaved
        if (!SamePlace(await GetPlacesAsync(), was, now))
        {
            // Moving a light between a room and its own spot leaves some plants lit all the way, so they get no lines
            var known = await GetPlacesAsync();
            await WriteAsync(was, "GrowLightRemoved", wasName, now is null ? null : known.IdsIn(now));
            await WriteAsync(now, "GotGrowLight", light.Name, was is null ? null : known.IdsIn(was));
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var stored = await lights.GetAsync(id);
        var wasPlace = stored is { IsDeleted: false } ? stored.PlaceId : null;
        var wasName = stored?.Name ?? "";
        await lights.DeleteAsync(id);
        await WriteAsync(wasPlace, "GrowLightRemoved", wasName);
    }

    /// <summary>
    /// What a move of a plant or propagation between two places adds to its history about grow lights. Pass it to the
    /// update that saves the move.
    /// </summary>
    public async Task<Func<Guid?, Guid?, IReadOnlyList<HistoryLine>>> MoveLinesAsync()
    {
        var all = await lights.GetAllAsync();
        var known = await GetPlacesAsync();
        return (from, to) => GrowLights.MoveLines(from, to, all, known);
    }

    private async Task<Places> GetPlacesAsync() => places is null ? Places.None : await places.GetPlacesAsync();

    private static bool SamePlace(Places known, Guid? a, Guid? b) =>
        (known.Find(a)?.Id ?? a) == (known.Find(b)?.Id ?? b);

    // One entry on every active plant and propagation standing where the light shines
    private async Task WriteAsync(Guid? placeId, string key, string name, IReadOnlySet<Guid>? skip = null)
    {
        if (placeId is null || plants is null || propagations is null || places is null || timeline is null)
            return;

        var known = await places.GetPlacesAsync();
        if (known.Find(placeId) is not { } place)
            return;

        // A light on a room shines on its spots too
        var here = known.IdsIn(place.Id);
        var at = (time ?? TimeProvider.System).GetUtcNow();
        var line = new[] { HistoryText.Line(key, HistoryArg.Of(name)) };

        foreach (var plant in await plants.GetAllAsync())
        {
            if (plant.IsDeleted || plant.Status != PlantStatus.Active || plant.PlaceId is not { } id || !here.Contains(id) || skip?.Contains(id) == true)
                continue;
            await AddEntryAsync(SubjectType.Plant, plant.Id, at, line);
        }

        foreach (var propagation in await propagations.GetAllAsync())
        {
            if (propagation.IsDeleted || !propagation.IsActive || propagation.PlaceId is not { } id || !here.Contains(id) || skip?.Contains(id) == true)
                continue;
            await AddEntryAsync(SubjectType.Propagation, propagation.Id, at, line);
        }
    }

    private Task AddEntryAsync(SubjectType type, Guid id, DateTimeOffset at, HistoryLine[] line)
    {
        var entry = new TimelineEntry { SubjectType = type, SubjectId = id, Kind = TimelineKind.Change, OccurredAt = at };
        HistoryText.Set(entry, line);
        return timeline!.AddAsync(entry);
    }

    /// <summary>The fixed id of the light added by saying a place is lit, named after the place like a put-off's.</summary>
    public static Guid DefaultIdFor(Guid placeId) => PutOff.IdFor($"growlight:{placeId}");

    /// <summary>
    /// Says a room or spot is lit or not. Turning it on adds one light there if there is none, and
    /// turning it off removes every light pointing at it, since one left behind would keep it lit.
    /// </summary>
    public async Task SetLitAsync(Guid placeId, bool lit)
    {
        var here = (await lights.GetAllAsync()).Where(l => !l.IsDeleted && l.PlaceId == placeId).ToList();
        if (lit)
        {
            if (here.Count > 0)
                return;

            // The light gets an id from the place, so two devices lighting the same place make
            // one record. One turned off before is brought back rather than made again.
            var id = DefaultIdFor(placeId);
            var light = await lights.GetAsync(id) ?? new GrowLight { Id = id, Name = DefaultName, PlaceId = placeId };
            light.DeletedAt = null;
            light.Name = string.IsNullOrWhiteSpace(light.Name) ? DefaultName : light.Name;
            light.PlaceId = placeId;
            await SaveAsync(light);
            return;
        }

        foreach (var light in here)
            await DeleteAsync(light.Id);
    }
}
