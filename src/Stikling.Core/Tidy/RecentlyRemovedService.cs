using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Sync;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tidy;

/// <summary>The photo images on this device, which a removed photo keeps for 30 days.</summary>
public interface IRemovedPhotoImages
{
    /// <summary>True when the photo's image data is on this device.</summary>
    Task<bool> HasBytesAsync(Guid id);

    /// <summary>Puts the photo's images on the list to send to the server again, for a photo that is put back.</summary>
    Task QueueUploadAsync(Guid id);
}

/// <summary>
/// Lists what was removed in the last 30 days and puts it back. A removal is a soft delete, so putting back
/// is saving the record again without its <c>DeletedAt</c>, through the repository so its <c>UpdatedAt</c> is
/// new and the put-back record wins over the removal on every other device.
/// </summary>
public sealed class RecentlyRemovedService(
    IRecordStore store,
    IPlantRepository plants,
    IPropagationRepository propagations,
    IPhotoRepository photos,
    ITimelineRepository timeline,
    IRemovedPhotoImages images,
    TimeProvider time)
{
    public async Task<RemovedReport> GetAsync()
    {
        var photoList = await store.GetAllAsync(RecordKinds.Photos);
        var recent = photoList.Where(p => p.DeletedAt is { } at && RemovedRules.IsRecent(at, time.GetUtcNow()) && RemovedRules.IsFull(p));
        var withImages = new HashSet<Guid>();
        foreach (var photo in recent)
        {
            if (await images.HasBytesAsync(photo.Id))
                withImages.Add(photo.Id);
        }

        return RemovedRules.Build(
            await store.GetAllAsync(RecordKinds.Plants),
            await store.GetAllAsync(RecordKinds.Propagations),
            photoList,
            await store.GetAllAsync(RecordKinds.Timeline),
            withImages,
            time.GetUtcNow());
    }

    /// <summary>
    /// Puts the thing back. A photo or a note whose plant or propagation is removed too brings that back as well.
    /// Does nothing and returns false when it was put back already, or when only its stripped copy is here.
    /// </summary>
    public async Task<bool> PutBackAsync(RemovedItem item) => item.Kind switch
    {
        RemovedKind.Plant => await PutBackPlantAsync(item.Id),
        RemovedKind.Propagation => await PutBackPropagationAsync(item.Id),
        RemovedKind.Photo => await PutBackPhotoAsync(item.Id),
        _ => await PutBackNoteAsync(item.Id)
    };

    private async Task<bool> PutBackPlantAsync(Guid id)
    {
        var plant = (await store.GetAllAsync(RecordKinds.Plants)).FirstOrDefault(p => p.Id == id);
        if (plant is null || !plant.IsDeleted || !RemovedRules.IsFull(plant))
            return false;

        plant.DeletedAt = null;
        await plants.SaveAsync(plant);
        return true;
    }

    private async Task<bool> PutBackPropagationAsync(Guid id)
    {
        var propagation = (await store.GetAllAsync(RecordKinds.Propagations)).FirstOrDefault(p => p.Id == id);
        if (propagation is null || !propagation.IsDeleted || !RemovedRules.IsFull(propagation))
            return false;

        propagation.DeletedAt = null;
        await propagations.SaveAsync(propagation);
        return true;
    }

    private async Task PutBackOwnerAsync(SubjectType type, Guid id)
    {
        if (type == SubjectType.Plant)
            await PutBackPlantAsync(id);
        else if (type == SubjectType.Propagation)
            await PutBackPropagationAsync(id);
    }

    private async Task<bool> PutBackPhotoAsync(Guid id)
    {
        var photo = (await store.GetAllAsync(RecordKinds.Photos)).FirstOrDefault(p => p.Id == id);
        if (photo is null || !photo.IsDeleted || !RemovedRules.IsFull(photo) || !await images.HasBytesAsync(id))
            return false;

        // A photo of something that is removed too would be put back where nobody can see it
        await PutBackOwnerAsync(photo.SubjectType, photo.SubjectId);

        var removedAt = photo.DeletedAt!.Value;
        photo.DeletedAt = null;
        await photos.UpdateAsync(photo);
        await images.QueueUploadAsync(id);

        // Deleting the last photo of an entry with no text deletes the entry in the same step, so it comes back
        // too. An entry deleted on its own at another time stays deleted.
        var emptied = (await store.GetAllAsync(RecordKinds.Timeline)).Where(e =>
            e.IsDeleted && e.SubjectId == photo.SubjectId && string.IsNullOrWhiteSpace(e.Text) && e.PhotoIds.Contains(id)
            && (e.DeletedAt!.Value - removedAt).Duration() <= TimeSpan.FromMinutes(1));
        foreach (var entry in emptied)
        {
            entry.DeletedAt = null;
            await timeline.UpdateAsync(entry);
        }
        return true;
    }

    private async Task<bool> PutBackNoteAsync(Guid id)
    {
        var entry = (await store.GetAllAsync(RecordKinds.Timeline)).FirstOrDefault(e => e.Id == id);
        if (entry is null || !entry.IsDeleted || !RemovedRules.IsFull(entry))
            return false;

        await PutBackOwnerAsync(entry.SubjectType, entry.SubjectId);

        entry.DeletedAt = null;
        await timeline.UpdateAsync(entry);
        return true;
    }
}
