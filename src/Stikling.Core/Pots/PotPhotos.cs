using Stikling.Core.Models;

namespace Stikling.Core.Pots;

/// <summary>Which photo stands for a pot, on the pot library, the plant page and the pot picker.</summary>
public static class PotPhotos
{
    /// <summary>
    /// The pot's chosen cover, or its newest photo when none is chosen or the chosen one was
    /// deleted, which can happen on another device. Null when the pot has no photos.
    /// </summary>
    public static Photo? Cover(Pot pot, IEnumerable<Photo> photos)
    {
        var own = photos.Where(p => p.SubjectType == SubjectType.Pot && p.SubjectId == pot.Id && !p.IsDeleted).ToList();
        return own.FirstOrDefault(p => p.Id == pot.CoverPhotoId) ?? own.MaxBy(p => p.TakenAt);
    }

    /// <summary>The photo for each pot that has one, by the pot's id.</summary>
    public static IReadOnlyDictionary<Guid, Photo> Covers(IEnumerable<Pot> pots, IEnumerable<Photo> photos)
    {
        var bySubject = photos.ToLookup(p => p.SubjectId);
        var covers = new Dictionary<Guid, Photo>();
        foreach (var pot in pots)
        {
            if (Cover(pot, bySubject[pot.Id]) is { } cover)
                covers[pot.Id] = cover;
        }
        return covers;
    }
}
