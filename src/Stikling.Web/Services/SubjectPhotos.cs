using Microsoft.AspNetCore.Components;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Timeline;

namespace Stikling.Web.Services;

/// <summary>
/// Saves the photo chosen in a file input straight onto a plant's or propagation's history, for the
/// places that take a photo without opening its page (the photo round on Today and going round).
/// </summary>
public sealed class SubjectPhotos(
    PhotoService photos, TimelineService timeline, IPlantRepository plants, IPropagationRepository propagations)
{
    /// <summary>
    /// Saves the photos in <paramref name="input"/>. The first photo of something becomes its cover,
    /// the same as on its own page. Returns what to tell the person and whether anything was saved.
    /// </summary>
    public async Task<(string? Message, bool Saved)> TakeAsync(ElementReference input, Entity subject)
    {
        var subjectType = subject is Plant ? SubjectType.Plant : SubjectType.Propagation;
        var result = await photos.SaveFromInputAsync(input, subjectType, subject.Id);
        return (result.Message(guessesMatter: false), await FinishAsync(subject, result));
    }

    /// <summary>
    /// What follows saving photos of <paramref name="subject"/>: they go on its history, and the first
    /// one becomes its cover when it has none. Returns whether anything was saved.
    /// </summary>
    public async Task<bool> FinishAsync(Entity subject, PhotoSaveResult result)
    {
        if (result.Saved.Count == 0)
            return false;

        var subjectType = subject is Plant ? SubjectType.Plant : SubjectType.Propagation;
        await timeline.AddPhotosAsync(subjectType, subject.Id, result.Saved);
        await SetCoverAsync(subject, PhotoEntries.Cover(result.Saved).Id);
        return true;
    }

    private async Task SetCoverAsync(Entity subject, Guid photoId)
    {
        switch (subject)
        {
            case Plant { CoverPhotoId: null } plant:
                plant.CoverPhotoId = photoId;
                await plants.SaveAsync(plant);
                break;
            case Propagation { CoverPhotoId: null } propagation:
                propagation.CoverPhotoId = photoId;
                await propagations.SaveAsync(propagation);
                break;
        }
    }
}
