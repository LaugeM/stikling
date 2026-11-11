using System.Text.Json.Serialization;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>What a loose supply is for. Stored as text, and an unknown name reads as null, which counts as Other.</summary>
public enum SupplyKind
{
    /// <summary>Goes in soil mixes, like LECA and perlite.</summary>
    Mixing,

    /// <summary>Goes in the water, like a fertiliser.</summary>
    Water,

    /// <summary>For pests, like sticky traps and alcohol.</summary>
    Pests,

    Other
}

/// <summary>What a supply is counted in. Stored as text, and an unknown name reads as null, which means no unit.</summary>
public enum SupplyUnit
{
    Litres,
    Kilograms,
    Grams,
    Millilitres,
    Pieces,
    Packs
}

/// <summary>
/// Something kept in stock that isn't a plant or a pot: LECA, perlite, sphagnum, a fertiliser,
/// sticky traps, alcohol. How much there is lives in <see cref="SupplyEntry"/> records, one for each
/// change, so two devices never overwrite each other's stock. A supply can point at one of the
/// person's <see cref="Product"/>s instead of being typed again.
/// </summary>
public sealed class Supply : Entity
{
    /// <summary>Typed by the person, or copied from the product when <see cref="ProductId"/> is set.</summary>
    public string? Name { get; set; }

    [JsonConverter(typeof(TolerantNullableEnumConverter<SupplyKind>))]
    public SupplyKind? Kind { get; set; }

    [JsonConverter(typeof(TolerantNullableEnumConverter<SupplyUnit>))]
    public SupplyUnit? Unit { get; set; }

    /// <summary>
    /// Set when the supply is one of the person's products. Its Kind is Water then, and the product's
    /// name is shown while the product exists, with <see cref="Name"/> as the fallback.
    /// </summary>
    public Guid? ProductId { get; set; }

    public string? Notes { get; set; }

    /// <summary>The kind, with a missing or unknown one counted as Other.</summary>
    [JsonIgnore]
    public SupplyKind EffectiveKind => Kind ?? SupplyKind.Other;

    /// <summary>
    /// The id of the supply for a product. A supply for a product always has this id, so two devices
    /// that make one for the same product make the same record.
    /// </summary>
    public static Guid IdForProduct(Guid productId) => NameBasedId.For("supply-for-product:" + productId);

    /// <summary>The unit that fits a kind, picked when the kind is picked. It can be changed.</summary>
    public static SupplyUnit DefaultUnit(SupplyKind kind) => kind switch
    {
        SupplyKind.Mixing => SupplyUnit.Litres,
        SupplyKind.Water => SupplyUnit.Millilitres,
        _ => SupplyUnit.Pieces
    };

    public Supply Copy() => (Supply)MemberwiseClone();

    public IReadOnlyList<string> Validate() =>
        ProductId is null && string.IsNullOrWhiteSpace(Name) ? [Strings.Stock.SupplyNeedsName] : [];
}
