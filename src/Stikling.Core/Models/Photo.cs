namespace Stikling.Core.Models;

/// <summary>
/// Photo metadata. The image itself (and a thumbnail) is stored separately as binary data,
/// keyed by the photo's id, so lists of photos stay small and fast to load.
/// </summary>
public sealed class Photo : Entity
{
    public SubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }

    /// <summary>When the photo was taken, as far as the device knows.</summary>
    public DateTimeOffset TakenAt { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>How the photo sits where it's cropped, on cards and at the top of a page. Null is centred.</summary>
    public PhotoFrame? Frame { get; set; }
}

/// <summary>
/// How a photo sits where it's cropped. <paramref name="X"/> and <paramref name="Y"/> are the point
/// of the photo that goes in the middle of the crop, from 0 at the left or top edge to 1 at the
/// right or bottom, and <paramref name="Zoom"/> is how far it's zoomed in. A crop shows that point
/// in its middle as far as the photo's edges allow, so one frame suits a square card and the wide
/// top of a page alike. Only these numbers are saved, the image itself is never changed.
/// </summary>
public sealed record PhotoFrame(double X, double Y, double Zoom)
{
    public const double MaxZoom = 4;

    /// <summary>
    /// The frame for these numbers, kept in range, or null when it comes out centred and not zoomed,
    /// so a photo put back in the middle is stored the same as one never moved.
    /// </summary>
    public static PhotoFrame? From(double x, double y, double zoom)
    {
        var frame = new PhotoFrame(Tidy(x, 0, 1, 0.5), Tidy(y, 0, 1, 0.5), Tidy(zoom, 1, MaxZoom, 1));
        return frame is { X: 0.5, Y: 0.5, Zoom: 1 } ? null : frame;
    }

    private static double Tidy(double value, double min, double max, double neutral) =>
        double.IsFinite(value) ? Math.Round(Math.Clamp(value, min, max), 3) : neutral;
}
