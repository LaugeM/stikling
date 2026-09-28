using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;

namespace Stikling.Core.Rooms;

/// <summary>What a rename touched, for the message afterwards.</summary>
/// <param name="Merged">True when the new name was already in use, so two places became one.</param>
public sealed record RoomRename(string Name, int Plants, int Propagations, bool Merged)
{
    public int Total => Plants + Propagations;
}

/// <summary>
/// Rooms and spots: finding or making the one a form typed in, and renaming. Renaming onto a
/// name that already exists merges the two, which is how duplicates like "Stue" and
/// "Living room" get sorted out.
/// </summary>
public sealed class RoomService(IPlaceRepository places, IPlantRepository plants, IPropagationRepository propagations)
{
    /// <summary>The rooms in use, with the spots inside them.</summary>
    public async Task<IReadOnlyList<Room>> GetAllAsync() =>
        Room.List(await places.GetPlacesAsync(), await plants.GetAllAsync(), await propagations.GetAllAsync());

    public Task<Places> GetPlacesAsync() => places.GetPlacesAsync();

    /// <summary>
    /// The place written as "Living room" or "Living room / On top of the PC", made if it doesn't
    /// exist yet. Null when nothing was written.
    /// </summary>
    public async Task<Guid?> PlaceIdAsync(string? place)
    {
        var (roomName, spotName) = RoomName.Split(place);
        if (roomName is null)
            return null;

        var all = await places.GetPlacesAsync();
        var room = all.RoomNamed(roomName) ?? await AddAsync(new Place { Name = roomName });
        if (spotName is null)
            return room.Id;

        var spot = all.SpotNamed(room.Id, spotName) ?? await AddAsync(new Place { Name = spotName, RoomId = room.Id });
        return spot.Id;
    }

    /// <summary>
    /// Renames a room or a spot. Only the place itself changes, so whatever is in it, and the
    /// spots inside a room, go along without being touched.
    /// </summary>
    public async Task<RoomRename> RenameAsync(Guid id, string? name)
    {
        var all = await places.GetPlacesAsync();
        if (all.Find(id) is not { } place)
            throw new ArgumentException("That room isn't there any more.", nameof(id));

        var renamed = new Place { Name = RoomName.Clean(name) ?? "", RoomId = place.RoomId };
        if (renamed.Validate().FirstOrDefault() is { } error)
            throw new ArgumentException(error, nameof(name));

        var plantCount = (await plants.GetAllAsync()).Count(p => all.IsIn(p.PlaceId, place.Id));
        var propagationCount = (await propagations.GetAllAsync()).Count(p => all.IsIn(p.PlaceId, place.Id));

        // A spot whose room was deleted on another device has no room to look for a twin in, so
        // it's only renamed
        var existing = !place.IsSpot ? all.RoomNamed(renamed.Name)
            : all.RoomOf(place.Id) is { } room ? all.SpotNamed(room.Id, renamed.Name)
            : null;

        if (existing is null || existing.Id == place.Id)
        {
            place.Name = renamed.Name;
            await places.SaveAsync(place);
            return new RoomRename(place.Name, plantCount, propagationCount, Merged: false);
        }

        // A spot in both rooms becomes one spot too. The others follow the room they were in.
        if (!place.IsSpot)
        {
            foreach (var spot in all.SpotsIn(place.Id).ToList())
            {
                if (all.SpotNamed(existing.Id, spot.Name) is { } twin)
                    await MergeAsync(spot, twin);
            }
        }

        await MergeAsync(place, existing);
        return new RoomRename(existing.Name, plantCount, propagationCount, Merged: true);
    }

    private async Task MergeAsync(Place from, Place into)
    {
        from.MergedIntoId = into.Id;
        await places.SaveAsync(from);
        await places.DeleteAsync(from.Id);
    }

    private async Task<Place> AddAsync(Place place)
    {
        await places.SaveAsync(place);
        return place;
    }
}
