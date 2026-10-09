namespace Stikling.Core.Sharing;

/// <summary>How long each photo of a time-lapse is held.</summary>
public enum TimelapseSpeed
{
    Slow,
    Normal,
    Fast,
}

/// <summary>A photo to put in a time-lapse and the day it was taken, which is what sets the order.</summary>
public readonly record struct TimelapsePhoto(Guid Id, DateTimeOffset TakenAt);

/// <summary>When one photo comes on screen and for how long, in seconds from the start of the video.</summary>
/// <param name="Hold">The time up to the next photo. The last photo's is longer, so it stays up at the end.</param>
public sealed record TimelapseShot(Guid PhotoId, double Start, double Hold);

/// <summary>
/// The timing of a time-lapse: the photos oldest first, each held for a moment and faded into the next.
/// A photo is held until the next one's start, and the next one fades in over the <see cref="Crossfade"/>
/// before that. The video is drawn from this, so the preview and the file agree.
/// </summary>
public sealed class TimelapsePlan
{
    /// <summary>How much longer the last photo stays up than the others.</summary>
    public const double LastExtraSeconds = 1.5;

    /// <summary>The most photos a time-lapse takes, so a long history doesn't make a video that takes minutes to make.</summary>
    public const int MaxPhotos = 60;

    private const double MaxCrossfadeSeconds = 0.2;

    private TimelapsePlan(IReadOnlyList<TimelapseShot> shots, double crossfade, double total)
    {
        Shots = shots;
        Crossfade = crossfade;
        Total = total;
    }

    public IReadOnlyList<TimelapseShot> Shots { get; }

    /// <summary>How long the next photo takes to fade in over the one before it.</summary>
    public double Crossfade { get; }

    /// <summary>The length of the whole video. Zero without photos.</summary>
    public double Total { get; }

    /// <summary>How long each photo but the last is held.</summary>
    public static double HoldSeconds(TimelapseSpeed speed) => speed switch
    {
        TimelapseSpeed.Slow => 1.0,
        TimelapseSpeed.Fast => 0.35,
        _ => 0.6,
    };

    /// <summary>
    /// The photos to start a time-lapse with: all of them up to <paramref name="max"/>, and over that, that many
    /// spread evenly through the history by date, always including the first and the newest.
    /// </summary>
    public static IReadOnlyList<TimelapsePhoto> Spread(IEnumerable<TimelapsePhoto> photos, int max = MaxPhotos)
    {
        var ordered = photos.OrderBy(p => p.TakenAt).ThenBy(p => p.Id).ToList();
        if (ordered.Count <= max || max < 2)
            return max < 2 ? ordered.Take(Math.Max(max, 0)).ToList() : ordered;

        return Enumerable.Range(0, max)
            .Select(i => ordered[(int)Math.Round(i * (ordered.Count - 1) / (double)(max - 1))])
            .ToList();
    }

    /// <summary>The photos in the order they were taken. Photos from the same moment keep a steady order.</summary>
    public static TimelapsePlan Create(IEnumerable<TimelapsePhoto> photos, TimelapseSpeed speed)
    {
        var ordered = photos.OrderBy(p => p.TakenAt).ThenBy(p => p.Id).ToList();
        if (ordered.Count == 0)
            return new TimelapsePlan([], 0, 0);

        var hold = HoldSeconds(speed);
        var shots = ordered
            .Select((p, i) => new TimelapseShot(p.Id, i * hold, i == ordered.Count - 1 ? hold + LastExtraSeconds : hold))
            .ToList();
        // A short hold gets a short fade, so a photo is still on screen on its own for a while
        var crossfade = Math.Min(MaxCrossfadeSeconds, hold / 3);
        return new TimelapsePlan(shots, crossfade, ordered.Count * hold + LastExtraSeconds);
    }

    /// <summary>The speed saved on a device, or normal when it is missing or not one of the three.</summary>
    public static TimelapseSpeed ParseSpeed(string? saved) =>
        Enum.TryParse<TimelapseSpeed>(saved, ignoreCase: true, out var speed) && Enum.IsDefined(speed) ? speed : TimelapseSpeed.Normal;
}
