using System.Globalization;
using System.Text.Json.Serialization;
using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;

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

/// <summary>
/// The kinds the pot library shows as tabs. Never stored: a container for propagating is a
/// standalone pot with <see cref="Pot.ForPropagation"/> set. The names of the first three must
/// match <see cref="PotGroup"/>, since the shown tab is kept on the device by name.
/// </summary>
public enum PotKind
{
    Inner,
    Outer,
    Standalone,
    Propagation
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

/// <summary>The colour of a pot, so pots with no name can still be told apart.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PotColour>))]
public enum PotColour
{
    White,
    Black,
    Grey,
    Clear,
    Beige,
    Brown,
    Green,
    Blue,
    Pink,
    Patterned
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

    /// <summary>
    /// True for a container people root cuttings or corms in. Such a pot has Group Standalone and
    /// this set, rather than a new PotGroup value: PotGroup is read with a plain string enum
    /// converter, so an older version on another device would fail to read an unknown value. Older
    /// versions list these under "On its own".
    /// </summary>
    public bool ForPropagation { get; set; }

    /// <summary>The tab the pot is shown under. Worked out from Group and ForPropagation, never stored.</summary>
    [JsonIgnore]
    public PotKind Kind => ForPropagation
        ? PotKind.Propagation
        : Group switch
        {
            PotGroup.Inner => PotKind.Inner,
            PotGroup.Outer => PotKind.Outer,
            _ => PotKind.Standalone
        };

    /// <summary>
    /// The family name, shared by every size of the same pot. Optional, except on a container for
    /// propagating, since propagations point at those by name. See <see cref="FamilyName"/>.
    /// </summary>
    public string? Name { get; set; }

    public PotMaterial Material { get; set; } = PotMaterial.Plastic;

    /// <summary>The colour, or null when it hasn't been set. Part of the generated name of a pot without one.</summary>
    public PotColour? Colour { get; set; }

    /// <summary>
    /// Across the top on the outside, outer edge to outer edge, the way pots are sold. Sizes saved before
    /// the form said so can be measured another way, and nothing can tell which, so they stay as typed.
    /// </summary>
    public decimal? TopCm { get; set; }

    /// <summary>Across the bottom on the outside.</summary>
    public decimal? BottomCm { get; set; }

    public decimal? HeightCm { get; set; }

    /// <summary>
    /// How wide the rim or wall is seen from above, outer edge to inner edge, in millimetres. A rim that
    /// sticks out lets a nursery pot hang on an outer pot instead of standing in it, which is how a plain
    /// ceramic becomes a self-watering setup.
    /// </summary>
    public decimal? RimMm { get; set; }

    /// <summary>How many of this pot there are.</summary>
    public int Owned { get; set; } = 1;

    /// <summary>How the pot waters itself, or null when it doesn't.</summary>
    public PotWatering? SelfWatering { get; set; }

    public string? Notes { get; set; }

    /// <summary>The photo shown for the pot. Null shows the newest, see <see cref="Pots.PotPhotos"/>.</summary>
    public Guid? CoverPhotoId { get; set; }

    [JsonIgnore]
    public bool HasRim => RimMm > 0;

    /// <summary>The width inside the top, worked out from the size and the rim. Null when either is missing or the rim leaves nothing.</summary>
    [JsonIgnore]
    public decimal? InsideTopCm => TopCm is { } top && RimMm is { } rim && top - rim / 5 is > 0 and var inside
        ? inside
        : null;

    /// <summary>
    /// The name shared by every size of the same pot: the name when there is one, otherwise one made
    /// from the colour, material and kind, like "White ceramic outer pot".
    /// </summary>
    [JsonIgnore]
    public string FamilyName => string.IsNullOrWhiteSpace(Name) ? Generated() : Name.Trim();

    /// <summary>"Clear nursery pot, 13 cm", or "5.12 in" when lengths are shown in inches.</summary>
    [JsonIgnore]
    public string DisplayName => TopCm is { } top ? $"{FamilyName}, {Lengths.Cm(top)}" : FamilyName;

    /// <summary>
    /// The name in cm whatever the unit setting is. This is the one to write into a history line, since
    /// nothing saved on a record may depend on the setting.
    /// </summary>
    [JsonIgnore]
    public string StoredName => TopCm is { } top ? $"{FamilyName}, {Size(top)} cm" : FamilyName;

    /// <summary>
    /// The words go in a format of their own for each combination, because the Danish order differs
    /// ("Hvid yderpotte i keramik"), so nothing is joined by hand.
    /// </summary>
    private string Generated()
    {
        var culture = CultureInfo.CurrentUICulture;
        var colour = Colour is { } c ? ColourLabel(c) : null;
        var material = Material switch
        {
            PotMaterial.Plastic => Strings.Supplies.MaterialPlasticName,
            PotMaterial.Ceramic => Strings.Supplies.MaterialCeramicName,
            PotMaterial.Terracotta => Strings.Supplies.MaterialTerracottaName,
            PotMaterial.Glass => Strings.Supplies.MaterialGlassName,
            PotMaterial.Metal => Strings.Supplies.MaterialMetalName,
            _ => null
        };
        var kind = Group switch
        {
            PotGroup.Inner => Strings.Supplies.PotKindInnerName,
            PotGroup.Outer => Strings.Supplies.PotKindOuterName,
            _ => Strings.Supplies.PotKindStandaloneName
        };

        var format = (colour is not null, material is not null) switch
        {
            (true, true) => Strings.Supplies.PotNameColourMaterial,
            (true, false) => Strings.Supplies.PotNameColour,
            (false, true) => Strings.Supplies.PotNameMaterial,
            _ => Strings.Supplies.PotNameKind
        };

        var name = string.Format(culture, format, colour, material, kind);
        return char.ToUpper(name[0], culture) + name[1..];
    }

    /// <summary>"White", "Hvid".</summary>
    public static string ColourLabel(PotColour colour) => colour switch
    {
        PotColour.White => Strings.Supplies.PotColourWhite,
        PotColour.Black => Strings.Supplies.PotColourBlack,
        PotColour.Grey => Strings.Supplies.PotColourGrey,
        PotColour.Clear => Strings.Supplies.PotColourClear,
        PotColour.Beige => Strings.Supplies.PotColourBeige,
        PotColour.Brown => Strings.Supplies.PotColourBrown,
        PotColour.Green => Strings.Supplies.PotColourGreen,
        PotColour.Blue => Strings.Supplies.PotColourBlue,
        PotColour.Pink => Strings.Supplies.PotColourPink,
        _ => Strings.Supplies.PotColourPatterned
    };

    /// <summary>A number without trailing zeros, so 13 stays "13" and 5.8 stays "5.8".</summary>
    public static string Size(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public Pot Copy() => (Pot)MemberwiseClone();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        // Propagations point at a container by name, so only a container has to have one
        if (ForPropagation && string.IsNullOrWhiteSpace(Name))
            errors.Add(Strings.Supplies.PotNeedsNameContainer);

        if (Owned < 1)
            errors.Add(Strings.Supplies.PotNeedsOne);

        if (new[] { TopCm, BottomCm, HeightCm, RimMm }.Any(m => m <= 0))
            errors.Add(Strings.Supplies.PotMeasurement);

        if (BottomCm > TopCm)
            errors.Add(Strings.Supplies.PotBottomWider);

        if (TopCm > 0 && RimMm > 0 && InsideTopCm is null)
            errors.Add(Strings.Supplies.PotRimTooWide);

        return errors;
    }
}
