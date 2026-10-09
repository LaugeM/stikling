using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>
/// Cuttings, corms, seeds and so on that are on their way to becoming plants. One propagation
/// can hold several units (a batch, e.g. "3 corms in LECA") that are potted up or fail one by one.
/// </summary>
public sealed class Propagation : Entity
{
    /// <summary>Your own label, e.g. "Corm test: perlite".</summary>
    public string? Nickname { get; set; }

    public string? Genus { get; set; }
    public string? Species { get; set; }
    public string? Cultivar { get; set; }

    /// <summary>The plant it was taken from. Empty for e.g. bought seeds.</summary>
    public Guid? ParentPlantId { get; set; }

    /// <summary>Where it came from when there's no parent plant, e.g. "Seeds from the garden centre".</summary>
    public string? Source { get; set; }

    public PropagationType Type { get; set; } = PropagationType.Cutting;
    public GrowingMedium Medium { get; set; } = GrowingMedium.Water;

    /// <summary>The soil mix it is in, when the medium is soil and it is one you have saved.</summary>
    public Guid? SoilMixId { get; set; }

    /// <summary>Free text for the setup, e.g. "Humidity box" or "Glass jar".</summary>
    public string? Container { get; set; }

    /// <summary>The room or spot it stands in. See <see cref="Place"/>.</summary>
    public Guid? PlaceId { get; set; }

    /// <summary>
    /// Groups propagations started together so they can be compared, e.g. "Alocasia corm test"
    /// for the same corms in LECA, perlite, sphagnum and on a riser.
    /// </summary>
    public string? Experiment { get; set; }

    public DateOnly StartedOn { get; set; }

    /// <summary>The day it first reached Rooted, kept so batches can be compared.</summary>
    public DateOnly? RootedOn { get; set; }

    /// <summary>The day the last unit was potted up or failed, so the propagation became Done or Failed. Null while it is going.</summary>
    public DateOnly? FinishedOn { get; set; }

    /// <summary>The day the first root showed on any unit in the batch.</summary>
    public DateOnly? FirstRootOn { get; set; }

    /// <summary>The day the first new leaf showed on any unit in the batch.</summary>
    public DateOnly? FirstLeafOn { get; set; }

    /// <summary>How many of the seeds have come up so far. Only for seeds, where <see cref="InitialCount"/> is the number sown and <see cref="StartedOn"/> the sowing day.</summary>
    public int? SeedsGerminated { get; set; }

    /// <summary>The day the first seed came up.</summary>
    public DateOnly? FirstGerminatedOn { get; set; }

    /// <summary>The size of the corm when it was started, in millimetres, measured across. Only for corms.</summary>
    public decimal? CormSizeMm { get; set; }

    /// <summary>What was used to help it root, e.g. cinnamon on the cut or a dome for humidity.</summary>
    public List<RootingAid> RootingAids { get; set; } = [];

    /// <summary>How many units the batch started with.</summary>
    public int InitialCount { get; set; } = 1;

    public int PottedUpCount { get; set; }
    public int FailedCount { get; set; }

    public PropagationStage Stage { get; set; } = PropagationStage.Started;

    public string? Notes { get; set; }

    public Guid? CoverPhotoId { get; set; }

    /// <summary>
    /// The day it went dormant, e.g. a corm sitting still for the winter. Null while it's
    /// growing. A dormant propagation doesn't come up on Today to be checked on.
    /// </summary>
    public DateOnly? DormantSince { get; set; }

    [JsonIgnore]
    public bool IsDormant => DormantSince is not null;

    /// <summary>Something it needs doing, which keeps it on Today until cleared. Null when nothing is.</summary>
    public Attention? Attention { get; set; }

    /// <summary>Share of the sown seeds that came up. Null unless it's seeds with a count of how many came up.</summary>
    [JsonIgnore]
    public double? GerminationRate =>
        Type == PropagationType.Seed && SeedsGerminated is { } up && InitialCount > 0 ? up / (double)InitialCount : null;

    /// <summary>Units still in the propagation (not potted up and not failed).</summary>
    [JsonIgnore]
    public int RemainingCount => InitialCount - PottedUpCount - FailedCount;

    /// <summary>Still in progress (not done and not failed).</summary>
    [JsonIgnore]
    public bool IsActive => Stage is not (PropagationStage.Done or PropagationStage.Failed);

    [JsonIgnore]
    public string? BotanicalName => PlantNames.Botanical(Genus, Species, Cultivar);

    [JsonIgnore]
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Nickname) ? Nickname.Trim() : BotanicalName ?? "Unnamed propagation";

    /// <summary>How long it took to root, or null while it hasn't.</summary>
    [JsonIgnore]
    public int? DaysToRoot =>
        RootedOn is { } rooted ? Math.Max(0, rooted.DayNumber - StartedOn.DayNumber) : null;

    /// <summary>Days from the start to the first root, or null while none has shown.</summary>
    [JsonIgnore]
    public int? DaysToFirstRoot => DaysFromStart(FirstRootOn);

    /// <summary>Days from the start to the first leaf, or null while none has shown.</summary>
    [JsonIgnore]
    public int? DaysToFirstLeaf => DaysFromStart(FirstLeafOn);

    private int? DaysFromStart(DateOnly? date) =>
        date is { } d ? Math.Max(0, d.DayNumber - StartedOn.DayNumber) : null;

    /// <summary>Days dormant, counting the day it went dormant as day 0.</summary>
    public int? DaysDormant(DateOnly today) =>
        DormantSince is { } since ? Math.Max(0, today.DayNumber - since.DayNumber) : null;

    /// <summary>Days since it was started: 0 on the day itself.</summary>
    public int DaysSinceStart(DateOnly today) => Math.Max(0, today.DayNumber - StartedOn.DayNumber);

    /// <summary>
    /// Remembers the day it first reached Rooted. Only Rooted, never Done: a batch can go
    /// straight from Started to potted up, and a guessed date would spoil the comparison.
    /// </summary>
    public void NoteRooted(DateOnly today)
    {
        if (Stage == PropagationStage.Rooted && RootedOn is null)
            RootedOn = today;
    }

    /// <summary>
    /// A root showing means it's rooting, so a first root date on one still at Started moves it
    /// on. A stage picked in a form never fills in the date, since it may have been rooting for a
    /// while before it was written down. See <see cref="NoteFirstSign"/> for the one that does.
    /// </summary>
    public void NoteFirstRoot()
    {
        if (FirstRootOn is not null && Stage == PropagationStage.Started)
            Stage = PropagationStage.Rooting;
    }

    /// <summary>
    /// Moving it to Rooting on its page means a root, or for seeds the first seedling, was just
    /// seen, so that day is kept as the first one unless it already has a date.
    /// </summary>
    public void NoteFirstSign(DateOnly on)
    {
        if (Stage != PropagationStage.Rooting)
            return;
        if (Type == PropagationType.Seed)
            FirstGerminatedOn ??= on;
        else
            FirstRootOn ??= on;
    }

    /// <summary>Sets a milestone to the given day. Only on one still going.</summary>
    public void RecordMilestone(Milestone milestone, DateOnly on)
    {
        if (!IsActive)
            throw new InvalidOperationException("This propagation is finished.");
        if (milestone == Milestone.FirstRoot)
            FirstRootOn = on;
        else
            FirstLeafOn = on;
        NoteFirstRoot();
    }

    /// <summary>A mix only makes sense in soil, so moving to another medium lets go of it.</summary>
    public void ClearMixUnlessSoil()
    {
        if (Medium != GrowingMedium.Soil)
            SoilMixId = null;
    }

    public Propagation Copy()
    {
        var copy = (Propagation)MemberwiseClone();
        copy.RootingAids = [.. RootingAids];
        return copy;
    }

    /// <summary>Moves between Started, Rooting and Rooted. Done and Failed follow from the counts.</summary>
    public void SetStage(PropagationStage stage)
    {
        if (!IsActive)
            throw new InvalidOperationException("This propagation is finished.");
        if (stage is PropagationStage.Done or PropagationStage.Failed)
            throw new ArgumentException("Done and Failed are set by potting up or marking units as failed.", nameof(stage));
        Stage = stage;
    }

    /// <summary>Records that <paramref name="count"/> units became plants on <paramref name="on"/>.</summary>
    public void RecordPottedUp(int count, DateOnly on)
    {
        CheckCount(count);
        PottedUpCount += count;
        SyncStageWithCounts(on);
    }

    /// <summary>Records that <paramref name="count"/> units didn't make it on <paramref name="on"/>.</summary>
    public void RecordFailed(int count, DateOnly on)
    {
        CheckCount(count);
        FailedCount += count;
        SyncStageWithCounts(on);
    }

    private void CheckCount(int count)
    {
        if (!IsActive)
            throw new InvalidOperationException("This propagation is finished.");
        if (count < 1 || count > RemainingCount)
            throw new ArgumentOutOfRangeException(nameof(count), count, $"Choose between 1 and {RemainingCount}.");
    }

    /// <summary>
    /// Nothing left: it's done if anything was potted up, otherwise it failed. Units left on a
    /// finished propagation (the count was raised, or a count corrected, when editing) open it
    /// again, at the stage its dates show it had reached. A finished propagation isn't resting any
    /// more, so it stops being dormant, and nothing is left to do for it.
    /// </summary>
    public void SyncStageWithCounts(DateOnly today)
    {
        if (RemainingCount <= 0)
        {
            Stage = PottedUpCount > 0 ? PropagationStage.Done : PropagationStage.Failed;
            FinishedOn ??= today;
            DormantSince = null;
            Attention = null;
        }
        else if (!IsActive)
        {
            Stage = RootedOn is not null ? PropagationStage.Rooted
                : FirstRootOn is not null || FirstGerminatedOn is not null ? PropagationStage.Rooting
                : PropagationStage.Started;
            FinishedOn = null;
        }
    }

    public IReadOnlyList<string> Validate(DateOnly today)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Nickname) && string.IsNullOrWhiteSpace(Genus))
            errors.Add("Give the propagation a nickname or a genus.");
        if (PottedUpCount < 0 || FailedCount < 0)
            errors.Add("The number potted up or failed can't be negative.");
        if (InitialCount < 1)
            errors.Add("Start with at least 1.");
        else if (InitialCount < PottedUpCount + FailedCount)
            errors.Add($"The count can't be lower than the {PottedUpCount + FailedCount} already potted up or failed.");
        if (RootedOn is { } rooted && rooted < StartedOn)
            errors.Add("It can't have rooted before it was started.");
        if (FirstRootOn < StartedOn || FirstLeafOn < StartedOn)
            errors.Add("A root or leaf can't show before it was started.");
        if (FirstRootOn > today || FirstLeafOn > today)
            errors.Add("A root or leaf can't show in the future.");
        if (SeedsGerminated < 0)
            errors.Add("The number that came up can't be negative.");
        else if (SeedsGerminated > InitialCount)
            errors.Add("More seeds can't have come up than were sown.");
        if (FirstGerminatedOn < StartedOn)
            errors.Add("A seed can't come up before it was sown.");
        if (FirstGerminatedOn > today)
            errors.Add("A seed can't come up in the future.");
        if (CormSizeMm is <= 0 or > 200)
            errors.Add("The corm size must be more than 0 and at most 200 mm.");
        if (DormantSince > today)
            errors.Add("It can't go dormant in the future.");
        return errors;
    }
}

/// <summary>The first signs of growth a propagation is compared on.</summary>
public enum Milestone
{
    FirstRoot,
    FirstLeaf
}
