namespace Stikling.Core.Year;

/// <summary>When one card comes on screen and for how long, in seconds from the start of the video.</summary>
/// <param name="Hold">The time up to the next card. The last card's is longer, so it stays up at the end.</param>
public sealed record YearVideoShot(string CardId, double Start, double Hold);

/// <summary>
/// The timing of "Your year" as a video: the cards in the order given, each held long enough to read and
/// faded into the next. It is slower than a time-lapse, since a card has words on it.
/// </summary>
public sealed class YearVideoPlan
{
    /// <summary>How long each card but the last is held.</summary>
    public const double HoldSeconds = 3;

    /// <summary>How much longer the last card stays up than the others.</summary>
    public const double LastExtraSeconds = 1.5;

    /// <summary>How long the next card takes to fade in over the one before it.</summary>
    public const double FadeSeconds = 0.4;

    /// <summary>A single card isn't worth a video, so the page only offers one from this many.</summary>
    public const int MinCards = 2;

    private YearVideoPlan(IReadOnlyList<YearVideoShot> shots, double crossfade, double total)
    {
        Shots = shots;
        Crossfade = crossfade;
        Total = total;
    }

    public IReadOnlyList<YearVideoShot> Shots { get; }

    /// <summary>Zero when there is nothing to fade between.</summary>
    public double Crossfade { get; }

    /// <summary>The length of the whole video. Zero without cards.</summary>
    public double Total { get; }

    public static YearVideoPlan Create(IEnumerable<string> cardIds)
    {
        var ids = cardIds.ToList();
        if (ids.Count == 0)
            return new YearVideoPlan([], 0, 0);

        var shots = ids
            .Select((id, i) => new YearVideoShot(id, i * HoldSeconds, i == ids.Count - 1 ? HoldSeconds + LastExtraSeconds : HoldSeconds))
            .ToList();
        return new YearVideoPlan(shots, ids.Count > 1 ? FadeSeconds : 0, ids.Count * HoldSeconds + LastExtraSeconds);
    }
}
