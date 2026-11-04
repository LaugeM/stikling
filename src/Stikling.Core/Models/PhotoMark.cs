namespace Stikling.Core.Models;

/// <summary>
/// A dot on a photo, marking where a plant or propagation is in it. One record per mark, so two
/// devices marking the same photo add records instead of changing a list. The photo and the plant
/// are pointed at by id, and either can be gone by the time the mark is read.
/// </summary>
public sealed class PhotoMark : Entity
{
    public Guid PhotoId { get; set; }

    /// <summary>A plant or a propagation.</summary>
    public SubjectType SubjectType { get; set; }

    public Guid SubjectId { get; set; }

    /// <summary>Where in the photo, from 0 at the left edge to 1 at the right edge.</summary>
    public double X { get; set; }

    /// <summary>Where in the photo, from 0 at the top edge to 1 at the bottom edge.</summary>
    public double Y { get; set; }

    /// <summary>A position kept between 0 and 1 and rounded to 4 decimals. Not a number comes out as the middle.</summary>
    public static double Tidy(double value) =>
        double.IsFinite(value) ? Math.Round(Math.Clamp(value, 0, 1), 4) : 0.5;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (PhotoId == Guid.Empty)
            errors.Add("A mark needs a photo.");
        if (SubjectId == Guid.Empty)
            errors.Add("A mark needs a plant or propagation.");
        if (SubjectType is not (SubjectType.Plant or SubjectType.Propagation))
            errors.Add("A mark is of a plant or a propagation.");
        if (!double.IsFinite(X) || X is < 0 or > 1 || !double.IsFinite(Y) || Y is < 0 or > 1)
            errors.Add("A mark has to be inside the photo.");
        return errors;
    }
}
