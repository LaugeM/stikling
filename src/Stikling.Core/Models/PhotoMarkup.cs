using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

public enum MarkupShape
{
    Circle,
    Arrow,
}

/// <summary>
/// A circle or an arrow drawn on a photo, with an optional short label. One record per mark, so two
/// devices marking the same photo add records instead of changing a list. The photo's image is never
/// changed, and the photo can be gone by the time the mark is read.
/// </summary>
public sealed class PhotoMarkup : Entity
{
    public const int MaxLabelLength = 40;

    /// <summary>The smallest an arrow can be, in the 0 to 1 space of the photo.</summary>
    public const double MinArrowLength = 0.01;

    public Guid PhotoId { get; set; }

    /// <summary>
    /// Read with the tolerant converter, so a shape added in a later version reads as null here. A mark
    /// with no shape is never drawn, listed or edited, but stays stored as it is.
    /// </summary>
    [JsonConverter(typeof(TolerantNullableEnumConverter<MarkupShape>))]
    public MarkupShape? Shape { get; set; }

    /// <summary>A circle's centre, or an arrow's tip (what it points at). From 0 at the left edge to 1 at the right.</summary>
    public double X1 { get; set; }

    /// <summary>From 0 at the top edge to 1 at the bottom edge.</summary>
    public double Y1 { get; set; }

    /// <summary>A point on a circle's edge, or an arrow's tail.</summary>
    public double X2 { get; set; }

    public double Y2 { get; set; }

    public string? Label { get; set; }

    /// <summary>A label trimmed and cut to the longest allowed. Empty comes out as null.</summary>
    public static string? TidyLabel(string? label)
    {
        var text = label?.Trim();
        if (string.IsNullOrEmpty(text))
            return null;
        return text.Length > MaxLabelLength ? text[..MaxLabelLength].TrimEnd() : text;
    }

    /// <summary>True when the two points are too close for the shape to be seen.</summary>
    public static bool IsTooSmall(MarkupShape shape, double x1, double y1, double x2, double y2) =>
        shape == MarkupShape.Circle
            ? x1 == x2 && y1 == y2
            : Math.Sqrt(((x2 - x1) * (x2 - x1)) + ((y2 - y1) * (y2 - y1))) < MinArrowLength;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (PhotoId == Guid.Empty)
            errors.Add("A mark needs a photo.");
        if (Shape is null)
            errors.Add("A mark is a circle or an arrow.");
        if (!Inside(X1) || !Inside(Y1) || !Inside(X2) || !Inside(Y2))
            errors.Add("A mark has to be inside the photo.");
        if (Label is { Length: > MaxLabelLength })
            errors.Add("A label is at most " + MaxLabelLength + " characters.");
        return errors;
    }

    private static bool Inside(double value) => double.IsFinite(value) && value is >= 0 and <= 1;
}
