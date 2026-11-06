using Stikling.Core.Care;
using Stikling.Core.Plants;

namespace Stikling.Core.Problems;

/// <summary>
/// One thing the app knows about a plant that the symptom check can use. Each can be unticked on
/// the screen, so each has its own kind. Nothing here is stored.
/// </summary>
public enum SymptomFactKind
{
    Arrived,
    Repotted,
    Moved,
    DarkerThanItLikes,
    BrighterThanItLikes,
    LikesDryOut,
    LikesHumidity,
    MeterReading,
    WateredRecently,
    LongerThanUsual,
    FedLastMonth,
    FedOften,
    NotFed,
    NotRepotted,
    PestCase,
    Resting,
    WaterCulture,
    DarkerHalf,
    HeatingHalf
}

/// <summary>
/// What the app knows about a plant, as far as the symptom check goes. Only what is true is set,
/// so a fact the person unticks is simply taken out (see <see cref="Without"/>).
/// </summary>
public sealed record SymptomFacts
{
    /// <summary>Arrival, repot and move in the last 4 weeks (see <see cref="RecentChanges"/>).</summary>
    public IReadOnlyList<RecentChange> Changes { get; init; } = [];

    /// <summary>Only Darker or Brighter. Null when it fits or either side is not set.</summary>
    public LightFit? Light { get; init; }

    public bool LikesDryOut { get; init; }

    public bool LikesHighHumidity { get; init; }

    /// <summary>The newest meter reading since the plant was last watered, when it is recent enough to say anything.</summary>
    public MoistureReading? Meter { get; init; }

    /// <summary>The reading at or below which the soil counts as dry: the plant's own watering level, or the usual 3.</summary>
    public int DryAt { get; init; } = SymptomScoring.MeterDryTo;

    public bool WateredRecently { get; init; }

    /// <summary>Longer since watering than its usual gap. Only set once the app has learned the plant.</summary>
    public bool LongerThanUsual { get; init; }

    public bool FedLastMonth { get; init; }

    /// <summary>How many days it was fed on in the last 3 months. Null when this isn't known or was unticked.</summary>
    public int? FeedingsLast3Months { get; init; }

    public bool NotRepottedInTwoYears { get; init; }

    public bool OpenPestCase { get; init; }

    /// <summary>It is its rest time, or it is marked dormant.</summary>
    public bool Resting { get; init; }

    /// <summary>Semi-hydro, water or a self-watering pot.</summary>
    public bool WaterCulture { get; init; }

    public bool DarkerHalf { get; init; }

    public bool HeatingHalf { get; init; }

    public static SymptomFacts None { get; } = new();

    public bool Has(SymptomFactKind kind) => kind switch
    {
        SymptomFactKind.Arrived => Changes.Any(c => c.Kind == RecentChangeKind.Arrived),
        SymptomFactKind.Repotted => Changes.Any(c => c.Kind == RecentChangeKind.Repotted),
        SymptomFactKind.Moved => Changes.Any(c => c.Kind == RecentChangeKind.Moved),
        SymptomFactKind.DarkerThanItLikes => Light == LightFit.Darker,
        SymptomFactKind.BrighterThanItLikes => Light == LightFit.Brighter,
        SymptomFactKind.LikesDryOut => LikesDryOut,
        SymptomFactKind.LikesHumidity => LikesHighHumidity,
        SymptomFactKind.MeterReading => Meter is not null,
        SymptomFactKind.WateredRecently => WateredRecently,
        SymptomFactKind.LongerThanUsual => LongerThanUsual,
        SymptomFactKind.FedLastMonth => FedLastMonth,
        SymptomFactKind.FedOften => FeedingsLast3Months > SymptomScoring.FedOftenAbove,
        SymptomFactKind.NotFed => FeedingsLast3Months == 0,
        SymptomFactKind.NotRepotted => NotRepottedInTwoYears,
        SymptomFactKind.PestCase => OpenPestCase,
        SymptomFactKind.Resting => Resting,
        SymptomFactKind.WaterCulture => WaterCulture,
        SymptomFactKind.DarkerHalf => DarkerHalf,
        _ => HeatingHalf
    };

    /// <summary>The same facts with these taken out, for the ones the person unticked.</summary>
    public SymptomFacts Without(IEnumerable<SymptomFactKind> kinds)
    {
        var result = this;
        foreach (var kind in kinds)
            result = result.Without(kind);
        return result;
    }

    public SymptomFacts Without(SymptomFactKind kind)
    {
        if (!Has(kind))
            return this;

        return kind switch
        {
            SymptomFactKind.Arrived => this with { Changes = Changes.Where(c => c.Kind != RecentChangeKind.Arrived).ToList() },
            SymptomFactKind.Repotted => this with { Changes = Changes.Where(c => c.Kind != RecentChangeKind.Repotted).ToList() },
            SymptomFactKind.Moved => this with { Changes = Changes.Where(c => c.Kind != RecentChangeKind.Moved).ToList() },
            SymptomFactKind.DarkerThanItLikes or SymptomFactKind.BrighterThanItLikes => this with { Light = null },
            SymptomFactKind.LikesDryOut => this with { LikesDryOut = false },
            SymptomFactKind.LikesHumidity => this with { LikesHighHumidity = false },
            SymptomFactKind.MeterReading => this with { Meter = null },
            SymptomFactKind.WateredRecently => this with { WateredRecently = false },
            SymptomFactKind.LongerThanUsual => this with { LongerThanUsual = false },
            SymptomFactKind.FedLastMonth => this with { FedLastMonth = false },
            SymptomFactKind.FedOften or SymptomFactKind.NotFed => this with { FeedingsLast3Months = null },
            SymptomFactKind.NotRepotted => this with { NotRepottedInTwoYears = false },
            SymptomFactKind.PestCase => this with { OpenPestCase = false },
            SymptomFactKind.Resting => this with { Resting = false },
            SymptomFactKind.WaterCulture => this with { WaterCulture = false },
            SymptomFactKind.DarkerHalf => this with { DarkerHalf = false },
            _ => this with { HeatingHalf = false }
        };
    }
}

/// <summary>A fact as a line on the screen, like "Moved 5 days ago". The person can untick it.</summary>
public sealed record SymptomFactLine(SymptomFactKind Kind, string Text);

/// <summary>What the service gathers for a plant: the facts, the lines that show them and the soil answer a meter reading fills in.</summary>
/// <param name="SoilFromMeter">The soil answer a reading from the last two days fills in, null when there is none.</param>
/// <param name="MeterDay">The day of that reading.</param>
public sealed record SymptomContext(
    SymptomFacts Facts, IReadOnlyList<SymptomFactLine> Lines, SoilNow? SoilFromMeter, DateOnly? MeterDay);
