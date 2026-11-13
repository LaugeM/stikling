using Strings = Stikling.Core.Resources;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using static Stikling.Core.Text.Format;

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
    /// <summary>Every room, with the spots inside it, including the ones with nothing in them.</summary>
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
    /// Adds a room, or a spot in the room with this id. When the name is already taken there, the
    /// one that was there is returned instead, with <c>Existed</c> set.
    /// </summary>
    /// <remarks>The room is found by id, so a spot goes where it was asked for even when two rooms share a name after a sync.</remarks>
    public async Task<(Place Place, bool Existed)> AddPlaceAsync(string? name, Guid? roomId = null)
    {
        // Validate turns down a room with a / in it, and a name that is nothing but slashes
        var added = new Place { Name = RoomName.Clean(name) ?? "", RoomId = roomId };
        if (added.Validate().FirstOrDefault() is { } error)
            throw new ArgumentException(error, nameof(name));

        var all = await places.GetPlacesAsync();
        Place? existing;
        if (roomId is { } id)
        {
            if (all.Find(id) is not { } room)
                throw new ArgumentException(Strings.Shared.RoomGone, nameof(roomId));
            added.RoomId = room.Id;
            existing = all.SpotNamed(room.Id, added.Name);
        }
        else
            existing = all.RoomNamed(added.Name);

        return existing is not null ? (existing, true) : (await AddAsync(added), false);
    }

    /// <summary>
    /// Renames a room or a spot. Only the place itself changes, so whatever is in it, and the
    /// spots inside a room, go along without being touched.
    /// </summary>
    public async Task<RoomRename> RenameAsync(Guid id, string? name)
    {
        var all = await places.GetPlacesAsync();
        if (all.Find(id) is not { } place)
            throw new ArgumentException(Strings.Shared.RoomGone, nameof(id));

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

    /// <summary>Marks a room or spot as outdoors, or as indoors again.</summary>
    public async Task SetOutdoorsAsync(Guid id, bool on)
    {
        var all = await places.GetPlacesAsync();
        if (all.Find(id) is not { } place)
            throw new ArgumentException(Strings.Shared.RoomGone, nameof(id));

        if (place.Outdoors == on)
            return;
        place.Outdoors = on;
        await places.SaveAsync(place);
    }

    /// <summary>
    /// Removes a room or spot with nothing in it. A room goes together with its empty spots. Anything
    /// still in it, or in a spot of the room, makes this throw.
    /// </summary>
    /// <remarks>
    /// A plant another device moves into the place at the same time can end up pointing at a
    /// removed place. That shows as no location, the same as a place deleted on another device.
    /// A spot another device adds to a removed room at the same time shows as a room of its own
    /// (see <see cref="Places.Rooms"/>).
    /// </remarks>
    public async Task RemoveAsync(Guid id)
    {
        var all = await places.GetPlacesAsync();
        if (all.Find(id) is not { } place)
            throw new ArgumentException(Strings.Shared.RoomGone, nameof(id));

        var held = (await plants.GetAllAsync()).Count(p => !p.IsDeleted && all.IsIn(p.PlaceId, place.Id))
            + (await propagations.GetAllAsync()).Count(p => !p.IsDeleted && all.IsIn(p.PlaceId, place.Id));
        if (held > 0)
            throw new InvalidOperationException(F(Strings.Shared.PlaceNotEmpty, place.Name));

        if (!place.IsSpot)
        {
            foreach (var spot in all.SpotsIn(place.Id).ToList())
                await places.DeleteAsync(spot.Id);
        }

        await places.DeleteAsync(place.Id);
    }

    /// <summary>
    /// Sets the note of a room or spot. Empty text clears it. Throws when the place isn't there.
    /// </summary>
    public async Task SetNotesAsync(Guid id, string? text)
    {
        var all = await places.GetPlacesAsync();
        if (all.Find(id) is not { } place)
            throw new ArgumentException(Strings.Shared.RoomGone, nameof(id));

        var notes = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (place.Notes == notes)
            return;
        place.Notes = notes;
        await places.SaveAsync(place);
    }

    /// <summary>
    /// What a room or spot's own page shows: the place, what stands in it (and, for a room, in its
    /// spots) and its spots. Null when the place isn't there. The id of a merged place gives the
    /// place it went into.
    /// </summary>
    public async Task<RoomPageView?> GetPageAsync(Guid id)
    {
        var all = await places.GetPlacesAsync();
        if (all.Find(id) is not { } place)
            return null;

        // A spot whose room was removed on another device is shown as a room of its own
        var room = place.IsSpot ? all.Find(place.RoomId) : null;

        // Plants no longer in the collection and finished propagations aren't listed, the same as
        // in the Plants and Propagations lists
        string? SpotNameOf(Guid? placeId) =>
            room is null && all.Find(placeId) is { } at && at.Id != place.Id ? at.Name : null;

        var here = (await plants.GetAllAsync())
            .Where(p => !p.IsDeleted && p.InCollection && all.IsIn(p.PlaceId, place.Id))
            .Select(p => new RoomPagePlant(p, SpotNameOf(p.PlaceId)))
            .OrderBy(p => p.SpotName is not null).ThenBy(p => p.SpotName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Plant.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var cuttings = (await propagations.GetAllAsync())
            .Where(p => !p.IsDeleted && p.IsActive && all.IsIn(p.PlaceId, place.Id))
            .Select(p => new RoomPagePropagation(p, SpotNameOf(p.PlaceId)))
            .OrderBy(p => p.SpotName is not null).ThenBy(p => p.SpotName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Propagation.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var spots = room is not null ? []
            : all.SpotsIn(place.Id)
                .Select(s => new RoomSpot(s.Id, s.Name, all.NameOf(s.Id)!,
                    here.Count(p => all.Find(p.Plant.PlaceId)?.Id == s.Id),
                    cuttings.Count(p => all.Find(p.Propagation.PlaceId)?.Id == s.Id)))
                .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        return new RoomPageView(place, all.NameOf(place.Id) ?? place.Name, room, room?.Outdoors == true, spots, here, cuttings);
    }

    private async Task MergeAsync(Place from, Place into)
    {
        // The place that stays keeps its note and takes the other one's when it has none, or gets
        // both, so nothing a person wrote is lost
        var notes = (into.Notes, from.Notes) switch
        {
            (null or "", var other) => other,
            (var kept, null or "") => kept,
            // The same note on both, as after merging twice, is kept once
            var (kept, other) when kept.Trim() == other.Trim() => kept,
            var (kept, other) => kept + "\n\n" + other
        };
        if (notes != into.Notes)
        {
            into.Notes = notes;
            await places.SaveAsync(into);
        }

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
