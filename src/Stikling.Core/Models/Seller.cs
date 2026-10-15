using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>What sort of place or person something came from. Stored as text.</summary>
[JsonConverter(typeof(TolerantSellerKindConverter))]
public enum SellerKind
{
    Shop,
    OnlineShop,
    Person,
    SwapEvent,
    Other
}

/// <summary>Writes the kind as text and reads a name this version doesn't know as Other.</summary>
public sealed class TolerantSellerKindConverter : JsonConverter<SellerKind>
{
    public override SellerKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String
            && Enum.TryParse<SellerKind>(reader.GetString(), out var value)
            && Enum.IsDefined(value))
            return value;
        reader.TrySkip();
        return SellerKind.Other;
    }

    public override void Write(Utf8JsonWriter writer, SellerKind value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

/// <summary>
/// Anyone a plant came from: a shop, an online seller, a friend, a swap event. Plants and
/// propagations point at one by id, and keep its name in their Source text too, so older
/// versions and the CSV still show it.
/// </summary>
public sealed class Seller : Entity
{
    public string Name { get; set; } = "";

    public SellerKind Kind { get; set; } = SellerKind.Shop;

    public string? Notes { get; set; }

    /// <summary>
    /// Set when the seller was merged into another one. It's deleted then, and whatever still
    /// points at it counts as coming from the other one, including plants saved on another device
    /// before the merge reached it.
    /// </summary>
    public Guid? MergedIntoId { get; set; }

    public IReadOnlyList<string> Validate() =>
        string.IsNullOrWhiteSpace(Name) ? ["Give the seller a name."] : [];
}
