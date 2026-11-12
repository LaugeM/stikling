namespace Stikling.Web.Components;

/// <summary>One point on a <see cref="LineChart"/>: where it is in time, its value, and what it stands for.</summary>
/// <param name="X">The day, which the points are spread over in proportion to.</param>
/// <param name="Y">The value.</param>
/// <param name="Key">What a tap on the point is about, like the id of the history entry it came from.</param>
public sealed record ChartPoint(DateOnly X, double Y, Guid? Key = null);
