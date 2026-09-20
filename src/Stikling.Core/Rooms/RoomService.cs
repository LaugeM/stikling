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
/// Renaming a room or a spot everywhere it is used. Renaming onto a name that already exists
/// merges the two, which is how duplicates like "Stue" and "Living room" get sorted out.
/// </summary>
public sealed class RoomService(IPlantRepository plants, IPropagationRepository propagations)
{
    /// <summary>The rooms in use, with the spots inside them.</summary>
    public async Task<IReadOnlyList<Room>> GetAllAsync() =>
        Room.List(await plants.GetAllAsync(), await propagations.GetAllAsync());

    /// <summary>
    /// Renames a room or a spot on every plant and propagation in it. Spots move with their room,
    /// so renaming "Stue" also turns "Stue / Windowsill" into "Living room / Windowsill".
    /// </summary>
    public async Task<RoomRename> RenameAsync(string? from, string? to)
    {
        if (RoomName.Clean(from) is not { } source)
            throw new ArgumentException("Say which room to rename.", nameof(from));
        if (RoomName.Clean(to) is not { } target)
            throw new ArgumentException("Give the room a name.", nameof(to));

        var allPlants = await plants.GetAllAsync();
        var allPropagations = await propagations.GetAllAsync();

        // Worked out before anything moves, while the old and the new name still tell each other apart
        var merged = allPlants.Select(p => p.Location)
            .Concat(allPropagations.Select(p => p.Location))
            .Any(place => RoomName.IsIn(place, target) && !RoomName.IsIn(place, source));

        var changedPlants = 0;
        foreach (var plant in allPlants.Where(p => RoomName.IsIn(p.Location, source)))
        {
            var renamed = RoomName.Rename(plant.Location, source, target);
            if (renamed == plant.Location)
                continue;

            plant.Location = renamed;
            await plants.SaveAsync(plant);
            changedPlants++;
        }

        var changedPropagations = 0;
        foreach (var propagation in allPropagations.Where(p => RoomName.IsIn(p.Location, source)))
        {
            var renamed = RoomName.Rename(propagation.Location, source, target);
            if (renamed == propagation.Location)
                continue;

            propagation.Location = renamed;
            await propagations.SaveAsync(propagation);
            changedPropagations++;
        }

        return new RoomRename(target, changedPlants, changedPropagations, merged);
    }
}
