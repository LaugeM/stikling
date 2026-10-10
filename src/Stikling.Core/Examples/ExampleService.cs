using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Timeline;

namespace Stikling.Core.Examples;

/// <summary>Loads the example family on request and takes it away again, with whatever was added to it.</summary>
public sealed class ExampleService(
    IPlantRepository plants,
    IPropagationRepository propagations,
    IPhotoRepository photos,
    ITimelineRepository timeline,
    ICareLogRepository careLogs,
    IExamplePhotos images,
    TimeProvider time)
{
    /// <summary>True while any example plant or propagation is there.</summary>
    public async Task<bool> IsLoadedAsync()
    {
        foreach (var id in ExampleFamily.PlantIds)
            if (await plants.GetAsync(id) is not null)
                return true;
        foreach (var id in ExampleFamily.PropagationIds)
            if (await propagations.GetAsync(id) is not null)
                return true;
        return false;
    }

    /// <summary>
    /// Stores the five images first, so a failure leaves nothing half loaded: the images already
    /// stored are freed and no record is written. After a removal the same ids come back live.
    /// </summary>
    public async Task LoadAsync()
    {
        var sizes = new Dictionary<Guid, (int Width, int Height)>();
        try
        {
            foreach (var (photoId, fileName) in ExampleFamily.Images)
                sizes[photoId] = await images.StoreAsync(photoId, fileName);
        }
        catch
        {
            foreach (var id in sizes.Keys)
                await images.RemoveBytesAsync(id);
            throw;
        }

        var records = ExampleFamily.Build(time, sizes);
        foreach (var photo in records.Photos)
            await photos.AddAsync(photo);
        foreach (var plant in records.Plants)
            await plants.SaveAsync(plant);
        foreach (var propagation in records.Propagations)
            await propagations.SaveAsync(propagation);
        foreach (var entry in records.Entries)
            await timeline.AddAsync(entry);
    }

    /// <summary>
    /// Deletes the example and everything added to it: notes, photos and care logs. Plants and
    /// propagations made from it stay, and keep their link to the deleted parent.
    /// </summary>
    public async Task RemoveAsync()
    {
        var subjects = new List<Guid>([.. ExampleFamily.PlantIds, .. ExampleFamily.PropagationIds]);

        var photoIds = new HashSet<Guid>(ExampleFamily.PhotoIds);
        var entryIds = new HashSet<Guid>();
        foreach (var subject in subjects)
        {
            foreach (var photo in await photos.GetForAsync(subject))
                photoIds.Add(photo.Id);
            foreach (var entry in await timeline.GetForAsync(subject))
                entryIds.Add(entry.Id);
        }
        var logIds = (await careLogs.GetAllAsync())
            .Where(log => ExampleFamily.PlantIds.Contains(log.PlantId))
            .Select(log => log.Id)
            .ToList();

        foreach (var id in entryIds)
            await timeline.DeleteAsync(id);
        foreach (var id in logIds)
            await careLogs.DeleteAsync(id);
        foreach (var id in photoIds)
        {
            if (await photos.GetAsync(id) is not null)
                await photos.DeleteAsync(id);
            await images.RemoveBytesAsync(id);
        }
        foreach (var id in ExampleFamily.PlantIds)
            await plants.DeleteAsync(id);
        foreach (var id in ExampleFamily.PropagationIds)
            await propagations.DeleteAsync(id);
    }
}
