using Stikling.Core.Models;

namespace Stikling.Core.Photos;

/// <summary>
/// What the photo viewer does with the circles and arrows drawn on photos. Every change is saved at
/// once. Marks with a shape this version doesn't know are left out, and never touched.
/// </summary>
public sealed class PhotoMarkupService(IPhotoMarkupRepository marks)
{
    /// <summary>The marks of the photos, grouped by photo, in the order they were made. Photos without any aren't in it.</summary>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<PhotoMarkup>>> GetForPhotosAsync(IEnumerable<Guid> photoIds)
    {
        var wanted = photoIds.ToHashSet();
        if (wanted.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<PhotoMarkup>>();

        return (await marks.GetAllAsync())
            .Where(m => m.Shape is not null && wanted.Contains(m.PhotoId))
            .OrderBy(m => m.CreatedAt)
            .GroupBy(m => m.PhotoId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PhotoMarkup>)g.ToList());
    }

    /// <summary>Adds a mark, or returns null when it would be too small to see.</summary>
    public async Task<PhotoMarkup?> AddAsync(Guid photoId, MarkupShape shape, double x1, double y1, double x2, double y2)
    {
        var (a, b, c, d) = (PhotoMark.Tidy(x1), PhotoMark.Tidy(y1), PhotoMark.Tidy(x2), PhotoMark.Tidy(y2));
        if (PhotoMarkup.IsTooSmall(shape, a, b, c, d))
            return null;

        var markup = new PhotoMarkup { PhotoId = photoId, Shape = shape, X1 = a, Y1 = b, X2 = c, Y2 = d };
        await marks.SaveAsync(markup);
        return markup;
    }

    /// <summary>Moves a mark's points. A mark that would end up too small, or isn't there, is left as it is and null is returned.</summary>
    public async Task<PhotoMarkup?> MoveAsync(Guid id, double x1, double y1, double x2, double y2)
    {
        if (await Find(id) is not { Shape: { } shape } markup)
            return null;

        var (a, b, c, d) = (PhotoMark.Tidy(x1), PhotoMark.Tidy(y1), PhotoMark.Tidy(x2), PhotoMark.Tidy(y2));
        if (PhotoMarkup.IsTooSmall(shape, a, b, c, d))
            return null;

        (markup.X1, markup.Y1, markup.X2, markup.Y2) = (a, b, c, d);
        await marks.SaveAsync(markup);
        return markup;
    }

    public async Task<PhotoMarkup?> SetLabelAsync(Guid id, string? label)
    {
        if (await Find(id) is not { } markup)
            return null;

        markup.Label = PhotoMarkup.TidyLabel(label);
        await marks.SaveAsync(markup);
        return markup;
    }

    public async Task RemoveAsync(Guid id)
    {
        if (await Find(id) is not null)
            await marks.DeleteAsync(id);
    }

    private async Task<PhotoMarkup?> Find(Guid id) =>
        (await marks.GetAllAsync()).FirstOrDefault(m => m.Id == id && m.Shape is not null);
}
