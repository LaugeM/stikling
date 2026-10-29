using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Sharing;

/// <summary>One tile of the swap list picture: a photo with a name and a count under it.</summary>
/// <param name="Tone">"propagation" for cuttings, "plant" for the rest, as on the share card.</param>
/// <param name="PhotoId">The cover photo of the first record in the group that has one, or null.</param>
public sealed record SwapTile(string Name, string Detail, string Tone, Guid? PhotoId, PhotoFrame? Frame);

/// <summary>The swap list as tiles for a picture, in the same groups and order as the shared text.</summary>
public static class SwapTiles
{
    /// <summary>Tiles to a picture: 9 on a square, 12 on 4:5 and 9:16.</summary>
    public static int PerPicture(string format) => format == "square" ? 9 : 12;

    /// <summary>
    /// One tile per line of the text: cuttings, then cuttings on request, then plants. The same name
    /// and stage are added up, and a plant with a collection number is on its own.
    /// </summary>
    /// <param name="photos">Used to find how a cover photo is framed.</param>
    public static IReadOnlyList<SwapTile> For(
        IEnumerable<Plant> plants,
        IEnumerable<Propagation> propagations,
        IEnumerable<Photo>? photos,
        bool nicknames)
    {
        var frames = new Dictionary<Guid, PhotoFrame?>();
        var references = new HashSet<Guid>();
        foreach (var photo in photos ?? [])
        {
            frames[photo.Id] = photo.Frame;
            if (photo.IsReference)
                references.Add(photo.Id);
        }

        var byName = StringComparer.CurrentCultureIgnoreCase;
        string NameOf(string? nick, string? g, string? s, string? c, NameQualifier? q, string? n) => SwapLists.ShareName(nick, g, s, c, null, nicknames, q, n);

        (Guid? Id, PhotoFrame? Frame) CoverOf(IEnumerable<(string Name, Guid? Cover)> members)
        {
            var id = members.OrderBy(m => m.Name, byName).Select(m => m.Cover).FirstOrDefault(c => c is not null && !references.Contains(c.Value));
            return (id, id is { } found ? frames.GetValueOrDefault(found) : null);
        }

        var tiles = new List<SwapTile>();

        var cuttings = propagations.Where(p => p.SpareAvailable > 0)
            .GroupBy(p => (Name: NameOf(p.Nickname, p.Genus, p.Species, p.Cultivar, p.NameQualifier, p.NameNote), p.Stage))
            .OrderBy(g => g.Key.Name, byName).ThenBy(g => g.Key.Stage);
        foreach (var group in cuttings)
        {
            var count = group.Sum(p => p.SpareAvailable);
            var (id, frame) = CoverOf(group.Select(p => (p.DisplayName, p.CoverPhotoId)));
            tiles.Add(new SwapTile(group.Key.Name,
                string.Format(System.Globalization.CultureInfo.CurrentCulture, count == 1 ? Strings.Shared.SwapTileCuttings_One : Strings.Shared.SwapTileCuttings_Other, count, SwapLists.StageText(group.Key.Stage)),
                "propagation", id, frame));
        }

        var offered = plants.Where(p => p.IsOffered).ToList();
        IEnumerable<SwapTile> Counted(SpareMark mark, bool onRequest) => offered.Where(p => p.Spare == mark)
            .GroupBy(p => (Number: CollectionNumbers.Normalize(p.CollectionNumber), Name: NameOf(p.Nickname, p.Genus, p.Species, p.Cultivar, p.NameQualifier, p.NameNote)))
            .OrderBy(g => g.Key.Name, byName).ThenBy(g => g.Key.Number, StringComparer.Ordinal)
            .Select(g =>
            {
                var (id, frame) = CoverOf(g.Select(p => (p.DisplayName, p.CoverPhotoId)));
                var detail = onRequest ? Strings.Shared.SwapListOnRequest : g.Count() > 1 ? string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Shared.SwapTilePlants, g.Count()) : Strings.Shared.SwapTilePlant;
                return new SwapTile(SwapLists.WithNumber(g.Key.Number, g.Key.Name), detail, "plant", id, frame);
            });

        tiles.AddRange(Counted(SpareMark.CuttingsToSpare, onRequest: true));
        tiles.AddRange(Counted(SpareMark.UpForSwap, onRequest: false));
        return tiles;
    }

    /// <summary>
    /// The tiles split into pictures: the fewest that hold them at that size, shared out evenly so
    /// 13 tiles at 12 a picture make 7 and 6. The order is kept.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<SwapTile>> Split(IReadOnlyList<SwapTile> tiles, string format)
    {
        if (tiles.Count == 0)
            return [];

        var pictures = (int)Math.Ceiling(tiles.Count / (double)PerPicture(format));
        var basic = tiles.Count / pictures;
        var extra = tiles.Count - basic * pictures;
        var result = new List<IReadOnlyList<SwapTile>>();
        var next = 0;
        for (var i = 0; i < pictures; i++)
        {
            var size = basic + (i < extra ? 1 : 0);
            result.Add(tiles.Skip(next).Take(size).ToList());
            next += size;
        }
        return result;
    }
}
