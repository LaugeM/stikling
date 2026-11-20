using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>
/// A dated batch of soil mixed from a mix's recipe. How much there is, is the sum of its
/// <see cref="BatchMixing"/> records, one for the first mixing and one for every top-up, so two devices
/// topping up the same batch never overwrite each other. A plant or cutting points at the batch it was
/// potted in beside the mix.
/// </summary>
public sealed class SoilBatch : Entity
{
    /// <summary>The mix it was mixed from. A batch of a deleted mix keeps working.</summary>
    public Guid SoilMixId { get; set; }

    /// <summary>The mix's name when the batch was made, for when the mix is gone.</summary>
    public string? MixName { get; set; }

    /// <summary>The day it was first mixed.</summary>
    public DateOnly MixedOn { get; set; }

    /// <summary>Set when it has run out. A batch that is used up is no longer offered when potting.</summary>
    public DateOnly? UsedUpOn { get; set; }

    public string? Notes { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsUsedUp => UsedUpOn is not null;

    public SoilBatch Copy() => (SoilBatch)MemberwiseClone();

    public IReadOnlyList<string> Validate() => [];
}

/// <summary>One time soil was mixed into a batch: the first mixing, or a top-up.</summary>
public sealed class BatchMixing : Entity
{
    public Guid BatchId { get; set; }

    public DateOnly MixedOn { get; set; }

    public decimal Litres { get; set; }

    public BatchMixing Copy() => (BatchMixing)MemberwiseClone();

    public IReadOnlyList<string> Validate() => Litres > 0 ? [] : [Strings.Supplies.BatchNeedsVolume];
}

/// <summary>
/// One time a can of feed was mixed: which feed, the day, how much water, and the doses as they were,
/// so changing the feed later doesn't rewrite what went into the can.
/// </summary>
public sealed class FeedMixing : Entity
{
    public Guid FeedId { get; set; }

    /// <summary>The feed's name at the time, for when the feed is gone.</summary>
    public string? FeedName { get; set; }

    public DateOnly MixedOn { get; set; }

    /// <summary>The water in the can.</summary>
    public decimal Litres { get; set; }

    public List<ProductDose> Doses { get; set; } = [];

    public FeedMixing Copy()
    {
        var copy = (FeedMixing)MemberwiseClone();
        copy.Doses = [.. Doses.Select(d => d.Copy())];
        return copy;
    }

    public IReadOnlyList<string> Validate() => Litres > 0 ? [] : [Strings.Supplies.CanNeedsWater];
}
