using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Web.Services;

/// <summary>
/// Which photos are reference photos, so a cover that became one (on another device, say) stops
/// showing as the cover everywhere it's drawn. Worked out when it's shown: the stored cover is never
/// changed, and a cover whose photo isn't here yet shows as before.
/// </summary>
public sealed class CoverPhotos(IPhotoRepository photos)
{
    private IReadOnlySet<Guid> references = new HashSet<Guid>();
    private Task? loading;

    /// <summary>Raised after the reference photos changed, so what shows covers can be drawn again.</summary>
    public event Action? Changed;

    /// <summary>The reference photos of plants and propagations. Empty until <see cref="EnsureLoadedAsync"/> is done.</summary>
    public IReadOnlySet<Guid> ReferenceIds => references;

    /// <summary>The cover to show: the chosen one, or null when it's a reference photo.</summary>
    public Guid? Shown(Guid? cover)
    {
        // The first drawing starts reading; Changed draws again when it's done
        _ = EnsureLoadedAsync();
        return PhotoKinds.Shown(cover, references);
    }

    /// <summary>Reads the reference photos once, for whoever needs them before drawing.</summary>
    public Task EnsureLoadedAsync() => loading ??= RefreshAsync();

    /// <summary>Reads them again, e.g. when sync brought changes.</summary>
    public async Task RefreshAsync()
    {
        var found = (await photos.GetOfKindAsync(SubjectType.Plant))
            .Concat(await photos.GetOfKindAsync(SubjectType.Propagation))
            .Where(p => p.IsReference)
            .Select(p => p.Id)
            .ToHashSet();
        var changed = !found.SetEquals(references);
        references = found;
        if (changed)
            Changed?.Invoke();
    }
}
