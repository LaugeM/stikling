using Stikling.Core.Models;

namespace Stikling.Core.Photos;

/// <summary>The points of the mark a tap makes, and of a mark moved with the keys. Everything is from 0 to 1 across the photo.</summary>
public static class StandardMarkup
{
    /// <summary>A circle's radius, as a share of the photo's shorter drawn side.</summary>
    public const double CircleRadius = 0.08;

    /// <summary>How far an arrow's tail is from its tip, as a share of the photo's shorter drawn side.</summary>
    public const double ArrowLength = 0.18;

    /// <summary>
    /// The mark a tap at (x, y) makes on a photo drawn <paramref name="width"/> by <paramref name="height"/> pixels.
    /// A circle has the tap as its centre and the edge to its right, or to its left near the right edge. An arrow
    /// has the tap as its tip and the tail up and to the left, on another diagonal when that would leave the photo.
    /// </summary>
    public static (double X1, double Y1, double X2, double Y2) At(MarkupShape shape, double x, double y, double width, double height)
    {
        x = PhotoMark.Tidy(x);
        y = PhotoMark.Tidy(y);
        if (width <= 0 || height <= 0)
            return (x, y, x, y);

        var shorter = Math.Min(width, height);
        if (shape == MarkupShape.Circle)
        {
            var reach = CircleRadius * shorter / width;
            return (x, y, PhotoMark.Tidy(x + reach <= 1 ? x + reach : x - reach), y);
        }

        var along = ArrowLength * shorter * Math.Sqrt(0.5);
        var (dx, dy) = (along / width, along / height);
        foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
        {
            var (tx, ty) = (x + sx * dx, y + sy * dy);
            if (tx is >= 0 and <= 1 && ty is >= 0 and <= 1)
                return (x, y, PhotoMark.Tidy(tx), PhotoMark.Tidy(ty));
        }

        return (x, y, PhotoMark.Tidy(x - dx), PhotoMark.Tidy(y - dy));
    }

    /// <summary>Both points moved by (dx, dy), by less when one of them would leave the photo, so the shape keeps its size.</summary>
    public static (double X1, double Y1, double X2, double Y2) Shift(double x1, double y1, double x2, double y2, double dx, double dy)
    {
        dx = Math.Clamp(dx, -Math.Min(x1, x2), 1 - Math.Max(x1, x2));
        dy = Math.Clamp(dy, -Math.Min(y1, y2), 1 - Math.Max(y1, y2));
        return (x1 + dx, y1 + dy, x2 + dx, y2 + dy);
    }
}
