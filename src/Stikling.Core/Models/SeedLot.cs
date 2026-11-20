using System.Text.Json.Serialization;
using Stikling.Core.Crosses;
using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>
/// Seeds you have before sowing: from a cross, or bought or swapped. The amount can be rough. Sowing
/// from a lot starts a seed propagation that points back to it with <see cref="Propagation.SeedLotId"/>.
/// </summary>
public sealed class SeedLot : Entity
{
    public string? Genus { get; set; }
    public string? Species { get; set; }
    public string? Cultivar { get; set; }

    /// <summary>The cross the seed came from, when it is one. A bought lot can name one too, with a cross that has typed parents.</summary>
    public Guid? CrossId { get; set; }

    /// <summary>Where the seed came from in words, like "Seed swap" or "Pod from the kitchen window".</summary>
    public string? Source { get; set; }

    /// <summary>The seller, for bought seed, with the name kept in <see cref="Source"/> too.</summary>
    public Guid? SellerId { get; set; }

    /// <summary>The day the seed was collected or bought.</summary>
    public DateOnly? CollectedOn { get; set; }

    /// <summary>The day it is best sown by.</summary>
    public DateOnly? SowBy { get; set; }

    /// <summary>How much there is. See <see cref="SeedAmounts"/>.</summary>
    public decimal? Amount { get; set; }

    [JsonConverter(typeof(TolerantNullableEnumConverter<SeedUnit>))]
    public SeedUnit? AmountUnit { get; set; }

    public bool AmountIsAbout { get; set; }

    public string? Note { get; set; }

    /// <summary>The day the last seed was sown or thrown away. Null while there is some.</summary>
    public DateOnly? UsedUpOn { get; set; }

    [JsonIgnore]
    public bool IsUsedUp => UsedUpOn is not null;

    [JsonIgnore]
    public string? BotanicalName => PlantNames.Botanical(Genus, Species, Cultivar);

    public SeedLot Copy() => (SeedLot)MemberwiseClone();

    public IReadOnlyList<string> Validate(DateOnly today)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Genus) && string.IsNullOrWhiteSpace(Source) && CrossId is null && SellerId is null)
            errors.Add(Strings.Crosses.ErrLotNeedsName);
        if (CollectedOn is { } collected && collected > today)
            errors.Add(Strings.Crosses.ErrFuture);
        errors.AddRange(SeedAmounts.Errors(Amount, AmountUnit));
        return errors;
    }
}
