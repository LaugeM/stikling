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

    /// <summary>Free text for the setup, e.g. "Humidity box" or "Glass jar".</summary>
    public string? Container { get; set; }

    public string? Location { get; set; }

    /// <summary>
    /// Groups propagations started together so they can be compared, e.g. "Alocasia corm test"
    /// for the same corms in LECA, perlite, sphagnum and on a riser.
    /// </summary>
    public string? Experiment { get; set; }

    public DateOnly StartedOn { get; set; }

    /// <summary>The day it first reached Rooted, kept so batches can be compared.</summary>
    public DateOnly? RootedOn { get; set; }

    /// <summary>How many units the batch started with.</summary>
    public int InitialCount { get; set; } = 1;

    public int PottedUpCount { get; set; }
    public int FailedCount { get; set; }

    public PropagationStage Stage { get; set; } = PropagationStage.Started;

    public string? Notes { get; set; }

    public Guid? CoverPhotoId { get; set; }

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

    public Propagation Copy() => (Propagation)MemberwiseClone();

    /// <summary>Moves between Started, Rooting and Rooted. Done and Failed follow from the counts.</summary>
    public void SetStage(PropagationStage stage)
    {
        if (!IsActive)
            throw new InvalidOperationException("This propagation is finished.");
        if (stage is PropagationStage.Done or PropagationStage.Failed)
            throw new ArgumentException("Done and Failed are set by potting up or marking units as failed.", nameof(stage));
        Stage = stage;
    }

    /// <summary>Records that <paramref name="count"/> units became plants.</summary>
    public void RecordPottedUp(int count)
    {
        CheckCount(count);
        PottedUpCount += count;
        SyncStageWithCounts();
    }

    /// <summary>Records that <paramref name="count"/> units didn't make it.</summary>
    public void RecordFailed(int count)
    {
        CheckCount(count);
        FailedCount += count;
        SyncStageWithCounts();
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
    /// finished propagation (the count was raised when editing) open it again.
    /// </summary>
    public void SyncStageWithCounts()
    {
        if (RemainingCount <= 0)
            Stage = PottedUpCount > 0 ? PropagationStage.Done : PropagationStage.Failed;
        else if (!IsActive)
            Stage = PropagationStage.Started;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Nickname) && string.IsNullOrWhiteSpace(Genus))
            errors.Add("Give the propagation a nickname or a genus.");
        if (InitialCount < 1)
            errors.Add("Start with at least 1.");
        else if (InitialCount < PottedUpCount + FailedCount)
            errors.Add($"The count can't be lower than the {PottedUpCount + FailedCount} already potted up or failed.");
        if (RootedOn is { } rooted && rooted < StartedOn)
            errors.Add("It can't have rooted before it was started.");
        return errors;
    }
}
