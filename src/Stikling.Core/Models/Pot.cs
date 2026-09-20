using System.Globalization;
using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>
/// Where a pot sits. Nursery pots go inside something, outer pots have no drainage and hold a
/// nursery pot, and some pots need nothing around them at all.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PotGroup>))]
public enum PotGroup
{
    Inner,
    Outer,
    Standalone
}

[JsonConverter(typeof(JsonStringEnumConverter<PotMaterial>))]
public enum PotMaterial
{
    Plastic,
    Ceramic,
    Terracotta,
    Glass,
    Metal,
    Other
}

/// <summary>How a self-watering pot gets the water up: a wick, or the pot standing in it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PotWatering>))]
public enum PotWatering
{
    Wick,
    Submerged
}

/// <summary>
/// A pot you own, as a kind rather than a single object: five identical nursery pots are one
/// entry with a count of five, while a one-off ceramic is an entry with a count of one.
///
/// The name is shared across the sizes of the same pot ("Clear nursery pot"), which is what
/// lets the library group them and what keeps six near-identical entries from being typed six
/// slightly different ways.
/// </summary>
public sealed class Pot : Entity
{
    public PotGroup Group { get; set; } = PotGroup.Inner;

    /// <summary>The family name, shared by every size of the same pot.</summary>
    public string? Name { get; set; }

    public PotMaterial Material { get; set; } = PotMaterial.Plastic;

    /// <summary>Across the top, not counting the rim. On an outer pot, the inside.</summary>
    public decimal? TopCm { get; set; }

    /// <summary>Across the bottom, which is the measurement that decides whether a pot fits inside another.</summary>
    public decimal? BottomCm { get; set; }

    public decimal? HeightCm { get; set; }

    /// <summary>
    /// How far the rim sticks out, in millimetres. A rimmed nursery pot can hang on an outer pot
    /// instead of standing in it, which is how a plain ceramic becomes a self-watering setup.
    /// </summary>
    public decimal? RimMm { get; set; }

    /// <summary>How many of this pot there are.</summary>
    public int Owned { get; set; } = 1;

    /// <summary>How the pot waters itself, or null when it doesn't.</summary>
    public PotWatering? SelfWatering { get; set; }

    public string? Notes { get; set; }

    [JsonIgnore]
    public bool HasRim => RimMm > 0;

    /// <summary>"Clear nursery pot, 13 cm".</summary>
    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(Name) ? "Pot" : Name.Trim();
            return TopCm is { } top ? $"{name}, {Size(top)} cm" : name;
        }
    }

    /// <summary>A number without trailing zeros, so 13 stays "13" and 5.8 stays "5.8".</summary>
    public static string Size(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public Pot Copy() => (Pot)MemberwiseClone();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Give the pot a name, like \"Clear nursery pot\".");

        if (Owned < 1)
            errors.Add("You have to have at least one of it.");

        if (new[] { TopCm, BottomCm, HeightCm, RimMm }.Any(m => m <= 0))
            errors.Add("A measurement has to be more than 0.");

        if (BottomCm > TopCm)
            errors.Add("The bottom can't be wider than the top.");

        return errors;
    }
}
