using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Problems;

/// <summary>Which leaves show it. The sources split yellowing this way.</summary>
public enum WhichLeaves
{
    NotSure,
    Older,
    Newest,
    AllOver
}

/// <summary>How fast it came on. The sources split leaf drop this way.</summary>
public enum HowFast
{
    NotSure,
    FewDays,
    Weeks
}

/// <summary>How the soil is now.</summary>
public enum SoilNow
{
    NotSure,
    Wet,
    Damp,
    Dry
}

/// <summary>The causes the check can list, in the order of docs/dev/symptom-check.md. Ties keep this order.</summary>
public enum SymptomCauseKind
{
    TooWet,
    TooDry,
    TooLittleLight,
    TooMuchSun,
    RecentChange,
    Cold,
    DryAir,
    Salts,
    Hungry,
    RootsFillPot,
    Pests,
    LeafSpot,
    Ageing,
    Rest
}

/// <summary>Where a cause comes from. The ids are the ones in docs/dev/symptom-check.md.</summary>
public enum SymptomSource
{
    UcIpm,
    Rhs,
    Caes,
    Isu
}

/// <summary>The links to the sources. Their titles are text people see, see <see cref="SymptomText.SourceTitle"/>.</summary>
public static class SymptomSources
{
    public static string Url(SymptomSource source) => source switch
    {
        SymptomSource.UcIpm => "https://ipm.ucanr.edu/home-and-landscape/houseplant-problems/",
        SymptomSource.Rhs => "https://www.rhs.org.uk/prevention-protection/leaf-damage-on-houseplants",
        SymptomSource.Caes => "https://portal.ct.gov/-/media/caes/documents/publications/fact_sheets/plant_pathology_and_ecology/diagnosisofcommonhouseplantproblems0503rpdf.pdf",
        _ => "https://yardandgarden.extension.iastate.edu/faq/what-causes-leaves-my-houseplant-turn-yellow-or-brown-and-drop"
    };
}

/// <summary>The numbers the ranking runs on, all in one place so they can be tuned.</summary>
public static class SymptomScoring
{
    /// <summary>Points for each sign a cause explains.</summary>
    public const int PerSign = 2;

    /// <summary>Added for each picked sign that this cause is the only one to explain.</summary>
    public const int PerOnlyCause = 1;

    /// <summary>Points for each fact or answer that moves a cause up, and taken off for each that moves it down.</summary>
    public const int PerMove = 1;

    /// <summary>A cause is listed from this many points.</summary>
    public const int ListedAt = 3;

    /// <summary>At most this many causes are listed. More than this many tied at the top say nothing clear.</summary>
    public const int MaxListed = 3;

    /// <summary>A meter reading from this value on is in the wet part.</summary>
    public const int MeterWetFrom = 7;

    /// <summary>A meter reading up to this value is in the dry part, unless the plant has a watering level of its own.</summary>
    public const int MeterDryTo = 3;

    /// <summary>A reading this recent fills in the soil answer.</summary>
    public const int MeterFillsSoilDays = 2;

    /// <summary>A reading older than this says nothing about the soil now.</summary>
    public const int MeterFreshDays = 7;

    /// <summary>Arrived, repotted or moved within this many days counts as a recent change.</summary>
    public const int RecentChangeDays = 28;

    /// <summary>Watered within this many days counts as watered recently.</summary>
    public const int WateredRecentlyDays = 2;

    /// <summary>Fed within this many days counts as fed in the last month.</summary>
    public const int FedLastMonthDays = 30;

    /// <summary>The feeding is counted over this many days, which is 3 months.</summary>
    public const int FeedingWindowDays = 91;

    /// <summary>Fed on more days than this in the feeding window counts as fed often.</summary>
    public const int FedOftenAbove = 3;

    /// <summary>Not repotted for this many years counts as long ago.</summary>
    public const int NotRepottedYears = 2;
}

/// <summary>The few likely causes for what someone sees, or none when the answers don't point anywhere.</summary>
/// <param name="Points">What it scored. Listed from <see cref="SymptomScoring.ListedAt"/>.</param>
/// <param name="Because">The facts that moved it up, for "Why it's listed".</param>
/// <param name="Sources">The sources that name it for the signs picked.</param>
public sealed record SymptomCause(
    SymptomCauseKind Kind, int Points, IReadOnlyList<SymptomFactKind> Because, IReadOnlyList<SymptomSource> Sources);

public sealed record SymptomResult(IReadOnlyList<SymptomCause> Causes)
{
    /// <summary>Nothing reached the bar, or too many tied at the top, so the general checks are shown instead.</summary>
    public bool NoClearCause => Causes.Count == 0;
}

/// <summary>
/// Ranks the likely causes of what someone sees on a plant. It follows docs/dev/symptom-check.md:
/// a change to one is a change to the other. A cause is only ever considered for a sign a source
/// gives it for, and what the app knows only moves it up or down. It never says what to do.
/// </summary>
public static class SymptomCheck
{
    // A sign a cause explains, the sources that say so, and the answer it needs to count
    private sealed record Explains(SymptomCauseKind Cause, ProblemSign Sign, WhichLeaves? Needs, SymptomSource[] Sources);

    private const SymptomSource U = SymptomSource.UcIpm;
    private const SymptomSource R = SymptomSource.Rhs;
    private const SymptomSource C = SymptomSource.Caes;
    private const SymptomSource I = SymptomSource.Isu;

    private static Explains E(SymptomCauseKind cause, ProblemSign sign, params SymptomSource[] sources) =>
        new(cause, sign, null, sources);

    private static Explains E(SymptomCauseKind cause, ProblemSign sign, WhichLeaves needs, params SymptomSource[] sources) =>
        new(cause, sign, needs, sources);

    private static readonly IReadOnlyList<Explains> Signs =
    [
        E(SymptomCauseKind.TooWet, ProblemSign.YellowLeaves, U, R, I),
        E(SymptomCauseKind.TooWet, ProblemSign.Drooping, U, C),
        E(SymptomCauseKind.TooWet, ProblemSign.LeafDrop, U, C),
        E(SymptomCauseKind.TooWet, ProblemSign.RootRot, U),
        E(SymptomCauseKind.TooWet, ProblemSign.MushyStem, U),
        E(SymptomCauseKind.TooWet, ProblemSign.Edema, R),
        E(SymptomCauseKind.TooWet, ProblemSign.Spots, C, U),
        E(SymptomCauseKind.TooWet, ProblemSign.BrownTips, U, R),

        E(SymptomCauseKind.TooDry, ProblemSign.Drooping, U),
        E(SymptomCauseKind.TooDry, ProblemSign.BrownTips, U, R),
        E(SymptomCauseKind.TooDry, ProblemSign.LeafDrop, U, C),
        E(SymptomCauseKind.TooDry, ProblemSign.YellowLeaves, R, I),
        E(SymptomCauseKind.TooDry, ProblemSign.PaleNewGrowth, C),

        E(SymptomCauseKind.TooLittleLight, ProblemSign.Leggy, U),
        E(SymptomCauseKind.TooLittleLight, ProblemSign.YellowLeaves, WhichLeaves.Older, U, R),
        E(SymptomCauseKind.TooLittleLight, ProblemSign.LeafDrop, U, I),
        E(SymptomCauseKind.TooLittleLight, ProblemSign.LostVariegation, R),
        E(SymptomCauseKind.TooLittleLight, ProblemSign.PaleNewGrowth, C),

        E(SymptomCauseKind.TooMuchSun, ProblemSign.Sunburn, U),
        E(SymptomCauseKind.TooMuchSun, ProblemSign.Spots, R, U),
        E(SymptomCauseKind.TooMuchSun, ProblemSign.BrownTips, U),
        E(SymptomCauseKind.TooMuchSun, ProblemSign.YellowLeaves, WhichLeaves.AllOver, U),
        E(SymptomCauseKind.TooMuchSun, ProblemSign.Drooping, U),

        E(SymptomCauseKind.RecentChange, ProblemSign.LeafDrop, U, C),
        E(SymptomCauseKind.RecentChange, ProblemSign.Drooping, U),
        E(SymptomCauseKind.RecentChange, ProblemSign.YellowLeaves, I),

        E(SymptomCauseKind.Cold, ProblemSign.LeafDrop, C, I),
        E(SymptomCauseKind.Cold, ProblemSign.YellowLeaves, R, I),
        E(SymptomCauseKind.Cold, ProblemSign.Spots, U),
        E(SymptomCauseKind.Cold, ProblemSign.Drooping, C),

        E(SymptomCauseKind.DryAir, ProblemSign.BrownTips, U, R, C, I),

        E(SymptomCauseKind.Salts, ProblemSign.BrownTips, U, C),
        E(SymptomCauseKind.Salts, ProblemSign.Drooping, C),

        E(SymptomCauseKind.Hungry, ProblemSign.YellowLeaves, WhichLeaves.Older, U),
        E(SymptomCauseKind.Hungry, ProblemSign.PaleNewGrowth, C),
        E(SymptomCauseKind.Hungry, ProblemSign.PaleLeaves, C),

        E(SymptomCauseKind.RootsFillPot, ProblemSign.YellowLeaves, WhichLeaves.Older, U),
        E(SymptomCauseKind.RootsFillPot, ProblemSign.LeafDrop, U),
        E(SymptomCauseKind.RootsFillPot, ProblemSign.Drooping, U),

        E(SymptomCauseKind.Pests, ProblemSign.Speckled, U),
        E(SymptomCauseKind.Pests, ProblemSign.Sticky, U),
        E(SymptomCauseKind.Pests, ProblemSign.Curled, C),
        E(SymptomCauseKind.Pests, ProblemSign.YellowLeaves, U),
        E(SymptomCauseKind.Pests, ProblemSign.LeafDrop, U),

        E(SymptomCauseKind.LeafSpot, ProblemSign.Spots, U, R, C),

        // The document names no source for leaf drop here
        E(SymptomCauseKind.Ageing, ProblemSign.YellowLeaves, R, I),

        // The document names no source for these
        E(SymptomCauseKind.Rest, ProblemSign.YellowLeaves),
        E(SymptomCauseKind.Rest, ProblemSign.LeafDrop),
        E(SymptomCauseKind.Rest, ProblemSign.Drooping)
    ];

    /// <summary>
    /// The up to three likeliest causes for the signs, best first. Ties keep the order of
    /// <see cref="SymptomCauseKind"/>. When nothing reaches the bar, or more than three tie at the
    /// top, the result has no causes.
    /// </summary>
    public static SymptomResult Rank(
        IEnumerable<ProblemSign> signs, WhichLeaves leaves, HowFast speed, SoilNow soil, SymptomFacts facts)
    {
        var picked = signs.ToHashSet();
        var scored = new List<SymptomCause>();

        foreach (var cause in Enum.GetValues<SymptomCauseKind>())
        {
            if (!IsListable(cause, leaves, speed, facts))
                continue;

            var explained = Signs.Where(e => e.Cause == cause && picked.Contains(e.Sign) && (e.Needs is null || e.Needs == leaves)).ToList();
            if (explained.Count == 0)
                continue;

            // A sign that no other cause explains, like sticky leaves, is enough to list this one on its own
            var onlyCause = explained.Count(e => Signs.Where(o => o.Sign == e.Sign).Select(o => o.Cause).Distinct().Count() == 1);
            var tally = new Tally(explained.Count * SymptomScoring.PerSign + onlyCause * SymptomScoring.PerOnlyCause);
            Adjust(cause, new Answers(leaves, speed, soil), facts, tally);
            if (tally.Points < SymptomScoring.ListedAt)
                continue;

            var sources = explained.SelectMany(e => e.Sources).Distinct().Order().ToList();
            scored.Add(new SymptomCause(cause, tally.Points, tally.Because.Distinct().ToList(), sources));
        }

        if (scored.Count == 0)
            return new SymptomResult([]);

        var best = scored.Max(c => c.Points);
        if (scored.Count(c => c.Points == best) > SymptomScoring.MaxListed)
            return new SymptomResult([]);

        // OrderByDescending is stable, so equal points stay in the order of the causes
        return new SymptomResult(scored.OrderByDescending(c => c.Points).Take(SymptomScoring.MaxListed).ToList());
    }

    /// <summary>
    /// The soil answer a meter reading fills in: wet from 7, dry up to the plant's watering level
    /// (3 unless it has its own), otherwise damp.
    /// </summary>
    public static SoilNow SoilFromMeter(int reading, int dryAt = SymptomScoring.MeterDryTo) =>
        reading >= SymptomScoring.MeterWetFrom ? SoilNow.Wet : reading <= dryAt ? SoilNow.Dry : SoilNow.Damp;

    // Some causes are only worth listing when something else says so
    private static bool IsListable(SymptomCauseKind cause, WhichLeaves leaves, HowFast speed, SymptomFacts facts) => cause switch
    {
        SymptomCauseKind.RecentChange => facts.Changes.Count > 0 || speed == HowFast.FewDays,
        SymptomCauseKind.Ageing => leaves == WhichLeaves.Older,
        SymptomCauseKind.Rest => facts.Resting,
        _ => true
    };

    private sealed record Answers(WhichLeaves Leaves, HowFast Speed, SoilNow Soil);

    private sealed class Tally(int points)
    {
        public int Points { get; private set; } = points;

        public List<SymptomFactKind> Because { get; } = [];

        /// <summary>One point for an answer or a fact, and the fact is shown as the reason.</summary>
        public void Up(bool when, params SymptomFactKind[] facts)
        {
            if (!when)
                return;
            Points += SymptomScoring.PerMove;
            Because.AddRange(facts);
        }

        public void Down(bool when)
        {
            if (when)
                Points -= SymptomScoring.PerMove;
        }
    }

    private static void Adjust(SymptomCauseKind cause, Answers a, SymptomFacts f, Tally t)
    {
        var meterWet = f.Meter is { } wet && wet.Value >= SymptomScoring.MeterWetFrom;
        var meterDry = f.Meter is { } dry && dry.Value <= f.DryAt;
        var moved = f.Has(SymptomFactKind.Moved);

        switch (cause)
        {
            case SymptomCauseKind.TooWet:
                t.Up(a.Soil == SoilNow.Wet);
                t.Up(meterWet, SymptomFactKind.MeterReading);
                t.Up(f.LikesDryOut, SymptomFactKind.LikesDryOut);
                t.Up(f.Resting, SymptomFactKind.Resting);
                t.Down(a.Soil == SoilNow.Dry);
                t.Down(f.WaterCulture);
                break;

            case SymptomCauseKind.TooDry:
                t.Up(a.Soil == SoilNow.Dry);
                t.Up(meterDry, SymptomFactKind.MeterReading);
                t.Up(f.LongerThanUsual, SymptomFactKind.LongerThanUsual);
                t.Up(f.NotRepottedInTwoYears, SymptomFactKind.NotRepotted);
                t.Down(a.Soil == SoilNow.Wet);
                t.Down(f.WateredRecently);
                break;

            case SymptomCauseKind.TooLittleLight:
                t.Up(f.Light == LightFit.Darker, SymptomFactKind.DarkerThanItLikes);
                t.Up(moved, SymptomFactKind.Moved);
                t.Up(f.DarkerHalf, SymptomFactKind.DarkerHalf);
                t.Down(f.Light == LightFit.Brighter);
                break;

            case SymptomCauseKind.TooMuchSun:
                t.Up(f.Light == LightFit.Brighter, SymptomFactKind.BrighterThanItLikes);
                t.Up(moved, SymptomFactKind.Moved);
                t.Down(f.Light == LightFit.Darker);
                break;

            case SymptomCauseKind.RecentChange:
                t.Up(f.Changes.Count > 0, ChangeFacts(f));
                t.Up(a.Speed == HowFast.FewDays);
                break;

            case SymptomCauseKind.Cold:
                t.Up(a.Speed == HowFast.FewDays);
                t.Up(f.HeatingHalf, SymptomFactKind.HeatingHalf);
                break;

            case SymptomCauseKind.DryAir:
                t.Up(f.LikesHighHumidity, SymptomFactKind.LikesHumidity);
                t.Up(f.HeatingHalf, SymptomFactKind.HeatingHalf);
                break;

            case SymptomCauseKind.Salts:
                t.Up(f.Has(SymptomFactKind.FedOften), SymptomFactKind.FedOften);
                t.Up(f.NotRepottedInTwoYears, SymptomFactKind.NotRepotted);
                break;

            case SymptomCauseKind.Hungry:
                t.Up(f.Has(SymptomFactKind.NotFed) && !f.Resting, SymptomFactKind.NotFed);
                t.Up(f.NotRepottedInTwoYears, SymptomFactKind.NotRepotted);
                t.Down(f.FedLastMonth);
                t.Down(f.Resting);
                break;

            case SymptomCauseKind.RootsFillPot:
                t.Up(f.NotRepottedInTwoYears, SymptomFactKind.NotRepotted);
                break;

            case SymptomCauseKind.Pests:
                t.Up(f.OpenPestCase, SymptomFactKind.PestCase);
                t.Up(f.Has(SymptomFactKind.Arrived), SymptomFactKind.Arrived);
                break;

            case SymptomCauseKind.LeafSpot:
                t.Up(f.Has(SymptomFactKind.Arrived), SymptomFactKind.Arrived);
                t.Up(a.Soil == SoilNow.Wet);
                break;

            // The document gives these two no moves of their own, only the condition for being listed.
            // Without a point for it they could never beat the causes that explain the same signs.
            case SymptomCauseKind.Ageing:
                t.Up(a.Leaves == WhichLeaves.Older);
                break;

            case SymptomCauseKind.Rest:
                t.Up(f.Resting, SymptomFactKind.Resting);
                break;
        }
    }

    private static SymptomFactKind[] ChangeFacts(SymptomFacts f) =>
        [.. f.Changes.Select(c => c.Kind switch
        {
            RecentChangeKind.Arrived => SymptomFactKind.Arrived,
            RecentChangeKind.Repotted => SymptomFactKind.Repotted,
            _ => SymptomFactKind.Moved
        })];
}
