using System.Globalization;
using Stikling.Core.Sharing;

namespace Stikling.Api.Sharing;

/// <summary>
/// The srcset for a photo shown in a square crop on the shared and public pages, so the browser
/// takes the thumbnail only where it is sharp enough and the full photo everywhere else.
/// </summary>
public static class ShareImage
{
    // The sizes the app stores (FULL_SIZE and THUMB_SIZE in photos.js), as the longest side
    private const int FullSize = 1600;
    private const int ThumbSize = 360;

    // A square crop shows the photo's short side across, so that is the width the browser compares.
    // Older photos have no size stored; a 4:3 photo is the usual case.
    private const double UsualShortToLong = 0.75;

    /// <summary>Square tiles of the public page's wall, the big first tile of the wall, grids and shelves.</summary>
    public const string WallSizes = "(min-width: 64rem) 30rem, (min-width: 40rem) 24rem, 34vw";
    public const string WallFirstSizes = "(min-width: 64rem) 60rem, (min-width: 40rem) 48rem, 34vw";
    public const string GridSizes = "(min-width: 100rem) 30rem, (min-width: 64rem) 26rem, (min-width: 40rem) 20rem, 50vw";
    public const string ShelfSizes = "(min-width: 100rem) 15rem, (min-width: 64rem) 13rem, (min-width: 40rem) 11rem, 8rem";

    /// <summary>The photos in a story's day, three or four across a page at most 40rem wide.</summary>
    public const string StorySizes = "(min-width: 40rem) 10rem, 34vw";

    /// <param name="prefix">Everything before /photos/ in the photo's address.</param>
    /// <param name="hasThumbnail">Whether the server has the photo's thumbnail.</param>
    public static string SrcSet(string prefix, SharePhoto photo, bool hasThumbnail)
    {
        var full = $"{prefix}/photos/{photo.Id}/full {ShortSide(photo, FullSize)}w";
        return hasThumbnail
            ? $"{prefix}/photos/{photo.Id}/thumb {ShortSide(photo, ThumbSize)}w, {full}"
            : full;
    }

    private static string ShortSide(SharePhoto photo, int longest)
    {
        var known = photo.Width > 0 && photo.Height > 0;
        var ratio = known
            ? (double)Math.Min(photo.Width, photo.Height) / Math.Max(photo.Width, photo.Height)
            : UsualShortToLong;
        var stored = known ? Math.Min(longest, Math.Max(photo.Width, photo.Height)) : longest;
        return Math.Max(1, (int)Math.Round(stored * ratio)).ToString(CultureInfo.InvariantCulture);
    }
}
