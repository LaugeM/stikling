using System.Text.Json.Serialization;

namespace Stikling.Core.Names;

/// <summary>
/// What people call plants in one language, like "Snake plant" for Dracaena trifasciata, as stored in
/// <c>wwwroot/data/everyday-names.{language}.json</c>. Built by the script in <c>tools/plant-names</c>
/// along with the plant names, and never edited by hand.
/// </summary>
public sealed record EverydayNameData
{
    public static readonly EverydayNameData Empty = new();

    /// <summary>The language the names are in, "en" or "da".</summary>
    public string Language { get; init; } = "";

    /// <summary>Where the names come from, to credit them in Settings.</summary>
    public IReadOnlyList<NameSource> Sources { get; init; } = [];

    public IReadOnlyList<EverydayNames> Plants { get; init; } = [];
}

/// <summary>
/// The everyday names of a genus, a species or a cultivar. The first one is the name the app shows,
/// the others only help find it.
/// </summary>
/// <param name="Common">
/// A plant many people grow, from the lists kept by hand. The search box puts these before plants
/// with a similar name that hardly anyone has, so "snake" finds the snake plant before Snakeshead.
/// </param>
public sealed record EverydayNames(
    string Genus,
    string? Species,
    string? Cultivar,
    IReadOnlyList<string> Names,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Common = false);
